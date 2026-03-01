using System.Text.RegularExpressions;
using AozoraEpub3.Core.Converter;

namespace AozoraEpub3.Core.Info;

/// <summary>タイトル著作者等のメタ情報を格納</summary>
public class BookInfo
{
    /// <summary>タイトル記載種別</summary>
    public enum TitleType
    {
        TITLE_AUTHOR, AUTHOR_TITLE, SUBTITLE_AUTHOR, TITLE_ONLY, TITLE_AUTHOR_ONLY, NONE
    }

    public const int TITLE_NONE = -1;
    public const int TITLE_NORMAL = 0;
    public const int TITLE_MIDDLE = 1;
    public const int TITLE_HORIZONTAL = 2;

    public int TitlePageType { get; set; } = 0;

    string[]? _metaLines;
    int _metaLineStart;
    public int MetaLineStart => _metaLineStart;

    public int TotalLineNum { get; set; } = -1;

    public string? Title { get; set; }
    public int TitleLine { get; set; } = -1;
    public string? TitleAs { get; set; }

    public string? SubTitle { get; set; }
    public int SubTitleLine { get; set; } = -1;

    public int OrgTitleLine { get; set; } = -1;
    public int SubOrgTitleLine { get; set; } = -1;

    public string? Creator { get; set; }
    public int CreatorLine { get; set; } = -1;
    public int SubCreatorLine { get; set; } = -1;
    public string? CreatorAs { get; set; }

    public int SeriesLine { get; set; } = -1;
    public int PublisherLine { get; set; } = -1;
    public string? Publisher { get; set; }

    public int TitleEndLine { get; set; } = -1;
    int _firstCommentLineNum = -1;

    public DateTime Published { get; set; }
    public DateTime Modified { get; set; } = DateTime.Now;

    public bool Vertical { get; set; } = true;
    public bool Rtl { get; set; } = false;

    public string SrcFilePath { get; set; }
    public string? TextEntryName { get; set; }

    public int FirstImageLineNum { get; set; } = -1;
    public int FirstImageIdx { get; set; } = -1;

    /// <summary>表紙ファイル名 フルパスかURL ""なら先頭の挿絵 nullなら表紙無し</summary>
    public string? CoverFileName { get; set; }
    /// <summary>表紙に使う挿絵の本文内Index -1なら本文内の挿絵は使わない</summary>
    public int CoverImageIndex { get; set; } = -1;
    public string? CoverExt { get; set; }

    public bool InsertCoverPage { get; set; } = false;
    public bool InsertCoverPageToc { get; set; } = false;
    public bool InsertTitlePage { get; set; } = false;
    public bool InsertTocPage { get; set; } = false;
    public bool TocVertical { get; set; } = false;
    public bool InsertTitleToc { get; set; } = true;
    public bool ImageOnly { get; set; } = false;

    Dictionary<int, string>? _mapImageSectionLine;
    HashSet<int>? _mapPageBreakLine;
    HashSet<int>? _mapNoPageBreakLine;
    HashSet<int>? _mapIgnoreLine;
    Dictionary<int, ChapterLineInfo>? _mapChapterLine;

    public BookInfo(string srcFilePath)
    {
        SrcFilePath = srcFilePath;
        Modified = DateTime.Now;
    }

    public void Clear()
    {
        _mapImageSectionLine?.Clear();
        _mapPageBreakLine?.Clear();
        _mapNoPageBreakLine?.Clear();
        _mapIgnoreLine?.Clear();
    }

    public void AddImageSectionLine(int lineNum, string imageFileName)
    {
        _mapImageSectionLine ??= new Dictionary<int, string>();
        _mapImageSectionLine[lineNum] = imageFileName;
    }
    public bool IsImageSectionLine(int lineNum) => _mapImageSectionLine?.ContainsKey(lineNum) ?? false;
    public string? GetImageSectionFileName(int lineNum) => _mapImageSectionLine?.GetValueOrDefault(lineNum);

    public void AddPageBreakLine(int lineNum)
    {
        _mapPageBreakLine ??= new HashSet<int>();
        _mapPageBreakLine.Add(lineNum);
    }
    public bool IsPageBreakLine(int lineNum) => _mapPageBreakLine?.Contains(lineNum) ?? false;

