using System;
using System.IO;
using System.Text;
using AozoraEpub3.Core.Io;
using Scriban;
using Scriban.Runtime;
using Xunit;

namespace AozoraEpub3.Tests;

/// <summary>
/// タイトルページ(title_horizontal.sbn / title_middle.sbn)の長タイトル自動調整
/// (TITLE_LENGTH による font-size ラダー)と Epub3Writer.DisplayTextLength のテスト。
///
/// Java 版 (D:\git\AozoraEpub3\AozoraEpub3) の
///   test/com/github/hmdev/writer/Epub3WriterDisplayTextLengthTest.java
///   test/com/github/hmdev/epub/TitlePageAutofitTemplateTest.java
/// と同じ観点。設計は Java 側 docs/title-page-autofit-plan.md を参照。
/// </summary>
public class TitlePageAutofitTests
{
    // ── Epub3Writer.DisplayTextLength ────────────────────────────────────────

    [Fact]
    public void DisplayTextLength_PlainText()
        => Assert.Equal(5, Epub3Writer.DisplayTextLength("あいうえお"));

    [Fact]
    public void DisplayTextLength_Null()
        => Assert.Equal(0, Epub3Writer.DisplayTextLength(null));

    [Fact]
    public void DisplayTextLength_Empty()
        => Assert.Equal(0, Epub3Writer.DisplayTextLength(""));

    /// <summary>ルビは rt を除き親文字のみ数える</summary>
    [Fact]
    public void DisplayTextLength_RubyCountsBaseTextOnly()
        => Assert.Equal(3, Epub3Writer.DisplayTextLength("前<ruby>漢字<rt>かんじ</rt></ruby>"));

    [Fact]
    public void DisplayTextLength_RubyWithRpCountsBaseTextOnly()
        => Assert.Equal(2, Epub3Writer.DisplayTextLength("<ruby>漢字<rp>（</rp><rt>かんじ</rt><rp>）</rp></ruby>"));

    /// <summary>属性付きの rt/rp タグも除去する(chuki_tag.txt はユーザ編集可能)</summary>
    [Fact]
    public void DisplayTextLength_RubyWithAttributesCountsBaseTextOnly()
        => Assert.Equal(2, Epub3Writer.DisplayTextLength("<ruby>漢字<rt class=\"r\">かんじ</rt></ruby>"));

    /// <summary>外字画像は1文字として数える</summary>
    [Fact]
    public void DisplayTextLength_GaijiImageCountsAsOneChar()
        => Assert.Equal(5, Epub3Writer.DisplayTextLength("外字<img src=\"../gaiji/u2eb66.png\" alt=\"〓\"/>あり"));

    /// <summary>文字実体参照は1文字として数える</summary>
    [Fact]
    public void DisplayTextLength_CharacterEntityCountsAsOneChar()
    {
        Assert.Equal(3, Epub3Writer.DisplayTextLength("A&amp;B"));
        Assert.Equal(3, Epub3Writer.DisplayTextLength("A&#x3042;B"));
    }

    /// <summary>その他のタグは除去して数える</summary>
    [Fact]
    public void DisplayTextLength_OtherTagsAreStripped()
        => Assert.Equal(2, Epub3Writer.DisplayTextLength("<b>太字</b>"));

    /// <summary>サロゲートペアはコードポイントで1文字と数える</summary>
    [Fact]
    public void DisplayTextLength_SurrogatePairCountsAsOneChar()
        => Assert.Equal(3, Epub3Writer.DisplayTextLength("𠀋の話"));

    /// <summary>なろうの実タイトル相当の長さが素直に数えられる</summary>
    [Fact]
    public void DisplayTextLength_LongNarouTitle()
    {
        const string title =
            "S級探索者を5人育てたら全員に独立された中年コーチ、暇つぶしの初心者講座配信が「人類の攻略常識」を書き換え始める";
        Assert.Equal(title.Length, Epub3Writer.DisplayTextLength(title));
    }

    // ── テンプレートレンダリング ─────────────────────────────────────────────

    private const string TemplateResourcePrefix = "AozoraEpub3.Core.Resources.template.OPS.xhtml.";

