using System.Text.RegularExpressions;

namespace AozoraEpub3.Core.Converter;

/// <summary>変換設定（変換開始前にセット、変換中は不変）</summary>
internal sealed class ConverterSettings
{
    // --- Auto Yoko (5) ---
    public bool AutoYoko { get; set; } = true;
    public bool AutoYokoNum1 { get; set; } = true;
    public bool AutoYokoNum3 { get; set; } = true;
    public bool AutoYokoEQ1 { get; set; } = true;
    public bool AutoYokoEQ3 { get; set; } = true;

    // --- Image (3) ---
    public bool NoIllust { get; set; }
    public bool ImageFloatPage { get; set; }
    public bool ImageFloatBlock { get; set; }

    // --- Character (6) ---
    public int DakutenType { get; set; } = 1;
    public bool PrintIvsBMP { get; set; }
    public bool PrintIvsSSP { get; set; } = true;
    public bool ChukiRuby { get; set; }
    public bool ChukiKogaki { get; set; }
    public int SpaceHyphenation { get; set; }

    // --- Comment (2) ---
    public bool CommentPrint { get; set; }
    public bool CommentConvert { get; set; }

    // --- Formatting (6) ---
    public bool Vertical { get; set; } = true;
    public bool WithMarkId { get; set; }
    public bool ForceIndent { get; set; }
    public int RemoveEmptyLine { get; set; }
    public int MaxEmptyLine { get; set; } = int.MaxValue;
    public bool SeparateColophon { get; set; } = true;

    // --- Page Break (6) ---
    public bool ForcePageBreak { get; set; }
    public int ForcePageBreakSize { get; set; }
    public int ForcePageBreakEmptyLine { get; set; }
    public int ForcePageBreakEmptySize { get; set; }
    public int ForcePageBreakChapterLevel { get; set; }
    public int ForcePageBreakChapterSize { get; set; }

    // --- Chapter (11) ---
    public bool ChapterSection { get; set; } = true;
    public bool AutoChapterName { get; set; }
    public bool AutoChapterNumOnly { get; set; }
    public bool AutoChapterNumTitle { get; set; }
    public bool AutoChapterNumParen { get; set; }
    public bool AutoChapterNumParenTitle { get; set; }
    public bool ExcludeSequentialChapter { get; set; } = true;
    public bool UseNextLineChapterName { get; set; } = true;
    public int MaxChapterNameLength { get; set; } = 64;
    public Regex? ChapterPattern { get; set; }
    public Dictionary<string, int>? ChapterChukiMap { get; set; }
}
