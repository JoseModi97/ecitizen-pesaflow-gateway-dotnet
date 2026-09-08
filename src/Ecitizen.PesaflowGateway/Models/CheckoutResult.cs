using System.Collections.Generic;

namespace Ecitizen.PesaflowGateway.Models;

/// <summary>
/// Result of generating an eCitizen checkout payload.
/// </summary>
public class CheckoutResult
{
    /// <summary>
    /// Target eCitizen payment iframe endpoint.
    /// </summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// Key-value dictionary containing all signed fields ready to POST.
    /// </summary>
    public IReadOnlyDictionary<string, string> Payload { get; set; } = new Dictionary<string, string>();
}
