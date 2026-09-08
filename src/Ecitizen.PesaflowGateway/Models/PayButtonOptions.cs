using System.Collections.Generic;

namespace Ecitizen.PesaflowGateway.Models;

/// <summary>
/// Customization options for rendering the HTML payment button and form.
/// </summary>
public class PayButtonOptions
{
    /// <summary>
    /// CSS class name for the button element (defaults to "btn btn-primary").
    /// </summary>
    public string? Class { get; set; } = "btn btn-primary";

    /// <summary>
    /// HTML ID attribute for the button element.
    /// </summary>
    public string? Id { get; set; }

    /// <summary>
    /// Form target attribute (e.g. "_blank", "_self"). Defaults to "_blank".
    /// </summary>
    public string? Target { get; set; } = "_blank";

    /// <summary>
    /// Inline CSS styles for the button element.
    /// </summary>
    public string? Style { get; set; }

    /// <summary>
    /// Additional HTML attributes to add to the button element.
    /// </summary>
    public IDictionary<string, string>? ExtraAttributes { get; set; }
}
