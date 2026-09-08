using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Ecitizen.PesaflowGateway.Cli;

public class SetupAnswers
{
    public string ApiClientID { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string Secret { get; set; } = string.Empty;
    public string ServiceID { get; set; } = string.Empty;
    public string GatewayUrl { get; set; } = EcitizenConfig.DefaultGatewayUrl;
    public string Currency { get; set; } = EcitizenConfig.DefaultCurrency;
    public string Framework { get; set; } = "minimal"; // "minimal", "mvc", "console"
    public string TargetDir { get; set; } = string.Empty;
}

public static class ProjectScaffolder
{
    public static (string DetectedFramework, string? CsprojPath) DetectEnvironment(string targetDir)
    {
        var csprojFiles = Directory.GetFiles(targetDir, "*.csproj", SearchOption.TopDirectoryOnly);
        var csprojPath = csprojFiles.Length > 0 ? csprojFiles[0] : null;

        var hasControllers = Directory.Exists(Path.Combine(targetDir, "Controllers"));
        if (hasControllers)
        {
            return ("mvc", csprojPath);
        }

        if (csprojPath != null)
        {
            var content = File.ReadAllText(csprojPath);
            if (content.Contains("Microsoft.NET.Sdk.Web"))
            {
                return ("minimal", csprojPath);
            }
        }

        return ("minimal", csprojPath);
    }

    public static string UpdateAppSettings(string targetDir, SetupAnswers answers)
    {
        var settingsPath = Path.Combine(targetDir, "appsettings.Development.json");
        if (!File.Exists(settingsPath))
        {
            settingsPath = Path.Combine(targetDir, "appsettings.json");
        }

        JsonObject rootObj;
        if (File.Exists(settingsPath))
        {
            try
            {
                var text = File.ReadAllText(settingsPath);
                rootObj = JsonNode.Parse(text)?.AsObject() ?? new JsonObject();
            }
            catch
            {
                rootObj = new JsonObject();
            }
        }
        else
        {
            settingsPath = Path.Combine(targetDir, "appsettings.json");
            rootObj = new JsonObject();
        }

        var ecitizenNode = new JsonObject
        {
            ["ApiClientID"] = answers.ApiClientID,
            ["ApiKey"] = answers.ApiKey,
            ["Secret"] = answers.Secret,
            ["ServiceID"] = answers.ServiceID,
            ["Url"] = answers.GatewayUrl,
            ["Currency"] = answers.Currency
        };

        rootObj["Ecitizen"] = ecitizenNode;

        var options = new JsonSerializerOptions { WriteIndented = true };
        File.WriteAllText(settingsPath, rootObj.ToJsonString(options) + Environment.NewLine);

        return settingsPath;
    }

