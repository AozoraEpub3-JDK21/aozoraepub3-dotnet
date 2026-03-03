using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AozoraEpub3.Gui.Services;

namespace AozoraEpub3.Gui.ViewModels;

/// <summary>アプリ設定ページの ViewModel</summary>
public sealed partial class SettingsPageViewModel : ViewModelBase
{
    // ───── 言語 ───────────────────────────────────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLangJapanese))]
    [NotifyPropertyChangedFor(nameof(IsLangEnglish))]
    private string _selectedLanguage = "ja";

    partial void OnSelectedLanguageChanged(string value)
        => LocalizationService.SetLanguage(value);

    public bool IsLangJapanese
    {
        get => SelectedLanguage == "ja";
        set { if (value) SelectedLanguage = "ja"; }
    }

    public bool IsLangEnglish
    {
        get => SelectedLanguage == "en";
        set { if (value) SelectedLanguage = "en"; }
    }

    // ───── テーマ ─────────────────────────────────────────────────────────────

    /// <summary>テーマ: "default" / "light" / "dark"</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ThemeIndex))]
    private string _selectedTheme = "default";

    /// <summary>ComboBox用: 0=default, 1=light, 2=dark</summary>
    public int ThemeIndex
    {
        get => SelectedTheme switch { "light" => 1, "dark" => 2, _ => 0 };
        set => SelectedTheme = value switch { 1 => "light", 2 => "dark", _ => "default" };
    }

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
