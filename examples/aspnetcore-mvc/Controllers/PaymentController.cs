using System;
using System.Threading.Tasks;
using Ecitizen.PesaflowGateway;
using Ecitizen.PesaflowGateway.AspNetCore;
using Ecitizen.PesaflowGateway.Models;
using Microsoft.AspNetCore.Mvc;

namespace AspNetCoreMvcExample.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PaymentController : ControllerBase
{
    private readonly EcitizenClient _client;

    public PaymentController(EcitizenClient client)
    {
        _client = client;
    }

    /// <summary>
    /// Generates signed payment form HTML payload.
    /// GET /api/payment/pay?amount=1500&name=John+Doe
    /// </summary>
    [HttpGet("pay")]
    public IActionResult Pay(
        [FromQuery] decimal amount = 500m,
        [FromQuery] string? reference = null,
        [FromQuery] string description = "Service Fee",
        [FromQuery] string name = "Jane Doe",
        [FromQuery] string idNumber = "12345678",
        [FromQuery] string phone = "0712345678")
    {
        var refNo = string.IsNullOrWhiteSpace(reference) ? $"INV-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}" : reference!;
        var baseUrl = $"{Request.Scheme}://{Request.Host}";

        var formHtml = _client.PayButton(new PaymentInput
        {
            Amount = amount,
            Reference = refNo,
            Description = description,
            Name = name,
            IdNumber = idNumber,
            Phone = phone,
            SendStkPush = false, // false enables full iframe checkout with multiple payment options
            CallbackUrl = $"{baseUrl}/payment/success",
            NotifyUrl = $"{baseUrl}/api/payment/notify"
        }, "Proceed to eCitizen", new PayButtonOptions { Class = "btn btn-success" });

        return Content(formHtml, "text/html");
    }

    /// <summary>
    /// Server-to-server IPN webhook endpoint called by eCitizen.
    /// POST /api/payment/notify
    /// </summary>
    [HttpPost("notify")]
    public async Task<IResult> Notify()
    {
        return await EcitizenWebhookHandler.ProcessAsync(
            Request,
            _client,
            onSuccess: async (result) =>
            {
                Console.WriteLine($"[MVC Webhook] Payment Verified: {result.Reference}, Amount: {result.AmountPaid}");
                // TODO: Update database status
                await Task.CompletedTask;
            },
            onFailure: async (result) =>
            {
                Console.WriteLine($"[MVC Webhook] Signature Mismatch: {result.Description}");
                await Task.CompletedTask;
            });
    }
}
