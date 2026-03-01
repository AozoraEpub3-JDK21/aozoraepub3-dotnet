using AozoraEpub3.Core.Converter;

namespace AozoraEpub3.Tests;

public class JisConverterTests
{
    private readonly JisConverter _converter = JisConverter.GetConverter();

    // 面0 (ASCII相当)
    [Theory]
    [InlineData(0, 0, 0, " ")]   // space (_men0[0])
    [InlineData(0, 0, 1, "!")]   // _men0[1]
    [InlineData(0, 0, 32, "@")]  // _men0[32] = '@'
    [InlineData(0, 0, 33, "A")]  // _men0[33] = 'A'
    [InlineData(0, 0, 64, "`")]  // _men0[64] = '`'
    [InlineData(0, 0, 65, "a")]  // _men0[65] = 'a'
    public void ToCharString_Men0_ReturnsAsciiChar(int men, int ku, int ten, string expected)
    {
        Assert.Equal(expected, _converter.ToCharString(men, ku, ten));
    }

    // 面0 境界外
    [Theory]
    [InlineData(0, 1, 0)]   // ku != 0
    [InlineData(0, 0, 999)] // ten out of range
    public void ToCharString_Men0_OutOfRange_ReturnsNull(int men, int ku, int ten)
    {
        Assert.Null(_converter.ToCharString(men, ku, ten));
    }

    // 面1, ku1-13 (文字列テーブル)
    [Theory]
    [InlineData(1, 1, 1, "　")]   // 全角スペース
    [InlineData(1, 1, 2, "、")]   // 読点
    [InlineData(1, 1, 3, "。")]   // 句点
    [InlineData(1, 1, 9, "？")]   // 全角?
    [InlineData(1, 1, 10, "！")]  // 全角!
    public void ToCharString_Men1_Ku1_ReturnsExpected(int men, int ku, int ten, string expected)
    {
        Assert.Equal(expected, _converter.ToCharString(men, ku, ten));
    }

    // 面1, ku0 (無効)
    [Fact]
    public void ToCharString_Men1_Ku0_ReturnsNull()
    {
        Assert.Null(_converter.ToCharString(1, 0, 1));
    }

    // 面1, ku95 (無効)
    [Fact]
    public void ToCharString_Men1_Ku95_ReturnsNull()
    {
        Assert.Null(_converter.ToCharString(1, 95, 1));
    }

    // 面1, ten0 (無効)
    [Fact]
    public void ToCharString_Men1_Ten0_ReturnsNull()
    {
        Assert.Null(_converter.ToCharString(1, 1, 0));
    }

    // 未知の面
    [Fact]
    public void ToCharString_UnknownMen_ReturnsNull()
    {
        Assert.Null(_converter.ToCharString(5, 1, 1));
    }

    // シングルトン
    [Fact]
    public void GetConverter_ReturnsSameInstance()
    {
        var c1 = JisConverter.GetConverter();
        var c2 = JisConverter.GetConverter();
        Assert.Same(c1, c2);
    }
}
