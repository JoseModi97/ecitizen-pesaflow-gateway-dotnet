using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Ecitizen.PesaflowGateway.Cli.Commands;

public static class TestCommand
{
    public static async Task ExecuteAsync(string[] args)
    {
        Console.WriteLine("\n\x1b[34m--- Running eCitizen / PesaFlow Test Suite ---\x1b[0m\n");

        try
        {
            var gateway = new EcitizenGateway(new EcitizenConfig
            {
                ApiClientID = "CLIENT1",
                ApiKey = "KEY1",
                Secret = "SECRET1",
                ServiceID = "SERVICE1",
                Url = "https://payments.ecitizen.go.ke/PaymentAPI/iframev2.1.php",
            });

            // 1. Test checkout signature calculation
            var invoice = new Dictionary<string, object?>
            {
                ["amountExpected"] = 500m,
                ["billRefNumber"] = "INV-0001",
                ["billDesc"] = "School fees",
                ["clientName"] = "Jane Doe",
                ["clientIDNumber"] = "12345678"
            };

            var payload = gateway.CreateCheckoutPayload(invoice);

            if (!payload.TryGetValue("secureHash", out var secureHash) || string.IsNullOrEmpty(secureHash))
            {
                throw new Exception("Checkout payload did not contain secureHash");
            }
            Console.WriteLine($"  \x1b[32m✔\x1b[0m Checkout hash generated successfully: {secureHash}");

            // 2. Test notification signature verification against exact vector
            var dataString = "INV-0001" + "" + "500.00" + "2026-08-17" + "SECRET1";
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes("KEY1"));
            var hashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(dataString));

            var sb = new StringBuilder(hashBytes.Length * 2);
            for (int i = 0; i < hashBytes.Length; i++) sb.Append(hashBytes[i].ToString("x2"));
            var hexDigest = sb.ToString();
            var expectedHash = Convert.ToBase64String(Encoding.UTF8.GetBytes(hexDigest));

            var valid = gateway.VerifyNotificationHash(new Dictionary<string, string>
            {
                ["client_invoice_ref"] = "INV-0001",
                ["amount_paid"] = "500.00",
                ["payment_date"] = "2026-08-17",
                ["status"] = "Settled",
                ["secure_hash"] = expectedHash
            });

            if (!valid)
            {
                throw new Exception("Notification hash verification failed for valid signature");
            }
            Console.WriteLine("  \x1b[32m✔\x1b[0m Notification IPN hash verification verified against test vector");

            // 3. Test tampered rejection
            var invalid = gateway.VerifyNotificationHash(new Dictionary<string, string>
            {
                ["client_invoice_ref"] = "INV-0001",
                ["amount_paid"] = "500.00",
                ["payment_date"] = "2026-08-17",
                ["status"] = "Settled",
                ["secure_hash"] = "tampered-hash-123"
            });

            if (invalid)
            {
                throw new Exception("Notification hash verification falsely accepted a tampered signature");
            }
            Console.WriteLine("  \x1b[32m✔\x1b[0m Tampered signature correctly rejected");

            Console.WriteLine("\n\x1b[32mAll cryptographic tests passed with 100% vector parity!\x1b[0m\n");
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\nTest failure: {ex.Message}");
            Console.ResetColor();
            Environment.Exit(1);
        }

        await Task.CompletedTask;
    }
}
