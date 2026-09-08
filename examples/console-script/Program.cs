using System;
using System.Threading.Tasks;
using Ecitizen.PesaflowGateway;
using Ecitizen.PesaflowGateway.Models;

Console.WriteLine("====================================================");
Console.WriteLine("eCitizen / PesaFlow Console Script Integration");
Console.WriteLine("====================================================");

// Read from environment variables, with fallback placeholders
var client = new EcitizenClient(new EcitizenConfig
{
    ApiClientID = Environment.GetEnvironmentVariable("ECITIZEN_CLIENT_ID") ?? "YOUR_CLIENT_ID",
    ApiKey = Environment.GetEnvironmentVariable("ECITIZEN_API_KEY") ?? "YOUR_API_KEY",
    Secret = Environment.GetEnvironmentVariable("ECITIZEN_SECRET") ?? "YOUR_MERCHANT_SECRET",
    ServiceID = Environment.GetEnvironmentVariable("ECITIZEN_SERVICE_ID") ?? "YOUR_SERVICE_ID",
    Url = Environment.GetEnvironmentVariable("ECITIZEN_GATEWAY_URL") ?? "https://payments.ecitizen.go.ke/PaymentAPI/iframev2.1.php",
    Currency = "KES"
});

var payment = new PaymentInput
{
    Amount = 500.00m,
    Reference = $"CLI-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",
    Description = "Utility Bill Payment",
    Name = "Jane Doe",
    IdNumber = "12345678",
    Phone = "0712345678",
    SendStkPush = false, // false for iframe checkout form, true for M-Pesa STK push
    CallbackUrl = "https://yourdomain.com/payment/success",
    NotifyUrl = "https://yourdomain.com/payment/notify"
};

// 1. Build signed checkout payload
var checkout = client.Checkout(payment);
Console.WriteLine($"Target Endpoint: {checkout.Url}");
Console.WriteLine($"Secure Hash (HMAC-SHA256): {checkout.Payload["secureHash"]}\n");

Console.WriteLine("Full Form Payload:");
foreach (var (key, value) in checkout.Payload)
{
    Console.WriteLine($"  {key} = {value}");
}

// 2. Generate ready-to-embed HTML Pay Button
var buttonHtml = client.PayButton(payment, "Proceed to Payment", new PayButtonOptions { Class = "btn-pay" });
Console.WriteLine($"\nHTML Button Snippet:\n{buttonHtml}");