    public static List<string> ScaffoldFiles(string targetDir, SetupAnswers answers)
    {
        var created = new List<string>();

        switch (answers.Framework.ToLowerInvariant())
        {
            case "mvc":
            {
                var controllersDir = Path.Combine(targetDir, "Controllers");
                Directory.CreateDirectory(controllersDir);
                var controllerPath = Path.Combine(controllersDir, "PaymentController.cs");

                var code = @"using System;
using System.Threading.Tasks;
using Ecitizen.PesaflowGateway;
using Ecitizen.PesaflowGateway.AspNetCore;
using Ecitizen.PesaflowGateway.Models;
using Microsoft.AspNetCore.Mvc;

namespace YourApp.Controllers;

[ApiController]
[Route(""[controller]"")]
public class PaymentController : ControllerBase
{
    private readonly EcitizenClient _client;

    public PaymentController(EcitizenClient client)
    {
        _client = client;
    }

    /// <summary>
    /// 1. Checkout & Pay Button View
    /// Generates an HTML pay button posting to eCitizen PaymentAPI.
    /// </summary>
    [HttpGet(""pay"")]
    public ContentResult Pay(
        [FromQuery] decimal amount = 500,
        [FromQuery] string? reference = null,
        [FromQuery] string description = ""Payment for services"",
        [FromQuery] string name = ""Jane Doe"",
        [FromQuery] string idNumber = ""12345678"",
        [FromQuery] string phone = ""0712345678"")
    {
        var refNo = string.IsNullOrWhiteSpace(reference) ? $""INV-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}"" : reference!;
        var baseUrl = $""{Request.Scheme}://{Request.Host}"";

        var payButtonHtml = _client.PayButton(new PaymentInput
        {
            Amount = amount,
            Reference = refNo,
            Description = description,
            Name = name,
            IdNumber = idNumber,
            Phone = phone,
            SendStkPush = true,
            CallbackUrl = $""{baseUrl}/Payment/Success?reference={Uri.EscapeDataString(refNo)}"",
            NotifyUrl = $""{baseUrl}/Payment/Notify"",
        }, ""Proceed to eCitizen"", new PayButtonOptions { Class = ""btn btn-success btn-lg"" });

        var html = $@""<!DOCTYPE html>
<html>
<head>
    <title>eCitizen Checkout</title>
    <link rel='stylesheet' href='https://cdn.jsdelivr.net/npm/bootstrap@5.3.0/dist/css/bootstrap.min.css'>
</head>
<body class='bg-light py-5'>
    <div class='container text-center' style='max-width: 500px;'>
        <div class='card p-4 shadow-sm'>
            <h3 class='card-title mb-3'>Invoice #{refNo}</h3>
            <p class='lead'>Amount: <strong>KES {amount:F2}</strong></p>
            <p class='text-muted'>{description}</p>
            <div class='mt-3'>
                {payButtonHtml}
            </div>
        </div>
    </div>
</body>
</html>"";

        return Content(html, ""text/html"");
    }

    /// <summary>
    /// 2. Inbound Server-to-Server IPN Webhook Notification
    /// Automatically validates HMAC signature against merchant secret.
    /// </summary>
    [HttpPost(""notify"")]
    public async Task<IResult> Notify()
    {
        return await EcitizenWebhookHandler.ProcessAsync(Request, _client,
            onSuccess: async (result) =>
            {
                // TODO: Update your database record here
                // await db.Orders.Where(o => o.Reference == result.Reference).ExecuteUpdateAsync(...);
                Console.WriteLine($""[eCitizen] Payment confirmed for {result.Reference}, Amount: {result.AmountPaid}"");
            },
            onFailure: async (result) =>
            {
                Console.WriteLine($""[eCitizen] Payment rejected: {result.Description}"");
            });
    }

    /// <summary>
    /// 3. Payer Return Page
    /// </summary>
    [HttpGet(""success"")]
    public ContentResult Success([FromQuery] string? reference)
    {
        var html = $@""<!DOCTYPE html>
<html>
<head>
    <title>Payment Received</title>
    <link rel='stylesheet' href='https://cdn.jsdelivr.net/npm/bootstrap@5.3.0/dist/css/bootstrap.min.css'>
</head>
<body class='bg-light py-5 text-center'>
    <div class='container' style='max-width: 480px;'>
        <div class='card p-4 shadow-sm'>
            <div class='text-success display-4 mb-2'>✓</div>
            <h2>Payment Complete</h2>
            <p class='text-muted'>Your transaction was submitted to eCitizen.</p>
            <p><strong>Reference:</strong> <code>{reference}</code></p>
        </div>
    </div>
</body>
</html>"";

        return Content(html, ""text/html"");
    }
}
";
                File.WriteAllText(controllerPath, code);
                created.Add(controllerPath);
                break;
            }

            case "console":
            {
                var demoPath = Path.Combine(targetDir, "EcitizenDemo.cs");
                var code = @"using System;
using System.Threading.Tasks;
using Ecitizen.PesaflowGateway;
using Ecitizen.PesaflowGateway.Models;

class Program
{
    static async Task Main(string[] args)
    {
        // 1. Initialize client (reads ECITIZEN_* env vars or pass EcitizenConfig)
        var client = new EcitizenClient();

        // 2. Build signed checkout
        var checkout = client.Checkout(new PaymentInput
        {
            Amount = 500,
            Reference = $""INV-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}"",
            Description = ""Land Rates Clearance"",
            Name = ""Jane Doe"",
            IdNumber = ""12345678"",
            Phone = ""0712345678"",
            SendStkPush = true,
            CallbackUrl = ""https://example.com/payment/success"",
            NotifyUrl = ""https://example.com/payment/notify""
        });

        Console.WriteLine(""eCitizen Target URL: "" + checkout.Url);
        Console.WriteLine(""Generated secureHash: "" + checkout.Payload[""secureHash""]);

        // 3. Render HTML pay button
        var html = client.PayButton(new PaymentInput
        {
            Amount = 500,
            Reference = ""INV-1001"",
            Description = ""Registration Fee"",
            Name = ""Jane Doe"",
            IdNumber = ""12345678""
        });
        Console.WriteLine(""\nHTML Form:\n"" + html);
    }
}
";
                File.WriteAllText(demoPath, code);
                created.Add(demoPath);
                break;
            }

