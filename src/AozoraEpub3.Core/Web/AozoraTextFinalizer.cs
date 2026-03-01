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

        if (_settings.EnableAuthorComments)
            DetectAndMarkAuthorComments(lines);

        if (_settings.EnableAutoIndent)
            ApplyAutoIndent(lines);

        if (_settings.EnableEnchantMidashi)
            EnchantMidashi(lines);

        if (_settings.EnableAutoJoinInBrackets)
            AutoJoinInBrackets(lines);

        if (_settings.EnableAutoJoinLine)
            AutoJoinLine(lines);

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

    /// <summary>自動行頭字下げ (narou.rb互換)</summary>
    private void ApplyAutoIndent(List<string> lines)
    {
        for (int i = 0; i < lines.Count; i++)
        {
            string line = lines[i];
            if (line.Length == 0) continue;
            if (line.StartsWith('　') || line.StartsWith(' ')) continue;
            if (line.StartsWith("［＃")) continue;
            if (OpenBrackets.Contains(line[0])) continue;
            if (line.Contains("［＃中見出し］") || line.Contains("［＃大見出し］") ||
                line.Contains("［＃区切り線］") || line.Contains("［＃挿絵") ||
                line.Contains("［＃改ページ］")) continue;

            bool isParagraphStart = i == 0 || lines[i - 1].Length == 0;
            if (isParagraphStart)
                lines[i] = "　" + line;
        }
    }

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
}
