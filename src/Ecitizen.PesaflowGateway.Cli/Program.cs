using System;
using System.Reflection;
using System.Threading.Tasks;
using Ecitizen.PesaflowGateway.Cli.Commands;

namespace Ecitizen.PesaflowGateway.Cli;

public class Program
{
    public const string Banner = @"
\x1b[32m=========================================================\x1b[0m
\x1b[1;32m      eCitizen / PesaFlow Gateway Setup Wizard (CLI)     \x1b[0m
\x1b[32m=========================================================\x1b[0m
Interactive configurator for ASP.NET Core Minimal APIs & MVC
";

    public static async Task<int> Main(string[] args)
    {
        var command = args.Length > 0 ? args[0] : "init";
        var restArgs = args.Length > 1 ? args[1..] : Array.Empty<string>();

        if (command is "--help" or "-h" or "help")
        {
            PrintHelp();
            return 0;
        }

        if (command is "--version" or "-v" or "version")
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";
            Console.WriteLine($"ecitizen-pesaflow v{version}");
            return 0;
        }

        PrintBanner();

        try
        {
            switch (command.ToLowerInvariant())
            {
                case "init":
                    await InitCommand.ExecuteAsync(restArgs);
                    return 0;

                case "pay":
                    await PayCommand.ExecuteAsync(restArgs);
                    return 0;

                case "checkout":
                    await CheckoutCommand.ExecuteAsync(restArgs);
                    return 0;

                case "status":
                    await StatusCommand.ExecuteAsync(restArgs);
                    return 0;

                case "test":
                    await TestCommand.ExecuteAsync(restArgs);
                    return 0;

                default:
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"Unknown command: {command}\n");
                    Console.ResetColor();
                    PrintHelp();
                    return 1;
            }
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\nError: {ex.Message}");
            Console.ResetColor();
            return 1;
        }
    }

    public static void PrintBanner()
    {
        Console.WriteLine("\x1b[32m=========================================================\x1b[0m");
        Console.WriteLine("\x1b[1;32m      eCitizen / PesaFlow Gateway Setup Wizard (CLI)     \x1b[0m");
        Console.WriteLine("\x1b[32m=========================================================\x1b[0m");
        Console.WriteLine("Interactive configurator for ASP.NET Core Minimal APIs & MVC\n");
    }

    public static void PrintHelp()
    {
        Console.WriteLine(@"
Usage:
  ecitizen-pesaflow [command] [options]
  dotnet ecitizen-pesaflow [command] [options]

Commands:
  init            Interactive setup wizard to configure credentials & endpoints (Default)
  pay             Sign and submit a payment prompt directly to eCitizen - no browser required
  checkout        Open the real eCitizen payment page in your browser, then watch for settlement
  status          Poll ECITIZEN_STATUS_URL for the settlement status of an invoice reference
  test            Run cryptographic verification check against test vectors
  help, --help    Show this help message
  --version, -v   Show version

Options for 'init':
  --client-id <id>       eCitizen API Client ID
  --api-key <key>        eCitizen API Key
  --secret <secret>      eCitizen Merchant Secret
  --service-id <id>      eCitizen Service ID
  --url <url>            Payment API endpoint (default: live eCitizen URL)
  --currency <curr>      Default currency code (default: KES)
  --framework <name>     Target: minimal | mvc | console
  --yes, -y              Skip prompts and use defaults or provided flags

Options for 'pay' (credentials read from appsettings / ECITIZEN_* env vars):
  --amount <n>            Amount to charge (required)
  --reference <ref>       Unique invoice reference (default: auto-generated)
  --description <text>    What the payment is for (required)
  --name <name>           Payer's full name (required)
  --id-number <id>        Payer's National ID / Passport number (required)
  --phone <msisdn>        Payer's phone number, normalized automatically for STK push
  --email <email>         Payer's email
  --currency <curr>       Overrides the configured default currency
  --callback-url <url>    Browser return URL after payment
  --notify-url <url>      Server-to-server webhook URL for settlement confirmation
  --no-send-stk           Do not request an M-Pesa STK push
  --dry-run               Build and print the signed payload without sending it
  --yes, -y               Skip confirmation prompt before submitting

Options for 'checkout' (same payment flags as 'pay', plus):
  --poll-interval <sec>   Seconds between settlement checks (default: 5)
  --timeout <sec>         Give up watching after this many seconds (default: 600)
  --no-open               Don't auto-launch the browser, just print the URL
  --status-url <url>      Overrides ECITIZEN_STATUS_URL for polling

Options for 'status':
  --reference <ref>       Invoice reference to check (required)
  --status-url <url>      Overrides ECITIZEN_STATUS_URL for this call
");
    }
}
