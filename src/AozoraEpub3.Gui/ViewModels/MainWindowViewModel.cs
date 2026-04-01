using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AozoraEpub3.Core.Editor;
using AozoraEpub3.Gui.Services;

namespace AozoraEpub3.Gui.ViewModels;

/// <summary>
/// Shell（メインウィンドウ）の ViewModel。
/// サイドバーのナビゲーションと SPA ルーティングを担当する。
/// 起動時に設定を読み込み、終了時に保存する。
/// </summary>
public sealed partial class MainWindowViewModel : ViewModelBase
{
    // ───── 子 ViewModel ─────────────────────────────────────────────────────

    /// <summary>層1「読む」コンテナ（URL変換 + ファイル変換）</summary>
    public ReadViewModel ReadVm { get; } = new();

    /// <summary>層2「書く」— フルテキストエディタ</summary>
    public EditorViewModel EditorVm { get; } = new();

    /// <summary>層2「書く」— カード執筆ボード</summary>
    public CardBoardViewModel CardBoardVm { get; } = new();

    /// <summary>層2「書く」コンテナ（カードボード + エディタ）</summary>
    public WriteViewModel WriteVm { get; }

    /// <summary>層3「本にする」— ブックエディタ</summary>
    public CardEditorViewModel CardEditorVm { get; } = new();

    public SettingsPageViewModel SettingsVm { get; } = new();
    public PreviewViewModel PreviewVm { get; } = new();
    public ValidateViewModel ValidateVm { get; } = new();

    // 後方互換アクセサ
    public LocalConvertViewModel LocalConvertVm => ReadVm.LocalConvertVm;
    public WebConvertViewModel   WebConvertVm   => ReadVm.WebConvertVm;

    // ───── SPA ルーティング ──────────────────────────────────────────────────

    /// <summary>
    /// ContentControl にバインドする「現在のページ ViewModel」。
    /// App.axaml に登録した DataTemplate が対応する View を自動解決する。
    /// </summary>
    [ObservableProperty]
    private ViewModelBase _currentPage;

