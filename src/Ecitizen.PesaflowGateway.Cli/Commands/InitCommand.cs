using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Ecitizen.PesaflowGateway.Cli.Commands;

public static class InitCommand
{
    public static async Task ExecuteAsync(string[] args)
    {
        var targetDir = Directory.GetCurrentDirectory();
        var (detectedFramework, csprojPath) = ProjectScaffolder.DetectEnvironment(targetDir);

        var flags = ParseFlags(args);
        var autoYes = flags.ContainsKey("yes") || flags.ContainsKey("y");

        var apiClientId = flags.GetValueOrDefault("client-id") ?? Environment.GetEnvironmentVariable("ECITIZEN_CLIENT_ID") ?? string.Empty;
        var apiKey = flags.GetValueOrDefault("api-key") ?? Environment.GetEnvironmentVariable("ECITIZEN_API_KEY") ?? string.Empty;
        var secret = flags.GetValueOrDefault("secret") ?? Environment.GetEnvironmentVariable("ECITIZEN_SECRET") ?? string.Empty;
        var serviceId = flags.GetValueOrDefault("service-id") ?? Environment.GetEnvironmentVariable("ECITIZEN_SERVICE_ID") ?? string.Empty;
        var url = flags.GetValueOrDefault("url") ?? Environment.GetEnvironmentVariable("ECITIZEN_GATEWAY_URL") ?? EcitizenConfig.DefaultGatewayUrl;
        var currency = flags.GetValueOrDefault("currency") ?? Environment.GetEnvironmentVariable("ECITIZEN_CURRENCY") ?? EcitizenConfig.DefaultCurrency;
        var framework = flags.GetValueOrDefault("framework") ?? detectedFramework;

        if (!autoYes)
        {
            Console.WriteLine("\n\x1b[1mStep 1: Enter your eCitizen / PesaFlow Merchant Credentials\x1b[0m\n");

            apiClientId = Prompter.Ask("eCitizen API Client ID", apiClientId, s => !string.IsNullOrWhiteSpace(s), "API Client ID cannot be empty.");
            apiKey = Prompter.AskSecret("eCitizen API Key", apiKey, s => !string.IsNullOrWhiteSpace(s), "API Key cannot be empty.");
            secret = Prompter.AskSecret("eCitizen Merchant Secret", secret, s => !string.IsNullOrWhiteSpace(s), "Merchant Secret cannot be empty.");
            serviceId = Prompter.Ask("eCitizen Service ID", serviceId, s => !string.IsNullOrWhiteSpace(s), "Service ID cannot be empty.");

            Console.WriteLine("\n\x1b[1mStep 2: Endpoint & Currency Configuration\x1b[0m\n");

            url = Prompter.Ask("Gateway URL", url);
            currency = Prompter.Ask("Default Currency Code", currency);

            Console.WriteLine("\n\x1b[1mStep 3: Target Architecture\x1b[0m\n");

            var choices = new List<(string Label, string Value)>
            {
                ("ASP.NET Core Minimal APIs (Endpoints/PaymentEndpoints.cs)", "minimal"),
                ("ASP.NET Core MVC (Controllers/PaymentController.cs)", "mvc"),
                ("Standalone Console script (EcitizenDemo.cs)", "console")
            };

            var defaultIndex = framework == "mvc" ? 1 : (framework == "console" ? 2 : 0);
            framework = Prompter.Select("Select application style:", choices, defaultIndex);

            var proceed = Prompter.Confirm("\nApply configuration and scaffold files now?", true);
            if (!proceed)
            {
                Console.WriteLine("\nSetup aborted by user.");
                return;
            }
        }

        Console.WriteLine("\n\x1b[36mGenerating configuration...\x1b[0m");

        var answers = new SetupAnswers
        {
            ApiClientID = apiClientId,
            ApiKey = apiKey,
            Secret = secret,
            ServiceID = serviceId,
            GatewayUrl = url,
            Currency = currency,
            Framework = framework,
            TargetDir = targetDir
        };

        // 1. Update appsettings.json
        var settingsFile = ProjectScaffolder.UpdateAppSettings(targetDir, answers);
        Console.WriteLine($"  \x1b[32m✔\x1b[0m Configured settings: \x1b[1m{Path.GetFileName(settingsFile)}\x1b[0m");

        // 2. Scaffold controller / endpoints
        var files = ProjectScaffolder.ScaffoldFiles(targetDir, answers);
        foreach (var file in files)
        {
            Console.WriteLine($"  \x1b[32m✔\x1b[0m Generated code file: \x1b[1m{Path.GetRelativePath(targetDir, file)}\x1b[0m");
        }

        Console.WriteLine("\n\x1b[32m=========================================================\x1b[0m");
        Console.WriteLine("\x1b[1;32m  ✔ Setup completed successfully!\x1b[0m");
        Console.WriteLine("\x1b[32m=========================================================\x1b[0m\n");
        Console.WriteLine("Next Steps:");
        Console.WriteLine("1. Ensure your Program.cs calls builder.Services.AddEcitizenPesaflowGateway(builder.Configuration)");
        Console.WriteLine("2. Map your payment endpoints (e.g. app.MapPaymentEndpoints() or app.MapControllers())");
        Console.WriteLine("3. Run \x1b[36mecitizen-pesaflow test\x1b[0m anytime to verify HMAC signatures\n");

        await Task.CompletedTask;
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
