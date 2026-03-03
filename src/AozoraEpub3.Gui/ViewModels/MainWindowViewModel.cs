using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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
        LoadSettings();
    }

    [RelayCommand]
    private void NavigateTo(string page)
    {
        CurrentPage = page switch
        {
            "local"    => LocalConvertVm,
            "web"      => WebConvertVm,
            "settings" => SettingsVm,
            _          => LocalConvertVm
        };
    }

    // ───── 言語トグル ────────────────────────────────────────────────────────

    /// <summary>現在の言語コード ("ja" / "en")</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsJapanese))]
    private string _currentLanguage = "ja";

    public bool IsJapanese => CurrentLanguage == "ja";

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
        AppSettingsStorage.Save(s);
    }
}
