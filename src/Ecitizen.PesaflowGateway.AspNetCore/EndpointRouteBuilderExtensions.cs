using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Ecitizen.PesaflowGateway;
using Ecitizen.PesaflowGateway.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Ecitizen.PesaflowGateway.AspNetCore;

/// <summary>
/// Minimal API extensions for mapping eCitizen webhook endpoints.
/// </summary>
public static class EndpointRouteBuilderExtensions
{
    /// <summary>
    /// Maps an endpoint to receive and verify eCitizen IPN server-to-server callbacks.
    /// Exempts the route from Antiforgery validation.
    /// </summary>
    public static RouteHandlerBuilder MapEcitizenWebhook(
        this IEndpointRouteBuilder endpoints,
        string pattern,
        Func<VerifyResult, HttpContext, Task>? onSuccess = null,
        Func<VerifyResult, HttpContext, Task>? onFailure = null)
    {
        var endpoint = endpoints.MapPost(pattern, async (HttpContext context, EcitizenClient client) =>
        {
            var payload = await ReadPayloadAsync(context.Request).ConfigureAwait(false);
            var result = client.Verify(payload);

            if (result.Success)
            {
                if (onSuccess != null)
                {
                    await onSuccess(result, context).ConfigureAwait(false);
                }

                if (!context.Response.HasStarted)
                {
                    return Results.Ok(new { status = "ok" });
                }

                return Results.Empty;
            }

            if (onFailure != null)
            {
                await onFailure(result, context).ConfigureAwait(false);
            }

            if (!context.Response.HasStarted)
            {
                return Results.BadRequest(new
                {
                    status = "error",
                    message = string.IsNullOrWhiteSpace(result.Description)
                        ? "Invalid signature or unconfirmed payment status."
                        : result.Description
                });
            }

            return Results.Empty;
        });

        endpoint.DisableAntiforgery();
        return endpoint;
    }

    /// <summary>
    /// Reads the webhook payload from either form-encoded or JSON requests.
    /// </summary>
    public static async Task<Dictionary<string, string>> ReadPayloadAsync(HttpRequest request)
    {
        var payload = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (request.HasFormContentType)
        {
            var form = await request.ReadFormAsync().ConfigureAwait(false);
            foreach (var key in form.Keys)
            {
                payload[key] = form[key].ToString();
            }
            return payload;
        }

        if (request.ContentType != null && request.ContentType.Contains("json"))
        {
            using var reader = new StreamReader(request.Body);
            var json = await reader.ReadToEndAsync().ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(json))
            {
                using var doc = JsonDocument.Parse(json);
                foreach (var prop in doc.RootElement.EnumerateObject())
                {
                    payload[prop.Name] = prop.Value.ToString();
                }
            }
            return payload;
        }

        // Fallback for form-encoded bodies without header or direct stream read
        using var streamReader = new StreamReader(request.Body);
        var body = await streamReader.ReadToEndAsync().ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(body))
        {
            // Try parse query-string format
            var pairs = body.Split('&');
            foreach (var pair in pairs)
            {
                var idx = pair.IndexOf('=');
                if (idx > 0)
                {
                    var k = Uri.UnescapeDataString(pair.Substring(0, idx).Replace('+', ' '));
                    var v = Uri.UnescapeDataString(pair.Substring(idx + 1).Replace('+', ' '));
                    payload[k] = v;
                }
            }
        }

        return payload;
    }
}
