using System;
using System.Security.Cryptography;
using System.Text;

namespace Ecitizen.PesaflowGateway.Internal;

internal static class CryptoUtils
{
    /// <summary>
    /// eCitizen's secureHash is base64(hex(hmac_sha256(...))) - PHP's
    /// hash_hmac() returns a lowercase hex string, and the reference
    /// implementation base64-encodes that hex string directly rather than the
    /// raw digest bytes.
    /// </summary>
    public static string HmacSha256HexThenBase64(string dataString, string apiKey)
    {
        var keyBytes = Encoding.UTF8.GetBytes(apiKey);
        var dataBytes = Encoding.UTF8.GetBytes(dataString);

        using var hmac = new HMACSHA256(keyBytes);
        var hashBytes = hmac.ComputeHash(dataBytes);

#if NET8_0_OR_GREATER
        var hex = Convert.ToHexString(hashBytes).ToLowerInvariant();
#else
        var sb = new StringBuilder(hashBytes.Length * 2);
        for (int i = 0; i < hashBytes.Length; i++)
        {
            sb.Append(hashBytes[i].ToString("x2"));
        }
        var hex = sb.ToString();
#endif

        var hexBytes = Encoding.UTF8.GetBytes(hex);
        return Convert.ToBase64String(hexBytes);
    }

    /// <summary>
    /// Timing-safe equality check to prevent timing attacks when comparing signatures.
    /// </summary>
    public static bool FixedTimeEquals(string expected, string provided)
    {
        if (expected == null || provided == null)
        {
            return false;
        }

        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var providedBytes = Encoding.UTF8.GetBytes(provided);

        if (expectedBytes.Length != providedBytes.Length)
        {
            return false;
        }

#if NET8_0_OR_GREATER
        return CryptographicOperations.FixedTimeEquals(expectedBytes, providedBytes);
#else
        int diff = 0;
        for (int i = 0; i < expectedBytes.Length; i++)
        {
            diff |= expectedBytes[i] ^ providedBytes[i];
        }
        return diff == 0;
#endif
    }
}
