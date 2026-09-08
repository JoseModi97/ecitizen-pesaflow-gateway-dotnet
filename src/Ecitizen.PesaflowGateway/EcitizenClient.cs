using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Ecitizen.PesaflowGateway.Models;

namespace Ecitizen.PesaflowGateway;

/// <summary>
/// The beginner-friendly, idiomatic C# client for Kenya's eCitizen and PesaFlow PaymentAPI.
/// Supports checkout payload signing, HTML pay button generation, server-to-server payment prompt initiation,
/// status checking, and IPN callback verification.
/// </summary>
public class EcitizenClient
{
    private readonly EcitizenGateway _gateway;
    private readonly HttpClient _httpClient;
    private static readonly HttpClient SharedHttpClient = new();

    /// <summary>
    /// Initializes an EcitizenClient reading settings from environment variables (ECITIZEN_*).
    /// </summary>
    public EcitizenClient() : this(new EcitizenConfig())
    {
    }

    /// <summary>
    /// Initializes an EcitizenClient with explicit configuration options.
    /// </summary>
    public EcitizenClient(EcitizenConfig config, HttpClient? httpClient = null)
    {
        if (config == null) throw new ArgumentNullException(nameof(config));
        _gateway = new EcitizenGateway(config);
        _httpClient = httpClient ?? SharedHttpClient;
    }

    /// <summary>
    /// Initializes an EcitizenClient using a configuration action.
    /// </summary>
    public EcitizenClient(Action<EcitizenConfig> configure, HttpClient? httpClient = null)
    {
        if (configure == null) throw new ArgumentNullException(nameof(configure));
        var config = new EcitizenConfig();
        configure(config);
        _gateway = new EcitizenGateway(config);
        _httpClient = httpClient ?? SharedHttpClient;
    }

    /// <summary>
    /// Builds and signs an eCitizen checkout payload.
    /// Returns { Url, Payload } - post payload to Url or use PayButton() directly.
    /// </summary>
    public CheckoutResult Checkout(PaymentInput payment)
    {
        if (payment == null) throw new ArgumentNullException(nameof(payment));
        AssertRequiredPaymentFields(payment);

        var invoice = new Dictionary<string, object?>
        {
            ["amountExpected"] = payment.Amount,
            ["billRefNumber"] = payment.Reference,
            ["billDesc"] = payment.Description,
            ["clientName"] = payment.Name,
            ["clientIDNumber"] = payment.IdNumber,
        };

        if (!string.IsNullOrWhiteSpace(payment.Phone))
        {
            invoice["clientMSISDN"] = PhoneHelper.Normalize(payment.Phone!);
        }

        if (!string.IsNullOrWhiteSpace(payment.Email))
        {
            invoice["clientEmail"] = payment.Email;
        }

        if (!string.IsNullOrWhiteSpace(payment.Currency))
        {
            invoice["currency"] = payment.Currency;
        }

        var callbackUrl = payment.CallbackUrl ?? payment.SuccessUrl;
        if (!string.IsNullOrWhiteSpace(callbackUrl))
        {
            invoice["callBackURLOnSuccess"] = callbackUrl;
        }

        if (!string.IsNullOrWhiteSpace(payment.NotifyUrl))
        {
            invoice["notificationURL"] = payment.NotifyUrl;
        }

        if (payment.SendStkPush.HasValue)
        {
            invoice["sendSTK"] = payment.SendStkPush.Value;
        }

        if (!string.IsNullOrWhiteSpace(payment.PictureURL))
        {
            invoice["pictureURL"] = payment.PictureURL;
        }

        if (!string.IsNullOrWhiteSpace(payment.Format))
        {
            invoice["format"] = payment.Format;
        }

        if (payment.ExtraFields != null)
        {
            foreach (var kvp in payment.ExtraFields)
            {
                invoice[kvp.Key] = kvp.Value;
            }
        }

        var payload = _gateway.CreateCheckoutPayload(invoice);

        return new CheckoutResult
        {
            Url = _gateway.Url,
            Payload = payload,
        };
    }

