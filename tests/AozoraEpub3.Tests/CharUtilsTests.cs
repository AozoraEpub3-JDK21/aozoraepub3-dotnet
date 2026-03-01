using AozoraEpub3.Core.Converter;

namespace AozoraEpub3.Tests;

public class CharUtilsTests
{
    // FullToHalf
    [Theory]
    [InlineData("０１２３４５６７８９", "0123456789")]
    [InlineData("ａｂｃｄｅｆｇｈｉｊｋｌｍｎｏｐｑｒｓｔｕｖｗｘｙｚ", "abcdefghijklmnopqrstuvwxyz")]
    [InlineData("ＡＢＣ", "ABC")]
    [InlineData("テスト", "テスト")]
    [InlineData("", "")]
    public void FullToHalf_ConvertsCorrectly(string input, string expected)
    {
        Assert.Equal(expected, CharUtils.FullToHalf(input));
    }

    // IsNum
    [Theory]
    [InlineData('0', true)]
    [InlineData('9', true)]
    [InlineData('5', true)]
    [InlineData('a', false)]
    [InlineData('０', false)]
    [InlineData(' ', false)]
    public void IsNum_ReturnsExpected(char ch, bool expected)
    {
        Assert.Equal(expected, CharUtils.IsNum(ch));
    }

    // IsHalf (excludes space 0x20)
    [Theory]
    [InlineData('!', true)]   // 0x21
    [InlineData('z', true)]
    [InlineData('A', true)]
    [InlineData(' ', false)]  // 0x20
    [InlineData('あ', false)]
    public void IsHalf_ReturnsExpected(char ch, bool expected)
    {
        Assert.Equal(expected, CharUtils.IsHalf(ch));
    }

    // IsHalfSpace (includes space 0x20)
    [Theory]
    [InlineData(' ', true)]   // 0x20
    [InlineData('!', true)]
    [InlineData('a', true)]
    [InlineData('あ', false)]
    public void IsHalfSpace_ReturnsExpected(char ch, bool expected)
    {
        Assert.Equal(expected, CharUtils.IsHalfSpace(ch));
    }

    // IsHiragana
    [Theory]
    [InlineData('あ', true)]
    [InlineData('ぁ', true)]
    [InlineData('ん', true)]
    [InlineData('ー', true)]
    [InlineData('ア', false)]
    [InlineData('a', false)]
    [InlineData('漢', false)]
    public void IsHiragana_ReturnsExpected(char ch, bool expected)
    {
        Assert.Equal(expected, CharUtils.IsHiragana(ch));
    }

    // IsKatakana
    [Theory]
    [InlineData('ア', true)]
    [InlineData('ァ', true)]
    [InlineData('ヶ', true)]
    [InlineData('あ', false)]
    [InlineData('a', false)]
    [InlineData('漢', false)]
    public void IsKatakana_ReturnsExpected(char ch, bool expected)
    {
        Assert.Equal(expected, CharUtils.IsKatakana(ch));
    }

    // IsFullAlpha
    [Theory]
    [InlineData('Ａ', true)]
    [InlineData('Ｚ', true)]
    [InlineData('ａ', true)]
    [InlineData('ｚ', true)]
    [InlineData('０', true)]
    [InlineData('９', true)]
    [InlineData('A', false)]
    [InlineData('a', false)]
    public void IsFullAlpha_ReturnsExpected(char ch, bool expected)
    {
        Assert.Equal(expected, CharUtils.IsFullAlpha(ch));
    }

    // IsSpace
    [Theory]
    [InlineData("　", true)]
    [InlineData(" ", true)]
    [InlineData("　 　", true)]
    [InlineData("", true)]
    [InlineData("あ", false)]
    [InlineData("a", false)]
    [InlineData("  a", false)]
    public void IsSpace_ReturnsExpected(string s, bool expected)
    {
        Assert.Equal(expected, CharUtils.IsSpace(s));
    }

    // IsAlpha
    [Theory]
    [InlineData('A', true)]
    [InlineData('Z', true)]
    [InlineData('a', true)]
    [InlineData('z', true)]
    [InlineData('あ', false)]
    [InlineData('0', false)]
    public void IsAlpha_ReturnsExpected(char ch, bool expected)
    {
        Assert.Equal(expected, CharUtils.IsAlpha(ch));
    }

    // IsKanji - basic CJK Unified Ideographs (U+4E00 to U+9FFF)
    [Theory]
    [InlineData('愛', true)]
    [InlineData('漢', true)]
    [InlineData('字', true)]
    [InlineData('々', true)]
    [InlineData('A', false)]
    [InlineData('あ', false)]
    [InlineData('ア', false)]
    public void IsKanji_ReturnsExpected(char ch, bool expected)
    {
        char[] chars = { ch };
        Assert.Equal(expected, CharUtils.IsKanji(chars, 0));
    }

