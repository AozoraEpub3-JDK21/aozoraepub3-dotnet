using System.Text;
using System.Text.RegularExpressions;

namespace AozoraEpub3.Core.Editor;

/// <summary>
/// 小説向け自動整形（Lint/Formatter）。
/// 入力テキストを日本語小説の慣習に合わせて整形する。
/// ConversionProfile の設定に従い、有効なルールのみ適用する。
/// </summary>
public sealed partial class NovelFormatter
{
    private readonly ConversionProfile _profile;

    public NovelFormatter(ConversionProfile profile)
    {
        _profile = profile;
    }

    /// <summary>テキスト全体を整形して返す</summary>
    public string Format(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;

        var result = text;

        if (_profile.AutoEllipsis)
            result = FormatEllipsis(result);

        if (_profile.AutoDash)
            result = FormatDash(result);

        if (_profile.AutoExclamationSpace)
            result = FormatExclamationSpace(result);

        return result;
    }

    /// <summary>整形問題をレポートする（テキストは変更しない）</summary>
    public IReadOnlyList<LintWarning> Lint(string text)
    {
        if (string.IsNullOrEmpty(text)) return [];

        var warnings = new List<LintWarning>();

        if (_profile.AutoEllipsis)
            LintEllipsis(text, warnings);

        if (_profile.AutoDash)
            LintDash(text, warnings);

        if (_profile.AutoExclamationSpace)
            LintExclamationSpace(text, warnings);

        return warnings;
    }

    // ── 三点リーダー: ... / 。。。 / ・・・ → …… ──

    [GeneratedRegex(@"\.{3,}|。{3,}|・{3,}")]
    private static partial Regex EllipsisPattern();

    private static string FormatEllipsis(string text)
        => EllipsisPattern().Replace(text, "……");

    private void LintEllipsis(string text, List<LintWarning> warnings)
    {
        foreach (var m in EllipsisPattern().EnumerateMatches(text))
        {
            var (line, col) = GetLineCol(text, m.Index);
            warnings.Add(new LintWarning
            {
                Rule = "L1",
                Message = "三点リーダーは「……」に統一",
                Line = line,
                Column = col,
                Length = m.Length,
                Suggestion = "……"
            });
        }
    }

    // ── ダッシュ: -- → ―― ──

    [GeneratedRegex(@"(?<!-)--(?!-)")]
    private static partial Regex DashPattern();

    [GeneratedRegex(@"─{2,}")]
    private static partial Regex BoxDrawDashPattern();

    private static string FormatDash(string text)
    {
        var result = DashPattern().Replace(text, "――");
        result = BoxDrawDashPattern().Replace(result, "――");
        return result;
    }

    private void LintDash(string text, List<LintWarning> warnings)
    {
        foreach (var m in DashPattern().EnumerateMatches(text))
        {
            var (line, col) = GetLineCol(text, m.Index);
            warnings.Add(new LintWarning
            {
                Rule = "L2",
                Message = "ダッシュは「――」に統一",
                Line = line,
                Column = col,
                Length = m.Length,
                Suggestion = "――"
            });
        }
        foreach (var m in BoxDrawDashPattern().EnumerateMatches(text))
        {
            var (line, col) = GetLineCol(text, m.Index);
            warnings.Add(new LintWarning
            {
                Rule = "L2",
                Message = "ダッシュは「――」に統一",
                Line = line,
                Column = col,
                Length = m.Length,
                Suggestion = "――"
            });
        }
    }

    // ── 感嘆符/疑問符後の全角スペース ──
    // ！や？の直後に全角文字が続く場合、全角スペースを挿入
    // ただし行末・閉じ括弧の前では挿入しない

    [GeneratedRegex(@"([！？!?]+)(?=[^\s！？!?」』）〕】〉》≫\r\n])")]
    private static partial Regex ExclamationSpacePattern();

    private static string FormatExclamationSpace(string text)
        => ExclamationSpacePattern().Replace(text, "$1　");

    private void LintExclamationSpace(string text, List<LintWarning> warnings)
    {
        foreach (var m in ExclamationSpacePattern().EnumerateMatches(text))
        {
            var (line, col) = GetLineCol(text, m.Index);
            warnings.Add(new LintWarning
            {
                Rule = "L3",
                Message = "感嘆符・疑問符の後に全角スペースを挿入",
                Line = line,
                Column = col,
                Length = m.Length,
                Suggestion = null // 元のテキストに依存
            });
        }
    }

    // ── ユーティリティ ──

    private static (int line, int col) GetLineCol(string text, int index)
    {
        int line = 1, col = 1;
        for (int i = 0; i < index && i < text.Length; i++)
        {
            if (text[i] == '\n')
            {
                line++;
                col = 1;
            }
            else
            {
                col++;
            }
        }
        return (line, col);
    }
}

/// <summary>Lint 警告</summary>
public sealed class LintWarning
{
    public required string Rule { get; init; }
    public required string Message { get; init; }
    public required int Line { get; init; }
    public required int Column { get; init; }
    public required int Length { get; init; }
    public string? Suggestion { get; init; }
}
