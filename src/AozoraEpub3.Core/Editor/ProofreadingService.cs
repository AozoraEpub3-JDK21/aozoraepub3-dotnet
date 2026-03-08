using System.Text.RegularExpressions;

namespace AozoraEpub3.Core.Editor;

/// <summary>
/// 校正支援サービス（E6-5）。
/// テキストの表記ゆれ、括弧の不整合、連続重複表現などを検出する。
/// </summary>
public sealed partial class ProofreadingService
{
    /// <summary>テキスト全体を校正チェックし、警告リストを返す。</summary>
    public IReadOnlyList<LintWarning> Check(string text)
    {
        if (string.IsNullOrEmpty(text)) return [];

        var warnings = new List<LintWarning>();

        CheckNotationVariants(text, warnings);
        CheckBracketMismatch(text, warnings);
        CheckConsecutiveDuplicates(text, warnings);

        return warnings;
    }

    // ═══════════════════════════════════════════════════════════
    // 表記ゆれ検出
    // ═══════════════════════════════════════════════════════════

    /// <summary>よくある表記ゆれペア</summary>
    private static readonly (string a, string b, string hint)[] VariantPairs =
    [
        ("行なう", "行う", "「行う」に統一を推奨"),
        ("行なっ", "行っ", "「行っ」に統一を推奨"),
        ("行なわ", "行わ", "「行わ」に統一を推奨"),
        ("行ない", "行い", "「行い」に統一を推奨"),
        ("おこなう", "行う", "「行う」に統一を推奨"),
        ("つくる", "作る", "表記を統一してください"),
        ("できる", "出来る", "表記を統一してください"),
        ("わかる", "分かる", "表記を統一してください"),
        ("すべて", "全て", "表記を統一してください"),
        ("さまざま", "様々", "表記を統一してください"),
        ("ひとつ", "一つ", "表記を統一してください"),
        ("ふたつ", "二つ", "表記を統一してください"),
        ("ちょっと", "一寸", "表記を統一してください"),
        ("たくさん", "沢山", "表記を統一してください"),
        ("ところ", "所", "表記を統一してください"),
        ("こと", "事", "表記を統一してください（名詞的用法）"),
        ("とき", "時", "表記を統一してください"),
        ("もの", "物", "表記を統一してください"),
    ];

    private void CheckNotationVariants(string text, List<LintWarning> warnings)
    {
        foreach (var (a, b, hint) in VariantPairs)
        {
            var hasA = text.Contains(a, StringComparison.Ordinal);
            var hasB = text.Contains(b, StringComparison.Ordinal);

            if (hasA && hasB)
            {
                // 両方の表記が混在 → 少ない方に警告
                var countA = CountOccurrences(text, a);
                var countB = CountOccurrences(text, b);
                var minority = countA <= countB ? a : b;
                var majority = countA <= countB ? b : a;

                foreach (var idx in FindAllOccurrences(text, minority))
                {
                    var (line, col) = GetLineCol(text, idx);
                    warnings.Add(new LintWarning
                    {
                        Rule = "P1",
                        Message = $"表記ゆれ: 「{minority}」と「{majority}」が混在。{hint}",
                        Line = line,
                        Column = col,
                        Length = minority.Length,
                        Suggestion = majority
                    });
                }
            }
        }
    }

    // ═══════════════════════════════════════════════════════════
    // 括弧の不整合検出
    // ═══════════════════════════════════════════════════════════

    private static readonly (char open, char close)[] BracketPairsToCheck =
    [
        ('「', '」'), ('『', '』'), ('（', '）'), ('【', '】'),
        ('《', '》'), ('［', '］'), ('｛', '｝'),
        ('(', ')'), ('[', ']'), ('{', '}'),
    ];

    private void CheckBracketMismatch(string text, List<LintWarning> warnings)
    {
        foreach (var (open, close) in BracketPairsToCheck)
        {
            var openCount = 0;
            var closeCount = 0;
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] == open) openCount++;
                else if (text[i] == close) closeCount++;
            }

            if (openCount != closeCount)
            {
                warnings.Add(new LintWarning
                {
                    Rule = "P2",
                    Message = $"括弧の不整合: '{open}' が {openCount} 個、'{close}' が {closeCount} 個",
                    Line = 1,
                    Column = 1,
                    Length = 0,
                    Suggestion = null
                });
            }
        }
    }

    // ═══════════════════════════════════════════════════════════
    // 連続重複表現検出
    // ═══════════════════════════════════════════════════════════

    [GeneratedRegex(@"(\p{IsHiragana}{2,8})\1", RegexOptions.Compiled)]
    private static partial Regex ConsecutiveDuplicatePattern();

    private void CheckConsecutiveDuplicates(string text, List<LintWarning> warnings)
    {
        foreach (Match m in ConsecutiveDuplicatePattern().Matches(text))
        {
            var word = m.Groups[1].Value;
            // 意図的な繰り返し（擬音語等）を除外
            if (IsIntentionalRepetition(word)) continue;

            var (line, col) = GetLineCol(text, m.Index);
            warnings.Add(new LintWarning
            {
                Rule = "P3",
                Message = $"連続重複: 「{word}」が連続しています",
                Line = line,
                Column = col,
                Length = m.Length,
                Suggestion = word
            });
        }
    }

    private static bool IsIntentionalRepetition(string word)
    {
        // 1文字の繰り返しは擬音語の可能性大
        if (word.Length <= 1) return true;
        // よくある擬音語パターン
        return word is "どき" or "わく" or "きら" or "ぴか" or "がた" or "ごと"
            or "ぶる" or "ひら" or "ゆら" or "ふわ" or "ぐる" or "くる"
            or "にこ" or "うろ" or "おろ" or "ばた" or "ぱた" or "ぐず";
    }

    // ═══════════════════════════════════════════════════════════
    // ユーティリティ
    // ═══════════════════════════════════════════════════════════

    private static int CountOccurrences(string text, string word)
    {
        int count = 0, idx = 0;
        while ((idx = text.IndexOf(word, idx, StringComparison.Ordinal)) >= 0)
        {
            count++;
            idx += word.Length;
        }
        return count;
    }

    private static List<int> FindAllOccurrences(string text, string word)
    {
        var result = new List<int>();
        int idx = 0;
        while ((idx = text.IndexOf(word, idx, StringComparison.Ordinal)) >= 0)
        {
            result.Add(idx);
            idx += word.Length;
        }
        return result;
    }

    private static (int line, int col) GetLineCol(string text, int index)
    {
        int line = 1, col = 1;
        for (int i = 0; i < index && i < text.Length; i++)
        {
            if (text[i] == '\n') { line++; col = 1; }
            else col++;
        }
        return (line, col);
    }
}
