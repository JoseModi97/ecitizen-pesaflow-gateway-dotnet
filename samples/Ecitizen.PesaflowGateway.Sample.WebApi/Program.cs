using Ecitizen.PesaflowGateway;
using Ecitizen.PesaflowGateway.AspNetCore;
using Ecitizen.PesaflowGateway.Models;

var builder = WebApplication.CreateBuilder(args);

// 1. Register eCitizen / PesaFlow Gateway services
builder.Services.AddEcitizenPesaflowGateway(builder.Configuration);

var app = builder.Build();

// 2. Checkout & Pay Button View
app.MapGet("/payment/pay", (
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
    var refNo = string.IsNullOrWhiteSpace(reference) ? $"INV-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}" : reference!;
    var desc = description ?? "Land Rates Clearance";
    var payer = name ?? "Jane Doe";
    var id = idNumber ?? "12345678";
    var msisdn = phone ?? "0712345678";

    var baseUrl = $"{context.Request.Scheme}://{context.Request.Host}";

    var buttonHtml = client.PayButton(new PaymentInput
    {
        Amount = amt,
        Reference = refNo,
        Description = desc,
        Name = payer,
        IdNumber = id,
        Phone = msisdn,
        SendStkPush = true,
        CallbackUrl = $"{baseUrl}/payment/success?reference={Uri.EscapeDataString(refNo)}",
        NotifyUrl = $"{baseUrl}/payment/notify"
    }, "Proceed to eCitizen", new PayButtonOptions { Class = "btn btn-success btn-lg" });

    var html = $@"<!DOCTYPE html>
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
</html>";

    return Results.Content(html, "text/html");
});

// 3. Inbound Server-to-Server IPN Webhook
app.MapEcitizenWebhook("/payment/notify",
    onSuccess: async (result, ctx) =>
    {
        Console.WriteLine($"[eCitizen] Payment Confirmed: Reference={result.Reference}, Amount={result.AmountPaid}");
        await Task.CompletedTask;
    },
    onFailure: async (result, ctx) =>
    {
        Console.WriteLine($"[eCitizen] Payment Failed: Reference={result.Reference}, Reason={result.Description}");
        await Task.CompletedTask;
    });

// 4. Return landing page
app.MapGet("/payment/success", (string? reference) =>
{
    var html = $@"<!DOCTYPE html>
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
</html>";

    return Results.Content(html, "text/html");
});

app.MapGet("/", () => Results.Redirect("/payment/pay"));

app.Run();