    // RemoveTag
    [Theory]
    [InlineData("<tag>content</tag>", "content")]
    [InlineData("［＃注記］text", "text")]
    [InlineData("no tags", "no tags")]
    [InlineData("<b>bold</b> text", "bold text")]
    public void RemoveTag_RemovesTags(string input, string expected)
    {
        Assert.Equal(expected, CharUtils.RemoveTag(input));
    }

    // RemoveSpace
    [Theory]
    [InlineData("  text  ", "text")]
    [InlineData("　text　", "text")]
    [InlineData("text", "text")]
    [InlineData("  ", "")]
    public void RemoveSpace_RemovesLeadingTrailing(string input, string expected)
    {
        Assert.Equal(expected, CharUtils.RemoveSpace(input));
    }

    // RemoveRuby
    [Theory]
    [InlineData("｜漢字《かんじ》rest", "漢字rest")]
    [InlineData("no ruby", "no ruby")]
    [InlineData("text《ruby》more", "textmore")]
    public void RemoveRuby_RemovesRubyMarkup(string input, string expected)
    {
        Assert.Equal(expected, CharUtils.RemoveRuby(input));
    }

    // EscapeHtml
    [Theory]
    [InlineData("a&b<c>d", "a&amp;b&lt;c&gt;d")]
    [InlineData("no special", "no special")]
    [InlineData("&", "&amp;")]
    [InlineData("<", "&lt;")]
    [InlineData(">", "&gt;")]
    public void EscapeHtml_EscapesSpecialChars(string input, string expected)
    {
        Assert.Equal(expected, CharUtils.EscapeHtml(input));
    }

    // EscapeUrlToFile
    [Theory]
    [InlineData("file?a=b", "file/a=b")]
    [InlineData("file:name", "file_name")]
    [InlineData("normal", "normal")]
    [InlineData("file&query", "file/query")]
    public void EscapeUrlToFile_EscapesChars(string input, string expected)
    {
        Assert.Equal(expected, CharUtils.EscapeUrlToFile(input));
    }

    // GetChapterName
    [Theory]
    [InlineData("abc", 10, false, "abc")]
    [InlineData("abcdefghij", 5, false, "abcde...")]
    [InlineData("abc", 0, false, "abc")]
    [InlineData("  text  ", 0, false, "text")]
    [InlineData("［＃注記］text", 0, false, "text")]
    public void GetChapterName_TruncatesAndCleans(string input, int maxLen, bool reduce, string expected)
    {
        Assert.Equal(expected, CharUtils.GetChapterName(input, maxLen, reduce));
    }

    // RemoveBOM
    [Fact]
    public void RemoveBOM_RemovesBOMFromStart()
    {
        string withBom = "\uFEFFtest";
        Assert.Equal("test", CharUtils.RemoveBOM(withBom));
    }

    [Fact]
    public void RemoveBOM_ReturnsNullForNull()
    {
        Assert.Null(CharUtils.RemoveBOM(null));
    }

    [Fact]
    public void RemoveBOM_ReturnsUnchangedIfNoBOM()
    {
        Assert.Equal("test", CharUtils.RemoveBOM("test"));
    }

    // IsSameChars
    [Fact]
    public void IsSameChars_AllSame_ReturnsTrue()
    {
        Assert.True(CharUtils.IsSameChars("aaa".ToCharArray(), 0, 3));
    }

    [Fact]
    public void IsSameChars_NotAllSame_ReturnsFalse()
    {
        Assert.False(CharUtils.IsSameChars("abc".ToCharArray(), 0, 3));
    }

    [Fact]
    public void IsSameChars_SubRange_RespectsRange()
    {
        // "aab" - range [0,2) = "aa" → same
        Assert.True(CharUtils.IsSameChars("aab".ToCharArray(), 0, 2));
        // range [0,3) = "aab" → not same
        Assert.False(CharUtils.IsSameChars("aab".ToCharArray(), 0, 3));
    }

    // IsEscapedChar (char[])
    [Fact]
    public void IsEscapedChar_NoPrefix_ReturnsFalse()
    {
        char[] ch = "X".ToCharArray();
        Assert.False(CharUtils.IsEscapedChar(ch, 0));
    }

    [Fact]
    public void IsEscapedChar_OneEscapeChar_ReturnsTrue()
    {
        char[] ch = "※X".ToCharArray();
        Assert.True(CharUtils.IsEscapedChar(ch, 1));
    }

    [Fact]
    public void IsEscapedChar_TwoEscapeChars_ReturnsFalse()
    {
        char[] ch = "※※X".ToCharArray();
        Assert.False(CharUtils.IsEscapedChar(ch, 2));
    }

    [Fact]
    public void IsEscapedChar_NonEscapePrefix_ReturnsFalse()
    {
        char[] ch = "aX".ToCharArray();
        Assert.False(CharUtils.IsEscapedChar(ch, 1));
    }
}
