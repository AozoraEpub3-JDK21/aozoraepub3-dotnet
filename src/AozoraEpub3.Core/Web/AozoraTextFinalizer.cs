using System.Text.RegularExpressions;
using AozoraEpub3.Core.Converter;

namespace AozoraEpub3.Core.Web;

/// <summary>
/// 青空文庫テキストのファイナライズ処理を行うクラス。
/// ストリーム変換後に文章全体を評価する後処理を in-memory で実施する。
/// narou.rb互換の処理を実装。
/// </summary>
public class AozoraTextFinalizer
{
    private readonly NarouFormatSettings _settings;

    /// <summary>前書きの区切りパターン（*が44個）</summary>
    private static readonly Regex AuthorIntroductionSplitter = new(@"^[ 　]*[\*＊]{44}$", RegexOptions.Multiline);
    /// <summary>後書きの区切りパターン（*が48個）</summary>
    private static readonly Regex AuthorPostscriptSplitter = new(@"^[ 　]*[\*＊]{48}$", RegexOptions.Multiline);

    private const string OpenBrackets = "「『〔（【〈《≪";
    private const string CloseBrackets = "」』〕）】〉》≫";

    public AozoraTextFinalizer(NarouFormatSettings settings)
    {
        _settings = settings;
    }

    /// <summary>in-memory でファイナライズ処理を適用する</summary>
    public void Finalize(List<string> lines)
    {
        LogAppender.Println("ファイナライズ処理を開始");

        if (_settings.EnablePackBlankLine)
            PackBlankLine(lines);

        if (_settings.EnableAuthorComments)
            DetectAndMarkAuthorComments(lines);

        // 改ページ直後の見出し化（漢数字変換より前に実行し、見出し行を保護する）
        if (_settings.EnableEnchantMidashi)
            EnchantMidashi(lines);

        if (_settings.EnableConvertNumToKanji)
            ConvertNumToKanji(lines);
        else
            HankakuNumToZenkaku(lines);

        AlphabetToZenkaku(lines, _settings.EnableAlphabetForceZenkaku);

        if (_settings.EnableAutoIndent)
            ApplyAutoIndent(lines);

        if (_settings.EnableHalfIndentBracket)
            HalfIndentBracket(lines);

        if (_settings.EnableAutoJoinInBrackets)
            AutoJoinInBrackets(lines);

        if (_settings.EnableAutoJoinLine)
            AutoJoinLine(lines);

        if (_settings.EnableDisplayEndOfBook)
            AppendEndOfBook(lines);

        if (_settings.EnableInspectInvalidOpenCloseBrackets)
            InspectBrackets(lines);

        if (_settings.TextReplacePatterns.Count > 0)
            ApplyReplacePatterns(lines);

        LogAppender.Println("ファイナライズ処理が完了しました");
    }

    /// <summary>前書き・後書きの自動検出と注記挿入 (narou.rb互換)</summary>
    private void DetectAndMarkAuthorComments(List<string> lines)
    {
        bool inIntroduction = false;
        bool inPostscript = false;

        for (int i = 0; i < lines.Count; i++)
        {
            string line = lines[i];

            if (!inIntroduction && !inPostscript && AuthorIntroductionSplitter.IsMatch(line))
            {
                lines.Insert(i + 1, "［＃ここから前書き］");
                i++;
                inIntroduction = true;
                continue;
            }

            if (!inIntroduction && !inPostscript && AuthorPostscriptSplitter.IsMatch(line))
            {
                lines.Insert(i + 1, "［＃ここから後書き］");
                i++;
                inPostscript = true;
                continue;
            }

            if (inIntroduction && (line.StartsWith("［＃改ページ］") || Regex.IsMatch(line, @"^第.+章.*$")))
            {
                lines.Insert(i, "［＃ここで前書き終わり］");
                i++;
                inIntroduction = false;
            }

            if (inPostscript && line.StartsWith("［＃改ページ］"))
            {
                lines.Insert(i, "［＃ここで後書き終わり］");
                i++;
                inPostscript = false;
            }
        }

        if (inIntroduction) lines.Add("［＃ここで前書き終わり］");
        if (inPostscript) lines.Add("［＃ここで後書き終わり］");
    }

