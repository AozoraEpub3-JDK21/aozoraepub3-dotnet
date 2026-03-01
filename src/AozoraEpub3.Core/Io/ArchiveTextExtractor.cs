using System.IO.Compression;
using System.Text;
using SharpCompress.Archives;
using SharpCompress.Archives.Rar;

namespace AozoraEpub3.Core.Io;

/// <summary>
/// アーカイブ/txtファイルからテキストエントリを読み取る。
/// キャッシュを使用して複数回のスキャンを避ける。
/// </summary>
public sealed class ArchiveTextExtractor
{
    private static readonly Dictionary<string, ArchiveCache> _cacheMap = new();
    private static readonly object _cacheLock = new();

    private ArchiveTextExtractor() { }

    private static ArchiveCache GetCache(string srcFilePath, string ext)
    {
        lock (_cacheLock)
        {
            if (!_cacheMap.TryGetValue(srcFilePath, out var cache))
            {
                cache = new ArchiveCache(srcFilePath, ext);
                _cacheMap[srcFilePath] = cache;
            }
            return cache;
        }
    }

    public static void ClearCache(string srcFilePath)
    {
        lock (_cacheLock)
        {
            _cacheMap.Remove(srcFilePath);
        }
    }

    /// <summary>テキストコンテンツのストリームを取得。呼び出し側でClose()すること</summary>
    public static Stream? GetTextInputStream(string srcFilePath, string ext, string[]? textEntryName, int txtIdx)
    {
        if (ext == "txt")
        {
            return new FileStream(srcFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        }
        else if (ext == "zip" || ext == "txtz" || ext == "rar")
        {
            var cache = GetCache(srcFilePath, ext);
            cache.Scan();

            string? entryName = cache.GetTextEntryName(txtIdx);
            if (entryName == null)
            {
                Console.Error.WriteLine($"{ext}内にtxtファイルがありません: {Path.GetFileName(srcFilePath)}");
                return null;
            }

            if (textEntryName != null) textEntryName[0] = entryName;
            return cache.GetTextInputStream(txtIdx);
        }
        else
        {
            Console.Error.WriteLine($"txt, zip, rar, txtz のみ変換可能です: {srcFilePath}");
        }
        return null;
    }

    public static int CountZipText(string zipFilePath)
    {
        var cache = GetCache(zipFilePath, "zip");
        cache.Scan();
        return cache.TextFileCount;
    }

    public static int CountRarText(string rarFilePath)
    {
        var cache = GetCache(rarFilePath, "rar");
        cache.Scan();
        return cache.TextFileCount;
    }
}

internal class ArchiveCache
{
    private readonly string _archiveFile;
    private readonly string _ext;

    private int _textFileCount = -1;
    private List<TextEntry>? _textEntries;
    private List<string>? _imageEntries;
    private bool _scanned = false;
    private readonly object _scanLock = new();

    public int TextFileCount
    {
        get
        {
            if (!_scanned) Scan();
            return _textFileCount;
        }
    }

    public ArchiveCache(string archiveFile, string ext)
    {
        _archiveFile = archiveFile;
        _ext = ext;
    }

    public void Scan()
    {
        lock (_scanLock)
        {
            if (_scanned) return;

            _textEntries = new List<TextEntry>();
            _imageEntries = new List<string>();

            if (_ext == "zip" || _ext == "txtz")
                ScanZip();
            else if (_ext == "rar")
                ScanRar();

            _textFileCount = _textEntries.Count;
            _scanned = true;
        }
    }

