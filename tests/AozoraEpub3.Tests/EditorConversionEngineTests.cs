using AozoraEpub3.Core.Editor;

namespace AozoraEpub3.Tests;

public class EditorConversionEngineTests
{
    private readonly EditorConversionEngine _engine = new(ConversionProfile.Default);

    // ════════════════════════════════════════════════════════
    // ルビ変換
    // ════════════════════════════════════════════════════════

    [Fact]
    public void Convert_Ruby_HalfPipeToFullPipe()
    {
        var result = _engine.Convert("|漢字《かんじ》");
        Assert.Equal("｜漢字《かんじ》", result);
    }

    [Fact]
    public void Convert_Ruby_MultipleInOneLine()
    {
        var result = _engine.Convert("|東京《とうきょう》から|大阪《おおさか》まで");
        Assert.Equal("｜東京《とうきょう》から｜大阪《おおさか》まで", result);
    }

    [Fact]
    public void Convert_Ruby_DoesNotAffectBareAngleBrackets()
    {
        var result = _engine.Convert("普通の《テキスト》です");
        Assert.Equal("普通の《テキスト》です", result);
    }

    // ════════════════════════════════════════════════════════
    // 傍点変換
    // ════════════════════════════════════════════════════════

    [Fact]
    public void Convert_Emphasis_DoubleAngleBrackets()
    {
        var result = _engine.Convert("これは《《重要》》です");
        Assert.Equal("これは［＃傍点］重要［＃傍点終わり］です", result);
    }

    [Fact]
    public void Convert_Emphasis_BeforeRuby()
    {
        // 傍点が先に処理されるため、ルビの《》と競合しない
        var result = _engine.Convert("《《強調》》と|漢字《かんじ》");
        Assert.Equal("［＃傍点］強調［＃傍点終わり］と｜漢字《かんじ》", result);
    }

    // ════════════════════════════════════════════════════════
    // 太字変換
    // ════════════════════════════════════════════════════════

    [Fact]
    public void Convert_Bold_DoubleAsterisk()
    {
        var result = _engine.Convert("これは**重要**です");
        Assert.Equal("これは［＃太字］重要［＃太字終わり］です", result);
    }

    [Fact]
    public void Convert_Bold_Multiple()
    {
        var result = _engine.Convert("**太字1**と**太字2**");
        Assert.Equal("［＃太字］太字1［＃太字終わり］と［＃太字］太字2［＃太字終わり］", result);
    }

    // ════════════════════════════════════════════════════════
    // 見出し変換
    // ════════════════════════════════════════════════════════

    [Fact]
    public void Convert_Heading_H1()
    {
        var result = _engine.Convert("# 第一章");
        Assert.Equal("［＃大見出し］第一章［＃大見出し終わり］", result);
    }

    [Fact]
    public void Convert_Heading_H2()
    {
        var result = _engine.Convert("## 第一節");
        Assert.Equal("［＃中見出し］第一節［＃中見出し終わり］", result);
    }

    [Fact]
    public void Convert_Heading_H3()
    {
        var result = _engine.Convert("### 小見出し");
        Assert.Equal("［＃小見出し］小見出し［＃小見出し終わり］", result);
    }

    [Fact]
    public void Convert_Heading_NotWithoutSpace()
    {
        // # の後にスペースがない場合は見出しとして扱わない
        var result = _engine.Convert("#タグ");
        Assert.Equal("#タグ", result);
    }

    [Fact]
    public void Convert_Heading_MultilinePreservesOtherLines()
    {
        var result = _engine.Convert("本文\n# 見出し\n本文続き");
        Assert.Equal("本文\n［＃大見出し］見出し［＃大見出し終わり］\n本文続き", result);
    }

    // ════════════════════════════════════════════════════════
    // 改ページ変換
    // ════════════════════════════════════════════════════════

    [Fact]
    public void Convert_PageBreak_ThreeDashes()
    {
        var result = _engine.Convert("前の章\n---\n次の章");
        Assert.Equal("前の章\n［＃改ページ］\n次の章", result);
    }

    [Fact]
    public void Convert_PageBreak_MoreDashes()
    {
        var result = _engine.Convert("-----");
        Assert.Equal("［＃改ページ］", result);
    }

