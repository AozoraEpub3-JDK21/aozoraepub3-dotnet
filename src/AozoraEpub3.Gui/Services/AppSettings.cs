using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AozoraEpub3.Gui.Services;

/// <summary>
/// GUI 設定の永続化データ。JSON にシリアライズされる。
/// </summary>
public sealed class GuiSettings
{
    // ──── 入力設定 ────
    public string InputEncoding { get; set; } = "MS932";
    public bool UseFileName { get; set; } = false;

    // ──── 書誌情報設定 ────
    public int TitleType { get; set; } = 0;
    public int CoverImageType { get; set; } = -1;
    public string CoverImagePath { get; set; } = "";
    public bool InsertCoverPage { get; set; } = false;
    public bool InsertCoverPageToc { get; set; } = false;
    public int MaxCoverLine { get; set; } = 0;
    public bool TitlePageWrite { get; set; } = false;
    public int TitlePageType { get; set; } = 0;

    // ──── 出力設定 ────
    public string OutputExtension { get; set; } = ".epub";
    public bool UseInputFileName { get; set; } = false;
    public bool ForKindle { get; set; } = false;

    // ──── ページ構成設定 ────
    public bool Vertical { get; set; } = true;
    public bool InsertTocPage { get; set; } = false;
    public bool TocVertical { get; set; } = false;
    public bool InsertTitleToc { get; set; } = true;

    // ──── 文字・変換設定 ────
    public bool NoIllust { get; set; } = false;
    public bool WithMarkId { get; set; } = false;
    public bool AutoYoko { get; set; } = true;
    public bool AutoYokoNum1 { get; set; } = true;
    public bool AutoYokoNum3 { get; set; } = true;
    public bool AutoYokoEQ1 { get; set; } = true;
    public int DakutenType { get; set; } = 1;
    public bool IvsBMP { get; set; } = false;
    public bool IvsSSP { get; set; } = true;
    public int SpaceHyphenation { get; set; } = 0;
    public bool CommentPrint { get; set; } = false;
    public bool CommentConvert { get; set; } = false;
    public int RemoveEmptyLine { get; set; } = 0;
    public int MaxEmptyLine { get; set; } = 0;

    // ──── 章認識設定 ────
    public int ChapterNameMaxLength { get; set; } = 64;
    public bool ExcludeSequentialChapter { get; set; } = true;
    public bool UseNextLineChapterName { get; set; } = true;
    public bool ChapterSection { get; set; } = true;
    public bool ChapterH { get; set; } = false;
    public bool ChapterH1 { get; set; } = false;
    public bool ChapterH2 { get; set; } = false;
    public bool ChapterH3 { get; set; } = false;
    public bool SameLineChapter { get; set; } = false;
    public bool ChapterName { get; set; } = false;
    public bool ChapterNumOnly { get; set; } = false;
    public bool ChapterNumTitle { get; set; } = false;
    public bool ChapterNumParen { get; set; } = false;
    public bool ChapterNumParenTitle { get; set; } = false;
    public bool ChapterPattern { get; set; } = false;
    public string ChapterPatternText { get; set; } = "";

    // ──── 改ページ設定 ────
    public bool PageBreak { get; set; } = false;
    public int PageBreakSize { get; set; } = 0;
    public bool PageBreakEmpty { get; set; } = false;
    public int PageBreakEmptyLine { get; set; } = 0;
    public int PageBreakEmptySize { get; set; } = 0;
    public bool PageBreakChapter { get; set; } = false;
    public int PageBreakChapterSize { get; set; } = 0;

    // ──── 最後に使用した設定 ────
    public string LastOutputDirectory { get; set; } = "";
    public int DownloadIntervalMs { get; set; } = 700;
    public string AppLanguage { get; set; } = "ja";
    public string AppTheme { get; set; } = "default";
    public string EpubcheckJarPath { get; set; } = "";
}

/// <summary>
/// GuiSettings を JSON ファイルに保存・読み込みする。
/// 保存先: %APPDATA%\AozoraEpub3\settings.json
/// </summary>
public static class AppSettingsStorage
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "AozoraEpub3", "settings.json");

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never
    };

    public static GuiSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var json = File.ReadAllText(FilePath);
                return JsonSerializer.Deserialize<GuiSettings>(json, _jsonOptions) ?? new();
            }
        }
        catch { /* 破損ファイルは無視してデフォルト値を使う */ }
        return new();
    }

    public static void Save(GuiSettings settings)
    {
        try
        {
            var dir = Path.GetDirectoryName(FilePath)!;
            Directory.CreateDirectory(dir);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(settings, _jsonOptions));
        }
        catch { /* 保存失敗は無視 */ }
    }
}