    /// <summary>
    /// サイドバーのどのページが選択中かを示す ID。
    /// "read" / "write" / "publish" / "settings"
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsReadPage))]
    [NotifyPropertyChangedFor(nameof(IsWritePage))]
    [NotifyPropertyChangedFor(nameof(IsPublishPage))]
    [NotifyPropertyChangedFor(nameof(IsSettingsPage))]
    private string _currentPageId = "read";

    public bool IsReadPage     => CurrentPageId == "read";
    public bool IsWritePage    => CurrentPageId == "write";
    public bool IsPublishPage  => CurrentPageId == "publish";
    public bool IsSettingsPage => CurrentPageId == "settings";

    public MainWindowViewModel()
    {
        WriteVm = new WriteViewModel(CardBoardVm, EditorVm);
        _currentPage = ReadVm;

        // 層1 → プレビューへ自動遷移
        ReadVm.OnConversionCompleted = OpenPreview;
        ReadVm.OpenPreviewRequested += () => NavigateTo("preview");

        // プレビュー連携
        PreviewVm.ToggleMaximizeRequested += () => IsPreviewMaximized = !IsPreviewMaximized;
        PreviewVm.ValidateRequested       += OnValidateRequested;
        PreviewVm.NavigateToCardsRequested += () => NavigateTo("cards");

        // 検証連携
        ValidateVm.JumpToFileRequested += OnJumpToFile;

        // カードボード連携
        CardBoardVm.EpubConversionRequested   += OnCardEpubConversion;
        CardBoardVm.MigrateToProjectRequested += OnMigrateToProject;

        // カードエディタ・フルエディタ連携
        CardEditorVm.EpubConversionRequested += OnCardEpubConversion;
        EditorVm.EpubConversionRequested     += OnCardEpubConversion;

        // 設定連携
        SettingsVm.EditorThemeSelectionChanged += OnEditorThemeChanged;
        SettingsVm.FontSettingsChanged         += OnFontSettingsChanged;

        LoadSettings();
    }

    [RelayCommand]
    private void NavigateTo(string page)
    {
        // ページ遷移時にプレビュー最大化を解除
        if (IsPreviewMaximized)
            IsPreviewMaximized = false;

        // サイドバーの選択状態を更新（内部遷移では変えない）
        CurrentPageId = page switch
        {
            "read" or "local" or "web"          => "read",
            "write" or "editor" or "cards"      => "write",
            "publish" or "project"              => "publish",
            "settings"                          => "settings",
            // preview / validate は内部遷移なので現在の選択を維持
            _                                   => CurrentPageId
        };

        // URL/ファイルモードのサブ切り替え
        if (page == "web")     ReadVm.IsUrlMode   = true;
        if (page == "local")   ReadVm.IsUrlMode   = false;
        if (page == "editor")  WriteVm.IsCardMode = false;
        if (page == "cards")   WriteVm.IsCardMode = true;

        // ページ遷移
        CurrentPage = page switch
        {
            "read" or "web" or "local"          => ReadVm,
            "write" or "editor" or "cards"      => WriteVm,
            "publish" or "project"              => CardEditorVm,
            "settings"                          => SettingsVm,
            "preview"                           => PreviewVm,
            "validate"                          => ValidateVm,
            _                                   => ReadVm
        };
    }

    // ── 言語・最大化 ──────────────────────────────────────────────────────
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsJapanese))]
    private string _currentLanguage = "ja";
    public bool IsJapanese => CurrentLanguage == "ja";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsHeaderVisible))]
    [NotifyPropertyChangedFor(nameof(IsSidebarVisible))]
    private bool _isPreviewMaximized;
    public bool IsHeaderVisible  => !IsPreviewMaximized;
    public bool IsSidebarVisible => !IsPreviewMaximized;

    [RelayCommand]
    private void ToggleLanguage()
    {
        LocalizationService.Toggle();
        CurrentLanguage = LocalizationService.CurrentLanguage;
    }

    [RelayCommand]
    private void SetLanguage(string lang)
    {
        LocalizationService.SetLanguage(lang);
        CurrentLanguage = LocalizationService.CurrentLanguage;
    }

    // ── プレビュー連携 ────────────────────────────────────────────────────
    private void OnValidateRequested(string epubPath)
    {
        ValidateVm.ValidateCurrentEpub(epubPath, ValidateVm.JarPath);
        CurrentPage = ValidateVm;
    }

    private void OnJumpToFile(string fileName)
    {
        if (!PreviewVm.IsEpubLoaded) return;
        var metaPath = SourceMappingService.GetMetaFilePath(PreviewVm.EpubFilePath);
        var map = SourceMappingService.Load(metaPath);
        if (map == null) return;
        var chapter = map.Chapters.FirstOrDefault(c =>
            c.Href.EndsWith(fileName, StringComparison.OrdinalIgnoreCase) ||
            fileName.EndsWith(c.Href, StringComparison.OrdinalIgnoreCase));
        if (chapter != null)
        {
            PreviewVm.GoToPageCommand.Execute(chapter.SpineIndex);
            CurrentPage = PreviewVm;
        }
    }

    private void OnMigrateToProject(CardCollection collection)
    {
        var projectService = new ProjectService();
        var appDataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "AozoraEpub3", "projects");
        Directory.CreateDirectory(appDataDir);
        var projectDir = projectService.ImportFromCardCollection(appDataDir, collection);

        // CardEditorViewModel にプロジェクトを読み込ませる → 層3へ
        CardEditorVm.LoadProject(projectDir);
        CurrentPage   = CardEditorVm;
        CurrentPageId = "publish";
    }

    private void OnCardEpubConversion(string tempTextPath)
    {
        // カードモードからのEPUB変換: テキストファイルをローカル変換に渡して「読む」へ
        ReadVm.LocalConvertVm.AddFilePaths([tempTextPath]);
        ReadVm.IsUrlMode = false;
        CurrentPage   = ReadVm;
        CurrentPageId = "read";
    }

    private void OnEditorThemeChanged(EditorTheme theme)
    {
        EditorVm.CurrentTheme    = theme;
        CardBoardVm.CurrentTheme = theme;
        CardEditorVm.CurrentTheme = theme;
    }

    private void OnFontSettingsChanged() { }

    public void OpenPreview(string epubPath)
    {
        PreviewVm.OpenEpubCommand.Execute(epubPath);
        CurrentPage = PreviewVm;
    }

    // ── 設定の永続化 ─────────────────────────────────────────────────────
    private void LoadSettings()
    {
        var s = AppSettingsStorage.Load();
        ReadVm.LocalConvertVm.Settings.LoadFrom(s);
        ReadVm.LocalConvertVm.OutputDirectory  = s.LastOutputDirectory;
        ReadVm.WebConvertVm.DownloadIntervalMs = s.DownloadIntervalMs;

        if (!string.IsNullOrEmpty(s.AppLanguage))
        {
            LocalizationService.SetLanguage(s.AppLanguage);
            CurrentLanguage        = s.AppLanguage;
            SettingsVm.SelectedLanguage = s.AppLanguage;
        }
        if (!string.IsNullOrEmpty(s.AppTheme))        SettingsVm.SelectedTheme = s.AppTheme;
        if (!string.IsNullOrEmpty(s.EpubcheckJarPath))
        {
            ValidateVm.JarPath         = s.EpubcheckJarPath;
            SettingsVm.EpubcheckJarPath = s.EpubcheckJarPath;
        }
        if (!string.IsNullOrEmpty(s.EditorThemeId))
        {
            SettingsVm.SetEditorThemeById(s.EditorThemeId);
            var theme = EditorThemes.GetById(s.EditorThemeId);
            EditorVm.CurrentTheme    = theme;
            CardBoardVm.CurrentTheme = theme;
            CardEditorVm.CurrentTheme = theme;
        }
        CardBoardVm.SuppressMigrationProposals = s.SuppressMigrationProposals;
        if (!string.IsNullOrEmpty(s.EditorFontFamily))  SettingsVm.EditorFontFamily  = s.EditorFontFamily;
        if (s.EditorFontSize > 0)                       SettingsVm.EditorFontSize    = s.EditorFontSize;
        if (!string.IsNullOrEmpty(s.PreviewFontFamily)) SettingsVm.PreviewFontFamily = s.PreviewFontFamily;
        if (s.PreviewFontSize > 0)                      SettingsVm.PreviewFontSize   = s.PreviewFontSize;
    }

    public void SaveSettings()
    {
        var s = new GuiSettings();
        ReadVm.LocalConvertVm.Settings.SaveTo(s);
        s.LastOutputDirectory  = ReadVm.LocalConvertVm.OutputDirectory;
        s.DownloadIntervalMs   = ReadVm.WebConvertVm.DownloadIntervalMs;
        s.AppLanguage          = LocalizationService.CurrentLanguage;
        s.AppTheme             = SettingsVm.SelectedTheme;
        s.EpubcheckJarPath     = ValidateVm.JarPath;
        if (SettingsVm.EditorThemeIndex >= 0 && SettingsVm.EditorThemeIndex < EditorThemes.All.Length)
            s.EditorThemeId = EditorThemes.All[SettingsVm.EditorThemeIndex].Id;
        s.EditorFontFamily  = SettingsVm.EditorFontFamily;
        s.EditorFontSize    = SettingsVm.EditorFontSize;
        s.PreviewFontFamily = SettingsVm.PreviewFontFamily;
        s.PreviewFontSize   = SettingsVm.PreviewFontSize;

        // マイグレーション提案
        s.SuppressMigrationProposals = CardBoardVm.SuppressMigrationProposals;
        _ = AppSettingsStorage.Save(s);
    }
}
