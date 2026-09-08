using System;
using System.Collections.Generic;
using Ecitizen.PesaflowGateway.Models;
using Xunit;

namespace Ecitizen.PesaflowGateway.Tests;

public class ClientTests
{
    private readonly EcitizenClient _client = new(new EcitizenConfig
    {
        ApiClientID = "CLIENT1",
        ApiKey = "KEY1",
        Secret = "SECRET1",
        ServiceID = "SERVICE1",
        Url = "https://payments.ecitizen.go.ke/PaymentAPI/iframev2.1.php",
    });

    [Fact]
    public void Checkout_MissingRequiredField_ThrowsArgumentException()
    {
        var payment = new PaymentInput
        {
            Amount = 0, // missing valid amount
            Reference = "INV-001",
            Description = "Fee",
            Name = "Jane Doe",
            IdNumber = "12345678"
        };

        Assert.Throws<ArgumentException>(() => _client.Checkout(payment));
    }

    [Fact]
    public void Checkout_BuildsValidPayload_WithNormalizedPhone()
    {
        var payment = new PaymentInput
        {
            Amount = 1500m,
            Reference = "INV-100",
            Description = "Rates clearance",
            Name = "John Doe",
            IdNumber = "28374619",
            Phone = "0712345678",
            SendStkPush = true,
            CallbackUrl = "https://example.com/payment/success",
            NotifyUrl = "https://example.com/payment/notify"
        };

        var result = _client.Checkout(payment);

        Assert.Equal("https://payments.ecitizen.go.ke/PaymentAPI/iframev2.1.php", result.Url);
        Assert.Equal("254712345678", result.Payload["clientMSISDN"]);
        Assert.Equal("1500.00", result.Payload["amountExpected"]);
        Assert.Equal("true", result.Payload["sendSTK"]);
        Assert.True(result.Payload.ContainsKey("secureHash"));
    }

    [Fact]
    public void PayButton_EscapesHtml_Correctly()
    {
        var payment = new PaymentInput
        {
            Amount = 250m,
            Reference = "INV-&<>\"'",
            Description = "Description with <b>HTML</b> & 'quotes'",
            Name = "O'Connor <script>",
            IdNumber = "ID-999"
        };

        var html = _client.PayButton(payment, "Proceed & Pay", new PayButtonOptions
        {
            Class = "btn btn-success",
            Target = "_self"
        });

        Assert.Contains("target=\"_self\"", html);
        Assert.Contains("class=\"btn btn-success\"", html);
        Assert.Contains("&lt;b&gt;HTML&lt;/b&gt;", html);
        Assert.Contains("Proceed &amp; Pay", html);
        Assert.DoesNotContain("<script>", html);
    }

    [Fact]
    public void Verify_ValidPayload_ReturnsSuccess()
    {
        var gateway = _client.GetGateway();
        var dataString = "INV-100" + "" + "1500.00" + "2026-09-08" + gateway.Secret;
        using var hmac = new System.Security.Cryptography.HMACSHA256(System.Text.Encoding.UTF8.GetBytes(gateway.ApiKey));
        var hash = Convert.ToHexString(hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(dataString))).ToLowerInvariant();
        var secureHash = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(hash));

        var callback = new Dictionary<string, string>
        {
            ["client_invoice_ref"] = "INV-100",
            ["amount_paid"] = "1500.00",
            ["payment_date"] = "2026-09-08",
            ["status"] = "Settled",
            ["secure_hash"] = secureHash
        };

        var result = _client.Verify(callback);

        Assert.True(result.Success);
        Assert.True(result.SignatureValid);
        Assert.Equal("INV-100", result.Reference);
        Assert.Equal(1500m, result.AmountPaid);
        Assert.True(_client.IsPaid(callback));
    }
}
