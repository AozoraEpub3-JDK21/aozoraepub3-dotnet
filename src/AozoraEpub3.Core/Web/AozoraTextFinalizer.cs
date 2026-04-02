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
    private static readonly Regex BorderSymbolLineRegex = new(
        @"^[ 　\t]*[■□◆◇○◎●★☆\*＊※♡♥❤∽♤♠§♯]+[ 　\t]*$",
        RegexOptions.Compiled);
    private static readonly Regex DashSeparatorLineRegex = new(
        @"^[ 　\t]*[─━―ー－]{3,}[ 　\t]*$",
        RegexOptions.Compiled);
    private static readonly Regex NoteLikeLineRegex = new(@"^[ 　]*[☆★※◇◆■□●○◎△▲▽▼─━―ー－][☆★※◇◆■□●○◎△▲▽▼─━―ー－ 　]*$", RegexOptions.Compiled);
    private static readonly Regex DecimalPointRegex = new(@"([0-9０-９〇一二三四五六七八九]+)[\.．]([0-9０-９〇一二三四五六七八九]+)", RegexOptions.Compiled);

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

        EnsureBorderSymbolSpacing(lines);
        EnsureDashSeparatorSpacing(lines);
        NormalizeLeadingAsciiSpace(lines);
        EnsureChapterEndingSpacing(lines);

        // 改ページ直後の見出し化（漢数字変換より前に実行し、見出し行を保護する）
        if (_settings.EnableEnchantMidashi)
            EnchantMidashi(lines);

        if (_settings.EnableConvertNumToKanji)
            ConvertNumToKanji(lines);
        else
            HankakuNumToZenkaku(lines);

        AlphabetToZenkaku(lines, _settings.EnableAlphabetForceZenkaku);

        if (_settings.EnableConvertNumToKanji)
            ExceptionReconvertKanjiToNum(lines);

        if (_settings.EnableConvertSymbolsToZenkaku)
            ConvertSymbolsToZenkaku(lines);

        NormalizeNestedQuoteOpeners(lines);

        if (_settings.EnableHalfIndentBracket || _settings.EnableAutoIndent)
            HalfIndentBracketAndAutoIndent(lines);

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

    /// <summary>区切り記号行の前後に空行を補完し、4字下げする (narou.rb互換)</summary>
    private static void EnsureBorderSymbolSpacing(List<string> lines)
    {
        for (int i = 0; i < lines.Count; i++)
        {
            if (!BorderSymbolLineRegex.IsMatch(lines[i])) continue;

            // narou.rb: jisage(line, 4)
            string trimmed = lines[i].TrimStart(' ', '　', '\t');
            lines[i] = "\u3000\u3000\u3000\u3000" + trimmed;

            if (i > 0 && !IsBlankLine(lines[i - 1]))
            {
                lines.Insert(i, "");
                i++;
            }

            if (i + 1 < lines.Count && !IsBlankLine(lines[i + 1]))
            {
                lines.Insert(i + 1, "");
            }
        }
    }

    private static bool IsBlankLine(string line) => line.Trim(' ', '　', '\t').Length == 0;

    /// <summary>罫線（─等のみの行）前後の空行と行頭全角スペースを補完する</summary>
    private static void EnsureDashSeparatorSpacing(List<string> lines)
    {
        for (int i = 0; i < lines.Count; i++)
        {
            if (!DashSeparatorLineRegex.IsMatch(lines[i])) continue;

            string trimmed = lines[i].TrimStart(' ', '　', '\t');
            lines[i] = "　" + trimmed;

            if (i > 0 && !IsBlankLine(lines[i - 1]))
            {
                lines.Insert(i, "");
                i++;
            }
        }
    }

    /// <summary>行頭半角スペースを全角スペースへ正規化する</summary>
    private static void NormalizeLeadingAsciiSpace(List<string> lines)
    {
        for (int i = 0; i < lines.Count; i++)
        {
            string line = lines[i];
            if (line.Length < 2 || line[0] != ' ') continue;
            if (line.StartsWith("［＃", StringComparison.Ordinal)) continue;

            int idx = 0;
            while (idx < line.Length && line[idx] == ' ') idx++;
            if (idx >= line.Length) continue;

            lines[i] = "　" + line[idx..];
        }
    }

    /// <summary>
    /// 会話内の二重引用符開始を正規化する。
    /// 例: 「”～」 → 「“～」、 「〟～」 → 「〝～」
    /// </summary>
    private static void NormalizeNestedQuoteOpeners(List<string> lines)
    {
        for (int i = 0; i < lines.Count; i++)
        {
            string line = lines[i]
                .Replace("「”", "「“", StringComparison.Ordinal)
                .Replace("「〟", "「〝", StringComparison.Ordinal);

            // 呪文表記（「＝」を含む引用）だけを 〝...〟 に寄せる。
            line = Regex.Replace(line, "[\"”]([^\"”\n]*?＝[^\"”\n]*?)[\"”]", "〝$1〟");
            line = Regex.Replace(line, "〟([^〟\n]*?＝[^〟\n]*?)〟", "〝$1〟");

            lines[i] = line;
        }
    }


    /// <summary>
    /// 章末の作者コメント周辺だけ空行を補完する。
    /// 汎用ルールを広げると差分が増えるため、互換差が出やすい末尾パターンに限定する。
    /// </summary>
    private static void EnsureChapterEndingSpacing(List<string> lines)
    {
        for (int i = 0; i < lines.Count; i++)
        {
            if (!IsChapterEndingCommentLine(lines[i]) && !IsChapterEndingSeparatorLine(lines, i))
                continue;

            if (i <= 0 || !IsBlankLine(lines[i - 1]))
                continue;

            bool hasTwoBlanksBefore = i - 2 >= 0 && IsBlankLine(lines[i - 2]);
            if (!hasTwoBlanksBefore)
            {
                lines.Insert(i, "");
                i++;
            }
        }

    }

    private static bool IsChapterEndingCommentLine(string line)
    {
        string trimmed = line.TrimStart(' ', '　', '\t');
        return trimmed.StartsWith("余談ですが、", StringComparison.Ordinal);
    }

    private static bool IsChapterEndingSeparatorLine(List<string> lines, int index)
    {
        if (index + 1 >= lines.Count) return false;
        if (!DashSeparatorLineRegex.IsMatch(lines[index])) return false;

        string next = lines[index + 1].TrimStart(' ', '　', '\t');
        return next.StartsWith("余談ですが、", StringComparison.Ordinal);
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

    /// <summary>二分アキ挿入 + 自動行頭字下げ (narou.rb / Java実装互換)</summary>
    private void HalfIndentBracketAndAutoIndent(List<string> lines)
    {
        bool doHalfIndent = _settings.EnableHalfIndentBracket;
        bool doAutoIndent = _settings.EnableAutoIndent;

        // narou.rb: inspector.inspect_indent
        // 字下げ不要な行を除いた中で、50%以上が未字下げなら適用
        int targetCount = 0, noIndentCount = 0;
        foreach (string line in lines)
        {
            if (line.Length == 0) continue;
            char ch = line[0];
            if (IgnoreIndentChars.Contains(ch)) continue;
            targetCount++;
            if (ch != ' ' && ch != '　') noIndentCount++;
        }
        bool shouldIndent = false;
        if (doAutoIndent && targetCount > 0)
        {
            double ratio = (double)noIndentCount / targetCount;
            shouldIndent = ratio > 0.5;
        }

        for (int i = 0; i < lines.Count; i++)
        {
            string line = lines[i];
            if (line.Length == 0) continue;

            // 二分アキ（行頭空白を除去し開き括弧前に挿入）
            if (doHalfIndent)
            {
                var m = HalfIndentTarget.Match(line);
                if (m.Success)
                {
                    lines[i] = "［＃二分アキ］" + m.Groups[1].Value + line[m.Length..];
                    continue;
                }
            }

            if (!shouldIndent) continue;

            if (line.StartsWith("――", StringComparison.Ordinal))
            {
                lines[i] = "　" + line;
                continue;
            }

            char ch = line[0];
            if (AutoIndentIgnoreChars.Contains(ch) || ch == ' ' || ch == '　' || ch == '\t' || ch == '［')
                continue;

            // 中黒1つだけの場合は字下げしない (narou.rb: 三点リーダー代替対策)
            if (ch == '・' && (line.Length < 2 || line[1] != '・'))
                continue;

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
        string text = string.Join("\n", lines);

        // narou.rb互換: かぎ括弧内の改行(空行含む)を除去して直結する（スペース挿入なし）
        // また、句点直後の全角スペースを除去（narou.rb互換）
        text = Regex.Replace(text, "「([^「」]*?)」", m =>
        {
            string body = Regex.Replace(m.Groups[1].Value, @"[ \t　]*\n+[ \t　]*", "");
            body = body.Replace("。　", "。");
            return "「" + body + "」";
        }, RegexOptions.Singleline);

        text = Regex.Replace(text, "『([^『』]*?)』", m =>
        {
            string body = Regex.Replace(m.Groups[1].Value, @"[ \t　]*\n+[ \t　]*", "");
            body = body.Replace("。　", "。");
            return "『" + body + "』";
        }, RegexOptions.Singleline);

        // narou.rb互換: 行頭の余分な全角スペース（kakuyomuの段落インデント）を
        // 「/『 の直前から除去する
        var newLines = text.Split('\n');
        for (int i = 0; i < newLines.Length; i++)
        {
            string line = newLines[i];
            while (line.StartsWith("　「", StringComparison.Ordinal) ||
                   line.StartsWith("　『", StringComparison.Ordinal))
                line = line[1..];
            newLines[i] = line;
        }
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
                bool keepSingle = count == 1 &&
                    ((i < lines.Count && NoteLikeLineRegex.IsMatch(lines[i])) ||
                     (result.Count > 0 && NoteLikeLineRegex.IsMatch(result[^1])));

                // narou.rb互換の圧縮: step1=count/2行、step2=3行以上は2行に上限
                // (単一空行は除去; note隣接の単一空行のみ保持)
                int step1 = count == 1 ? (keepSingle ? 1 : 0) : count / 2;
                int blanksToAdd = step1 >= 3 ? 2 : step1;
                for (int k = 0; k < blanksToAdd; k++) result.Add("");
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

            // narou.rb互換: 数字間の小数点を中点へ寄せる
            line = DecimalPointRegex.Replace(line, "$1・$2");

            // サブタイトル行（中見出し・大見出し）は縦中横または全角数字変換のみ
            // 2桁: ［＃縦中横］...［＃縦中横終わり］で囲む（縦書き時に横組み表示）
            // 1桁 / 3桁以上: 全角数字に変換
            if (line.Contains("［＃中見出し］") || line.Contains("［＃大見出し］"))
            {
                lines[i] = TransformOutsideAnnotations(line, s =>
                    SubtitleDigitRegex.Replace(s, m =>
                    {
                        string digits = NormalizeDigits(m.Value); // 全角数字も半角に統一
                        if (digits.Length == 2)
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
    private static readonly Regex ReconvertAfterAlphaRegex = new(@"([Ａ-Ｚａ-ｚ])([〇一二三四五六七八九・～]+)", RegexOptions.Compiled);
    private static readonly Regex ReconvertBeforeUnitRegex = new(@"([〇一二三四五六七八九・～]+)([Ａ-Ｚａ-ｚ％㎜㎝㎞㎎㎏㏄㎡㎥])", RegexOptions.Compiled);

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

    // narou.rb互換: ASCII英数字連続のみを英字語として扱う（日本語混在語は分割して判定）
    private static readonly Regex EnglishWordRegex = new(@"[A-Za-z0-9.,!?'"" &:;-]+", RegexOptions.Compiled);
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
    // B4.5: 記号全角化 (narou.rb: enable_convert_symbols_to_zenkaku)
    // ═══════════════════════════════════════════════════════════════

    private static readonly Regex SingleQuotePairRegex = new("'([^'\\n]+)'", RegexOptions.Compiled);

    private static void ConvertSymbolsToZenkaku(List<string> lines)
    {
        for (int i = 0; i < lines.Count; i++)
        {
            string line = lines[i];
            if (ShouldSkipConversion(line)) continue;

            lines[i] = TransformOutsideAnnotations(line, part =>
            {
                if (part.Length == 0) return part;
                part = SingleQuotePairRegex.Replace(part, "〝$1〟");

                var sb = new System.Text.StringBuilder(part.Length);
                foreach (char ch in part)
                {
                    sb.Append(ch switch
                    {
                        '=' => '＝',
                        '<' => '＜',
                        '>' => '＞',
                        '＜' => '〈',
                        '＞' => '〉',
                        '(' => '（',
                        ')' => '）',
                        '*' => '＊',
                        _ => ch
                    });
                }
                return sb.ToString();
            });
        }
    }

    /// <summary>漢数字化した数値のうち、英字・単位に隣接する箇所を全角アラビア数字へ戻す</summary>
    private static void ExceptionReconvertKanjiToNum(List<string> lines)
    {
        for (int i = 0; i < lines.Count; i++)
        {
            string line = lines[i];
            if (line.Length == 0) continue;
            if (ShouldSkipConversion(line)) continue;

            lines[i] = TransformOutsideAnnotations(line, s =>
            {
                s = ReconvertAfterAlphaRegex.Replace(s, m =>
                    m.Groups[1].Value + KanjiDigitsToZenkakuDigits(m.Groups[2].Value));
                s = ReconvertBeforeUnitRegex.Replace(s, m =>
                    KanjiDigitsToZenkakuDigits(m.Groups[1].Value) + m.Groups[2].Value);
                return s;
            });
        }
    }

    private static string KanjiDigitsToZenkakuDigits(string s)
    {
        var sb = new System.Text.StringBuilder(s.Length);
        foreach (char c in s)
        {
            sb.Append(c switch
            {
                '〇' => '０',
                '一' => '１',
                '二' => '２',
                '三' => '３',
                '四' => '４',
                '五' => '５',
                '六' => '６',
                '七' => '７',
                '八' => '８',
                '九' => '９',
                _ => c
            });
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
