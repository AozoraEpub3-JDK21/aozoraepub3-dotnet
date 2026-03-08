namespace AozoraEpub3.Core.Editor;

/// <summary>
/// 組み込みエディタテーマの一覧。
/// </summary>
public static class EditorThemes
{
    public static readonly EditorTheme LightDefault = new()
    {
        Id = "light-default",
        DisplayName = "ライト",
        Category = "light",
        EditorBackground = "#FFFFFF",
        EditorForeground = "#1E1E1E",
        EditorCaretColor = "#000000",
        EditorSelectionBg = "#ADD6FF",
        EditorLineNumberFg = "#858585",
        EditorLineNumberBg = "#F3F3F3",
        PreviewBackground = "#FFFFFF",
        PreviewForeground = "#1A1A1A"
    };

    public static readonly EditorTheme LightSepia = new()
    {
        Id = "light-sepia",
        DisplayName = "セピア（長時間執筆用）",
        Category = "light",
        EditorBackground = "#F5EDDC",
        EditorForeground = "#3B3228",
        EditorCaretColor = "#3B3228",
        EditorSelectionBg = "#D4C5A9",
        EditorLineNumberFg = "#9C8E78",
        EditorLineNumberBg = "#EDE4D3",
        PreviewBackground = "#F5EDDC",
        PreviewForeground = "#3B3228"
    };

    public static readonly EditorTheme LightManuscript = new()
    {
        Id = "light-manuscript",
        DisplayName = "原稿用紙",
        Category = "light",
        EditorBackground = "#FFFEF5",
        EditorForeground = "#2C2C2C",
        EditorCaretColor = "#2C2C2C",
        EditorSelectionBg = "#C8D8E8",
        EditorLineNumberFg = "#B0A090",
        EditorLineNumberBg = "#F5F0E0",
        PreviewBackground = "#FFFEF5",
        PreviewForeground = "#2C2C2C",
        PreviewFontFamily = "ＭＳ 明朝, Yu Mincho, serif"
    };

    public static readonly EditorTheme DarkDefault = new()
    {
        Id = "dark-default",
        DisplayName = "ダーク",
        Category = "dark",
        EditorBackground = "#1E1E1E",
        EditorForeground = "#DCDCDC",
        EditorCaretColor = "#AEAFAD",
        EditorSelectionBg = "#264F78",
        EditorLineNumberFg = "#858585",
        EditorLineNumberBg = "#252526",
        PreviewBackground = "#1E1E1E",
        PreviewForeground = "#D4D4D4"
    };

    public static readonly EditorTheme DarkMonokai = new()
    {
        Id = "dark-monokai",
        DisplayName = "Monokai",
        Category = "dark",
        EditorBackground = "#272822",
        EditorForeground = "#F8F8F2",
        EditorCaretColor = "#F8F8F0",
        EditorSelectionBg = "#49483E",
        EditorLineNumberFg = "#90908A",
        EditorLineNumberBg = "#2D2E27",
        PreviewBackground = "#272822",
        PreviewForeground = "#F8F8F2"
    };

    public static readonly EditorTheme DarkNord = new()
    {
        Id = "dark-nord",
        DisplayName = "Nord",
        Category = "dark",
        EditorBackground = "#2E3440",
        EditorForeground = "#D8DEE9",
        EditorCaretColor = "#D8DEE9",
        EditorSelectionBg = "#434C5E",
        EditorLineNumberFg = "#616E88",
        EditorLineNumberBg = "#2E3440",
        PreviewBackground = "#2E3440",
        PreviewForeground = "#ECEFF4"
    };

    /// <summary>全テーマのリスト</summary>
    public static readonly EditorTheme[] All =
    [
        LightDefault,
        LightSepia,
        LightManuscript,
        DarkDefault,
        DarkMonokai,
        DarkNord
    ];

    /// <summary>IDからテーマを取得。見つからなければダークデフォルト。</summary>
    public static EditorTheme GetById(string id)
        => Array.Find(All, t => t.Id == id) ?? DarkDefault;

    /// <summary>カテゴリに応じたデフォルトテーマを返す。</summary>
    public static EditorTheme GetDefault(string category)
        => category == "light" ? LightDefault : DarkDefault;
}
