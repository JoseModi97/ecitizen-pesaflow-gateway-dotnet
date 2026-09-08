using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Ecitizen.PesaflowGateway.Cli.Commands;

public static class StatusCommand
{
    public static async Task ExecuteAsync(string[] args)
    {
        var flags = ParseFlags(args);
        var reference = flags.GetValueOrDefault("reference");

        if (string.IsNullOrWhiteSpace(reference))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Missing required --reference <ref>\n");
            Console.ResetColor();
            Program.PrintHelp();
            Environment.Exit(1);
            return;
        }

        var config = CliConfigLoader.LoadConfig();
        var statusUrl = flags.GetValueOrDefault("status-url") ?? Environment.GetEnvironmentVariable("ECITIZEN_STATUS_URL");
        if (!string.IsNullOrWhiteSpace(statusUrl)) config.StatusUrl = statusUrl;
        var client = new EcitizenClient(config);

        try
        {
            Console.WriteLine($"\x1b[36mChecking status for reference:\x1b[0m {reference}\n");
            var result = await client.CheckPaymentStatusAsync(reference);
            Console.WriteLine($"\x1b[1mHTTP {result.HttpStatus}\x1b[0m");
            Console.WriteLine(result.ResponseBody);
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\nError: {ex.Message}");
            Console.ResetColor();
            Environment.Exit(1);
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
