using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Ecitizen.PesaflowGateway.Internal;

namespace Ecitizen.PesaflowGateway;

/// <summary>
/// Builds and signs eCitizen PaymentAPI checkout payloads, and verifies
/// inbound callback/notification signatures against the configured credentials.
/// Core engine with zero third-party dependencies using native .NET crypto.
/// </summary>
public class EcitizenGateway
{
    public string ApiClientID { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string Secret { get; set; } = string.Empty;
    public string ServiceID { get; set; } = string.Empty;
    public string Url { get; set; } = EcitizenConfig.DefaultGatewayUrl;
    public string? StatusUrl { get; set; }
    public string? PictureURL { get; set; }
    public string Currency { get; set; } = EcitizenConfig.DefaultCurrency;
    public bool SendSTK { get; set; }
    public IList<string> SuccessStatuses { get; set; } = new List<string>(EcitizenConfig.DefaultSuccessStatuses);

    public EcitizenGateway()
    {
    }

    public EcitizenGateway(EcitizenConfig config)
    {
        if (config == null) throw new ArgumentNullException(nameof(config));

        ApiClientID = config.ApiClientID;
        ApiKey = config.ApiKey;
        Secret = config.Secret;
        ServiceID = config.ServiceID;
        Url = string.IsNullOrWhiteSpace(config.Url) ? EcitizenConfig.DefaultGatewayUrl : config.Url;
        StatusUrl = config.StatusUrl;
        PictureURL = config.PictureURL;
        Currency = string.IsNullOrWhiteSpace(config.Currency) ? EcitizenConfig.DefaultCurrency : config.Currency;
        SendSTK = config.SendSTK;

        if (config.SuccessStatuses != null && config.SuccessStatuses.Count > 0)
        {
            SuccessStatuses = config.SuccessStatuses.Select(s => s.ToLowerInvariant().Trim()).ToList();
        }

        AssertConfigured();
    }

    /// <summary>
    /// Asserts that all required settings have been configured.
    /// </summary>
    public void AssertConfigured()
    {
        if (string.IsNullOrWhiteSpace(ApiClientID))
            throw new InvalidOperationException("The eCitizen gateway 'apiClientID' setting is required.");
        if (string.IsNullOrWhiteSpace(ApiKey))
            throw new InvalidOperationException("The eCitizen gateway 'apiKey' setting is required.");
        if (string.IsNullOrWhiteSpace(Secret))
            throw new InvalidOperationException("The eCitizen gateway 'secret' setting is required.");
        if (string.IsNullOrWhiteSpace(ServiceID))
            throw new InvalidOperationException("The eCitizen gateway 'serviceID' setting is required.");
        if (string.IsNullOrWhiteSpace(Url))
            throw new InvalidOperationException("The eCitizen gateway 'url' setting is required.");
    }

    /// <summary>
    /// Builds a signed checkout payload ready to post/auto-submit to the
    /// eCitizen PaymentAPI iframe endpoint.
    /// </summary>
    public Dictionary<string, string> CreateCheckoutPayload(IReadOnlyDictionary<string, object?> invoice)
    {
        AssertConfigured();

        var amountObj = GetValue(invoice, "amountExpected") ?? GetValue(invoice, "amount");
        var amount = NormalizeAmount(amountObj);

        var billRefNumber = (GetValue(invoice, "billRefNumber")?.ToString() ?? string.Empty).Trim();
        var billDesc = (GetValue(invoice, "billDesc")?.ToString() ?? string.Empty).Trim();
        var clientName = (GetValue(invoice, "clientName")?.ToString() ?? string.Empty).Trim();
        var clientIDNumber = (GetValue(invoice, "clientIDNumber")?.ToString() ?? string.Empty).Trim();

        var requiredFields = new Dictionary<string, string>
        {
            ["amountExpected"] = amount,
            ["billRefNumber"] = billRefNumber,
            ["billDesc"] = billDesc,
            ["clientName"] = clientName,
            ["clientIDNumber"] = clientIDNumber,
        };

        foreach (var kvp in requiredFields)
        {
            if (string.IsNullOrEmpty(kvp.Value))
            {
                throw new InvalidOperationException($"The eCitizen checkout field '{kvp.Key}' is required.");
            }
        }

        var sendStkObj = GetValue(invoice, "sendSTK");
        var isSendStk = sendStkObj != null ? IsTruthy(sendStkObj) : SendSTK;

        var currencyVal = GetValue(invoice, "currency")?.ToString();
        var clientCurrency = string.IsNullOrWhiteSpace(currencyVal) ? Currency : currencyVal!;

        var payload = new Dictionary<string, string>
        {
            ["apiClientID"] = ApiClientID,
            ["serviceID"] = ServiceID,
            ["billDesc"] = billDesc,
            ["currency"] = clientCurrency,
            ["billRefNumber"] = billRefNumber,
            ["clientMSISDN"] = (GetValue(invoice, "clientMSISDN")?.ToString() ?? string.Empty).Trim(),
            ["clientName"] = clientName,
            ["clientIDNumber"] = clientIDNumber,
            ["clientEmail"] = (GetValue(invoice, "clientEmail")?.ToString() ?? string.Empty).Trim(),
            ["callBackURLOnSuccess"] = (GetValue(invoice, "callBackURLOnSuccess")?.ToString() ?? string.Empty).Trim(),
            ["amountExpected"] = amount,
            ["notificationURL"] = (GetValue(invoice, "notificationURL")?.ToString() ?? string.Empty).Trim(),
            ["pictureURL"] = (GetValue(invoice, "pictureURL")?.ToString() ?? PictureURL ?? string.Empty).Trim(),
            ["format"] = (GetValue(invoice, "format")?.ToString() ?? "iframe").Trim(),
        };

        if (isSendStk)
        {
            payload["sendSTK"] = "true";
        }

        // Add any extra pass-through fields
        foreach (var entry in invoice)
        {
            if (!payload.ContainsKey(entry.Key) && entry.Value != null)
            {
                payload[entry.Key] = entry.Value.ToString() ?? string.Empty;
            }
        }

        payload["secureHash"] = GenerateCheckoutHash(payload);

        return payload;
    }

    /// <summary>
    /// Signature order matches the eCitizen PaymentAPI checkout spec.
    /// Concat: apiClientID + amountExpected + serviceID + clientIDNumber + currency + billRefNumber + billDesc + clientName + secret
    /// HMAC-SHA256 with key apiKey, Base64-encoded.
    /// </summary>
    public string GenerateCheckoutHash(IReadOnlyDictionary<string, string> payload)
    {
        AssertConfigured();

        var amountExpected = GetValue(payload, "amountExpected") ?? string.Empty;
        var clientIDNumber = GetValue(payload, "clientIDNumber") ?? string.Empty;
        var currency = GetValue(payload, "currency") ?? string.Empty;
        var billRefNumber = GetValue(payload, "billRefNumber") ?? string.Empty;
        var billDesc = GetValue(payload, "billDesc") ?? string.Empty;
        var clientName = GetValue(payload, "clientName") ?? string.Empty;

        var dataString =
            ApiClientID +
            amountExpected +
            ServiceID +
            clientIDNumber +
            currency +
            billRefNumber +
            billDesc +
            clientName +
            Secret;

        return CryptoUtils.HmacSha256HexThenBase64(dataString, ApiKey);
    }

    /// <summary>
    /// Verifies the secure_hash/secureHash on an inbound callback or
    /// notification payload against the configured credentials.
    /// </summary>
    public bool VerifyNotificationHash(IReadOnlyDictionary<string, string> payload)
    {
        AssertConfigured();

        var providedHash = (GetValue(payload, "secure_hash") ?? GetValue(payload, "secureHash") ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(providedHash))
        {
            return false;
        }

        var clientInvoiceRef = GetValue(payload, "client_invoice_ref") ?? GetValue(payload, "billRefNumber") ?? string.Empty;
        var invoiceNumber = GetValue(payload, "invoice_number") ?? string.Empty;
        var amountPaid = GetValue(payload, "amount_paid") ?? GetValue(payload, "amount") ?? string.Empty;
        var paymentDate = GetValue(payload, "payment_date") ?? string.Empty;

        var dataString =
            clientInvoiceRef +
            invoiceNumber +
            amountPaid +
            paymentDate +
            Secret;

        var expectedHash = CryptoUtils.HmacSha256HexThenBase64(dataString, ApiKey);

        return CryptoUtils.FixedTimeEquals(expectedHash, providedHash);
    }

    /// <summary>
    /// Overload for object dictionaries (e.g. from JSON deserialization).
    /// </summary>
    public bool VerifyNotificationHash(IReadOnlyDictionary<string, object?> payload)
    {
        var stringDict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var kvp in payload)
        {
            stringDict[kvp.Key] = kvp.Value?.ToString() ?? string.Empty;
        }
        return VerifyNotificationHash(stringDict);
    }

