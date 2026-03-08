namespace AozoraEpub3.Core.Editor;

/// <summary>
/// エディタ＋プレビューのカラーテーマ定義。
/// エディタ領域とプレビュー領域のスタイルを一括管理する。
/// </summary>
public sealed class EditorTheme
{
    public required string Id { get; init; }
    public required string DisplayName { get; init; }
    public required string Category { get; init; }    // "light" | "dark"

    // エディタ領域
    public required string EditorBackground { get; init; }
    public required string EditorForeground { get; init; }
    public required string EditorCaretColor { get; init; }
    public required string EditorSelectionBg { get; init; }
    public required string EditorLineNumberFg { get; init; }
    public required string EditorLineNumberBg { get; init; }

    // プレビュー領域
    public required string PreviewBackground { get; init; }
    public required string PreviewForeground { get; init; }

    // フォント
    public string EditorFontFamily { get; init; } = "BIZ UDGothic, Consolas, monospace";
    public double EditorFontSize { get; init; } = 14;
    public string PreviewFontFamily { get; init; } = "游明朝, Yu Mincho, serif";
    public double PreviewFontSize { get; init; } = 16;
}
