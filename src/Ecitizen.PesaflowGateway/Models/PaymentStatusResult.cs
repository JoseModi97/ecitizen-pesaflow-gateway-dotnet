namespace Ecitizen.PesaflowGateway.Models;

/// <summary>
/// Result of querying the settlement status of an invoice from the eCitizen status URL.
/// </summary>
public class PaymentStatusResult
{
    public string RequestUrl { get; set; } = string.Empty;
    public int HttpStatus { get; set; }
    public string ResponseBody { get; set; } = string.Empty;
}
