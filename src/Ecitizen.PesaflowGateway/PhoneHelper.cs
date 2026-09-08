using System.Text.RegularExpressions;

namespace Ecitizen.PesaflowGateway;

/// <summary>
/// Normalizes Kenyan MSISDNs to the 2547XXXXXXXX / 2541XXXXXXXX format that
/// eCitizen's sendSTK expects, and validates the result.
/// </summary>
public static class PhoneHelper
{
    private static readonly Regex NonDigitsRegex = new(@"\D+", RegexOptions.Compiled);
    private static readonly Regex NineDigitKenyanRegex = new(@"^[17]\d{8}$", RegexOptions.Compiled);
    private static readonly Regex ValidStkRegex = new(@"^254[17]\d{8}$", RegexOptions.Compiled);

    /// <summary>
    /// Normalizes Kenyan phone numbers to 254XXXXXXXXX format.
    /// Handles formats like:
    /// - 0712345678 -> 254712345678
    /// - 0112345678 -> 254112345678
    /// - +254712345678 -> 254712345678
    /// - 712345678 -> 254712345678
    /// - 112345678 -> 254112345678
    /// </summary>
    public static string Normalize(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
        {
            return string.Empty;
        }

        var digits = NonDigitsRegex.Replace(phone!, string.Empty);

        if (digits.StartsWith("254"))
        {
            return digits;
        }

        if (digits.StartsWith("0"))
        {
            return "254" + digits.Substring(1);
        }

        if (NineDigitKenyanRegex.IsMatch(digits))
        {
            return "254" + digits;
        }

        return digits;
    }

    /// <summary>
    /// Overload taking long or integer MSISDN values.
    /// </summary>
    public static string Normalize(long phone) => Normalize(phone.ToString());

    /// <summary>
    /// Matches valid Safaricom/Airtel MSISDNs in STK push format: 2547XXXXXXXX or 2541XXXXXXXX.
    /// </summary>
    public static bool IsValidStkPhone(string? normalizedPhone)
    {
        if (string.IsNullOrWhiteSpace(normalizedPhone))
        {
            return false;
        }

        return ValidStkRegex.IsMatch(normalizedPhone!);
    }
}
