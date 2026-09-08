using System;
using System.IO;
using System.Text.Json.Nodes;

namespace Ecitizen.PesaflowGateway.Cli;

public static class CliConfigLoader
{
    public static EcitizenConfig LoadConfig()
    {
        var config = new EcitizenConfig();

        // If environment variables already provided credentials, use them
        if (!string.IsNullOrWhiteSpace(config.ApiClientID) &&
            !string.IsNullOrWhiteSpace(config.ApiKey) &&
            !string.IsNullOrWhiteSpace(config.Secret) &&
            !string.IsNullOrWhiteSpace(config.ServiceID))
        {
            return config;
        }

        // Search for appsettings.Development.json or appsettings.json in current or parent directories
        var current = Directory.GetCurrentDirectory();
        while (!string.IsNullOrWhiteSpace(current))
        {
            var devJson = Path.Combine(current, "appsettings.Development.json");
            var prodJson = Path.Combine(current, "appsettings.json");

            if (TryLoadFromJson(devJson, config) || TryLoadFromJson(prodJson, config))
            {
                break;
            }

            var parent = Directory.GetParent(current);
            if (parent == null || parent.FullName == current) break;
            current = parent.FullName;
        }

        return config;
    }

    private static bool TryLoadFromJson(string filePath, EcitizenConfig config)
    {
        if (!File.Exists(filePath)) return false;

        try
        {
            var text = File.ReadAllText(filePath);
            var root = JsonNode.Parse(text)?.AsObject();
            if (root == null) return false;

            // Check if nested under "Ecitizen" or directly at root
            var node = (root["Ecitizen"] as JsonObject) ?? root;

            var id = node["ApiClientID"]?.ToString() ?? node["apiClientID"]?.ToString() ?? node["ClientId"]?.ToString();
            var key = node["ApiKey"]?.ToString() ?? node["apiKey"]?.ToString();
            var secret = node["Secret"]?.ToString() ?? node["secret"]?.ToString();
            var svc = node["ServiceID"]?.ToString() ?? node["serviceID"]?.ToString() ?? node["ServiceId"]?.ToString();
            var url = node["Url"]?.ToString() ?? node["url"]?.ToString() ?? node["GatewayUrl"]?.ToString();
            var curr = node["Currency"]?.ToString() ?? node["currency"]?.ToString();
            var statusUrl = node["StatusUrl"]?.ToString() ?? node["statusUrl"]?.ToString();

            bool updated = false;
            if (string.IsNullOrWhiteSpace(config.ApiClientID) && !string.IsNullOrWhiteSpace(id)) { config.ApiClientID = id; updated = true; }
            if (string.IsNullOrWhiteSpace(config.ApiKey) && !string.IsNullOrWhiteSpace(key)) { config.ApiKey = key; updated = true; }
            if (string.IsNullOrWhiteSpace(config.Secret) && !string.IsNullOrWhiteSpace(secret)) { config.Secret = secret; updated = true; }
            if (string.IsNullOrWhiteSpace(config.ServiceID) && !string.IsNullOrWhiteSpace(svc)) { config.ServiceID = svc; updated = true; }
            if (!string.IsNullOrWhiteSpace(url)) config.Url = url;
            if (!string.IsNullOrWhiteSpace(curr)) config.Currency = curr;
            if (!string.IsNullOrWhiteSpace(statusUrl)) config.StatusUrl = statusUrl;

            return updated && !string.IsNullOrWhiteSpace(config.ApiClientID);
        }
        catch
        {
            return false;
        }
    }
}
