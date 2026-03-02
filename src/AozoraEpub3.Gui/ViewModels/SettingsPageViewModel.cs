using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AozoraEpub3.Gui.Services;

namespace AozoraEpub3.Gui.ViewModels;

/// <summary>アプリ設定ページの ViewModel</summary>
public sealed partial class SettingsPageViewModel : ViewModelBase
{
    // ───── 言語 ───────────────────────────────────────────────────────────────

    [ObservableProperty] private string _selectedLanguage = "ja";

    partial void OnSelectedLanguageChanged(string value)
        => LocalizationService.SetLanguage(value);

    // ───── テーマ ─────────────────────────────────────────────────────────────

    /// <summary>テーマ: "default" / "light" / "dark"</summary>
    [ObservableProperty] private string _selectedTheme = "default";

    partial void OnSelectedThemeChanged(string value)
    {
        var variant = value switch
        {
            "light" => ThemeVariant.Light,
            "dark"  => ThemeVariant.Dark,
            _       => ThemeVariant.Default
        };
        LocalizationService.SetTheme(variant);
    }
}
