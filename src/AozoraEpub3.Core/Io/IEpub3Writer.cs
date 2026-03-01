namespace AozoraEpub3.Core.Io;

/// <summary>EPUB3出力クラスのインターフェース</summary>
public interface IEpub3Writer
{
    /// <summary>外字フォントディレクトリのパスを返す</summary>
    string GetGaijiFontPath();

    /// <summary>画像のEPUB内パスを返す</summary>
    string? GetImageFilePath(string srcImageFileName, int lineNum);

    /// <summary>表紙画像かどうか</summary>
    bool IsCoverImage();

    /// <summary>現在の画像インデックスを返す</summary>
    int GetImageIndex();

    /// <summary>画像のページ種別を返す</summary>
    int GetImagePageType(string srcFilePath, int tagLevel, int lineNum, bool hasCaption);

    /// <summary>画像の幅比率を返す</summary>
    double GetImageWidthRatio(string srcFilePath, bool hasCaption);

    /// <summary>次のセクションに進む（ページ区切り処理）</summary>
    void NextSection(TextWriter bw, int lineNum, int pageType, int imagePageType, string? srcImageFilePath);

    /// <summary>目次の章を追加する</summary>
    void AddChapter(string? chapterId, string name, int chapterLevel);

    /// <summary>外字フォントを追加する</summary>
    void AddGaijiFont(string className, string gaijiFilePath);

    /// <summary>進捗コールバック（Swingの jProgressBar の代替）</summary>
    Action<int>? ProgressCallback { get; set; }
}
