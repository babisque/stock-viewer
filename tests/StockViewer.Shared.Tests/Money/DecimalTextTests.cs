using StockViewer.Shared.Money;

namespace StockViewer.Shared.Tests.Money;

public class DecimalTextTests
{
    public static TheoryData<decimal, string> ToJsonCases => new()
    {
        { 28.45m, "28.45" },
        { 28.450m, "28.45" },
        { 1005.00m, "1005" },
        { 0m, "0" },
        { 0.00m, "0" },
        { -0.00m, "0" },
        { 0.00000001m, "0.00000001" },
        { -51m, "-51" },
        { 195.10m, "195.1" },
        { 12345678901234.565m, "12345678901234.565" },
        { 100000000000000000000m, "100000000000000000000" },
    };

    [Theory]
    [MemberData(nameof(ToJsonCases))]
    public void ToJson_writes_normalized_invariant_text(decimal value, string expected)
    {
        Assert.Equal(expected, DecimalText.ToJson(value));
    }

    [Theory]
    [InlineData("28.45", "28.45")]
    [InlineData("0", "0")]
    [InlineData("0.00000001", "0.00000001")]
    [InlineData("4.90", "4.9")]
    public void TryParse_accepts_unsigned_decimal_strings(string text, string expected)
    {
        Assert.True(DecimalText.TryParse(text, out var value));
        Assert.Equal(expected, DecimalText.ToJson(value));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("-1")]
    [InlineData("1e5")]
    [InlineData("28,45")]
    [InlineData(" 1")]
    [InlineData("1.")]
    [InlineData(".5")]
    [InlineData("١٢")]
    [InlineData("100000000000000000000000000000000")]
    public void TryParse_rejects_anything_else(string? text)
    {
        Assert.False(DecimalText.TryParse(text, out _));
    }

    [Theory]
    [InlineData("-51")]
    [InlineData("28.45")]
    public void TryParseSigned_also_accepts_negative_values(string text)
    {
        Assert.True(DecimalText.TryParseSigned(text, out var value));
        Assert.Equal(text, DecimalText.ToJson(value));
    }
}
