using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using Ecitizen.PesaflowGateway.Internal;
using Xunit;

namespace Ecitizen.PesaflowGateway.Tests;

public class GatewayCryptoTests
{
    private readonly EcitizenGateway _gateway = new(new EcitizenConfig
    {
        ApiClientID = "CLIENT1",
        ApiKey = "KEY1",
        Secret = "SECRET1",
        ServiceID = "SERVICE1",
        Url = "https://payments.ecitizen.go.ke/PaymentAPI/iframev2.1.php",
    });

    [Fact]
    public void GenerateCheckoutHash_Matches_Exact_Node_TestVector()
    {
        var invoice = new Dictionary<string, object?>
        {
            ["amountExpected"] = 500m,
            ["billRefNumber"] = "INV-0001",
            ["billDesc"] = "School fees",
            ["clientName"] = "Jane Doe",
            ["clientIDNumber"] = "12345678",
        };

        var payload = _gateway.CreateCheckoutPayload(invoice);

        Assert.True(payload.ContainsKey("secureHash"));
        var secureHash = payload["secureHash"];
        Assert.False(string.IsNullOrWhiteSpace(secureHash));

        // The exact concat string:
        // CLIENT1 + 500.00 + SERVICE1 + 12345678 + KES + INV-0001 + School fees + Jane Doe + SECRET1
        var expectedDataString = "CLIENT1500.00SERVICE112345678KESINV-0001School feesJane DoeSECRET1";

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes("KEY1"));
        var hashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(expectedDataString));
        var hex = Convert.ToHexString(hashBytes).ToLowerInvariant();
        var expectedBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(hex));

        Assert.Equal(expectedBase64, secureHash);
    }

    [Fact]
    public void VerifyNotificationHash_ValidSignature_ReturnsTrue()
    {
        var dataString = "INV-0001" + "" + "500.00" + "2026-08-17" + "SECRET1";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes("KEY1"));
        var hashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(dataString));
        var hex = Convert.ToHexString(hashBytes).ToLowerInvariant();
        var validHash = Convert.ToBase64String(Encoding.UTF8.GetBytes(hex));

        var validPayload = new Dictionary<string, string>
        {
            ["client_invoice_ref"] = "INV-0001",
            ["amount_paid"] = "500.00",
            ["payment_date"] = "2026-08-17",
            ["status"] = "Settled",
            ["secure_hash"] = validHash
        };

        Assert.True(_gateway.VerifyNotificationHash(validPayload));
    }

    [Fact]
    public void VerifyNotificationHash_TamperedSignature_ReturnsFalse()
    {
        var tamperedPayload = new Dictionary<string, string>
        {
            ["client_invoice_ref"] = "INV-0001",
            ["amount_paid"] = "500.00",
            ["payment_date"] = "2026-08-17",
            ["status"] = "Settled",
            ["secure_hash"] = "tampered-hash-123"
        };

        Assert.False(_gateway.VerifyNotificationHash(tamperedPayload));
    }

    [Theory]
    [InlineData("paid", true)]
    [InlineData("Settled", true)]
    [InlineData("SUCCESS", true)]
    [InlineData("completed", true)]
    [InlineData("failed", false)]
    [InlineData("cancelled", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsSuccessStatus_EvaluatesCorrectly(string? status, bool expected)
    {
        Assert.Equal(expected, _gateway.IsSuccessStatus(status));
    }

    [Theory]
    [InlineData(500, "500.00")]
    [InlineData(500.5, "500.50")]
    [InlineData("1250.75", "1250.75")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void NormalizeAmount_FormatsToTwoDecimalPlaces(object? amount, string expected)
    {
        Assert.Equal(expected, _gateway.NormalizeAmount(amount));
    }
}
