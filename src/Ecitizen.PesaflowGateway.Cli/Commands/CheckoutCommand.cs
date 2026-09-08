using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Ecitizen.PesaflowGateway.Models;

namespace Ecitizen.PesaflowGateway.Cli.Commands;

public static class CheckoutCommand
{
    public static async Task ExecuteAsync(string[] args)
    {
        Console.WriteLine("\x1b[1mOpening the eCitizen payment page in your browser\x1b[0m\n");

        var flags = ParseFlags(args);
        var config = CliConfigLoader.LoadConfig();
        var client = new EcitizenClient(config);

        var amountStr = flags.GetValueOrDefault("amount");
        if (string.IsNullOrWhiteSpace(amountStr))
        {
            amountStr = Prompter.Ask("Amount to charge", "500",
                s => decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) && v > 0,
                "Enter a valid amount greater than 0.");
        }
        var amount = decimal.Parse(amountStr!, CultureInfo.InvariantCulture);

        var reference = flags.GetValueOrDefault("reference");
        if (string.IsNullOrWhiteSpace(reference))
        {
            reference = $"CLI-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
        }

        var description = flags.GetValueOrDefault("description");
        if (string.IsNullOrWhiteSpace(description))
        {
            description = Prompter.Ask("Description (what is this payment for?)", null, s => !string.IsNullOrWhiteSpace(s), "Description cannot be empty.");
        }

        var name = flags.GetValueOrDefault("name");
        if (string.IsNullOrWhiteSpace(name))
        {
            name = Prompter.Ask("Payer's full name", null, s => !string.IsNullOrWhiteSpace(s), "Name cannot be empty.");
        }

        var idNumber = flags.GetValueOrDefault("id-number");
        if (string.IsNullOrWhiteSpace(idNumber))
        {
            idNumber = Prompter.Ask("Payer's National ID / Passport number", null, s => !string.IsNullOrWhiteSpace(s), "ID number cannot be empty.");
        }

        var phone = flags.GetValueOrDefault("phone");
        if (string.IsNullOrWhiteSpace(phone) && !flags.ContainsKey("phone"))
        {
            phone = Prompter.Ask("Payer's phone number (for M-Pesa STK push)", "");
        }

        var payment = new PaymentInput
        {
            Amount = amount,
            Reference = reference!,
            Description = description!,
            Name = name!,
            IdNumber = idNumber!,
            Phone = phone,
            Email = flags.GetValueOrDefault("email"),
            Currency = flags.GetValueOrDefault("currency"),
            CallbackUrl = flags.GetValueOrDefault("callback-url"),
            NotifyUrl = flags.GetValueOrDefault("notify-url"),
            SendStkPush = flags.ContainsKey("send-stk"),
        };

        var formHtml = client.PayButton(payment, "Continue to Payment", new PayButtonOptions { Target = "_self" });
        var pageHtml = $@"<!DOCTYPE html>
<html>
<head><meta charset=""utf-8""><title>Redirecting to eCitizen...</title></head>
<body style=""font-family:sans-serif;text-align:center;margin-top:15vh;"">
  <p>Redirecting you to eCitizen to complete payment&hellip;</p>
  {formHtml}
  <script>document.querySelector('form').submit();</script>
</body>
</html>";

        var port = GetAvailablePort();
        using var listener = new HttpListener();
        var prefix = $"http://127.0.0.1:{port}/";
        listener.Prefixes.Add(prefix);
        listener.Start();

        // Start background handler to serve the auto-submitting page
        _ = Task.Run(async () =>
        {
            try
            {
                while (listener.IsListening)
                {
                    var ctx = await listener.GetContextAsync();
                    var buf = Encoding.UTF8.GetBytes(pageHtml);
                    ctx.Response.ContentType = "text/html; charset=utf-8";
                    ctx.Response.ContentLength64 = buf.Length;
                    await ctx.Response.OutputStream.WriteAsync(buf, 0, buf.Length);
                    ctx.Response.Close();
                }
            }
            catch
            {
                // Listener stopped
            }
        });

        Console.WriteLine($"\n\x1b[1mReference:\x1b[0m {reference}");
        Console.WriteLine($"\x1b[1mOpening:\x1b[0m {prefix}");

        if (!flags.ContainsKey("no-open"))
        {
            OpenInBrowser(prefix);
        }

        Console.WriteLine("\nIf a browser window didn't open automatically, visit the URL above.");
        Console.WriteLine("Complete the payment in the browser - you can close the window once done.\n");

        var intervalSec = int.TryParse(flags.GetValueOrDefault("poll-interval"), out var pSec) ? pSec : 5;
        var timeoutSec = int.TryParse(flags.GetValueOrDefault("timeout"), out var tSec) ? tSec : 600;

