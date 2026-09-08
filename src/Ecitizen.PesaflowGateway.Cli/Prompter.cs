using System;
using System.Collections.Generic;

namespace Ecitizen.PesaflowGateway.Cli;

public static class Prompter
{
    public static string Ask(string message, string? defaultValue = null, Func<string, bool>? validate = null, string? validationMessage = null)
    {
        while (true)
        {
            if (!string.IsNullOrEmpty(defaultValue))
            {
                Console.Write($"{message} [{defaultValue}]: ");
            }
            else
            {
                Console.Write($"{message}: ");
            }

            var input = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(input))
            {
                if (!string.IsNullOrEmpty(defaultValue))
                {
                    return defaultValue!;
                }
            }
            else
            {
                var trimmed = input.Trim();
                if (validate == null || validate(trimmed))
                {
                    return trimmed;
                }

                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(validationMessage ?? "Invalid input. Please try again.");
                Console.ResetColor();
                continue;
            }

            if (validate == null || validate(defaultValue ?? string.Empty))
            {
                return defaultValue ?? string.Empty;
            }

            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(validationMessage ?? "Field cannot be empty.");
            Console.ResetColor();
        }
    }

    public static string AskSecret(string message, string? defaultValue = null, Func<string, bool>? validate = null, string? validationMessage = null)
    {
        while (true)
        {
            if (!string.IsNullOrEmpty(defaultValue))
            {
                Console.Write($"{message} [****]: ");
            }
            else
            {
                Console.Write($"{message}: ");
            }

            var password = ReadPassword();
            if (string.IsNullOrWhiteSpace(password))
            {
                if (!string.IsNullOrEmpty(defaultValue))
                {
                    return defaultValue!;
                }
            }
            else
            {
                var trimmed = password.Trim();
                if (validate == null || validate(trimmed))
                {
                    return trimmed;
                }

                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(validationMessage ?? "Invalid input. Please try again.");
                Console.ResetColor();
                continue;
            }

            if (validate == null || validate(defaultValue ?? string.Empty))
            {
                return defaultValue ?? string.Empty;
            }

            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(validationMessage ?? "Field cannot be empty.");
            Console.ResetColor();
        }
    }

    public static bool Confirm(string message, bool defaultYes = true)
    {
        var hint = defaultYes ? "[Y/n]" : "[y/N]";
        Console.Write($"{message} {hint}: ");
        var input = Console.ReadLine()?.Trim().ToLowerInvariant();

        if (string.IsNullOrEmpty(input))
        {
            return defaultYes;
        }

        return input is "y" or "yes" or "true" or "1";
    }

    public static string Select(string message, IReadOnlyList<(string Label, string Value)> choices, int defaultIndex = 0)
    {
        Console.WriteLine(message);
        for (int i = 0; i < choices.Count; i++)
        {
            var isDefault = i == defaultIndex ? " (default)" : "";
            Console.WriteLine($"  {i + 1}) {choices[i].Label}{isDefault}");
        }

        while (true)
        {
            Console.Write($"Enter selection (1-{choices.Count}) [{(defaultIndex + 1)}]: ");
            var input = Console.ReadLine()?.Trim();
            if (string.IsNullOrEmpty(input))
            {
                return choices[defaultIndex].Value;
            }

            if (int.TryParse(input, out var choice) && choice >= 1 && choice <= choices.Count)
            {
                return choices[choice - 1].Value;
            }

            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Please enter a number between 1 and {choices.Count}.");
            Console.ResetColor();
        }
    }

    private static string ReadPassword()
    {
        var pass = "";
        while (true)
        {
            var key = Console.ReadKey(true);
            if (key.Key == ConsoleKey.Enter)
            {
                Console.WriteLine();
                break;
            }
            if (key.Key == ConsoleKey.Backspace)
            {
                if (pass.Length > 0)
                {
                    pass = pass.Substring(0, pass.Length - 1);
                    Console.Write("\b \b");
                }
            }
            else if (!char.IsControl(key.KeyChar))
            {
                pass += key.KeyChar;
                Console.Write("*");
            }
        }
        return pass;
    }
}
