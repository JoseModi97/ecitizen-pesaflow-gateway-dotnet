using Ecitizen.PesaflowGateway;
using Ecitizen.PesaflowGateway.AspNetCore;
using Ecitizen.PesaflowGateway.Models;

var builder = WebApplication.CreateBuilder(args);

// 1. Register eCitizen / PesaFlow Gateway in Dependency Injection
builder.Services.AddEcitizenPesaflowGateway(builder.Configuration);

var app = builder.Build();

// 2. Render Checkout Form / Pay Button
app.MapGet("/payment/checkout", (HttpContext context, EcitizenClient client, decimal? amount, string? refNumber) =>
{
    var amt = amount ?? 1000m;
    var reference = refNumber ?? $"INV-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
    var baseUrl = $"{context.Request.Scheme}://{context.Request.Host}";

    var payButtonHtml = client.PayButton(new PaymentInput
    {
        Amount = amt,
        Reference = reference,
        Description = "Example Service Payment",
        Name = "Jane Doe",
        IdNumber = "12345678",
        Phone = "0712345678",
        SendStkPush = false, // false opens full iframe with all payment options (Cards, Airtel, RTGS, M-Pesa)
        CallbackUrl = $"{baseUrl}/payment/success?ref={Uri.EscapeDataString(reference)}",
        NotifyUrl = $"{baseUrl}/payment/notify"
    }, "Pay with eCitizen", new PayButtonOptions { Class = "btn btn-primary btn-lg" });

    var html = $@"<!DOCTYPE html>
<html>
<head><title>eCitizen Checkout</title><link rel='stylesheet' href='https://cdn.jsdelivr.net/npm/bootstrap@5.3.0/dist/css/bootstrap.min.css'></head>
<body class='bg-light py-5 text-center'>
    <div class='container' style='max-width: 480px;'>
        <div class='card p-4 shadow-sm'>
            <h2>Order Checkout</h2>
            <p class='lead'>Amount: <strong>KES {amt:F2}</strong></p>
            <p>Invoice: <code>{reference}</code></p>
            <div class='mt-3'>{payButtonHtml}</div>
        </div>
    </div>
</body>
</html>";

    return Results.Content(html, "text/html");
});

// 3. Server-to-server IPN Webhook Notification Endpoint
app.MapEcitizenWebhook("/payment/notify",
    onSuccess: async (result, ctx) =>
    {
        Console.WriteLine($"[IPN SUCCESS] Invoice: {result.Reference}, Amount: {result.AmountPaid}");
        // TODO: Update your database order status to 'Paid'
        await Task.CompletedTask;
    },
    onFailure: async (result, ctx) =>
    {
        Console.WriteLine($"[IPN FAILED] Invoice: {result.Reference}, Reason: {result.Description}");
        await Task.CompletedTask;
    });

// 4. User Landing Page after payment completion
app.MapGet("/payment/success", (string? @ref) =>
    Results.Content($"<h3>Payment submitted for invoice {@ref}! Verification is pending.</h3>", "text/html"));

app.Run();