    public void AddNoPageBreakLine(int lineNum)
    {
        _mapNoPageBreakLine ??= new HashSet<int>();
        _mapNoPageBreakLine.Add(lineNum);
    }
    public bool IsNoPageBreakLine(int lineNum) => _mapNoPageBreakLine?.Contains(lineNum) ?? false;

    public void AddIgnoreLine(int lineNum)
    {
        _mapIgnoreLine ??= new HashSet<int>();
        _mapIgnoreLine.Add(lineNum);
    }
    public bool IsIgnoreLine(int lineNum) => _mapIgnoreLine?.Contains(lineNum) ?? false;

    public void AddChapterLineInfo(ChapterLineInfo info)
    {
        _mapChapterLine ??= new Dictionary<int, ChapterLineInfo>();
        _mapChapterLine[info.LineNum] = info;
    }
    public void RemoveChapterLineInfo(int lineNum) => _mapChapterLine?.Remove(lineNum);
    public ChapterLineInfo? GetChapterLineInfo(int lineNum) => _mapChapterLine?.GetValueOrDefault(lineNum);
    public int GetChapterLevel(int lineNum)
    {
        if (_mapChapterLine == null) return 0;
        var info = _mapChapterLine.GetValueOrDefault(lineNum);
        return info?.Level ?? 0;
    }

    public List<ChapterLineInfo> GetChapterLineInfoList()
    {
        var list = new List<ChapterLineInfo>();
        if (_mapChapterLine == null) return list;
        var lines = _mapChapterLine.Keys.OrderBy(x => x).ToList();
        foreach (var ln in lines)
            list.Add(_mapChapterLine[ln]);
        return list;
    }

    public void ExcludeTocChapter()
    {
        if (_mapChapterLine == null) return;
        var excludeLine = new HashSet<int>();
        foreach (var lineNum in _mapChapterLine.Keys)
        {
            if (IsPattern(lineNum))
            {
                bool prevIsPattern = IsPattern(lineNum - 1) ||
                    (_mapChapterLine.GetValueOrDefault(lineNum)?.EmptyNext == true && IsPattern(lineNum - 2));
                bool nextIsPattern = IsPattern(lineNum + 1) || IsPattern(lineNum + 2);
                if (prevIsPattern && nextIsPattern) excludeLine.Add(lineNum);
            }
        }
        var excludeLine2 = new HashSet<int>();
        foreach (var lineNum in _mapChapterLine.Keys)
        {
            if (!excludeLine.Contains(lineNum) && IsPattern(lineNum))
            {
                if (excludeLine.Contains(lineNum - 1)) excludeLine2.Add(lineNum);
                else if (_mapChapterLine.GetValueOrDefault(lineNum)?.EmptyNext == true && excludeLine.Contains(lineNum - 2)) excludeLine2.Add(lineNum);
                else if (excludeLine.Contains(lineNum + 1)) excludeLine2.Add(lineNum);
                else if (excludeLine.Contains(lineNum + 2)) excludeLine2.Add(lineNum);
            }
        }
        foreach (var ln in excludeLine) _mapChapterLine.Remove(ln);
        foreach (var ln in excludeLine2) _mapChapterLine.Remove(ln);
    }

    private bool IsPattern(int num)
    {
        var info = GetChapterLineInfo(num);
        return info?.IsPattern() ?? false;
    }

    public string? GetTitleText() => TitleLine == -1 || _metaLines == null ? null : SafeGet(_metaLines, TitleLine - _metaLineStart);
    public string? GetSubTitleText() => SubTitleLine == -1 || _metaLines == null ? null : SafeGet(_metaLines, SubTitleLine - _metaLineStart);
    public string? GetOrgTitleText() => OrgTitleLine == -1 || _metaLines == null ? null : SafeGet(_metaLines, OrgTitleLine - _metaLineStart);
    public string? GetSubOrgTitleText() => SubOrgTitleLine == -1 || _metaLines == null ? null : SafeGet(_metaLines, SubOrgTitleLine - _metaLineStart);
    public string? GetCreatorText() => CreatorLine == -1 || _metaLines == null ? null : SafeGet(_metaLines, CreatorLine - _metaLineStart);
    public string? GetSubCreatorText() => SubCreatorLine == -1 || _metaLines == null ? null : SafeGet(_metaLines, SubCreatorLine - _metaLineStart);
    public string? GetSeriesText() => SeriesLine == -1 || _metaLines == null ? null : SafeGet(_metaLines, SeriesLine - _metaLineStart);
    public string? GetPublisherText() => PublisherLine == -1 || _metaLines == null ? null : SafeGet(_metaLines, PublisherLine - _metaLineStart);

