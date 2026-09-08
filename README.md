# Ecitizen.PesaflowGateway

[![NuGet](https://img.shields.io/nuget/v/Ecitizen.PesaflowGateway.svg)](https://www.nuget.org/packages/Ecitizen.PesaflowGateway)
[![NuGet Downloads](https://img.shields.io/nuget/dt/Ecitizen.PesaflowGateway.svg)](https://www.nuget.org/packages/Ecitizen.PesaflowGateway)
[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-netstandard2.0%20%7C%20net8.0%20%7C%20net10.0-blue.svg)](https://dotnet.microsoft.com/)

An idiomatic, beginner-friendly .NET client and ASP.NET Core adapter suite for Kenya's **eCitizen / PesaFlow PaymentAPI** (M-Pesa STK push, checkout forms, and IPN webhook signature verification).

Authored by [Modi97](https://www.nuget.org/profiles/Modi97) / [Jose Modi](https://github.com/JoseModi97).

Faithful C# port of the Node.js package [`ecitizen-pesaflow-gateway`](https://github.com/JoseModi97/ecitizen-pesaflow-gateway), featuring:
- **100% cryptographic wire parity** with eCitizen's PHP HMAC-SHA256 reference implementation.
- **Zero third-party dependencies** in the core package.
- **Multi-targeted** for `netstandard2.0`, `net8.0`, and `net10.0` (compatible with .NET Framework 4.6.1+, .NET Core 2.0+, and all modern .NET versions).
- **First-class ASP.NET Core integration** (`AddEcitizenPesaflowGateway` DI, Minimal API `MapEcitizenWebhook`, and MVC controller helpers).
- **Global CLI tool** (`dotnet-ecitizen-pesaflow` / `ecitizen-pesaflow`) mirroring the original CLI.

---

## Packages

| Package | Description | Target Frameworks |
|---|---|---|
| **`Ecitizen.PesaflowGateway`** | Core engine: signing, checkout payload generation, pay button HTML, status checking, IPN verification | `netstandard2.0`, `net8.0`, `net10.0` |
| **`Ecitizen.PesaflowGateway.AspNetCore`** | ASP.NET Core DI extensions and Minimal API route builder | `net8.0`, `net10.0` |
| **`dotnet-ecitizen-pesaflow`** | Global CLI tool for scaffolding, testing HMAC signatures, prompting payments, and browser checkout | `net8.0` (runs on .NET 8, 9, 10+) |

---

## Installation

### Core Package
```bash
dotnet add package Ecitizen.PesaflowGateway
```

### ASP.NET Core Adapter
```bash
dotnet add package Ecitizen.PesaflowGateway.AspNetCore
```

### Global CLI Tool
```bash
dotnet tool install --global dotnet-ecitizen-pesaflow
```

---

## Quickstart: ASP.NET Core Minimal APIs

### 1. Configure Credentials (`appsettings.json`)
```json
{
  "Ecitizen": {
    "ApiClientID": "YOUR_CLIENT_ID",
    "ApiKey": "YOUR_API_KEY",
    "Secret": "YOUR_MERCHANT_SECRET",
    "ServiceID": "YOUR_SERVICE_ID",
    "Url": "https://payments.ecitizen.go.ke/PaymentAPI/iframev2.1.php",
    "Currency": "KES"
  }
}
```

Or set standard environment variables:
- `ECITIZEN_CLIENT_ID`
- `ECITIZEN_API_KEY`
- `ECITIZEN_SECRET`
- `ECITIZEN_SERVICE_ID`
- `ECITIZEN_GATEWAY_URL`
- `ECITIZEN_CURRENCY`
- `ECITIZEN_STATUS_URL`

### 2. Register & Map Endpoints (`Program.cs`)
```csharp
using Ecitizen.PesaflowGateway;
using Ecitizen.PesaflowGateway.AspNetCore;
using Ecitizen.PesaflowGateway.Models;

var builder = WebApplication.CreateBuilder(args);

// Register eCitizen services
builder.Services.AddEcitizenPesaflowGateway(builder.Configuration);

var app = builder.Build();

// 1. Checkout & Pay Button View
app.MapGet("/payment/pay", (HttpContext context, EcitizenClient client) =>
{
    var baseUrl = $"{context.Request.Scheme}://{context.Request.Host}";
    var reference = $"INV-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";

    var payButtonHtml = client.PayButton(new PaymentInput
    {
        Amount = 500.00m,
        Reference = reference,
        Description = "Land Rates Clearance",
        Name = "Jane Doe",
        IdNumber = "12345678",
        Phone = "0712345678", // Automatically normalized to 254712345678
        SendStkPush = true,
        CallbackUrl = $"{baseUrl}/payment/success?reference={Uri.EscapeDataString(reference)}",
        NotifyUrl = $"{baseUrl}/payment/notify"
    }, "Proceed to eCitizen", new PayButtonOptions { Class = "btn btn-success btn-lg" });

    return Results.Content(payButtonHtml, "text/html");
});

// 2. Server-to-server IPN Webhook Notification
// Signature verification and CSRF bypass handled automatically
app.MapEcitizenWebhook("/payment/notify",
    onSuccess: async (result, ctx) =>
    {
        Console.WriteLine($"Payment confirmed: Ref={result.Reference}, Amount={result.AmountPaid}");
        // await db.Orders.Where(o => o.Reference == result.Reference).ExecuteUpdateAsync(...);
    },
    onFailure: async (result, ctx) =>
    {
        Console.WriteLine($"Payment verification failed: {result.Description}");
    });

// 3. Browser Return Landing Page
app.MapGet("/payment/success", (string? reference) =>
    Results.Content($"<h3>Payment Complete for {reference}!</h3>", "text/html"));

app.Run();
```

---

## Quickstart: ASP.NET Core MVC Controller

```csharp
using Ecitizen.PesaflowGateway;
using Ecitizen.PesaflowGateway.AspNetCore;
using Ecitizen.PesaflowGateway.Models;
using Microsoft.AspNetCore.Mvc;

namespace MyApp.Controllers;

[ApiController]
[Route("[controller]")]
public class PaymentController : ControllerBase
{
    private readonly EcitizenClient _client;

    public PaymentController(EcitizenClient client)
    {
        _client = client;
    }

    [HttpGet("pay")]
    public ContentResult Pay()
    {
        var html = _client.PayButton(new PaymentInput
        {
            Amount = 1500.00m,
            Reference = "INV-1002",
            Description = "Trading License",
            Name = "John Doe",
            IdNumber = "28374619",
            Phone = "0712345678"
        });
        return Content(html, "text/html");
    }

    [HttpPost("notify")]
    public async Task<IResult> Notify()
    {
        return await EcitizenWebhookHandler.ProcessAsync(Request, _client,
            onSuccess: async (result) =>
            {
                // Update database
            });
    }
}
```

---

## Standalone / Console C# Usage

```csharp
using Ecitizen.PesaflowGateway;
using Ecitizen.PesaflowGateway.Models;

// 1. Initialize client directly
var client = new EcitizenClient(new EcitizenConfig
{
    ApiClientID = "YOUR_CLIENT_ID",
    ApiKey = "YOUR_API_KEY",
    Secret = "YOUR_SECRET",
    ServiceID = "YOUR_SERVICE_ID",
});

// 2. Build signed checkout payload
var checkout = client.Checkout(new PaymentInput
{
    Amount = 500m,
    Reference = "INV-0001",
    Description = "School Fees",
    Name = "Jane Doe",
    IdNumber = "12345678",
    Phone = "0712345678",
    SendStkPush = true
});

Console.WriteLine("Target URL: " + checkout.Url);
Console.WriteLine("secureHash: " + checkout.Payload["secureHash"]);

// 3. Or trigger a direct server-to-server payment prompt (STK Push)
var submission = await client.InitiatePaymentAsync(new PaymentInput
{
    Amount = 500m,
    Reference = "INV-0001",
    Description = "School Fees",
    Name = "Jane Doe",
    IdNumber = "12345678",
    Phone = "0712345678"
});

Console.WriteLine($"Response: {submission.HttpStatus} - {submission.ResponseBody}");
```

---

## Phone Number Normalization (`PhoneHelper`)

eCitizen's M-Pesa STK push requires Kenyan phone numbers in international 12-digit format (`2547XXXXXXXX` or `2541XXXXXXXX`). `PhoneHelper` handles common input formats:

```csharp
using Ecitizen.PesaflowGateway;

PhoneHelper.Normalize("0712345678");   // "254712345678"
PhoneHelper.Normalize("0112345678");   // "254112345678"
PhoneHelper.Normalize("+254712345678"); // "254712345678"
PhoneHelper.Normalize("712345678");    // "254712345678"
PhoneHelper.Normalize("112345678");    // "254112345678"

PhoneHelper.IsValidStkPhone("254712345678"); // true
PhoneHelper.IsValidStkPhone("0712345678");    // false (not yet normalized)
```

---

## CLI Global Tool (`ecitizen-pesaflow`)

The .NET global tool mirrors the Node `npx ecitizen-pesaflow` CLI:

```bash
# Interactive setup wizard (scaffolds appsettings and payment endpoints/controllers)
ecitizen-pesaflow init

# Run cryptographic parity check against known test vectors
ecitizen-pesaflow test

# Prompt a payment directly from your terminal (M-Pesa STK push)
ecitizen-pesaflow pay --amount 500 --description "Test" --name "Jane Doe" --id-number 12345678 --phone 0712345678

# Open the real eCitizen payment page in your browser and watch for settlement
ecitizen-pesaflow checkout --amount 500 --description "Test" --name "Jane Doe" --id-number 12345678

# Check settlement status of an invoice reference
ecitizen-pesaflow status --reference INV-0001
```

---

## Cryptographic Specification & Wire Parity

eCitizen / PesaFlow generates `secureHash` using a two-step encoding:
$$\text{secureHash} = \text{base64}(\text{hex}(\text{HMAC-SHA256}(\text{data}, \text{apiKey})))$$
where the hex string is strictly lowercase.

### Checkout Hash Data String
$$\text{apiClientID} + \text{amountExpected} + \text{serviceID} + \text{clientIDNumber} + \text{currency} + \text{billRefNumber} + \text{billDesc} + \text{clientName} + \text{secret}$$

### Webhook IPN Verification Data String
$$(\text{client\_invoice\_ref} \mid \text{billRefNumber}) + \text{invoice\_number} + (\text{amount\_paid} \mid \text{amount}) + \text{payment\_date} + \text{secret}$$

Timing attacks during callback verification are prevented via timing-safe constant-time comparison (`CryptographicOperations.FixedTimeEquals`).

---

## License

MIT © [Jose Modi](https://github.com/JoseModi97) / [Modi97](https://www.nuget.org/profiles/Modi97)
