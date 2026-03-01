namespace AozoraEpub3.Core.Converter;

/// <summary>改ページ種別</summary>
public class PageBreakType
{
    public const int PAGE_NONE = 0;
    public const int PAGE_MIDDLE = 1;
    public const int PAGE_BOTTOM = 2;

    public const int IMAGE_PAGE_NONE = 0;
    public const int IMAGE_PAGE_W = 1;
    public const int IMAGE_PAGE_H = 2;
    public const int IMAGE_PAGE_NOFIT = 5;
    public const int IMAGE_PAGE_AUTO = 10;
    public const int IMAGE_INLINE_W = 11;
    public const int IMAGE_INLINE_H = 12;
    public const int IMAGE_INLINE_TOP = 20;
    public const int IMAGE_INLINE_BOTTOM = 21;
    public const int IMAGE_INLINE_TOP_W = 25;
    public const int IMAGE_INLINE_BOTTOM_W = 26;

    public bool PageBreak { get; set; }
    public int PageType { get; set; }
    public int ImagePageType { get; set; }
    public bool NoChapter { get; set; }

    /// <summary>画像ソースファイル名（Java: srcFileName）</summary>
    public string? SrcFileName { get; set; }

    public PageBreakType(bool pageBreak, int pageType, int imagePageType, bool noChapter = false)
    {
        PageBreak = pageBreak;
        PageType = pageType;
        ImagePageType = imagePageType;
        NoChapter = noChapter;
    }

    public bool IsMiddle => PageType == PAGE_MIDDLE;
    public bool IsBottom => PageType == PAGE_BOTTOM;
    public bool IsImagePage => ImagePageType != IMAGE_PAGE_NONE;
}
