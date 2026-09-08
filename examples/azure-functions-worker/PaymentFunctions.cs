using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Ecitizen.PesaflowGateway;
using Ecitizen.PesaflowGateway.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace AzureFunctionsExample;

public class PaymentFunctions
{
    private readonly EcitizenClient _client;

    public PaymentFunctions()
    {
        // Azure Functions reads settings from Environment Variables / App Settings
        _client = new EcitizenClient(new EcitizenConfig
        {
            ApiClientID = Environment.GetEnvironmentVariable("ECITIZEN_CLIENT_ID") ?? "YOUR_CLIENT_ID",
            ApiKey = Environment.GetEnvironmentVariable("ECITIZEN_API_KEY") ?? "YOUR_API_KEY",
            Secret = Environment.GetEnvironmentVariable("ECITIZEN_SECRET") ?? "YOUR_MERCHANT_SECRET",
            ServiceID = Environment.GetEnvironmentVariable("ECITIZEN_SERVICE_ID") ?? "YOUR_SERVICE_ID",
            Url = Environment.GetEnvironmentVariable("ECITIZEN_GATEWAY_URL") ?? "https://payments.ecitizen.go.ke/PaymentAPI/iframev2.1.php",
            Currency = "KES"
        });
    }

    [Function("CreatePaymentCheckout")]
    public async Task<HttpResponseData> CreateCheckout(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = "payment/checkout")] HttpRequestData req)
    {
        var checkout = _client.Checkout(new PaymentInput
        {
            Amount = 1000m,
            Reference = $"FN-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",
            Description = "Serverless Cloud Service",
            Name = "Jane Doe",
            IdNumber = "12345678",
            Phone = "0712345678",
            SendStkPush = false
        });

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(new
        {
            url = checkout.Url,
            payload = checkout.Payload
        });
        return response;
    }

    [Function("EcitizenWebhookNotification")]
    public async Task<HttpResponseData> WebhookNotification(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "payment/notify")] HttpRequestData req)
    {
        using var reader = new StreamReader(req.Body);
        var body = await reader.ReadToEndAsync();

        // In production, parse form-url-encoded or JSON payload from eCitizen
        var verification = _client.VerifyCallback(body);

        var response = req.CreateResponse(verification.IsValid ? HttpStatusCode.OK : HttpStatusCode.BadRequest);
        await response.WriteStringAsync(verification.IsValid ? "OK" : "INVALID_SIGNATURE");
        return response;
    }
}
