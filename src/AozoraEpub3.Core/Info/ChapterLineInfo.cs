namespace AozoraEpub3.Core.Info;

public class ChapterLineInfo
{
    /// <summary>見出しの行番号</summary>
    public int LineNum { get; set; }

    /// <summary>見出し種別</summary>
    public int Type { get; set; }
    /// <summary>見出しレベル 自動抽出は+10</summary>
    public int Level { get; set; }

    public bool PageBreakChapter { get; set; } = false;

    /// <summary>前の行が空行かどうか</summary>
    public bool EmptyNext { get; set; }

    /// <summary>目次に使う文字列</summary>
    public string? ChapterName { get; set; }

    public const int TYPE_TITLE = 1;
    public const int TYPE_PAGEBREAK = 2;
    public const int TYPE_CHUKI_H = 10;
    public const int TYPE_CHUKI_H1 = 11;
    public const int TYPE_CHUKI_H2 = 12;
    public const int TYPE_CHUKI_H3 = 13;
    public const int TYPE_CHAPTER_NAME = 21;
    public const int TYPE_CHAPTER_NUM = 22;
    public const int TYPE_PATTERN = 30;

    public const int LEVEL_TITLE = 0;
    public const int LEVEL_SECTION = 1;
    public const int LEVEL_H1 = 1;
    public const int LEVEL_H2 = 2;
    public const int LEVEL_H3 = 3;

    public ChapterLineInfo(int lineNum, int type, bool pageBreak, int level, bool emptyLineNext, string? chapterName = null)
    {
        LineNum = lineNum;
        Type = type;
        PageBreakChapter = pageBreak;
        Level = level;
        EmptyNext = emptyLineNext;
        ChapterName = chapterName;
    }

    public override string ToString() => ChapterName ?? "";

    public string GetTypeId() => Type switch
    {
        TYPE_TITLE => "題",
        TYPE_PAGEBREAK => "",
        TYPE_CHUKI_H => "見",
        TYPE_CHUKI_H1 => "大",
        TYPE_CHUKI_H2 => "中",
        TYPE_CHUKI_H3 => "小",
        TYPE_CHAPTER_NAME => "章",
        TYPE_CHAPTER_NUM => "数",
        TYPE_PATTERN => "他",
        _ => ""
    };

    public static int GetChapterType(char typeId) => typeId switch
    {
        '題' => TYPE_TITLE,
        '改' => TYPE_PAGEBREAK,
        '見' => TYPE_CHUKI_H,
        '大' => TYPE_CHUKI_H1,
        '中' => TYPE_CHUKI_H2,
        '小' => TYPE_CHUKI_H3,
        '章' => TYPE_CHAPTER_NAME,
        '数' => TYPE_CHAPTER_NUM,
        '他' => TYPE_PATTERN,
        _ => 0
    };

    public static int GetLevel(int type) => type switch
    {
        TYPE_TITLE => LEVEL_TITLE,
        TYPE_PAGEBREAK => LEVEL_SECTION,
        TYPE_CHUKI_H1 => LEVEL_H1,
        TYPE_CHUKI_H2 => LEVEL_H2,
        TYPE_CHUKI_H3 => LEVEL_H3,
        TYPE_CHAPTER_NUM => LEVEL_H2,
        _ => LEVEL_H1
    };

    /// <summary>章名や数字やパターンでマッチした行ならtrue</summary>
    public bool IsPattern() => Type switch
    {
        TYPE_TITLE => false,
        TYPE_PAGEBREAK => false,
        TYPE_CHUKI_H1 => false,
        TYPE_CHUKI_H2 => false,
        TYPE_CHUKI_H3 => false,
        _ => true
    };

    public void JoinChapterName(string chapterName)
    {
        if (ChapterName == null) ChapterName = chapterName;
        else ChapterName = ChapterName + "\u3000" + chapterName;
    }
}
