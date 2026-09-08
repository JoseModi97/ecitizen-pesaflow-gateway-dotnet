using System.Collections.Generic;

namespace Ecitizen.PesaflowGateway.Models;

/// <summary>
/// Result of verifying an inbound eCitizen payment callback/notification.
/// </summary>
public class VerifyResult
{
    /// <summary>
    /// True when the signature is cryptographically valid and status is recognized as successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// True when the HMAC SHA256 signature matches the configured secret.
    /// </summary>
    public bool SignatureValid { get; set; }

    /// <summary>
    /// Payment status reported by eCitizen (e.g. "Settled", "Paid", "Failed").
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Merchant invoice or transaction reference.
    /// </summary>
    public string Reference { get; set; } = string.Empty;

    /// <summary>
    /// Amount paid as reported in the callback.
    /// </summary>
    public decimal AmountPaid { get; set; }

    /// <summary>
    /// Descriptive message explaining status or verification result.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Raw callback payload dictionary.
    /// </summary>
    public IReadOnlyDictionary<string, string> Raw { get; set; } = new Dictionary<string, string>();
}
