using System;
using System.Threading.Tasks;
using Ecitizen.PesaflowGateway;
using Ecitizen.PesaflowGateway.Models;
using Microsoft.AspNetCore.Http;

namespace Ecitizen.PesaflowGateway.AspNetCore;

/// <summary>
/// Helper for handling eCitizen webhooks in classic MVC controllers or custom handlers.
/// </summary>
public static class EcitizenWebhookHandler
{
    /// <summary>
    /// Processes an incoming eCitizen webhook request and returns an <see cref="IResult"/>.
    /// </summary>
    public static async Task<IResult> ProcessAsync(
        HttpRequest request,
        EcitizenClient client,
        Func<VerifyResult, Task>? onSuccess = null,
        Func<VerifyResult, Task>? onFailure = null)
    {
        var payload = await EndpointRouteBuilderExtensions.ReadPayloadAsync(request).ConfigureAwait(false);
        var result = client.Verify(payload);

        if (result.Success)
        {
            if (onSuccess != null)
            {
                await onSuccess(result).ConfigureAwait(false);
            }

            return Results.Ok(new { status = "ok" });
        }

        if (onFailure != null)
        {
            await onFailure(result).ConfigureAwait(false);
        }

        return Results.BadRequest(new
        {
            status = "error",
            message = string.IsNullOrWhiteSpace(result.Description)
                ? "Invalid signature or unconfirmed payment status."
                : result.Description
        });
    }
}
