using AozoraEpub3.Core.Web;
using Xunit;

namespace AozoraEpub3.Tests;

/// <summary>
/// AozoraTextFinalizer の漢数字変換・英字全角化のバグ修正テスト
/// (v1.3.0-jdk21 互換)
/// </summary>
public class AozoraTextFinalizerTests
{
    private static AozoraTextFinalizer CreateFinalizer(
        bool convertNumToKanji = true,
        bool alphabetForceZenkaku = false,
        bool displayEndOfBook = false)
    {
        var settings = new NarouFormatSettings
        {
            EnableConvertNumToKanji = convertNumToKanji,
            EnableAlphabetForceZenkaku = alphabetForceZenkaku,
            EnableDisplayEndOfBook = displayEndOfBook,
            // テスト対象の変換のみ有効にし、他の処理を無効化
            EnablePackBlankLine = false,
            EnableAuthorComments = false,
            EnableAutoIndent = false,
            EnableHalfIndentBracket = false,
            EnableEnchantMidashi = false,
            EnableInspectInvalidOpenCloseBrackets = false,
        };
        return new AozoraTextFinalizer(settings);
    }

    // ── Fix 1: サブタイトル行の漢数字変換スキップ ──

    [Fact]
    public void ConvertNumToKanji_SubtitleLine_UsesZenkakuOnly()
    {
        var finalizer = CreateFinalizer(convertNumToKanji: true);
        var lines = new List<string>
        {
            "［＃３字下げ］［＃中見出し］第100話 タイトル［＃中見出し終わり］",
            "本文の100個の数字は漢数字になる",
        };
        finalizer.Finalize(lines);

        // サブタイトル行: 100 → 全角 １００ (漢数字にしない)
        Assert.Contains("第１００話", lines[0], StringComparison.Ordinal);
        Assert.DoesNotContain("一〇〇", lines[0], StringComparison.Ordinal);

        // 本文行: 100 → 漢数字 一〇〇
        Assert.Contains("一〇〇", lines[1], StringComparison.Ordinal);
    }

    [Fact]
    public void ConvertNumToKanji_OomidashiLine_UsesZenkakuOnly()
    {
        var finalizer = CreateFinalizer(convertNumToKanji: true);
        var lines = new List<string>
        {
            "［＃３字下げ］［＃大見出し］第5章［＃大見出し終わり］",
        };
        finalizer.Finalize(lines);

        Assert.Contains("第５章", lines[0], StringComparison.Ordinal);
        Assert.DoesNotContain("五", lines[0], StringComparison.Ordinal);
    }

    // ── Fix 4: 注記内の数字・英字が変換されない ──

    [Fact]
    public void ConvertNumToKanji_AnnotationNumbers_NotConverted()
    {
        var finalizer = CreateFinalizer(convertNumToKanji: true);
        var lines = new List<string>
        {
            "テスト※［＃米印、1-2-8］の文章",
        };
        finalizer.Finalize(lines);

        // 注記内の 1-2-8 は変換されない
        Assert.Contains("［＃米印、1-2-8］", lines[0]);
        // 注記外のテキストは変換対象（この例では数字なし）
    }

    [Fact]
    public void AlphabetToZenkaku_AnnotationAlphabet_NotConverted()
    {
        var finalizer = CreateFinalizer(convertNumToKanji: false);
        var lines = new List<string>
        {
            "テスト［＃縦中横］AB［＃縦中横終わり］の文章",
        };
        finalizer.Finalize(lines);

        // 注記内の AB は変換されない
        Assert.Contains("［＃縦中横］AB［＃縦中横終わり］", lines[0]);
    }

    // ── Fix 5: URL行・変換日時行のスキップ ──

    [Fact]
    public void ConvertNumToKanji_UrlLine_NotConverted()
    {
        var finalizer = CreateFinalizer(convertNumToKanji: true);
        var lines = new List<string>
        {
            "<a href=\"https://ncode.syosetu.com/n8005ls/\">https://ncode.syosetu.com/n8005ls/</a>",
        };
        finalizer.Finalize(lines);

        // URL行はそのまま（数字・英字が変換されていないこと）
        Assert.Equal(
            "<a href=\"https://ncode.syosetu.com/n8005ls/\">https://ncode.syosetu.com/n8005ls/</a>",
            lines[0]);
    }

    [Fact]
    public void ConvertNumToKanji_ConversionDateLine_NotConverted()
    {
        var finalizer = CreateFinalizer(convertNumToKanji: true);
        var lines = new List<string>
        {
            "変換日時：　2026/03/07 12:00:00",
        };
        finalizer.Finalize(lines);

        Assert.Contains("2026/03/07", lines[0]);
    }

    [Fact]
    public void AlphabetToZenkaku_UrlLine_NotConverted()
    {
        var finalizer = CreateFinalizer(convertNumToKanji: false);
        var lines = new List<string>
        {
            "底本：　<a href=\"https://ncode.syosetu.com/n8005ls/\">https://ncode.syosetu.com/n8005ls/</a>",
        };
        finalizer.Finalize(lines);

        Assert.Contains("n8005ls", lines[0]);
    }

    // ── Fix 3: 読了表示が1箇所のみ ──

    [Fact]
    public void Finalize_EndOfBook_AddedOnce()
    {
        var finalizer = CreateFinalizer(displayEndOfBook: true);
        var lines = new List<string> { "本文" };
        finalizer.Finalize(lines);

        int count = lines.Count(l => l.Contains("本を読み終わりました"));
        Assert.Equal(1, count);
    }
}
