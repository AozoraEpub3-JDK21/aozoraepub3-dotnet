using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AozoraEpub3.Core.Editor;
using AozoraEpub3.Gui.Services;

namespace AozoraEpub3.Gui.ViewModels;

public sealed partial class MainWindowViewModel : ViewModelBase
{
    // ── 子 ViewModel ──────────────────────────────────────────────────────
    public ReadViewModel       ReadVm      { get; } = new();
    public EditorViewModel     EditorVm    { get; } = new();
    public CardBoardViewModel  CardBoardVm { get; } = new();
    public CardEditorViewModel CardEditorVm { get; } = new();
    public SettingsPageViewModel SettingsVm { get; } = new();
    public PreviewViewModel    PreviewVm   { get; } = new();
    public ValidateViewModel   ValidateVm  { get; } = new();

    // 後方互換アクセサ
    public LocalConvertViewModel LocalConvertVm => ReadVm.LocalConvertVm;
    public WebConvertViewModel   WebConvertVm   => ReadVm.WebConvertVm;

    // ── SPA ルーティング ──────────────────────────────────────────────────
    [ObservableProperty]
    private ViewModelBase _currentPage;

    /// <summary>サイドバー選択状態：read / write / publish / settings</summary>
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
        _currentPage = ReadVm;

        ReadVm.OnConversionCompleted = OpenPreview;

        PreviewVm.ToggleMaximizeRequested  += () => IsPreviewMaximized = !IsPreviewMaximized;
        PreviewVm.ValidateRequested        += OnValidateRequested;
        PreviewVm.NavigateToCardsRequested += () => NavigateTo("cards");

        ValidateVm.JumpToFileRequested += OnJumpToFile;

        CardBoardVm.EpubConversionRequested   += OnCardEpubConversion;
        CardBoardVm.MigrateToProjectRequested += OnMigrateToProject;
        CardEditorVm.EpubConversionRequested  += OnCardEpubConversion;
        EditorVm.EpubConversionRequested      += OnCardEpubConversion;

        SettingsVm.EditorThemeSelectionChanged += OnEditorThemeChanged;
        SettingsVm.FontSettingsChanged         += OnFontSettingsChanged;

        LoadSettings();
    }

    [RelayCommand]
    private void NavigateTo(string page)
    {
        if (IsPreviewMaximized) IsPreviewMaximized = false;

        CurrentPageId = page switch
        {
            "read" or "local" or "web" => "read",
            "write" or "editor"        => "write",
            "cards"                    => "write",
            "publish" or "project"     => "publish",
            "settings"                 => "settings",
            _ => CurrentPageId   // preview / validate は内部遷移
        };

        if (page == "web")   ReadVm.IsUrlMode = true;
        if (page == "local") ReadVm.IsUrlMode = false;

        CurrentPage = page switch
        {
            "read" or "web" or "local" => ReadVm,
            "write" or "editor"        => EditorVm,
            "cards"                    => CardBoardVm,
            "publish" or "project"     => CardEditorVm,
            "settings"                 => SettingsVm,
            "preview"                  => PreviewVm,
            "validate"                 => ValidateVm,
            _                          => ReadVm
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
        CardEditorVm.LoadProject(projectDir);
        CurrentPage   = CardEditorVm;
        CurrentPageId = "publish";
    }

    private void OnCardEpubConversion(string tempTextPath)
    {
        ReadVm.LocalConvertVm.AddFilePaths([tempTextPath]);
        ReadVm.IsUrlMode = false;
        CurrentPage   = ReadVm;
        CurrentPageId = "read";
    }

    private void OnEditorThemeChanged(EditorTheme theme)
    {
        EditorVm.CurrentTheme    = theme;
        CardBoardVm.CurrentTheme  = theme;
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
            CardBoardVm.CurrentTheme  = theme;
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
        s.SuppressMigrationProposals = CardBoardVm.SuppressMigrationProposals;
        AppSettingsStorage.Save(s);
    }
}
