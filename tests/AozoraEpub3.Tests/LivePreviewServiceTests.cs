using AozoraEpub3.Core.Editor;
using AozoraEpub3.Gui.Services;

namespace AozoraEpub3.Tests;

public class LivePreviewServiceTests
{
    private readonly LivePreviewService _service = new(ConversionProfile.Default);

    [Fact]
    public void ConvertToXhtml_EmptyText_ReturnsValidXhtml()
    {
        var result = _service.ConvertToXhtml("");
        Assert.Contains("<!DOCTYPE html>", result);
        Assert.Contains("<body>", result);
        Assert.Contains("vertical-rl", result);
    }

    [Fact]
    public void ConvertToXhtml_HorizontalMode_UsesHorizontalTb()
    {
        var result = _service.ConvertToXhtml("テスト", vertical: false);
        Assert.Contains("horizontal-tb", result);
        Assert.DoesNotContain("vertical-rl", result);
    }

    [Fact]
    public void ConvertToXhtml_PlainText_GeneratesBody()
    {
        var result = _service.ConvertToXhtml("テスト文章です。\n二行目。");
        Assert.Contains("テスト文章です。", result);
        Assert.Contains("二行目。", result);
    }

    [Fact]
    public void ConvertToXhtml_RubyNotation_ConvertsToRubyTag()
    {
        var result = _service.ConvertToXhtml("|漢字《かんじ》");
        // 青空文庫記法 ｜漢字《かんじ》 → <ruby>漢字<rp>（</rp><rt>かんじ</rt><rp>）</rp></ruby>
        Assert.Contains("<ruby>", result);
        Assert.Contains("かんじ", result);
    }

    [Fact]
    public void ConvertToXhtml_EmphasisNotation_ConvertsToSesame()
    {
        var result = _service.ConvertToXhtml("《《強調テキスト》》");
        // 傍点 → sesame class
        Assert.Contains("強調テキスト", result);
    }

    [Fact]
    public void ConvertToXhtml_HeadingNotation_ConvertsToHeading()
    {
        var result = _service.ConvertToXhtml("# 見出しテスト");
        Assert.Contains("見出しテスト", result);
    }

    [Fact]
    public void ConvertToXhtml_PageBreak_GeneratesPageBreak()
    {
        var result = _service.ConvertToXhtml("前の文\n---\n後の文");
        Assert.Contains("前の文", result);
        Assert.Contains("後の文", result);
    }

    [Fact]
    public void ConvertToXhtml_BoldNotation_ConvertsToBold()
    {
        var result = _service.ConvertToXhtml("**太字テスト**");
        Assert.Contains("太字テスト", result);
    }

    [Fact]
    public void GetLintWarnings_TripleDots_ReturnsWarning()
    {
        var warnings = _service.GetLintWarnings("テスト...文章");
        Assert.True(warnings.Count > 0);
    }

    [Fact]
    public void ConvertToXhtml_NarouMode_Works()
    {
        var service = new LivePreviewService(ConversionProfile.Narou);
        var result = service.ConvertToXhtml("テスト文章");
        Assert.Contains("テスト文章", result);
    }

    [Fact]
    public void ConvertToXhtml_KakuyomuMode_Works()
    {
        var service = new LivePreviewService(ConversionProfile.Kakuyomu);
        var result = service.ConvertToXhtml("テスト文章");
        Assert.Contains("テスト文章", result);
    }
}
