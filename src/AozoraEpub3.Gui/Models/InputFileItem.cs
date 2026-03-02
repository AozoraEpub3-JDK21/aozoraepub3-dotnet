using System.IO;

namespace AozoraEpub3.Gui.Models;

/// <summary>変換対象ファイル一覧の1エントリ</summary>
public sealed class InputFileItem
{
    public InputFileItem(string fullPath)
    {
        FullPath = fullPath;
        FileName = Path.GetFileName(fullPath);
        Extension = Path.GetExtension(fullPath).ToLowerInvariant();
        SizeText = FormatSize(new FileInfo(fullPath).Length);
        IsUnsupported = Extension == ".cbz";
    }

    /// <summary>ファイルフルパス</summary>
    public string FullPath { get; }

    /// <summary>ファイル名（表示用）</summary>
    public string FileName { get; }

    /// <summary>拡張子（小文字）</summary>
    public string Extension { get; }

    /// <summary>ファイルサイズ（表示用）</summary>
    public string SizeText { get; }

    /// <summary>変換非対応ファイル (.cbz) かどうか</summary>
    public bool IsUnsupported { get; }

    private static string FormatSize(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
        _ => $"{bytes / (1024.0 * 1024):F1} MB"
    };
}
