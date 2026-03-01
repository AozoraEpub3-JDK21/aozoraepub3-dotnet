namespace AozoraEpub3.Core.Info;

/// <summary>セクションの情報 xhtmlに対応</summary>
public class SectionInfo
{
    /// <summary>セクションID 0001～</summary>
    public string SectionId { get; set; } = "";

    /// <summary>画像のみのページ時にtrue</summary>
    public bool ImagePage { get; set; } = false;

    public const int IMAGE_SIZE_TYPE_AUTO = 1;
    public const int IMAGE_SIZE_TYPE_HEIGHT = 2;
    public const int IMAGE_SIZE_TYPE_ASPECT = 3;

    /// <summary>画像のみのページで高さ%指定 -1なら指定無し</summary>
    public double ImageHeight { get; set; } = -1;
    /// <summary>画像のみのページで幅に合わせる場合にtrue</summary>
    public bool ImageFitW { get; set; } = false;
    /// <summary>画像のみのページで高さに合わせる場合にtrue</summary>
    public bool ImageFitH { get; set; } = false;

    /// <summary>ページ左右中央ならtrue</summary>
    public bool Middle { get; set; } = false;

    /// <summary>ページ左ならtrue</summary>
    public bool Bottom { get; set; } = false;

    /// <summary>セクション開始行</summary>
    public int StartLine { get; set; } = 0;

    /// <summary>セクション終了行</summary>
    public int EndLine { get; set; } = 0;

    public SectionInfo(string sectionId)
    {
        SectionId = sectionId;
    }

    public double ImageHeightPercent => (int)(ImageHeight * 1000) / 10.0;
    public double ImageHeightPadding => (int)((1 - ImageHeight) * 1000) / 20.0;
}