    /// <summary>自動行頭字下げ (narou.rb互換: inspector 判定 + 字下げ)</summary>
    private void ApplyAutoIndent(List<string> lines)
    {
        // narou.rb: inspector.inspect_indent — 字下げ不要な行を除いた中で
        // 50% 以上が字下げされていなければ自動字下げを適用
        int targetCount = 0, noIndentCount = 0;
        foreach (string line in lines)
        {
            if (line.Length == 0) continue;
            char ch = line[0];
            if (IgnoreIndentChars.Contains(ch)) continue;
            targetCount++;
            if (ch != ' ' && ch != '　') noIndentCount++;
        }
        if (targetCount == 0) return;
        double ratio = (double)noIndentCount / targetCount;
        if (ratio <= 0.5) return; // 半数以上が既に字下げされている

        // ダッシュ冒頭行に全角スペース追加
        for (int i = 0; i < lines.Count; i++)
        {
            if (lines[i].Length >= 2 && lines[i].StartsWith("――"))
                lines[i] = "　" + lines[i];
        }

        // 行頭字下げ
        for (int i = 0; i < lines.Count; i++)
        {
            string line = lines[i];
            if (line.Length == 0) continue;
            char ch = line[0];
            if (AutoIndentIgnoreChars.Contains(ch)) continue;
            // 中黒1つだけの場合は字下げしない (narou.rb: 三点リーダー代替対策)
            if (ch == '・' && (line.Length < 2 || line[1] != '・')) continue;
            if (ch == ' ' || ch == '　')
                lines[i] = "　" + line.TrimStart(' ', '　');
            else
                lines[i] = "　" + line;
        }
    }

    /// <summary>字下げ判定除外文字 (narou.rb: Inspector::IGNORE_INDENT_CHAR)</summary>
    private static readonly HashSet<char> IgnoreIndentChars =
        new("(（「『〈《≪【〔―・※［〝\n".ToCharArray());

    /// <summary>字下げ対象外文字 (IgnoreIndentChars から・を除外)</summary>
    private static readonly HashSet<char> AutoIndentIgnoreChars =
        new("(（「『〈《≪【〔―※［〝\n".ToCharArray());

    /// <summary>改ページ直後の見出し化 (narou.rb互換)</summary>
    private void EnchantMidashi(List<string> lines)
    {
        bool nextLineIsMidashi = false;

        for (int i = 0; i < lines.Count; i++)
        {
            string line = lines[i];

            if (line == "［＃改ページ］")
            {
                nextLineIsMidashi = true;
                continue;
            }

            if (nextLineIsMidashi)
            {
                if (line.Length > 0 && !line.StartsWith("［＃") &&
                    !line.Contains("［＃中見出し］") && !line.Contains("［＃大見出し］"))
                {
                    lines[i] = "［＃３字下げ］［＃中見出し］" + line + "［＃中見出し終わり］";
                }
                nextLineIsMidashi = false;
            }
        }
    }

    /// <summary>かぎ括弧内の自動連結</summary>
    private static void AutoJoinInBrackets(List<string> lines)
    {
        // 複数行をまたぐ括弧内連結のため全行を一度結合して処理
        string text = string.Join("\n", lines);
        bool changed = true;
        while (changed)
        {
            string before = text;
            text = Regex.Replace(text, "「([^「」]*)\n([^「」]*)」", "「$1　$2」");
            text = Regex.Replace(text, "『([^『』]*)\n([^『』]*)』", "『$1　$2』");
            changed = text != before;
        }
        var newLines = text.Split('\n');
        lines.Clear();
        lines.AddRange(newLines);
    }

    /// <summary>行末読点での自動連結</summary>
    private static void AutoJoinLine(List<string> lines)
    {
        for (int i = lines.Count - 2; i >= 0; i--)
        {
            if (lines[i].EndsWith('、'))
            {
                lines[i] = lines[i] + lines[i + 1];
                lines.RemoveAt(i + 1);
            }
        }
    }

    /// <summary>かぎ括弧の開閉チェック（警告のみ）</summary>
    private static void InspectBrackets(List<string> lines)
    {
        for (int lineNum = 0; lineNum < lines.Count; lineNum++)
        {
            int depth = 0;
            foreach (char ch in lines[lineNum])
            {
                if (OpenBrackets.Contains(ch)) depth++;
                else if (CloseBrackets.Contains(ch)) depth--;
                if (depth < 0)
                {
                    LogAppender.Println($"警告: かぎ括弧の閉じが多すぎます（行 {lineNum + 1}）");
                    break;
                }
            }
            if (depth > 0)
                LogAppender.Println($"警告: かぎ括弧が閉じていません（行 {lineNum + 1}）");
        }
    }

