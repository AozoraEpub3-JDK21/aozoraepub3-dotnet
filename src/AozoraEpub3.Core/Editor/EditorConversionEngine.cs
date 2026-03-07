using System.Text;
using System.Text.RegularExpressions;

namespace AozoraEpub3.Core.Editor;

/// <summary>
/// ハイブリッド記法 → 青空文庫記法の変換エンジン。
/// Markdown風/Web小説投稿サイト風の入力記法を、AozoraEpub3Converter が処理できる
/// 青空文庫記法テキストに変換する。
///
/// 変換順序（競合回避のため厳密に定義）:
///   1. エスケープ文字の退避
///   2. 傍点 《《…》》（ルビの《》と競合するため最優先）
///   3. ルビ |…《…》
///   4. 太字 **…**
///   5. 見出し # …
///   6. 改ページ ---
///   7. 引用 > …
///   8. エスケープ文字の復元
/// </summary>
public sealed partial class EditorConversionEngine
{
    private readonly ConversionProfile _profile;
    private readonly NovelFormatter _formatter;

    // エスケープ用プレースホルダ（Unicode 私用領域を使用）
    private const char EscapePipe = '\uE001';
    private const char EscapeAsterisk = '\uE002';
    private const char EscapeHash = '\uE003';
    private const char EscapeOpenAngle = '\uE004';
    private const char EscapeBackslash = '\uE005';

    public EditorConversionEngine(ConversionProfile profile)
    {
        _profile = profile;
        _formatter = new NovelFormatter(profile);
    }

    /// <summary>
    /// ハイブリッド記法テキストを青空文庫記法テキストに変換する。
    /// </summary>
    public string Convert(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;

        var result = text;

        // 1. エスケープ退避
        result = EscapeSpecialChars(result);

        // 2. 傍点（最優先: ルビの《》と競合するため）
        if (_profile.EnableEmphasis)
            result = ConvertEmphasis(result);

        // 3. ルビ
        if (_profile.EnableRuby)
            result = ConvertRuby(result);

        // 4. 太字
        if (_profile.EnableBold)
            result = ConvertBold(result);

        // 5. 見出し
        if (_profile.EnableHeadings)
            result = ConvertHeadings(result);

        // 6. 改ページ
        if (_profile.EnablePageBreak)
            result = ConvertPageBreak(result);

        // 7. 引用
        if (_profile.EnableBlockquote)
            result = ConvertBlockquote(result);

        // 8. エスケープ復元
        result = UnescapeSpecialChars(result);

        return result;
    }

    /// <summary>
    /// 自動整形（Lint）を適用してから変換する。
    /// プレビュー用のパイプライン全体。
    /// </summary>
    public string FormatAndConvert(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;

        var formatted = _formatter.Format(text);
        return Convert(formatted);
    }

    /// <summary>Lint 警告を取得する（テキストは変更しない）</summary>
    public IReadOnlyList<LintWarning> GetLintWarnings(string text)
        => _formatter.Lint(text);

    // ── エスケープ ──

    [GeneratedRegex(@"\\([|*#《\\])")]
    private static partial Regex EscapePattern();

    private static string EscapeSpecialChars(string text)
    {
        return EscapePattern().Replace(text, m => m.Groups[1].Value switch
        {
            "|" => EscapePipe.ToString(),
            "*" => EscapeAsterisk.ToString(),
            "#" => EscapeHash.ToString(),
            "《" => EscapeOpenAngle.ToString(),
            "\\" => EscapeBackslash.ToString(),
            _ => m.Value
        });
    }

    private static string UnescapeSpecialChars(string text)
    {
        return text
            .Replace(EscapePipe, '|')
            .Replace(EscapeAsterisk, '*')
            .Replace(EscapeHash, '#')
            .Replace(EscapeOpenAngle, '《')
            .Replace(EscapeBackslash, '\\');
    }

    // ── 傍点: 《《テキスト》》 → ［＃傍点］テキスト［＃傍点終わり］ ──

    [GeneratedRegex(@"《《([^》]+)》》")]
    private static partial Regex EmphasisPattern();

    private static string ConvertEmphasis(string text)
        => EmphasisPattern().Replace(text, "［＃傍点］$1［＃傍点終わり］");

    // ── ルビ: |漢字《かんじ》 → ｜漢字《かんじ》 ──

    [GeneratedRegex(@"\|([^\|《》\s]+)《([^》]+)》")]
    private static partial Regex RubyPattern();

    private static string ConvertRuby(string text)
        => RubyPattern().Replace(text, "｜$1《$2》");

    // ── 太字: **テキスト** → ［＃太字］テキスト［＃太字終わり］ ──

    [GeneratedRegex(@"\*\*([^\*]+)\*\*")]
    private static partial Regex BoldPattern();

    private static string ConvertBold(string text)
        => BoldPattern().Replace(text, "［＃太字］$1［＃太字終わり］");

    // ── 見出し: # テキスト → ［＃大見出し］テキスト［＃大見出し終わり］ ──

    [GeneratedRegex(@"^(#{1,3})\s+(.+)$", RegexOptions.Multiline)]
    private static partial Regex HeadingPattern();

    private static string ConvertHeadings(string text)
    {
        return HeadingPattern().Replace(text, m =>
        {
            var level = m.Groups[1].Value.Length;
            var content = m.Groups[2].Value;
            var heading = level switch
            {
                1 => "大見出し",
                2 => "中見出し",
                3 => "小見出し",
                _ => "大見出し"
            };
            return $"［＃{heading}］{content}［＃{heading}終わり］";
        });
    }

    // ── 改ページ: --- → ［＃改ページ］ ──

    [GeneratedRegex(@"^-{3,}\s*$", RegexOptions.Multiline)]
    private static partial Regex PageBreakPattern();

    private static string ConvertPageBreak(string text)
        => PageBreakPattern().Replace(text, "［＃改ページ］");

    // ── 引用: > テキスト → ［＃ここから１字下げ］\nテキスト\n［＃ここで字下げ終わり］ ──

    [GeneratedRegex(@"(?:^>\s*(.+)$\r?\n?)+", RegexOptions.Multiline)]
    private static partial Regex BlockquoteBlockPattern();

    [GeneratedRegex(@"^>\s*", RegexOptions.Multiline)]
    private static partial Regex BlockquoteLinePrefix();

    private static string ConvertBlockquote(string text)
    {
        return BlockquoteBlockPattern().Replace(text, m =>
        {
            var content = BlockquoteLinePrefix().Replace(m.Value, "").TrimEnd('\r', '\n');
            return $"［＃ここから１字下げ］\n{content}\n［＃ここで字下げ終わり］\n";
        });
    }
}
