using System.Collections.Generic;

namespace Ecitizen.PesaflowGateway.Models;

/// <summary>
/// Friendly payment parameters for building an eCitizen checkout or direct payment prompt.
/// </summary>
public class PaymentInput
{
    /// <summary>
    /// Amount to charge (e.g. 500.00).
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Unique merchant invoice or transaction reference (e.g. "INV-0001").
    /// </summary>
    public string Reference { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable description of what the payment is for (e.g. "Land Rates Clearance").
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Full name of the payer.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// National ID or Passport number of the payer.
    /// </summary>
    public string IdNumber { get; set; } = string.Empty;

    /// <summary>
    /// Payer's phone number. Will be normalized to 2547XXXXXXXX / 2541XXXXXXXX for STK push.
    /// </summary>
    public string? Phone { get; set; }

    /// <summary>
    /// Payer's email address.
    /// </summary>
    public string? Email { get; set; }

    /// <summary>
    /// Three-letter currency code (defaults to KES if not specified).
    /// </summary>
    public string? Currency { get; set; }

    /// <summary>
    /// Browser redirect URL after successful payment completion.
    /// </summary>
    public string? CallbackUrl { get; set; }

    /// <summary>
    /// Alias for CallbackUrl.
    /// </summary>
    public string? SuccessUrl
    {
        get => CallbackUrl;
        set => CallbackUrl = value;
    }

    /// <summary>
    /// Server-to-server webhook IPN URL for payment settlement confirmation.
    /// </summary>
    public string? NotifyUrl { get; set; }

    /// <summary>
    /// Whether to trigger an automatic M-Pesa STK push to the provided Phone.
    /// </summary>
    public bool? SendStkPush { get; set; }

    /// <summary>
    /// Optional logo/service image URL displayed on eCitizen checkout.
    /// </summary>
    public string? PictureURL { get; set; }

    /// <summary>
    /// Checkout format, defaults to "iframe".
    /// </summary>
    public string? Format { get; set; }

    /// <summary>
    /// Additional custom fields to pass through to the eCitizen payload.
    /// </summary>
    public IDictionary<string, string>? ExtraFields { get; set; }
}
