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

    public LocalConvertViewModel LocalConvertVm { get; } = new();
    public WebConvertViewModel WebConvertVm { get; } = new();
    public SettingsPageViewModel SettingsVm { get; } = new();
    public PreviewViewModel PreviewVm { get; } = new();
    public ValidateViewModel ValidateVm { get; } = new();
    public EditorViewModel EditorVm { get; } = new();
    public CardBoardViewModel CardBoardVm { get; } = new();
    public CardEditorViewModel CardEditorVm { get; } = new();

    // ───── SPA ルーティング ──────────────────────────────────────────────────

    /// <summary>
    /// ContentControl にバインドする「現在のページ ViewModel」。
    /// App.axaml に登録した DataTemplate が対応する View を自動解決する。
    /// </summary>
    [ObservableProperty]
    private ViewModelBase _currentPage;

    public MainWindowViewModel()
    {
        _currentPage = LocalConvertVm;
        LocalConvertVm.OnConversionCompleted = OpenPreview;
        PreviewVm.ToggleMaximizeRequested += () => IsPreviewMaximized = !IsPreviewMaximized;
        PreviewVm.ValidateRequested += OnValidateRequested;
        PreviewVm.NavigateToCardsRequested += () => NavigateTo("cards");
        ValidateVm.JumpToFileRequested += OnJumpToFile;
        CardBoardVm.EpubConversionRequested += OnCardEpubConversion;
        CardBoardVm.MigrateToProjectRequested += OnMigrateToProject;
        CardEditorVm.EpubConversionRequested += OnCardEpubConversion;
        EditorVm.EpubConversionRequested += OnCardEpubConversion;
        SettingsVm.EditorThemeSelectionChanged += OnEditorThemeChanged;
        SettingsVm.FontSettingsChanged += OnFontSettingsChanged;
        LoadSettings();
    }

    [RelayCommand]
    private void NavigateTo(string page)
    {
        // ページ遷移時にプレビュー最大化を解除
        if (IsPreviewMaximized)
            IsPreviewMaximized = false;

        CurrentPage = page switch
        {
            "local"    => LocalConvertVm,
            "web"      => WebConvertVm,
            "settings" => SettingsVm,
            "preview"  => PreviewVm,
            "validate" => ValidateVm,
            "editor"   => EditorVm,
            "cards"    => CardBoardVm,
            "project"  => CardEditorVm,
            _          => LocalConvertVm
        };
    }

    // ───── 言語トグル ────────────────────────────────────────────────────────

    /// <summary>現在の言語コード ("ja" / "en")</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsJapanese))]
    private string _currentLanguage = "ja";

    public bool IsJapanese => CurrentLanguage == "ja";

    /// <summary>プレビュー最大化モード（ヘッダー・サイドバーを隠す）</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsHeaderVisible))]
    [NotifyPropertyChangedFor(nameof(IsSidebarVisible))]
    private bool _isPreviewMaximized;

    public bool IsHeaderVisible => !IsPreviewMaximized;
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

    // ───── プレビュー連携 ────────────────────────────────────────────────────

    private void OnValidateRequested(string epubPath)
    {
        ValidateVm.ValidateCurrentEpub(epubPath, ValidateVm.JarPath);
        CurrentPage = ValidateVm;
    }

    private void OnJumpToFile(string fileName)
    {
        // エラー箇所のファイル名からプレビューのスパインインデックスを特定してジャンプ
        if (!PreviewVm.IsEpubLoaded) return;

        // ソースマッピングからファイル名→スパインインデックスを検索
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
        // カードコレクション → プロジェクトに変換
        var projectService = new AozoraEpub3.Core.Editor.ProjectService();
        var appDataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "AozoraEpub3", "projects");
        Directory.CreateDirectory(appDataDir);

        var projectDir = projectService.ImportFromCardCollection(appDataDir, collection);

        // CardEditorViewModel にプロジェクトを読み込ませる
        CardEditorVm.LoadProject(projectDir);
        CurrentPage = CardEditorVm;
    }

    private void OnCardEpubConversion(string tempTextPath)
    {
        // カードモードからのEPUB変換: テキストファイルをローカル変換に渡す
        LocalConvertVm.AddFilePaths([tempTextPath]);
        CurrentPage = LocalConvertVm;
    }

    private void OnEditorThemeChanged(EditorTheme theme)
    {
        EditorVm.CurrentTheme = theme;
        CardBoardVm.CurrentTheme = theme;
        CardEditorVm.CurrentTheme = theme;
    }

    private void OnFontSettingsChanged()
    {
        // フォント設定はテーマのオーバーライドとして反映
        // 空の場合はテーマデフォルトを使うので何もしない
    }

    /// <summary>指定 EPUB をプレビュータブで開く（変換完了後の自動遷移用）。</summary>
    public void OpenPreview(string epubPath)
    {
        PreviewVm.OpenEpubCommand.Execute(epubPath);
        CurrentPage = PreviewVm;
    }

    // ───── 設定の永続化 ──────────────────────────────────────────────────────

    /// <summary>起動時に保存済み設定を読み込む。</summary>
    private void LoadSettings()
    {
        var s = AppSettingsStorage.Load();
        LocalConvertVm.Settings.LoadFrom(s);
        LocalConvertVm.OutputDirectory = s.LastOutputDirectory;
        WebConvertVm.DownloadIntervalMs = s.DownloadIntervalMs;

        // 言語・テーマ
        if (!string.IsNullOrEmpty(s.AppLanguage))
        {
            LocalizationService.SetLanguage(s.AppLanguage);
            CurrentLanguage = s.AppLanguage;
            SettingsVm.SelectedLanguage = s.AppLanguage;
        }
        if (!string.IsNullOrEmpty(s.AppTheme))
        {
            SettingsVm.SelectedTheme = s.AppTheme;
        }
        if (!string.IsNullOrEmpty(s.EpubcheckJarPath))
        {
            ValidateVm.JarPath = s.EpubcheckJarPath;
            SettingsVm.EpubcheckJarPath = s.EpubcheckJarPath;
        }

        // エディタテーマ
        if (!string.IsNullOrEmpty(s.EditorThemeId))
        {
            SettingsVm.SetEditorThemeById(s.EditorThemeId);
            var theme = EditorThemes.GetById(s.EditorThemeId);
            EditorVm.CurrentTheme = theme;
            CardBoardVm.CurrentTheme = theme;
            CardEditorVm.CurrentTheme = theme;
        }

        // マイグレーション提案
        CardBoardVm.SuppressMigrationProposals = s.SuppressMigrationProposals;

        // フォント設定
        if (!string.IsNullOrEmpty(s.EditorFontFamily))
            SettingsVm.EditorFontFamily = s.EditorFontFamily;
        if (s.EditorFontSize > 0)
            SettingsVm.EditorFontSize = s.EditorFontSize;
        if (!string.IsNullOrEmpty(s.PreviewFontFamily))
            SettingsVm.PreviewFontFamily = s.PreviewFontFamily;
        if (s.PreviewFontSize > 0)
            SettingsVm.PreviewFontSize = s.PreviewFontSize;
    }

    /// <summary>終了時に現在の設定を保存する。</summary>
    public void SaveSettings()
    {
        var s = new GuiSettings();
        LocalConvertVm.Settings.SaveTo(s);
        s.LastOutputDirectory  = LocalConvertVm.OutputDirectory;
        s.DownloadIntervalMs   = WebConvertVm.DownloadIntervalMs;
        s.AppLanguage          = LocalizationService.CurrentLanguage;
        s.AppTheme             = SettingsVm.SelectedTheme;
        s.EpubcheckJarPath     = ValidateVm.JarPath;

        // エディタテーマ・フォント
        if (SettingsVm.EditorThemeIndex >= 0 && SettingsVm.EditorThemeIndex < EditorThemes.All.Length)
            s.EditorThemeId = EditorThemes.All[SettingsVm.EditorThemeIndex].Id;
        s.EditorFontFamily = SettingsVm.EditorFontFamily;
        s.EditorFontSize = SettingsVm.EditorFontSize;
        s.PreviewFontFamily = SettingsVm.PreviewFontFamily;
        s.PreviewFontSize = SettingsVm.PreviewFontSize;

        // マイグレーション提案
        s.SuppressMigrationProposals = CardBoardVm.SuppressMigrationProposals;

        AppSettingsStorage.Save(s);
    }
}
