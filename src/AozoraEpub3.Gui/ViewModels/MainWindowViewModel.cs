using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AozoraEpub3.Gui.Services;

namespace AozoraEpub3.Gui.ViewModels;

/// <summary>
/// Shell（メインウィンドウ）の ViewModel。
/// サイドバーのナビゲーションと SPA ルーティングを担当する。
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

    public bool IsJapanese => _currentLanguage == "ja";

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
}