    /// <summary>
    /// Returns a ready-to-render HTML form with a submit button that sends
    /// the payer to eCitizen. No frontend JavaScript or iframe wiring required.
    /// </summary>
    public string PayButton(
        PaymentInput payment,
        string buttonLabel = "Pay with eCitizen",
        PayButtonOptions? buttonOptions = null)
    {
        var checkout = Checkout(payment);
        var options = buttonOptions ?? new PayButtonOptions();

        var sb = new StringBuilder();
        var target = string.IsNullOrWhiteSpace(options.Target) ? "_blank" : options.Target!;
        sb.Append($"<form action=\"{EscapeAttribute(checkout.Url)}\" method=\"post\" target=\"{EscapeAttribute(target)}\">");

        foreach (var kvp in checkout.Payload)
        {
            sb.Append($"<input type=\"hidden\" name=\"{EscapeAttribute(kvp.Key)}\" value=\"{EscapeAttribute(kvp.Value)}\">");
        }

        sb.Append("<button type=\"submit\"");

        if (!string.IsNullOrWhiteSpace(options.Class))
        {
            sb.Append($" class=\"{EscapeAttribute(options.Class!)}\"");
        }

        if (!string.IsNullOrWhiteSpace(options.Id))
        {
            sb.Append($" id=\"{EscapeAttribute(options.Id!)}\"");
        }

        if (!string.IsNullOrWhiteSpace(options.Style))
        {
            sb.Append($" style=\"{EscapeAttribute(options.Style!)}\"");
        }

        if (options.ExtraAttributes != null)
        {
            foreach (var attr in options.ExtraAttributes)
            {
                if (string.Equals(attr.Key, "target", StringComparison.OrdinalIgnoreCase)) continue;
                sb.Append($" {EscapeAttribute(attr.Key)}=\"{EscapeAttribute(attr.Value)}\"");
            }
        }

        sb.Append($">{EscapeText(buttonLabel)}</button>");
        sb.Append("</form>");

        return sb.ToString();
    }

    /// <summary>
    /// Signs a checkout payload and submits it directly to the eCitizen
    /// PaymentAPI from the server - no browser, no HTML form. Use this to
    /// prompt/initiate a payment (e.g. trigger an M-Pesa STK push) from a
    /// backend job, API route, or CLI.
    /// </summary>
    public async Task<PaymentSubmissionResult> InitiatePaymentAsync(
        PaymentInput payment,
        CancellationToken cancellationToken = default)
    {
        var checkout = Checkout(payment);

        using var requestContent = new FormUrlEncodedContent(checkout.Payload);
        using var response = await _httpClient.PostAsync(checkout.Url, requestContent, cancellationToken).ConfigureAwait(false);

        var responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

        return new PaymentSubmissionResult
        {
            RequestUrl = checkout.Url,
            RequestPayload = checkout.Payload,
            HttpStatus = (int)response.StatusCode,
            ResponseBody = responseBody,
        };
    }