    /// <summary>
    /// True when the payload's status field is one of successStatuses.
    /// </summary>
    public bool IsSuccessStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return false;
        }

        var clean = status!.Trim().ToLowerInvariant();
        return SuccessStatuses.Contains(clean);
    }

    /// <summary>
    /// Normalizes amounts to standard two-decimal string representation (e.g. 500.00).
    /// </summary>
    public string NormalizeAmount(object? amount)
    {
        if (amount == null) return string.Empty;

        if (amount is decimal d)
            return d.ToString("F2", CultureInfo.InvariantCulture);

        if (amount is double dbl)
            return dbl.ToString("F2", CultureInfo.InvariantCulture);

        if (amount is float f)
            return f.ToString("F2", CultureInfo.InvariantCulture);

        if (amount is int or long or short)
            return Convert.ToDecimal(amount).ToString("F2", CultureInfo.InvariantCulture);

        var str = amount.ToString();
        if (string.IsNullOrWhiteSpace(str)) return string.Empty;

        if (decimal.TryParse(str, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed.ToString("F2", CultureInfo.InvariantCulture);
        }

        return string.Empty;
    }

    private static object? GetValue(IReadOnlyDictionary<string, object?> dict, string key)
    {
        if (dict.TryGetValue(key, out var val)) return val;
        // Case-insensitive lookup fallback
        foreach (var entry in dict)
        {
            if (string.Equals(entry.Key, key, StringComparison.OrdinalIgnoreCase))
                return entry.Value;
        }
        return null;
    }

    private static string? GetValue(IReadOnlyDictionary<string, string> dict, string key)
    {
        if (dict.TryGetValue(key, out var val)) return val;
        foreach (var entry in dict)
        {
            if (string.Equals(entry.Key, key, StringComparison.OrdinalIgnoreCase))
                return entry.Value;
        }
        return null;
    }

    private static bool IsTruthy(object value)
    {
        if (value is bool b) return b;
        if (value is int i) return i == 1;
        var s = value.ToString()?.Trim().ToLowerInvariant();
        return s is "true" or "1" or "yes" or "on";
    }
}