    static string? SafeGet(string[] arr, int idx)
    {
        try { return arr[idx]; } catch { return null; }
    }

    /// <summary>先頭行から表題と著者を取得</summary>
    public void SetMetaInfo(TitleType titleType, bool pubFirst, string[] metaLines, int metaLineStart, int firstCommentLineNum)
    {
        _firstCommentLineNum = firstCommentLineNum;

        TitleLine = -1; OrgTitleLine = -1; SubTitleLine = -1; SubOrgTitleLine = -1;
        CreatorLine = -1; SubCreatorLine = -1; PublisherLine = -1;
        Title = ""; TitleAs = null; Creator = ""; CreatorAs = null; Publisher = null;

        if (titleType == TitleType.NONE) return;

        _metaLines = metaLines;
        _metaLineStart = metaLineStart;

        int linesLength = 0;
        for (int i = 0; i < metaLines.Length; i++)
        {
            if (metaLines[i] == null || metaLines[i].Length == 0) { linesLength = i; break; }
            linesLength = i + 1;
        }

        int arrIndex = 0;
        if (pubFirst && linesLength >= 2)
        {
            PublisherLine = metaLineStart;
            Publisher = metaLines[0];
            metaLineStart++;
            linesLength--;
            arrIndex++;
        }

        if (linesLength > 0 && titleType == TitleType.TITLE_ONLY)
        {
            TitleLine = metaLineStart;
            Title = metaLines[0 + arrIndex];
            TitleEndLine = metaLineStart;
        }
        else if (linesLength > 0 && titleType == TitleType.TITLE_AUTHOR_ONLY)
        {
            TitleLine = metaLineStart;
            Title = metaLines[0 + arrIndex];
            Creator = SafeGet(metaLines, 1 + arrIndex) ?? "";
            TitleEndLine = metaLineStart + 1;
        }
        else
        {
            bool titleFirst = titleType == TitleType.TITLE_AUTHOR || titleType == TitleType.SUBTITLE_AUTHOR ||
                              titleType == TitleType.TITLE_ONLY || titleType == TitleType.TITLE_AUTHOR_ONLY;
            bool hasTitle = titleType != TitleType.NONE;
            bool hasAuthor = titleType != TitleType.TITLE_ONLY && titleType != TitleType.NONE;

            switch (Math.Min(6, linesLength))
            {
                case 6:
                    if (titleFirst)
                    {
                        TitleLine = metaLineStart; OrgTitleLine = metaLineStart + 1;
                        SubTitleLine = metaLineStart + 2; SubOrgTitleLine = metaLineStart + 3;
                        Title = (metaLines[0 + arrIndex] ?? "") + " " + (metaLines[2 + arrIndex] ?? "");
                        TitleEndLine = metaLineStart + 3;
                        if (hasAuthor) { CreatorLine = metaLineStart + 4; SubCreatorLine = metaLineStart + 5; Creator = metaLines[4 + arrIndex] ?? ""; TitleEndLine = metaLineStart + 5; }
                    }
                    else
                    {
                        CreatorLine = metaLineStart; SubCreatorLine = metaLineStart + 1; Creator = metaLines[0 + arrIndex] ?? ""; TitleEndLine = metaLineStart + 1;
                        if (hasTitle) { TitleLine = metaLineStart + 2; OrgTitleLine = metaLineStart + 3; SubTitleLine = metaLineStart + 4; SubOrgTitleLine = metaLineStart + 5; Title = (metaLines[2 + arrIndex] ?? "") + " " + (metaLines[4 + arrIndex] ?? ""); TitleEndLine = metaLineStart + 5; }
                    }
                    break;
                case 5:
                    if (titleFirst)
                    {
                        TitleLine = metaLineStart; OrgTitleLine = metaLineStart + 1; SubTitleLine = metaLineStart + 2;
                        Title = (metaLines[0 + arrIndex] ?? "") + " " + (metaLines[2 + arrIndex] ?? ""); TitleEndLine = metaLineStart + 2;
                        if (hasAuthor) { CreatorLine = metaLineStart + 3; SubCreatorLine = metaLineStart + 4; Creator = metaLines[3 + arrIndex] ?? ""; TitleEndLine = metaLineStart + 4; }
                    }
                    else
                    {
                        CreatorLine = metaLineStart; Creator = metaLines[0 + arrIndex] ?? ""; TitleEndLine = metaLineStart;
                        if (hasTitle) { TitleLine = metaLineStart + 1; OrgTitleLine = metaLineStart + 2; SubTitleLine = metaLineStart + 3; SubOrgTitleLine = metaLineStart + 4; Title = (metaLines[1 + arrIndex] ?? "") + " " + (metaLines[3 + arrIndex] ?? ""); }
                        TitleEndLine = metaLineStart + 4;
                    }
                    break;
                case 4:
                    if (titleFirst)
                    {
                        TitleLine = metaLineStart; SubTitleLine = metaLineStart + 1;
                        Title = (metaLines[0 + arrIndex] ?? "") + " " + (metaLines[1 + arrIndex] ?? ""); TitleEndLine = metaLineStart + 1;
                        if (hasAuthor) { CreatorLine = metaLineStart + 2; SubCreatorLine = metaLineStart + 3; Creator = metaLines[2 + arrIndex] ?? ""; TitleEndLine = metaLineStart + 3; }
                    }
                    else
                    {
                        CreatorLine = metaLineStart; SubCreatorLine = metaLineStart + 1; Creator = metaLines[0 + arrIndex] ?? ""; TitleEndLine = metaLineStart + 1;
                        if (hasTitle) { TitleLine = metaLineStart + 2; SubTitleLine = metaLineStart + 3; Title = (metaLines[2 + arrIndex] ?? "") + " " + (metaLines[3 + arrIndex] ?? ""); TitleEndLine = metaLineStart + 3; }
                    }
                    break;
                case 3:
                    if (titleFirst)
                    {
                        TitleLine = metaLineStart; SubTitleLine = metaLineStart + 1;
                        Title = (metaLines[0 + arrIndex] ?? "") + " " + (metaLines[1 + arrIndex] ?? ""); TitleEndLine = metaLineStart + 1;
                        if (hasAuthor)
                        {
                            var m2 = metaLines[2 + arrIndex] ?? "";
                            if (titleType != TitleType.SUBTITLE_AUTHOR && !(metaLines[1 + arrIndex] ?? "").StartsWith("―") &&
                                (m2.EndsWith("訳") || m2.EndsWith("編纂") || m2.EndsWith("校訂")))
                            {
                                TitleLine = metaLineStart; Title = metaLines[0 + arrIndex] ?? "";
                                SubTitleLine = -1;
                                CreatorLine = metaLineStart + 1; Creator = metaLines[1 + arrIndex] ?? "";
                                SubCreatorLine = metaLineStart + 2;
                            }
                            else { CreatorLine = metaLineStart + 2; Creator = metaLines[2 + arrIndex] ?? ""; }
                            TitleEndLine = metaLineStart + 2;
                        }
                    }
                    else
                    {
                        CreatorLine = metaLineStart; Creator = metaLines[0 + arrIndex] ?? ""; TitleEndLine = metaLineStart;
                        if (hasTitle) { TitleLine = metaLineStart + 1; SubTitleLine = metaLineStart + 2; Title = (metaLines[1 + arrIndex] ?? "") + " " + (metaLines[2 + arrIndex] ?? ""); TitleEndLine = metaLineStart + 2; }
                    }
                    break;
                case 2:
                    if (titleFirst)
                    {
                        TitleLine = metaLineStart; Title = metaLines[0 + arrIndex] ?? "";
                        if (hasAuthor)
                        {
                            if (firstCommentLineNum > 0 && firstCommentLineNum <= 6 && SafeGet(metaLines, 3 + arrIndex)?.Length > 0 && (SafeGet(metaLines, 4 + arrIndex)?.Length ?? 0) == 0)
                            {
                                SubTitleLine = metaLineStart + 1;
                                Title = (metaLines[0 + arrIndex] ?? "") + " " + (metaLines[1 + arrIndex] ?? "");
                                CreatorLine = metaLineStart + 3; Creator = metaLines[2 + arrIndex] ?? ""; TitleEndLine = metaLineStart + 3;
                            }
                            else { CreatorLine = metaLineStart + 1; Creator = metaLines[1 + arrIndex] ?? ""; TitleEndLine = metaLineStart + 1; }
                        }
                    }
                    else
                    {
                        CreatorLine = metaLineStart; Creator = metaLines[0 + arrIndex] ?? "";
                        if (hasTitle) { TitleLine = metaLineStart + 1; Title = metaLines[1 + arrIndex] ?? ""; }
                        TitleEndLine = metaLineStart + 1;
                    }
                    break;
                case 1:
                    if (titleFirst)
                    {
                        TitleLine = metaLineStart; Title = metaLines[0 + arrIndex] ?? ""; TitleEndLine = metaLineStart;
                        if (hasAuthor && (SafeGet(metaLines, 2 + arrIndex)?.Length ?? 0) > 0 && (SafeGet(metaLines, 3 + arrIndex)?.Length ?? 0) == 0)
                        { CreatorLine = metaLineStart + 2; Creator = metaLines[2 + arrIndex] ?? ""; TitleEndLine = metaLineStart + 2; }
                    }
                    else
                    {
                        CreatorLine = metaLineStart; Creator = metaLines[0 + arrIndex] ?? ""; TitleEndLine = metaLineStart;
                        if (hasTitle && (SafeGet(metaLines, 2 + arrIndex)?.Length ?? 0) > 0 && (SafeGet(metaLines, 3 + arrIndex)?.Length ?? 0) == 0)
                        { TitleLine = metaLineStart + 2; Title = metaLines[2 + arrIndex] ?? ""; TitleEndLine = metaLineStart + 2; }
                    }
                    break;
            }
        }

        if (Creator != null && (Creator.StartsWith("―") || Creator.StartsWith("【"))) Creator = null;
        if (Title != null) Title = CharUtils.GetChapterName(CharUtils.RemoveRuby(Title), 0, false);
        if (Creator != null) Creator = CharUtils.GetChapterName(CharUtils.RemoveRuby(Creator), 0);
    }

