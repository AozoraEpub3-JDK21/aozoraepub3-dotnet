using System.Text;
using System.Text.RegularExpressions;
using AozoraEpub3.Core.Io;

namespace AozoraEpub3.Core.Converter;

/// <summary>行バッファ出力・改ページロジック</summary>
internal sealed class OutputBufferService
{
    private readonly ConverterSettings _settings;
    private readonly ConverterState _state;
    private readonly IEpub3Writer _writer;

    public OutputBufferService(ConverterSettings settings, ConverterState state, IEpub3Writer writer)
    {
        _settings = settings;
        _state = state;
        _writer = writer;
    }

    /// <summary>改ページトリガーをセット</summary>
    internal void SetPageBreakTrigger(PageBreakType? trigger)
    {
        _state.PrintEmptyLines = 0;
        _state.PageBreakTrigger = trigger;
        if (_state.PageBreakTrigger != null && _state.PageBreakTrigger.PageType != PageBreakType.PAGE_NONE)
            _state.SkipMiddleEmpty = true;
    }

    /// <summary>行バッファを出力。改ページフラグがあれば改ページ処理。Java: printLineBuffer</summary>
    internal void PrintLineBuffer(TextWriter? output, StringBuilder buf, int lineNum, bool noBr)
    {
        string line = buf.ToString();
        int length = buf.Length;
        if (CharUtils.IsSpace(line)) { line = ""; length = 0; }

        if (_settings.RemoveEmptyLine > 0 && length > 0 && CharUtils.IsSpace(line)) { line = ""; length = 0; }

        if (length == 0)
        {
            if (!_state.SkipMiddleEmpty && !noBr) _state.PrintEmptyLines++;
            buf.Length = 0;
            return;
        }

        // タグ階層カウント
        int tagStart = 0, tagEnd = 0;
        bool inTag = false;
        for (int i = 0; i < length; i++)
        {
            if (inTag)
            {
                if (line[i] == '/' && i + 1 < length && line[i + 1] == '>') tagEnd++;
                if (line[i] == '>') inTag = false;
            }
            else
            {
                if (line[i] == '<')
                {
                    if (i < length - 1 && line[i + 1] == '/') tagEnd++;
                    else tagStart++;
                    inTag = true;
                }
            }
        }

        if (output != null)
        {
            // 強制改ページ
            if (_settings.ForcePageBreak && _state.PageBreakTrigger == null && _state.TagLevel == 0)
            {
                if (_state.PageByteSize > _settings.ForcePageBreakSize)
                    SetPageBreakTrigger(AozoraEpub3Converter._pageBreakNoChapter);
                else if (_settings.ForcePageBreakEmptyLine > 0 && _state.PrintEmptyLines >= _settings.ForcePageBreakEmptyLine &&
                         _state.PageByteSize > _settings.ForcePageBreakEmptySize)
                    SetPageBreakTrigger(AozoraEpub3Converter._pageBreakNoChapter);
                else if (_settings.ForcePageBreakChapterLevel > 0 && _state.PageByteSize > _settings.ForcePageBreakChapterSize)
                {
                    var cli = _state.BookInfo?.GetChapterLineInfo(lineNum);
                    if (cli != null) SetPageBreakTrigger(AozoraEpub3Converter._pageBreakNoChapter);
                    else if (tagStart - tagEnd > 0 && (_state.BookInfo?.GetChapterLevel(lineNum + 1) ?? 0) > 0)
                        SetPageBreakTrigger(AozoraEpub3Converter._pageBreakNoChapter);
                }
            }

            // 改ページ処理
            if (_state.PageBreakTrigger != null)
            {
                if (_state.PageBreakTrigger.PageType != PageBreakType.PAGE_NONE)
                    _writer.NextSection(output, lineNum, _state.PageBreakTrigger.PageType, PageBreakType.IMAGE_PAGE_NONE, null);
                else
                    _writer.NextSection(output, lineNum, PageBreakType.PAGE_NONE, _state.PageBreakTrigger.ImagePageType, _state.PageBreakTrigger.SrcFileName);

                _state.PageByteSize = 0;
                _state.SectionCharLength = 0;
                if (_state.TagLevel > 0) LogAppender.Error(lineNum, "タグが閉じていません");
                _state.TagLevel = 0;
                _state.LineIdNum = 0;
                _state.PageBreakTrigger = null;
            }

            _state.SkipMiddleEmpty = false;

            // 空行出力
            if (_state.PrintEmptyLines > 0)
            {
                string br = AozoraEpub3Converter._chukiMap["改行"][0];
                int lines = Math.Min(_settings.MaxEmptyLine, _state.PrintEmptyLines - _settings.RemoveEmptyLine);
                if (_state.LastChapterLine >= lineNum - _state.PrintEmptyLines - 2)
                    lines = Math.Max(1, lines);
                for (int i = lines - 1; i >= 0; i--)
                {
                    output.Write("<p>");
                    output.Write(br);
                    output.Write("</p>\n");
                }
                _state.PageByteSize += (br.Length + 8) * lines;
                _state.PrintEmptyLines = 0;
            }

            _state.LineIdNum++;
            var chapterLineInfo = _state.BookInfo?.GetChapterLineInfo(lineNum);
            string? chapterId = null;

            if (noBr)
            {
                if (chapterLineInfo != null)
                {
                    chapterId = "kobo." + _state.LineIdNum + ".1";
                    if (line.StartsWith("<"))
                        line = new Regex(@"(<[\d\w]+)").Replace(line, "$1 id=\"" + chapterId + "\"", 1);
                    else
                    {
                        output.Write("<span id=\"" + chapterId + "\">" + line[0] + "</span>");
                        _state.PageByteSize += chapterId.Length + 20;
                        line = line[1..];
                    }
                }
            }
            else
            {
                if (_settings.WithMarkId || (chapterLineInfo != null && !chapterLineInfo.PageBreakChapter))
                {
                    chapterId = "kobo." + _state.LineIdNum + ".1";
                    output.Write("<p id=\"" + chapterId + "\">");
                    _state.PageByteSize += chapterId.Length + 14;
                }
                else
                {
                    output.Write("<p>");
                    _state.PageByteSize += 7;
                }
            }

            output.Write(line);
            if (_settings.ForcePageBreak) _state.PageByteSize += System.Text.Encoding.UTF8.GetByteCount(line);

            if (!noBr) output.Write("</p>\n");

            // 章追加
            if (chapterLineInfo != null && _state.LastChapterLine != lineNum)
            {
                string? name = chapterLineInfo.ChapterName;
                if (name != null && name.Length > 0)
                {
                    if (chapterLineInfo.PageBreakChapter) _writer.AddChapter(null, name, chapterLineInfo.Level % 10);
                    else _writer.AddChapter(chapterId, name, chapterLineInfo.Level % 10);
                    _state.LastChapterLine = lineNum;
                }
            }

            _state.SectionCharLength += length;
        }

        _state.TagLevel += tagStart - tagEnd;
        buf.Length = 0;
    }
}
