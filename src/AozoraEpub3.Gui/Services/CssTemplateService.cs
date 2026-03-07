using System.IO;
using System.Text.RegularExpressions;

namespace AozoraEpub3.Gui.Services;

/// <summary>
/// EPUB 内の CSS を解析し、テンプレート変数（マージン、フォントサイズ等）を抽出・再生成する。
/// </summary>
public sealed class CssTemplateService
{
    /// <summary>CSS テキストからスタイルパラメータを抽出する。</summary>
    public static CssStyleParams ParseCss(string cssText)
    {
        var p = new CssStyleParams();

        // @page { margin: T R B L; }
        var pageMatch = Regex.Match(cssText, @"@page\s*\{[^}]*margin\s*:\s*([^;]+);", RegexOptions.Singleline);
        if (pageMatch.Success)
            ParseMarginValues(pageMatch.Groups[1].Value.Trim(), p.PageMargin);

        // html { margin: T R B L; }
        var htmlMatch = Regex.Match(cssText, @"html\s*\{[^}]*margin\s*:\s*([^;]+);", RegexOptions.Singleline);
        if (htmlMatch.Success)
            ParseMarginValues(htmlMatch.Groups[1].Value.Trim(), p.BodyMargin);

        // font-size: NNN%;
        var fsMatch = Regex.Match(cssText, @"font-size\s*:\s*(\d+)\s*%");
        if (fsMatch.Success)
            p.FontSize = int.Parse(fsMatch.Groups[1].Value);

        // line-height: N.N;
        var lhMatch = Regex.Match(cssText, @"line-height\s*:\s*([\d.]+)\s*;");
        if (lhMatch.Success && float.TryParse(lhMatch.Groups[1].Value, out var lh))
            p.LineHeight = lh;

        // writing-mode detection
        p.IsVertical = cssText.Contains("vertical-rl");

        // bold/gothic detection
        p.BoldUseGothic = Regex.IsMatch(cssText, @"\.b\s*,\s*\n?\s*\.gtc\s*\{");
        p.GothicUseBold = Regex.IsMatch(cssText, @"\.gtc\s*,\s*\n?\s*\.b\s*\{");

        return p;
    }

    /// <summary>元の CSS テキスト内の該当プロパティだけを書き換える（パッチ方式）。</summary>
    public static string PatchCss(string originalCss, CssStyleParams p)
    {
        var css = originalCss;

        // @page { margin: ... }
        css = Regex.Replace(css,
            @"(@page\s*\{[^}]*margin\s*:\s*)[^;]+(;)",
            $"${{1}}{p.PageMargin[0]} {p.PageMargin[1]} {p.PageMargin[2]} {p.PageMargin[3]}$2",
            RegexOptions.Singleline);

        // html { margin: ... }
        css = Regex.Replace(css,
            @"(html\s*\{[^}]*margin\s*:\s*)[^;]+(;)",
            $"${{1}}{p.BodyMargin[0]} {p.BodyMargin[1]} {p.BodyMargin[2]} {p.BodyMargin[3]}$2",
            RegexOptions.Singleline);

        // font-size: NNN%
        css = Regex.Replace(css, @"(font-size\s*:\s*)\d+(\s*%)", $"${{1}}{p.FontSize}$2");

        // line-height: N.N
        css = Regex.Replace(css, @"(line-height\s*:\s*)[\d.]+(\s*;)", $"${{1}}{p.LineHeight}$2");

        // writing-mode（3箇所: 標準、-webkit-、-epub-）
        var newMode = p.IsVertical ? "vertical-rl" : "horizontal-tb";
        css = Regex.Replace(css, @"((?:-webkit-|-epub-)?writing-mode\s*:\s*)\S+(;)", $"${{1}}{newMode}$2");

        return css;
    }

    /// <summary>パラメータから CSS テキストを生成する（新規生成用）。</summary>
    public static string GenerateCss(CssStyleParams p)
    {
        var writingMode = p.IsVertical ? "vertical-rl" : "horizontal-tb";
        var fontPrefix = p.IsVertical ? "'@ＭＳ ゴシック','@MS Gothic'" : "'ＭＳ ゴシック','MS Gothic'";

        var boldGothicBlock = p.BoldUseGothic
            ? $".b,\n.gtc {{\nfont-family: {fontPrefix},sans-serif;\n}}"
            : $".gtc {{\nfont-family: {fontPrefix},sans-serif;\n}}";

        var gothicBoldBlock = p.GothicUseBold
            ? ".gtc,\n.b { font-weight: bold; }"
            : ".b { font-weight: bold; }";

        return $$"""
            @charset "utf-8";
            @namespace "http://www.w3.org/1999/xhtml";

            @page {
            margin: {{p.PageMargin[0]}} {{p.PageMargin[1]}} {{p.PageMargin[2]}} {{p.PageMargin[3]}};
            }

            html {
            margin: {{p.BodyMargin[0]}} {{p.BodyMargin[1]}} {{p.BodyMargin[2]}} {{p.BodyMargin[3]}};
            padding: 0;
            writing-mode: {{writingMode}};
            -webkit-writing-mode: {{writingMode}};
            -epub-writing-mode: {{writingMode}};
            -epub-line-break: strict;
            line-break: strict;
            -epub-word-break: normal;
            word-break: normal;
            }
            body {
            margin: 0;
            padding: 0;
            display: block;
            color: #000;
            font-size: {{p.FontSize}}%;
            line-height: {{p.LineHeight}};
            vertical-align: baseline;
            }

            {{boldGothicBlock}}
            {{gothicBoldBlock}}
            .i { font-style: italic; }
            """;
    }

    private static void ParseMarginValues(string marginStr, string[] target)
    {
        var parts = marginStr.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        for (int i = 0; i < Math.Min(parts.Length, 4); i++)
            target[i] = parts[i];

        // CSS shorthand expansion
        if (parts.Length == 1)
            target[1] = target[2] = target[3] = target[0];
        else if (parts.Length == 2)
        {
            target[2] = target[0];
            target[3] = target[1];
        }
        else if (parts.Length == 3)
            target[3] = target[1];
    }
}

/// <summary>CSS テンプレートパラメータ</summary>
public sealed class CssStyleParams
{
    public string[] PageMargin { get; set; } = ["0", "0", "0", "0"];
    public string[] BodyMargin { get; set; } = ["0", "0", "0", "0"];
    public int FontSize { get; set; } = 100;
    public float LineHeight { get; set; } = 1.8f;
    public bool IsVertical { get; set; } = true;
    public bool BoldUseGothic { get; set; } = false;
    public bool GothicUseBold { get; set; } = false;
}