            default: // minimal
            {
                var endpointsDir = Path.Combine(targetDir, "Endpoints");
                Directory.CreateDirectory(endpointsDir);
                var endpointPath = Path.Combine(endpointsDir, "PaymentEndpoints.cs");

                var code = @"using System;
using Ecitizen.PesaflowGateway;
using Ecitizen.PesaflowGateway.AspNetCore;
using Ecitizen.PesaflowGateway.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace YourApp.Endpoints;

public static class PaymentEndpoints
{
    public static IEndpointRouteBuilder MapPaymentEndpoints(this IEndpointRouteBuilder app)
    {
        // 1. Checkout page with HTML Pay Button
        app.MapGet(""/payment/pay"", (
            HttpContext context,
            EcitizenClient client,
            decimal? amount,
            string? reference,
            string? description,
            string? name,
            string? idNumber,
            string? phone) =>
        {
            var amt = amount ?? 500m;
            var refNo = string.IsNullOrWhiteSpace(reference) ? $""INV-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}"" : reference!;
            var desc = description ?? ""Service Fee"";
            var payer = name ?? ""Jane Doe"";
            var id = idNumber ?? ""12345678"";
            var msisdn = phone ?? ""0712345678"";

            var baseUrl = $""{context.Request.Scheme}://{context.Request.Host}"";

            var buttonHtml = client.PayButton(new PaymentInput
            {
                Amount = amt,
                Reference = refNo,
                Description = desc,
                Name = payer,
                IdNumber = id,
                Phone = msisdn,
                SendStkPush = true,
                CallbackUrl = $""{baseUrl}/payment/success?reference={Uri.EscapeDataString(refNo)}"",
                NotifyUrl = $""{baseUrl}/payment/notify""
            }, ""Proceed to eCitizen"", new PayButtonOptions { Class = ""btn btn-success btn-lg"" });

            var page = $@""<!DOCTYPE html>
<html>
<head>
    <title>eCitizen Checkout</title>
    <link rel='stylesheet' href='https://cdn.jsdelivr.net/npm/bootstrap@5.3.0/dist/css/bootstrap.min.css'>
</head>
<body class='bg-light py-5'>
    <div class='container text-center' style='max-width: 500px;'>
        <div class='card p-4 shadow-sm'>
            <h3 class='card-title mb-3'>Invoice #{refNo}</h3>
            <p class='lead'>Amount: <strong>KES {amt:F2}</strong></p>
            <p class='text-muted'>{desc}</p>
            <div class='mt-3'>
                {buttonHtml}
            </div>
        </div>
    </div>
</body>
</html>"";

            return Results.Content(page, ""text/html"");
        });

        // 2. Server-to-server IPN webhook endpoint
        app.MapEcitizenWebhook(""/payment/notify"",
            onSuccess: async (result, ctx) =>
            {
                Console.WriteLine($""[eCitizen] Payment Confirmed: {result.Reference}, Amount: {result.AmountPaid}"");
                // TODO: Update your database record here
            },
            onFailure: async (result, ctx) =>
            {
                Console.WriteLine($""[eCitizen] Payment verification failed for {result.Reference}: {result.Description}"");
            });

        // 3. Browser success landing page
        app.MapGet(""/payment/success"", (string? reference) =>
        {
            var page = $@""<!DOCTYPE html>
<html>
<head>
    <title>Payment Received</title>
    <link rel='stylesheet' href='https://cdn.jsdelivr.net/npm/bootstrap@5.3.0/dist/css/bootstrap.min.css'>
</head>
<body class='bg-light py-5 text-center'>
    <div class='container' style='max-width: 480px;'>
        <div class='card p-4 shadow-sm'>
            <div class='text-success display-4 mb-2'>✓</div>
            <h2>Payment Complete</h2>
            <p class='text-muted'>Your transaction was submitted to eCitizen.</p>
            <p><strong>Reference:</strong> <code>{reference}</code></p>
        </div>
    </div>
</body>
</html>"";

            return Results.Content(page, ""text/html"");
        });

        return app;
    }
}
";
                File.WriteAllText(endpointPath, code);
                created.Add(endpointPath);
                break;
            }
        }

        return created;
    }
}