    private static string ReadTemplate(string fileName)
    {
        var asm = typeof(Epub3Writer).Assembly;
        using var stream = asm.GetManifestResourceStream(TemplateResourcePrefix + fileName)
            ?? throw new InvalidOperationException($"Embedded template not found: {fileName}");
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private static string Render(string fileName, Action<ScriptObject> setup)
    {
        var template = Template.Parse(ReadTemplate(fileName));
        Assert.False(template.HasErrors,
            template.HasErrors ? string.Join("\n", template.Messages) : "");

        var so = new ScriptObject();
        setup(so);
        // Epub3Writer.InitTemplateContext と同じ設定
        var ctx = new TemplateContext { MemberRenamer = member => member.Name };
        ctx.PushGlobal(so);
        return template.Render(ctx);
    }

    private static string RenderHorizontal(string? title, int? titleLength, string? series = null)
        => Render("title_horizontal.sbn", so =>
        {
            if (title != null) { so["title"] = title; so["TITLE"] = title; }
            else so["title"] = "t";
            if (titleLength != null) so["TITLE_LENGTH"] = titleLength.Value;
            if (series != null) so["SERIES"] = series;
            so["CREATOR"] = "テスト著者";
        });

    private static string RenderMiddle(string? title, int? titleLength)
        => Render("title_middle.sbn", so =>
        {
            if (title != null) { so["title"] = title; so["TITLE"] = title; }
            else so["title"] = "t";
            if (titleLength != null) so["TITLE_LENGTH"] = titleLength.Value;
            so["CREATOR"] = "テスト著者";
            // title_middle.sbn は縦書き判定に bookInfo.Vertical を参照する
            so["bookInfo"] = new ScriptObject { ["Vertical"] = true };
        });

    private static int CountOf(string text, string sub)
    {
        int count = 0;
        for (int i = text.IndexOf(sub, StringComparison.Ordinal); i >= 0;
             i = text.IndexOf(sub, i + sub.Length, StringComparison.Ordinal)) count++;
        return count;
    }

    /// <summary>.title { } ブロック内の font-size を返す
    /// (.subtitle 等の固定ルールにも font-size:1.6em 等が含まれるため Contains だとトートロジーになる)</summary>
    private static string TitleBlockFontSize(string output)
    {
        int start = output.IndexOf(".title {", StringComparison.Ordinal);
        Assert.True(start >= 0, ".title block not found");
        int end = output.IndexOf('}', start);
        return output.Substring(start + ".title {".Length, end - start - ".title {".Length).Trim();
    }

    // ── title_horizontal.sbn: font-size ラダー ───────────────────────────────

    [Theory]
    [InlineData(20,  "font-size:2em;")]
    [InlineData(30,  "font-size:2em;")]
    [InlineData(31,  "font-size:1.75em;")]
    [InlineData(45,  "font-size:1.75em;")]
    [InlineData(46,  "font-size:1.6em;")]
    [InlineData(60,  "font-size:1.6em;")]
    [InlineData(61,  "font-size:1.4em;")]
    [InlineData(80,  "font-size:1.4em;")]
    [InlineData(81,  "font-size:1.25em;")]
    [InlineData(120, "font-size:1.25em;")]
    [InlineData(121, "font-size:1.1em;")]
    public void Horizontal_FontSizeLadder(int titleLength, string expected)
        => Assert.Equal(expected, TitleBlockFontSize(RenderHorizontal("タイトル", titleLength)));

    // ── title_horizontal.sbn: 45文字以下は現行レイアウト維持 ─────────────────

    [Fact]
    public void Horizontal_ShortTitleKeepsLegacyLayout()
    {
        string output = RenderHorizontal("短いタイトル", 20);
        Assert.Contains(".upper { padding:10% 5% 0 5%; height:50%; text-align:center; }", output);
        // SERIES/ORGTITLE/SUBTITLE/SUBORGTITLE なし → space 4個
        Assert.Equal(4, CountOf(output, "<div class=\"space\"></div>"));
    }

    // ── title_horizontal.sbn: 46文字以上で構造調整 ───────────────────────────

    [Fact]
    public void Horizontal_LongTitleDropsSpacersAndFixedHeight()
    {
        string output = RenderHorizontal("長いタイトル", 90);
        // min-height でタイトルが50%を超えた時だけ伸びる(著者名と重ならず、通常は下段位置を維持)
        Assert.Contains(".upper { padding:5% 5% 0 5%; min-height:50%; text-align:center; }", output);
        Assert.DoesNotContain("; height:50%", output);
        Assert.Equal(0, CountOf(output, "<div class=\"space\"></div>"));
    }

    /// <summary>長タイトルでも SERIES がある場合は series div が出力される</summary>
    [Fact]
    public void Horizontal_LongTitleKeepsSeries()
    {
        string output = RenderHorizontal("長いタイトル", 90, "シリーズ名");
        Assert.Contains("<div class=\"series\">シリーズ名</div>", output);
    }

    // ── フォールバック: TITLE_LENGTH 未設定(旧バージョン + 新テンプレート) ────

    [Fact]
    public void Horizontal_FallbackUsesTitleStringLength()
    {
        Assert.Equal("font-size:1.6em;", TitleBlockFontSize(RenderHorizontal(new string('あ', 50), null)));
        Assert.Equal("font-size:2em;",   TitleBlockFontSize(RenderHorizontal(new string('あ', 20), null)));
    }

    /// <summary>TITLE も TITLE_LENGTH も無い場合(タイトル無し変換)は旧レイアウトを維持する</summary>
    [Fact]
    public void Horizontal_NoTitleKeepsLegacyLayout()
    {
        string output = RenderHorizontal(null, null);
        Assert.Equal(4, CountOf(output, "<div class=\"space\"></div>"));
        Assert.Contains(".upper { padding:10% 5% 0 5%; height:50%; text-align:center; }", output);
        Assert.Equal("font-size:2em;", TitleBlockFontSize(output));
    }

    // ── title_middle.sbn: 簡易ラダー ─────────────────────────────────────────

    [Theory]
    [InlineData(45, ".title { font-size:1.75em; }")]
    [InlineData(46, ".title { font-size:1.4em; }")]
    [InlineData(80, ".title { font-size:1.4em; }")]
    [InlineData(81, ".title { font-size:1.2em; }")]
    public void Middle_FontSizeLadder(int titleLength, string expected)
        => Assert.Contains(expected, RenderMiddle("タイトル", titleLength));

    /// <summary>title_middle.sbn も horizontal と同じフォールバックを持つため対でテストする</summary>
    [Fact]
    public void Middle_FallbackAndNoTitle()
    {
        // TITLE_LENGTH 未設定 → TITLE.size フォールバック
        Assert.Contains(".title { font-size:1.4em; }", RenderMiddle(new string('あ', 50), null));
        // TITLE も無し → 0 扱いで既定サイズ
        Assert.Contains(".title { font-size:1.75em; }", RenderMiddle(null, null));
    }
}
