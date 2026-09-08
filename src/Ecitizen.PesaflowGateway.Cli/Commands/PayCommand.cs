using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Ecitizen.PesaflowGateway.Models;

namespace Ecitizen.PesaflowGateway.Cli.Commands;

public static class PayCommand
{
    public static async Task ExecuteAsync(string[] args)
    {
        Console.WriteLine("\x1b[1mPrompting a payment directly via the eCitizen PaymentAPI (no browser required)\x1b[0m\n");

        var flags = ParseFlags(args);
        var autoYes = flags.ContainsKey("yes") || flags.ContainsKey("y");
        var dryRun = flags.ContainsKey("dry-run");

        var client = new EcitizenClient();

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
            SendStkPush = !flags.ContainsKey("no-send-stk"),
        };

        var checkout = client.Checkout(payment);

        Console.WriteLine("\n\x1b[1mSigned checkout payload:\x1b[0m");
        Console.WriteLine($"  Target URL: \x1b[36m{checkout.Url}\x1b[0m");
        foreach (var kvp in checkout.Payload)
        {
            Console.WriteLine($"  {kvp.Key}: {kvp.Value}");
        }

        if (dryRun)
        {
            Console.WriteLine("\n\x1b[33m--dry-run set - payload was not sent.\x1b[0m\n");
            return;
        }

        if (!autoYes)
        {
            var proceed = Prompter.Confirm("\nSubmit this payment to the LIVE eCitizen PaymentAPI now?", false);
            if (!proceed)
            {
                Console.WriteLine("\nAborted. No request was sent.");
                return;
            }
        }

        Console.WriteLine("\n\x1b[36mSubmitting to eCitizen...\x1b[0m");
        var result = await client.InitiatePaymentAsync(payment);

        Console.WriteLine($"\n\x1b[1mHTTP {result.HttpStatus}\x1b[0m");
        Console.WriteLine(result.ResponseBody);
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
