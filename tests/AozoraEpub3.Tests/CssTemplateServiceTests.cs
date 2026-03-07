using AozoraEpub3.Gui.Services;

namespace AozoraEpub3.Tests;

public class CssTemplateServiceTests
{
    [Fact]
    public void ParseCss_ExtractsPageMargin()
    {
        var css = "@page { margin: 1em 2em 3em 4em; }";
        var p = CssTemplateService.ParseCss(css);

        Assert.Equal("1em", p.PageMargin[0]);
        Assert.Equal("2em", p.PageMargin[1]);
        Assert.Equal("3em", p.PageMargin[2]);
        Assert.Equal("4em", p.PageMargin[3]);
    }

    [Fact]
    public void ParseCss_ExtractsFontSize()
    {
        var css = "body { font-size: 120%; }";
        var p = CssTemplateService.ParseCss(css);

        Assert.Equal(120, p.FontSize);
    }

    [Fact]
    public void ParseCss_ExtractsLineHeight()
    {
        var css = "body { line-height: 2.0; }";
        var p = CssTemplateService.ParseCss(css);

        Assert.Equal(2.0f, p.LineHeight);
    }

    [Fact]
    public void ParseCss_DetectsVerticalWriting()
    {
        var css = "html { writing-mode: vertical-rl; }";
        var p = CssTemplateService.ParseCss(css);

        Assert.True(p.IsVertical);
    }

    [Fact]
    public void ParseCss_DetectsHorizontalWriting()
    {
        var css = "html { writing-mode: horizontal-tb; }";
        var p = CssTemplateService.ParseCss(css);

        Assert.False(p.IsVertical);
    }

    [Fact]
    public void ParseCss_DefaultValues_WhenNoCssContent()
    {
        var p = CssTemplateService.ParseCss("");

        Assert.Equal(100, p.FontSize);
        Assert.Equal(1.8f, p.LineHeight);
        Assert.False(p.IsVertical);
    }

    [Fact]
    public void GenerateCss_ContainsVerticalWritingMode()
    {
        var p = new CssStyleParams { IsVertical = true };
        var css = CssTemplateService.GenerateCss(p);

        Assert.Contains("vertical-rl", css);
        Assert.DoesNotContain("horizontal-tb", css);
    }

    [Fact]
    public void GenerateCss_ContainsHorizontalWritingMode()
    {
        var p = new CssStyleParams { IsVertical = false };
        var css = CssTemplateService.GenerateCss(p);

        Assert.Contains("horizontal-tb", css);
        Assert.DoesNotContain("vertical-rl", css);
    }

    [Fact]
    public void GenerateCss_IncludesFontSizeAndLineHeight()
    {
        var p = new CssStyleParams { FontSize = 150, LineHeight = 2.5f };
        var css = CssTemplateService.GenerateCss(p);

        Assert.Contains("font-size: 150%", css);
        Assert.Contains("line-height: 2.5", css);
    }

    [Fact]
    public void GenerateCss_IncludesMargins()
    {
        var p = new CssStyleParams
        {
            PageMargin = ["1em", "2em", "3em", "4em"],
            BodyMargin = ["5px", "6px", "7px", "8px"],
        };
        var css = CssTemplateService.GenerateCss(p);

        Assert.Contains("margin: 1em 2em 3em 4em", css);
        Assert.Contains("margin: 5px 6px 7px 8px", css);
    }

    [Fact]
    public void Roundtrip_ParseAndGenerate_PreservesValues()
    {
        var original = new CssStyleParams
        {
            PageMargin = ["0", "0", "0", "0"],
            BodyMargin = ["0", "0", "0", "0"],
            FontSize = 100,
            LineHeight = 1.8f,
            IsVertical = true,
        };

        var css = CssTemplateService.GenerateCss(original);
        var parsed = CssTemplateService.ParseCss(css);

        Assert.Equal(original.FontSize, parsed.FontSize);
        Assert.Equal(original.LineHeight, parsed.LineHeight);
        Assert.Equal(original.IsVertical, parsed.IsVertical);
    }

    [Fact]
    public void ParseCss_ShorthandMargin_SingleValue()
    {
        var css = "@page { margin: 1em; }";
        var p = CssTemplateService.ParseCss(css);

        Assert.Equal("1em", p.PageMargin[0]);
        Assert.Equal("1em", p.PageMargin[1]);
        Assert.Equal("1em", p.PageMargin[2]);
        Assert.Equal("1em", p.PageMargin[3]);
    }

    [Fact]
    public void ParseCss_ShorthandMargin_TwoValues()
    {
        var css = "@page { margin: 1em 2em; }";
        var p = CssTemplateService.ParseCss(css);

        Assert.Equal("1em", p.PageMargin[0]);
        Assert.Equal("2em", p.PageMargin[1]);
        Assert.Equal("1em", p.PageMargin[2]);
        Assert.Equal("2em", p.PageMargin[3]);
    }
}
