using AozoraEpub3.Core.Info;

namespace AozoraEpub3.Core.Converter;

/// <summary>変換実行中の可変状態（各変換開始時にリセット）</summary>
internal sealed class ConverterState
{
    // --- 行追跡 ---
    public int LineNum { get; set; } = -1;

    // --- セクション追跡 ---
    public int PageByteSize { get; set; }
    public int SectionCharLength { get; set; }
    public int LineIdNum { get; set; } = 1;
    public int TagLevel { get; set; }

    // --- 行間持続状態 ---
    public int InJisage { get; set; } = -1;
    public bool InYoko { get; set; }
    public bool NextLineIsCaption { get; set; }
    public bool InImageTag { get; set; }
    public BookInfo? BookInfo { get; set; }

    // --- 改ページ状態 ---
    public PageBreakType? PageBreakTrigger { get; set; }
    public bool SkipMiddleEmpty { get; set; }
    public int PrintEmptyLines { get; set; }
    public int LastChapterLine { get; set; } = -1;

    // --- 行単位の一時状態 ---
    public readonly HashSet<int> NoTcyStart = new();
    public readonly HashSet<int> NoTcyEnd = new();

    // --- キャンセルフラグ ---
    public bool Canceled { get; set; }

    public void Reset()
    {
        LineNum = -1;
        PageByteSize = 0;
        SectionCharLength = 0;
        LineIdNum = 1;
        TagLevel = 0;
        InJisage = -1;
        InYoko = false;
        NextLineIsCaption = false;
        InImageTag = false;
        BookInfo = null;
        PageBreakTrigger = null;
        SkipMiddleEmpty = false;
        PrintEmptyLines = 0;
        LastChapterLine = -1;
        Canceled = false;
    }

    public void ClearPerLine()
    {
        NoTcyStart.Clear();
        NoTcyEnd.Clear();
    }
}