    /// <summary>
    /// Polls the configured status endpoint (ECITIZEN_STATUS_URL / StatusUrl)
    /// for the settlement status of a previously submitted invoice reference.
    /// </summary>
    public async Task<PaymentStatusResult> CheckPaymentStatusAsync(
        string reference,
        IDictionary<string, string>? extraParams = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reference))
            throw new ArgumentException("Reference cannot be empty.", nameof(reference));

        var statusUrl = _gateway.StatusUrl;
        if (string.IsNullOrWhiteSpace(statusUrl))
        {
            throw new InvalidOperationException(
                "CheckPaymentStatusAsync requires a status URL. Set EcitizenConfig.StatusUrl or environment variable ECITIZEN_STATUS_URL."
            );
        }

        var queryParams = new Dictionary<string, string>
        {
            ["apiClientID"] = _gateway.ApiClientID,
            ["serviceID"] = _gateway.ServiceID,
            ["billRefNumber"] = reference,
        };

        if (extraParams != null)
        {
            foreach (var kvp in extraParams)
            {
                queryParams[kvp.Key] = kvp.Value;
            }
        }

        var queryBuilder = new StringBuilder();
        var separator = statusUrl!.Contains("?") ? "&" : "?";

        foreach (var param in queryParams)
        {
            queryBuilder.Append(separator);
            queryBuilder.Append(Uri.EscapeDataString(param.Key));
            queryBuilder.Append('=');
            queryBuilder.Append(Uri.EscapeDataString(param.Value));
            separator = "&";
        }

        var requestUrl = statusUrl + queryBuilder.ToString();
        using var response = await _httpClient.GetAsync(requestUrl, cancellationToken).ConfigureAwait(false);
        var responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

        return new PaymentStatusResult
        {
            RequestUrl = requestUrl,
            HttpStatus = (int)response.StatusCode,
            ResponseBody = responseBody,
        };
    }

    /// <summary>
    /// Checks a callback/notification payload from eCitizen.
    /// Hand the POST form dictionary / webhook payload straight to Verify(data).
    /// </summary>
    public VerifyResult Verify(IReadOnlyDictionary<string, string> callbackData)
    {
        if (callbackData == null) throw new ArgumentNullException(nameof(callbackData));

        var status = (GetValue(callbackData, "status") ?? string.Empty).Trim();
        var signatureValid = _gateway.VerifyNotificationHash(callbackData);
        var success = signatureValid && _gateway.IsSuccessStatus(status);

        var reference = (GetValue(callbackData, "client_invoice_ref") ?? GetValue(callbackData, "billRefNumber") ?? string.Empty).Trim();
        var amountStr = GetValue(callbackData, "amount_paid") ?? GetValue(callbackData, "amount");

        decimal amountPaid = 0;
        if (!string.IsNullOrWhiteSpace(amountStr))
        {
            decimal.TryParse(amountStr, NumberStyles.Any, CultureInfo.InvariantCulture, out amountPaid);
        }

        var description = GetValue(callbackData, "description") ??
                          GetValue(callbackData, "billDesc") ??
                          (success ? "Payment verified successfully" : "Payment verification failed");

        return new VerifyResult
        {
            Success = success,
            SignatureValid = signatureValid,
            Status = status,
            Reference = reference,
            AmountPaid = amountPaid,
            Raw = callbackData,
            Description = description,
        };
    }

    /// <summary>
    /// Overload taking an object dictionary (e.g. deserialized JSON webhook).
    /// </summary>
    public VerifyResult Verify(IReadOnlyDictionary<string, object?> callbackData)
    {
        if (callbackData == null) throw new ArgumentNullException(nameof(callbackData));

        var stringDict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var kvp in callbackData)
        {
            stringDict[kvp.Key] = kvp.Value?.ToString() ?? string.Empty;
        }

        return Verify(stringDict);
    }

    /// <summary>
    /// Shorthand for Verify(callbackData).Success.
    /// </summary>
    public bool IsPaid(IReadOnlyDictionary<string, string> callbackData) => Verify(callbackData).Success;

    /// <summary>
    /// Shorthand for Verify(callbackData).Success.
    /// </summary>
    public bool IsPaid(IReadOnlyDictionary<string, object?> callbackData) => Verify(callbackData).Success;

    /// <summary>
    /// Access the underlying gateway engine instance.
    /// </summary>
    public EcitizenGateway GetGateway() => _gateway;

    private static void AssertRequiredPaymentFields(PaymentInput payment)
    {
        if (payment.Amount <= 0)
            throw new ArgumentException("eCitizen payment is missing: amount (must be greater than 0).", nameof(payment));

        if (string.IsNullOrWhiteSpace(payment.Reference))
            throw new ArgumentException("eCitizen payment is missing: reference (unique ID for this payment).", nameof(payment));

        if (string.IsNullOrWhiteSpace(payment.Description))
            throw new ArgumentException("eCitizen payment is missing: description (what the payment is for).", nameof(payment));

        if (string.IsNullOrWhiteSpace(payment.Name))
            throw new ArgumentException("eCitizen payment is missing: name (the payer's full name).", nameof(payment));

        if (string.IsNullOrWhiteSpace(payment.IdNumber))
            throw new ArgumentException("eCitizen payment is missing: idNumber (payer's National ID or Passport).", nameof(payment));
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

    private static string EscapeAttribute(string value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        return value
            .Replace("&", "&amp;")
            .Replace("\"", "&quot;")
            .Replace("'", "&#39;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;");
    }

    private static string EscapeText(string value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        return value
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;");
    }
}