    public void ReloadMetadata(TitleType titleType, bool pubFirst)
    {
        if (_metaLines != null)
            SetMetaInfo(titleType, pubFirst, _metaLines, _metaLineStart, _firstCommentLineNum);
    }

    private static readonly Regex _fileNamePattern = new(@"\[(.+?)\][ \u3000]*(.+?)[\(\（\.]", RegexOptions.Compiled);
    private static readonly Regex _fileNamePattern2 = new(@"^([^\(（]*?)[ \u3000]*(\(|（)", RegexOptions.Compiled);

    public static string?[] GetFileTitleCreator(string fileName)
    {
        var titleCreator = new string?[2];
        string noExtName = Regex.Replace(fileName, @"\.([A-Z]|[a-z]|[0-9])+$", "");
        noExtName = Regex.Replace(noExtName, @"\.([A-Z]|[a-z]|[0-9])+$", "");
        noExtName = noExtName.Replace("（", "(").Replace("）", ")");
        noExtName = Regex.Replace(noExtName, @"\(青空[^)]*\)", "");
        noExtName = Regex.Replace(noExtName, @"\([^)]*(校正|軽量|表紙|挿絵|補正|修正|ルビ|Rev|rev)[^)]*\)", "");

        // [著者名] タイトル.txt 形式
        var m = Regex.Match(noExtName, @"[\[|［]([^\]|\uFF3D]*?)[\]|］][ |　]*(.*?)[ |　]*$");
        if (m.Success)
        {
            titleCreator[0] = m.Groups[2].Value;
            titleCreator[1] = m.Groups[1].Value;
        }
        else
        {
            m = Regex.Match(noExtName, @"^([^\(（]*?)[ \u3000]*(\(|（)");
            if (m.Success) titleCreator[0] = m.Groups[1].Value;
            else titleCreator[0] = noExtName;
        }
        if (titleCreator[0] != null) { titleCreator[0] = titleCreator[0].Trim(); if (titleCreator[0].Length == 0) titleCreator[0] = null; }
        if (titleCreator[1] != null) { titleCreator[1] = titleCreator[1].Trim(); if (titleCreator[1].Length == 0) titleCreator[1] = null; }
        return titleCreator;
    }
}