    /// <summary>replace.txt によるテキスト置換</summary>
    private void ApplyReplacePatterns(List<string> lines)
    {
        LogAppender.Println($"replace.txt: {_settings.TextReplacePatterns.Count}件の置換ルールを適用");
        for (int i = 0; i < lines.Count; i++)
        {
            string line = lines[i];
            foreach (var pair in _settings.TextReplacePatterns)
                line = line.Replace(pair[0], pair[1]);
            lines[i] = line;
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // B2: 空行圧縮 (narou.rb: enable_pack_blank_line)
    // ═══════════════════════════════════════════════════════════════

    /// <summary>連続空行を圧縮する</summary>
    private static void PackBlankLine(List<string> lines)
    {
        // narou.rb: "\n\n" → "\n" then "(^\n){3}" → "\n\n"
        // 結果: 単一空行は除去、2連続空行は1つに
        var result = new List<string>(lines.Count);
        int i = 0;
        while (i < lines.Count)
        {
            if (lines[i].Length == 0)
            {
                int count = 0;
                while (i < lines.Count && lines[i].Length == 0)
                {
                    count++;
                    i++;
                }
                // 単一空行: 除去、2連続以上: 1行に圧縮
                if (count > 1)
                    result.Add("");
            }
            else
            {
                result.Add(lines[i]);
                i++;
            }
        }
        lines.Clear();
        lines.AddRange(result);
    }

    // ═══════════════════════════════════════════════════════════════
    // B3: 漢数字変換 (narou.rb: enable_convert_num_to_kanji)
    // ═══════════════════════════════════════════════════════════════

    /// <summary>URL行・変換日時行など変換不要な行を判定</summary>
    private static bool ShouldSkipConversion(string line) =>
        line.Contains("://") || line.StartsWith("変換日時");

    /// <summary>注記 ［＃...］ の範囲を除外して変換関数を適用</summary>
    private static readonly Regex AnnotationRegex = new(@"［＃[^］]*］", RegexOptions.Compiled);

    private static string TransformOutsideAnnotations(string line, Func<string, string> transform)
    {
        var matches = AnnotationRegex.Matches(line);
        if (matches.Count == 0) return transform(line);

        var sb = new System.Text.StringBuilder(line.Length);
        int pos = 0;
        foreach (Match m in matches)
        {
            if (m.Index > pos)
                sb.Append(transform(line[pos..m.Index]));
            sb.Append(m.Value);
            pos = m.Index + m.Length;
        }
        if (pos < line.Length)
            sb.Append(transform(line[pos..]));
        return sb.ToString();
    }

    private const string KanjiNum = "〇一二三四五六七八九";

    /// <summary>半角・全角数字を漢数字に変換</summary>
    private static void ConvertNumToKanji(List<string> lines)
    {
        for (int i = 0; i < lines.Count; i++)
        {
            string line = lines[i];
            if (line.Length == 0) continue;
            if (ShouldSkipConversion(line)) continue; // URL行・変換日時行スキップ

            // サブタイトル行（中見出し・大見出し）は縦中横または全角数字変換のみ
            // 2桁以上: ［＃縦中横］...［＃縦中横終わり］で囲む（縦書き時に横組み表示）
            // 1桁: 全角数字に変換
            if (line.Contains("［＃中見出し］") || line.Contains("［＃大見出し］"))
            {
                lines[i] = TransformOutsideAnnotations(line, s =>
                    SubtitleDigitRegex.Replace(s, m =>
                    {
                        string digits = NormalizeDigits(m.Value); // 全角数字も半角に統一
                        if (digits.Length >= 2)
                            return $"［＃縦中横］{digits}［＃縦中横終わり］";
                        return HankakuToZenkaku(digits);
                    }));
                continue;
            }

            // 半角数字 → 全角数字 → 漢数字 (注記内スキップ)
            lines[i] = TransformOutsideAnnotations(line, s =>
                NumToKanjiRegex.Replace(s, m =>
                {
                    string digits = m.Value;
                    // カンマ含む数字はそのまま全角化
                    if (digits.Contains(',') || digits.Contains('，'))
                        return HankakuToZenkaku(digits.Replace('，', ','));
                    return DigitsToKanji(NormalizeDigits(digits));
                }));
        }
    }

    private static readonly Regex NumToKanjiRegex = new(@"[\d０-９,，]+", RegexOptions.Compiled);
    private static readonly Regex SubtitleDigitRegex = new(@"[\d０-９]+", RegexOptions.Compiled);

    /// <summary>全角数字を半角に正規化</summary>
    private static string NormalizeDigits(string s)
    {
        var sb = new System.Text.StringBuilder(s.Length);
        foreach (char c in s)
        {
            if (c >= '０' && c <= '９') sb.Append((char)(c - '０' + '0'));
            else sb.Append(c);
        }
        return sb.ToString();
    }

    /// <summary>半角数字列を漢数字に変換</summary>
    private static string DigitsToKanji(string digits)
    {
        var sb = new System.Text.StringBuilder(digits.Length);
        foreach (char c in digits)
        {
            if (c >= '0' && c <= '9') sb.Append(KanjiNum[c - '0']);
            else sb.Append(c);
        }
        return sb.ToString();
    }

    /// <summary>半角数字を全角数字に変換</summary>
    private static string HankakuToZenkaku(string s)
    {
        var sb = new System.Text.StringBuilder(s.Length);
        foreach (char c in s)
        {
            if (c >= '0' && c <= '9') sb.Append((char)(c - '0' + '０'));
            else if (c == ',') sb.Append('，');
            else sb.Append(c);
        }
        return sb.ToString();
    }

    /// <summary>漢数字変換なし版: 半角数字を全角に (2桁は縦中横)</summary>
    private static void HankakuNumToZenkaku(List<string> lines)
    {
        for (int i = 0; i < lines.Count; i++)
        {
            string line = lines[i];
            if (line.Length == 0) continue;
            if (ShouldSkipConversion(line)) continue;

            lines[i] = TransformOutsideAnnotations(line, s =>
                Regex.Replace(s, @"\d+", m =>
                {
                    if (m.Value.Length == 2)
                        return $"［＃縦中横］{m.Value}［＃縦中横終わり］";
                    return HankakuToZenkaku(m.Value);
                }));
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // B4: 英字全角化 (narou.rb: alphabet_to_zenkaku)
    // ═══════════════════════════════════════════════════════════════

    private static readonly Regex EnglishWordRegex = new(@"[\w.,!?'"" &:;-]+", RegexOptions.Compiled);
    private const int EnglishSentenceMinLength = 8;

    /// <summary>英字を全角に変換 (長い英文は保護)</summary>
    private static void AlphabetToZenkaku(List<string> lines, bool force)
    {
        for (int i = 0; i < lines.Count; i++)
        {
            string line = lines[i];
            if (line.Length == 0) continue;
            if (ShouldSkipConversion(line)) continue;

            lines[i] = TransformOutsideAnnotations(line, s =>
                EnglishWordRegex.Replace(s, m =>
                {
                    string match = m.Value;
                    if (!ContainsAlpha(match)) return match;
                    if (force)
                        return AlphaToZenkaku(match);
                    // 英文（2語以上）または8文字以上 → 半角のまま保護
                    if (match.Split(' ').Length >= 2 || match.Length >= EnglishSentenceMinLength)
                        return match;
                    return AlphaToZenkaku(match);
                }));
        }
    }

    private static bool ContainsAlpha(string s)
    {
        foreach (char c in s)
            if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z')) return true;
        return false;
    }

    private static string AlphaToZenkaku(string s)
    {
        var sb = new System.Text.StringBuilder(s.Length);
        foreach (char c in s)
        {
            if (c >= 'a' && c <= 'z') sb.Append((char)(c - 'a' + 'ａ'));
            else if (c >= 'A' && c <= 'Z') sb.Append((char)(c - 'A' + 'Ａ'));
            else sb.Append(c);
        }
        return sb.ToString();
    }

    // ═══════════════════════════════════════════════════════════════
    // B5: 二分アキ (narou.rb: half_indent_bracket)
    // ═══════════════════════════════════════════════════════════════

    private static readonly Regex HalfIndentTarget = new(
        @"^[ 　\t]*((?:[〔「『(（【〈《≪〝]))", RegexOptions.Compiled);

    /// <summary>行頭かぎ括弧に二分アキ注記を挿入</summary>
    private static void HalfIndentBracket(List<string> lines)
    {
        for (int i = 0; i < lines.Count; i++)
        {
            lines[i] = HalfIndentTarget.Replace(lines[i], "［＃二分アキ］$1");
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // B6: 読了表示 (narou.rb: enable_display_end_of_book)
    // ═══════════════════════════════════════════════════════════════

    /// <summary>本の末尾に読了マーカーを追加</summary>
    private static void AppendEndOfBook(List<string> lines)
    {
        lines.Add("");
        lines.Add("［＃ここから地付き］［＃小書き］（本を読み終わりました）［＃小書き終わり］［＃ここで地付き終わり］");
    }
}