    private void ScanZip()
    {
        // Use System.IO.Compression - handles UTF-8 and CP437
        // For Shift-JIS filenames in ZIP, try with encoding
        try
        {
            using var fs = new FileStream(_archiveFile, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var zip = new ZipArchive(fs, ZipArchiveMode.Read);
            foreach (var entry in zip.Entries)
            {
                if (entry.FullName.EndsWith('/')) continue; // directory
                string entryName = entry.FullName;
                string entryExt = GetExtension(entryName);

                if (string.Equals(entryExt, "txt", StringComparison.OrdinalIgnoreCase))
                {
                    using var stream = entry.Open();
                    var content = ReadFully(stream);
                    _textEntries!.Add(new TextEntry(entryName, content));
                }
                else if (IsImageExtension(entryExt))
                {
                    _imageEntries!.Add(entryName);
                }
            }
        }
        catch
        {
            // Fallback: try with SharpCompress for better encoding support
            ScanZipSharpCompress();
        }
    }

    private void ScanZipSharpCompress()
    {
        try
        {
            using var archive = SharpCompress.Archives.Zip.ZipArchive.OpenArchive(new FileInfo(_archiveFile), null);
            foreach (var entry in archive.Entries)
            {
                if (entry.IsDirectory) continue;
                string entryName = entry.Key ?? "";
                string entryExt = GetExtension(entryName);

                if (string.Equals(entryExt, "txt", StringComparison.OrdinalIgnoreCase))
                {
                    using var stream = entry.OpenEntryStream();
                    var content = ReadFully(stream);
                    _textEntries!.Add(new TextEntry(entryName, content));
                }
                else if (IsImageExtension(entryExt))
                {
                    _imageEntries!.Add(entryName);
                }
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"ZIP読み込みエラー: {ex.Message}");
        }
    }

    private void ScanRar()
    {
        try
        {
            using var archive = RarArchive.OpenArchive(new FileInfo(_archiveFile), null);
            foreach (var entry in archive.Entries)
            {
                if (entry.IsDirectory) continue;
                string entryName = (entry.Key ?? "").Replace('\\', '/');
                string entryExt = GetExtension(entryName);

                if (string.Equals(entryExt, "txt", StringComparison.OrdinalIgnoreCase))
                {
                    using var stream = entry.OpenEntryStream();
                    var content = ReadFully(stream);
                    _textEntries!.Add(new TextEntry(entryName, content));
                }
                else if (IsImageExtension(entryExt))
                {
                    _imageEntries!.Add(entryName);
                }
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"RAR読み込みエラー: {ex.Message}");
        }
    }

    public Stream? GetTextInputStream(int txtIdx)
    {
        if (!_scanned) Scan();
        if (txtIdx < 0 || txtIdx >= _textEntries!.Count) return null;
        return new MemoryStream(_textEntries[txtIdx].Content);
    }

    public string? GetTextEntryName(int txtIdx)
    {
        if (!_scanned) Scan();
        if (txtIdx < 0 || txtIdx >= _textEntries!.Count) return null;
        return _textEntries[txtIdx].Name;
    }

    public List<string> GetImageEntries()
    {
        if (!_scanned) Scan();
        return new List<string>(_imageEntries!);
    }

    private static string GetExtension(string name)
    {
        int dot = name.LastIndexOf('.');
        return dot >= 0 ? name.Substring(dot + 1) : "";
    }

    private static bool IsImageExtension(string ext) =>
        ext.Equals("jpg", StringComparison.OrdinalIgnoreCase) ||
        ext.Equals("jpeg", StringComparison.OrdinalIgnoreCase) ||
        ext.Equals("png", StringComparison.OrdinalIgnoreCase) ||
        ext.Equals("gif", StringComparison.OrdinalIgnoreCase) ||
        ext.Equals("bmp", StringComparison.OrdinalIgnoreCase);

    internal static byte[] ReadFully(Stream stream)
    {
        using var ms = new MemoryStream();
        var buf = new byte[8192];
        int len;
        while ((len = stream.Read(buf, 0, buf.Length)) > 0)
            ms.Write(buf, 0, len);
        return ms.ToArray();
    }

    internal class TextEntry
    {
        public string Name { get; }
        public byte[] Content { get; }
        public TextEntry(string name, byte[] content)
        {
            Name = name;
            Content = content;
        }
    }
}
