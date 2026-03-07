namespace AozoraEpub3.Core.Editor;

/// <summary>エディタのモード（変換ルールセット）</summary>
public enum EditorMode { Generic, Narou, Kakuyomu }

/// <summary>
/// ハイブリッド記法 → 青空文庫記法 変換時のルール設定。
/// モード別にデフォルト設定を提供する。
/// </summary>
public sealed class ConversionProfile
{
    public EditorMode Mode { get; init; }

    // ── 記法変換 ──
    public bool EnableRuby { get; init; } = true;           // |漢字《かんじ》
    public bool EnableEmphasis { get; init; } = true;       // 《《強調》》
    public bool EnableBold { get; init; } = true;           // **太字**
    public bool EnableHeadings { get; init; } = true;       // # 見出し
    public bool EnablePageBreak { get; init; } = true;      // ---
    public bool EnableBlockquote { get; init; } = true;     // > 引用

    // ── 自動整形（Lint） ──
    public bool AutoEllipsis { get; init; } = true;         // ... → ……
    public bool AutoDash { get; init; } = true;             // -- → ――
    public bool AutoExclamationSpace { get; init; } = true; // ！の後に全角スペース

    /// <summary>汎用モード</summary>
    public static ConversionProfile Default => new() { Mode = EditorMode.Generic };

    /// <summary>小説家になろうモード</summary>
    public static ConversionProfile Narou => new() { Mode = EditorMode.Narou };

    /// <summary>カクヨムモード</summary>
    public static ConversionProfile Kakuyomu => new()
    {
        Mode = EditorMode.Kakuyomu,
        EnableEmphasis = true,
    };
}
