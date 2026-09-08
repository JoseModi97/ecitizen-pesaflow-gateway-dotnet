using Xunit;

namespace Ecitizen.PesaflowGateway.Tests;

public class PhoneHelperTests
{
    [Theory]
    [InlineData("0712345678", "254712345678")]
    [InlineData("0112345678", "254112345678")]
    [InlineData("+254712345678", "254712345678")]
    [InlineData("712345678", "254712345678")]
    [InlineData("112345678", "254112345678")]
    [InlineData("254712345678", "254712345678")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void Normalize_HandlesKenyanMsisdns(string? input, string expected)
    {
        Assert.Equal(expected, PhoneHelper.Normalize(input));
    }

    [Theory]
    [InlineData("254712345678", true)]
    [InlineData("254112345678", true)]
    [InlineData("0712345678", false)] // not yet normalized
    [InlineData("254212345678", false)] // invalid 2542 prefix
    [InlineData("25471234567", false)] // too short
    [InlineData("2547123456789", false)] // too long
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsValidStkPhone_ValidatesCorrectly(string? input, bool expected)
    {
        Assert.Equal(expected, PhoneHelper.IsValidStkPhone(input));
    }
}
