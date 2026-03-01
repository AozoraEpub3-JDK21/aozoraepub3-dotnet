using AozoraEpub3.Core.Converter;

namespace AozoraEpub3.Tests;

public class LatinConverterTests
{
    [Fact]
    public void Constructor_LoadsEmbeddedResources_MapIsPopulated()
    {
        var converter = new LatinConverter();
        // 変換できる場合にマップが読み込まれているか確認
        // "A`" → 'À' のようなマッピングが存在するはず
        string result = converter.ToLatinString("A`");
        Assert.Equal("À", result);
    }

    // 2文字マッピング
    [Theory]
    [InlineData("A`", "À")]   // A + grave
    [InlineData("A'", "Á")]   // A + acute
    [InlineData("E'", "É")]   // E + acute
    [InlineData("N~", "Ñ")]   // N + tilde
    [InlineData("O:", "Ö")]   // O + umlaut
    [InlineData("!@", "¡")]   // inverted !
    [InlineData("?@", "¿")]   // inverted ?
    public void ToLatinString_TwoCharMapping_ReturnsExtendedLatin(string input, string expected)
    {
        var converter = new LatinConverter();
        Assert.Equal(expected, converter.ToLatinString(input));
    }

    // 3文字マッピング
    [Fact]
    public void ToLatinString_ThreeCharMapping_AELigature()
    {
        var converter = new LatinConverter();
        // "AE&" → 'Æ'
        Assert.Equal("Æ", converter.ToLatinString("AE&"));
    }

    // マッピングなし → そのまま返す
    [Theory]
    [InlineData("Hello", "Hello")]
    [InlineData("ABC", "ABC")]
    [InlineData("", "")]
    public void ToLatinString_NoMapping_ReturnsUnchanged(string input, string expected)
    {
        var converter = new LatinConverter();
        Assert.Equal(expected, converter.ToLatinString(input));
    }

    // 混在: マッピングあり文字とない文字
    [Fact]
    public void ToLatinString_Mixed_ConvertsPartially()
    {
        var converter = new LatinConverter();
        // "E'cole" → "École"
        string result = converter.ToLatinString("E'cole");
        Assert.Equal("École", result);
    }

    // ファイルパスコンストラクタ: 存在しないパス → 空のマップ（変換なし）
    [Fact]
    public void Constructor_NonExistentFile_ReturnsUnchanged()
    {
        var converter = new LatinConverter("/nonexistent/path/chuki_latin.txt");
        // マップ空のため変換せず入力をそのまま返す
        Assert.Equal("A`", converter.ToLatinString("A`"));
    }
}
