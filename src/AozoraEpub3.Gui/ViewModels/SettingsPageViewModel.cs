using Avalonia.Media;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AozoraEpub3.Core.Editor;
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

    // ───── エディタテーマ ──────────────────────────────────────────────────────

    /// <summary>テーマ名の表示用リスト</summary>
    public string[] EditorThemeNames { get; } = EditorThemes.All.Select(t => t.DisplayName).ToArray();

    /// <summary>選択中のエディタテーマインデックス</summary>
    [ObservableProperty]
    private int _editorThemeIndex = 3; // DarkDefault

    /// <summary>エディタテーマ変更通知。MainWindowViewModel がハンドルする。</summary>
    public event Action<EditorTheme>? EditorThemeSelectionChanged;

    partial void OnEditorThemeIndexChanged(int value)
    {
        if (value >= 0 && value < EditorThemes.All.Length)
            EditorThemeSelectionChanged?.Invoke(EditorThemes.All[value]);
    }

    /// <summary>テーマIDからインデックスを設定する（設定読み込み用）</summary>
    public void SetEditorThemeById(string themeId)
    {
        for (int i = 0; i < EditorThemes.All.Length; i++)
        {
            if (EditorThemes.All[i].Id == themeId)
            {
                EditorThemeIndex = i;
                return;
            }
        }
    }

    // ───── フォント設定 ──────────────────────────────────────────────────────

    /// <summary>システムフォント一覧（先頭は空文字 = テーマデフォルト）</summary>
    public string[] AvailableFonts { get; } = BuildFontList();

    private static string[] BuildFontList()
    {
        var systemFonts = FontManager.Current.SystemFonts
            .Select(f => f.Name)
            .OrderBy(n => n)
            .Distinct()
            .ToArray();
        return ["", .. systemFonts];
    }

    [ObservableProperty]
    private string _editorFontFamily = "";

    [ObservableProperty]
    private double _editorFontSize = 14;

    [ObservableProperty]
    private string _previewFontFamily = "";

    [ObservableProperty]
    private double _previewFontSize = 16;

    /// <summary>フォント設定変更通知</summary>
    public event Action? FontSettingsChanged;

    partial void OnEditorFontFamilyChanged(string value) => FontSettingsChanged?.Invoke();
    partial void OnEditorFontSizeChanged(double value) => FontSettingsChanged?.Invoke();
    partial void OnPreviewFontFamilyChanged(string value) => FontSettingsChanged?.Invoke();
    partial void OnPreviewFontSizeChanged(double value) => FontSettingsChanged?.Invoke();

    // ───── epubcheck ─────────────────────────────────────────────────────────

    /// <summary>epubcheck.jar のパス</summary>
    [ObservableProperty]
    private string _epubcheckJarPath = "";
}
