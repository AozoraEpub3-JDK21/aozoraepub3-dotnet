using AozoraEpub3.Core.Converter;

namespace AozoraEpub3.Tests;

public class AozoraGaijiConverterTests
{
    [Fact]
    public void Constructor_LoadsEmbeddedResources_ChukiUtfMapIsPopulated()
    {
        var converter = new AozoraGaijiConverter();
        Assert.NotEmpty(converter.ChukiUtfMap);
    }

    [Fact]
    public void Constructor_LoadsEmbeddedResources_ChukiAltMapIsPopulated()
    {
        var converter = new AozoraGaijiConverter();
        Assert.NotEmpty(converter.ChukiAltMap);
    }

    [Fact]
    public void ToUtf_UnknownKey_ReturnsNull()
    {
        var converter = new AozoraGaijiConverter();
        Assert.Null(converter.ToUtf("存在しないキー"));
    }

    [Fact]
    public void ToAlterString_UnknownKey_ReturnsNull()
    {
        var converter = new AozoraGaijiConverter();
        Assert.Null(converter.ToAlterString("存在しないキー"));
    }

    // CodeToCharString(string) - U+ prefix
    [Theory]
    [InlineData("U+3042", "あ")]
    [InlineData("U+4E00", "一")]
    [InlineData("U+0041", "A")]
    public void CodeToCharString_UPlusPrefix_ReturnsChar(string code, string expected)
    {
        var converter = new AozoraGaijiConverter();
        Assert.Equal(expected, converter.CodeToCharString(code));
    }

    [Theory]
    [InlineData("UCS-3042", "あ")]
    [InlineData("UCS-4E00", "一")]
    public void CodeToCharString_UCSPrefix_ReturnsChar(string code, string expected)
    {
        var converter = new AozoraGaijiConverter();
        Assert.Equal(expected, converter.CodeToCharString(code));
    }

    [Theory]
    [InlineData("unicode3042", "あ")]
    [InlineData("unicode4E00", "一")]
    public void CodeToCharString_UnicodePrefix_ReturnsChar(string code, string expected)
    {
        var converter = new AozoraGaijiConverter();
        Assert.Equal(expected, converter.CodeToCharString(code));
    }

    // CodeToCharString(int) - static
    [Theory]
    [InlineData(0x3042, "あ")]
    [InlineData(0x4E00, "一")]
    [InlineData(0, null)]
    public void CodeToCharString_Int_ReturnsChar(int code, string? expected)
    {
        Assert.Equal(expected, AozoraGaijiConverter.CodeToCharString(code));
    }

    [Fact]
    public void CodeToCharString_InvalidCode_ReturnsNull()
    {
        var converter = new AozoraGaijiConverter();
        Assert.Null(converter.CodeToCharString("INVALID"));
    }

    // JIS code conversion: 第3水準/第4水準 フォーマット
    [Fact]
    public void CodeToCharString_JisCode_ReturnsChar()
    {
        var converter = new AozoraGaijiConverter();
        // men=1, ku=1, ten=1 → JisConverter → "　"
        string? result = converter.CodeToCharString("1-1-1");
        Assert.NotNull(result);
    }
}
