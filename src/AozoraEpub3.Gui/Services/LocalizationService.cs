using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Styling;

namespace AozoraEpub3.Gui.Services;

/// <summary>
/// アプリ全体の表示言語を切り替えるサービス。
/// <para>
/// 仕組み: Application.Resources.MergedDictionaries の先頭エントリを
/// Strings.{lang}.axaml に差し替える。XAML 内の {DynamicResource} が
/// 自動的に再評価されるため、View 側のコード変更は不要。
/// </para>
/// </summary>
public static class LocalizationService
{
    private const string AssemblyName = "AozoraEpub3.Gui";
    private static string _currentLanguage = "ja";

    public static string CurrentLanguage => _currentLanguage;

    /// <summary>言語を切り替える。"ja" または "en" を指定する。</summary>
    public static void SetLanguage(string langCode)
    {
        if (_currentLanguage == langCode) return;
        _currentLanguage = langCode;

        var uri = new Uri(
            $"avares://{AssemblyName}/Assets/Strings.{langCode}.axaml");

        var newDict = new ResourceInclude(uri) { Source = uri };

        var merged = Application.Current!.Resources.MergedDictionaries;

        // 既存の Strings.*.axaml を差し替える
        var existing = merged
            .OfType<ResourceInclude>()
            .FirstOrDefault(r => r.Source?.OriginalString.Contains("/Strings.") == true);

        if (existing is not null)
        {
            var idx = merged.IndexOf(existing);
            merged[idx] = newDict;
        }
        else
        {
            merged.Add(newDict);
        }
    }

    /// <summary>現在の言語が日本語かどうか</summary>
    public static bool IsJapanese => _currentLanguage == "ja";

    /// <summary>言語をトグルする（ja ⇔ en）</summary>
    public static void Toggle() =>
        SetLanguage(IsJapanese ? "en" : "ja");

    /// <summary>テーマを切り替える</summary>
    public static void SetTheme(ThemeVariant variant)
    {
        if (Application.Current is not null)
            Application.Current.RequestedThemeVariant = variant;
    }
}
