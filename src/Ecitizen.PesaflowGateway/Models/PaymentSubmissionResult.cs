using System.Collections.Generic;

namespace Ecitizen.PesaflowGateway.Models;

/// <summary>
/// Result of directly submitting an eCitizen payment request to the PaymentAPI endpoint.
/// </summary>
public class PaymentSubmissionResult
{
    public string RequestUrl { get; set; } = string.Empty;
    public IReadOnlyDictionary<string, string> RequestPayload { get; set; } = new Dictionary<string, string>();
    public int HttpStatus { get; set; }
    public string ResponseBody { get; set; } = string.Empty;
}