        Console.WriteLine($"\x1b[36mWatching for settlement (checking every {intervalSec}s, giving up after {timeoutSec}s)...\x1b[0m");
        Console.WriteLine("Press Ctrl+C to stop watching at any time.\n");

        var statusUrlOverride = flags.GetValueOrDefault("status-url");
        var statusClient = !string.IsNullOrWhiteSpace(statusUrlOverride)
            ? new EcitizenClient(new EcitizenConfig
            {
                ApiClientID = client.GetGateway().ApiClientID,
                ApiKey = client.GetGateway().ApiKey,
                Secret = client.GetGateway().Secret,
                ServiceID = client.GetGateway().ServiceID,
                Url = client.GetGateway().Url,
                StatusUrl = statusUrlOverride,
            })
            : client;

        var outcome = await PollForSettlementAsync(statusClient, reference!, intervalSec * 1000, timeoutSec * 1000);

        try { listener.Stop(); } catch { }

        if (outcome == "settled")
        {
            Console.WriteLine($"\n\x1b[1;32m✔ Payment confirmed for reference {reference}!\x1b[0m\n");
        }
        else if (outcome == "timeout")
        {
            Console.WriteLine($"\n\x1b[33mGave up watching after {timeoutSec}s - no confirmed settlement seen.\x1b[0m");
            Console.WriteLine($"Check again later with: \x1b[36mecitizen-pesaflow status --reference {reference}\x1b[0m\n");
        }
        else
        {
            Console.WriteLine($"\n\x1b[33mStopped watching.\x1b[0m Check later with: \x1b[36mecitizen-pesaflow status --reference {reference}\x1b[0m\n");
        }
    }

    private static async Task<string> PollForSettlementAsync(EcitizenClient client, string reference, int intervalMs, int timeoutMs)
    {
        var gateway = client.GetGateway();
        if (string.IsNullOrWhiteSpace(gateway.StatusUrl))
        {
            Console.WriteLine("  \x1b[90mNote: No status URL configured (ECITIZEN_STATUS_URL). Browser checkout opened.\x1b[0m\n");
            return "stopped";
        }

        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        using var cts = new CancellationTokenSource();

        Console.CancelKeyPress += (s, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        while (DateTime.UtcNow < deadline)
        {
            if (cts.IsCancellationRequested) return "interrupted";

            try
            {
                var result = await client.CheckPaymentStatusAsync(reference, cancellationToken: cts.Token);
                string statusText = "";
                try
                {
                    using var doc = JsonDocument.Parse(result.ResponseBody);
                    if (doc.RootElement.TryGetProperty("status", out var sProp) || doc.RootElement.TryGetProperty("Status", out sProp))
                    {
                        statusText = sProp.GetString() ?? "";
                    }
                }
                catch
                {
                    // Non-JSON response
                }

                if (!string.IsNullOrEmpty(statusText) && gateway.IsSuccessStatus(statusText))
                {
                    return "settled";
                }

                Console.WriteLine($"  \x1b[90m... still pending (HTTP {result.HttpStatus}{(string.IsNullOrEmpty(statusText) ? "" : $", status: {statusText}")})\x1b[0m");
            }
            catch (OperationCanceledException)
            {
                return "interrupted";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  \x1b[90m... status check failed ({ex.Message})\x1b[0m");
            }

            try
            {
                await Task.Delay(intervalMs, cts.Token);
            }
            catch (OperationCanceledException)
            {
                return "interrupted";
            }
        }

        return "timeout";
    }

    private static int GetAvailablePort()
    {
        using var tcp = new TcpListener(IPAddress.Loopback, 0);
        tcp.Start();
        var port = ((IPEndPoint)tcp.LocalEndpoint).Port;
        tcp.Stop();
        return port;
    }

    private static void OpenInBrowser(string url)
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Process.Start(new ProcessStartInfo("cmd", $"/c start \"\" \"{url}\"") { CreateNoWindow = true });
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                Process.Start("open", url);
            }
            else
            {
                Process.Start("xdg-open", url);
            }
        }
        catch
        {
            // Ignore failure to open browser
        }
    }

    private static Dictionary<string, string> ParseFlags(string[] args)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (arg.StartsWith("--"))
            {
                var key = arg.Substring(2);
                if (i + 1 < args.Length && !args[i + 1].StartsWith("--"))
                {
                    dict[key] = args[i + 1];
                    i++;
                }
                else
                {
                    dict[key] = "true";
                }
            }
            else if (arg.StartsWith("-"))
            {
                var key = arg.Substring(1);
                dict[key] = "true";
            }
        }
        return dict;
    }
}