    [Fact]
    public void Convert_PageBreak_NotInline()
    {
        // 行の途中にある --- は改ページにしない
        var result = _engine.Convert("テキスト---テキスト");
        Assert.Equal("テキスト---テキスト", result);
    }

    // ════════════════════════════════════════════════════════
    // 引用変換
    // ════════════════════════════════════════════════════════

    [Fact]
    public void Convert_Blockquote_SingleLine()
    {
        var result = _engine.Convert("> 引用文です");
        Assert.Equal("［＃ここから１字下げ］\n引用文です\n［＃ここで字下げ終わり］\n", result);
    }

    [Fact]
    public void Convert_Blockquote_MultipleLines()
    {
        var result = _engine.Convert("> 引用1行目\n> 引用2行目");
        Assert.Equal("［＃ここから１字下げ］\n引用1行目\n引用2行目\n［＃ここで字下げ終わり］\n", result);
    }

    // ════════════════════════════════════════════════════════
    // エスケープ
    // ════════════════════════════════════════════════════════

    [Fact]
    public void Convert_Escape_Pipe()
    {
        var result = _engine.Convert("\\|テスト《てすと》");
        Assert.Equal("|テスト《てすと》", result);
    }

    [Fact]
    public void Convert_Escape_Asterisk()
    {
        var result = _engine.Convert("\\*\\*テスト\\*\\*");
        Assert.Equal("**テスト**", result);
    }

    [Fact]
    public void Convert_Escape_Hash()
    {
        var result = _engine.Convert("\\# テスト");
        Assert.Equal("# テスト", result);
    }

    // ════════════════════════════════════════════════════════
    // 複合変換
    // ════════════════════════════════════════════════════════

    [Fact]
    public void Convert_Combined_AllNotations()
    {
        var input = "# 第一章\n\n本文の中に|漢字《かんじ》があり、《《重要》》な**太字**もある。\n\n---\n\n## 第二章";
        var result = _engine.Convert(input);

        Assert.Contains("［＃大見出し］第一章［＃大見出し終わり］", result);
        Assert.Contains("｜漢字《かんじ》", result);
        Assert.Contains("［＃傍点］重要［＃傍点終わり］", result);
        Assert.Contains("［＃太字］太字［＃太字終わり］", result);
        Assert.Contains("［＃改ページ］", result);
        Assert.Contains("［＃中見出し］第二章［＃中見出し終わり］", result);
    }

    // ════════════════════════════════════════════════════════
    // プロファイル設定による無効化
    // ════════════════════════════════════════════════════════

    [Fact]
    public void Convert_DisabledRuby_PassesThrough()
    {
        var engine = new EditorConversionEngine(new ConversionProfile
        {
            Mode = EditorMode.Generic,
            EnableRuby = false
        });
        var result = engine.Convert("|漢字《かんじ》");
        Assert.Equal("|漢字《かんじ》", result);
    }

    [Fact]
    public void Convert_DisabledBold_PassesThrough()
    {
        var engine = new EditorConversionEngine(new ConversionProfile
        {
            Mode = EditorMode.Generic,
            EnableBold = false
        });
        var result = engine.Convert("**太字**");
        Assert.Equal("**太字**", result);
    }

    // ════════════════════════════════════════════════════════
    // FormatAndConvert（整形 + 変換パイプライン）
    // ════════════════════════════════════════════════════════

    [Fact]
    public void FormatAndConvert_EllipsisAndConversion()
    {
        var result = _engine.FormatAndConvert("# 第一章\n\n本文...続き");
        Assert.Contains("［＃大見出し］第一章［＃大見出し終わり］", result);
        Assert.Contains("本文……続き", result);
    }

    // ════════════════════════════════════════════════════════
    // 空文字列・null
    // ════════════════════════════════════════════════════════

    [Fact]
    public void Convert_EmptyString_ReturnsEmpty()
    {
        Assert.Equal("", _engine.Convert(""));
    }

    [Theory]
    [InlineData(null)]
    public void Convert_Null_ReturnsNull(string? input)
    {
        Assert.Null(_engine.Convert(input!));
    }

    [Fact]
    public void Convert_PlainText_Unchanged()
    {
        var text = "普通のテキストです。\n改行もあります。";
        Assert.Equal(text, _engine.Convert(text));
    }
}
