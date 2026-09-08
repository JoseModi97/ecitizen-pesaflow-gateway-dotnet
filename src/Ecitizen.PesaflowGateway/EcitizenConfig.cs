using System;
using System.Collections.Generic;

namespace Ecitizen.PesaflowGateway;

/// <summary>
/// Configuration options for the eCitizen / PesaFlow PaymentAPI gateway.
/// </summary>
public class EcitizenConfig
{
    public const string DefaultGatewayUrl = "https://payments.ecitizen.go.ke/PaymentAPI/iframev2.1.php";
    public const string DefaultCurrency = "KES";

    public static readonly string[] DefaultSuccessStatuses =
    [
        "paid",
        "settled",
        "success",
        "successful",
        "completed",
        "complete"
    ];

    public string ApiClientID { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string Secret { get; set; } = string.Empty;
    public string ServiceID { get; set; } = string.Empty;
    public string Url { get; set; } = DefaultGatewayUrl;
    public string? StatusUrl { get; set; }
    public string? PictureURL { get; set; }
    public string Currency { get; set; } = DefaultCurrency;
    public bool SendSTK { get; set; }
    public IList<string> SuccessStatuses { get; set; } = new List<string>(DefaultSuccessStatuses);

    public EcitizenConfig()
    {
        // Populate from environment variables if present as fallback
        var envClientId = Environment.GetEnvironmentVariable("ECITIZEN_CLIENT_ID");
        if (!string.IsNullOrWhiteSpace(envClientId)) ApiClientID = envClientId!;

        var envApiKey = Environment.GetEnvironmentVariable("ECITIZEN_API_KEY");
        if (!string.IsNullOrWhiteSpace(envApiKey)) ApiKey = envApiKey!;

        var envSecret = Environment.GetEnvironmentVariable("ECITIZEN_SECRET");
        if (!string.IsNullOrWhiteSpace(envSecret)) Secret = envSecret!;

        var envServiceId = Environment.GetEnvironmentVariable("ECITIZEN_SERVICE_ID");
        if (!string.IsNullOrWhiteSpace(envServiceId)) ServiceID = envServiceId!;

        var envUrl = Environment.GetEnvironmentVariable("ECITIZEN_GATEWAY_URL");
        if (!string.IsNullOrWhiteSpace(envUrl)) Url = envUrl!;

        var envStatusUrl = Environment.GetEnvironmentVariable("ECITIZEN_STATUS_URL");
        if (!string.IsNullOrWhiteSpace(envStatusUrl)) StatusUrl = envStatusUrl;

        var envCurrency = Environment.GetEnvironmentVariable("ECITIZEN_CURRENCY");
        if (!string.IsNullOrWhiteSpace(envCurrency)) Currency = envCurrency!;
    }
}
