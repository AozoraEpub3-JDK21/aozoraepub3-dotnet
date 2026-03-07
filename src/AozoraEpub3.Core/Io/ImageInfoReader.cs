using System.IO.Compression;
using System.Text.RegularExpressions;
using AozoraEpub3.Core.Converter;
using AozoraEpub3.Core.Info;
using SharpCompress.Archives.Rar;

namespace AozoraEpub3.Core.Io;

/// <summary>
/// 画像情報を格納するクラス。
/// 画像取得関連のメソッドもここで定義。
/// </summary>
public class ImageInfoReader
{
    /// <summary>画像が圧縮ファイル内でなくファイルならtrue</summary>
    private readonly bool _isFile;

    /// <summary>変換するtxtまたは圧縮ファイルのパス</summary>
    private readonly string _srcFile;

    /// <summary>テキストファイルの親のパス 末尾は"/"</summary>
    private readonly string _srcParentPath;

    /// <summary>Zipならzip内テキストの親entry ルートなら空文字列</summary>
    public string ArchiveTextParentPath { get; private set; } = "";

    /// <summary>出力順にファイル名を格納 imageFileInfosのkeyと同じ文字列</summary>
    private readonly List<string> _imageFileNames = new();

    /// <summary>
    /// txtならZip内の画像情報（初回にすべて読み込む）、
    /// txtでなければファイルシステムの画像情報（取得するごとに追加）
    /// </summary>
    private readonly Dictionary<string, ImageInfo> _imageFileInfos = new();

    /// <summary>
    /// 初期化。画像情報格納用のリストとマップを生成。
    /// </summary>
    /// <param name="isFile">圧縮ファイル内ならfalse</param>
    /// <param name="srcFilePath">変換するソースファイルのパス</param>
    public ImageInfoReader(bool isFile, string srcFilePath)
    {
        _isFile = isFile;
        _srcFile = srcFilePath;
        string? parent = Path.GetDirectoryName(srcFilePath);
        _srcParentPath = parent != null ? parent.Replace('\\', '/').TrimEnd('/') + "/" : "";
        ArchiveTextParentPath = "";
    }

    /// <summary>zipの場合はzip内のtxtのentryNameと親のパスを設定</summary>
    public void SetArchiveTextEntry(string archiveTextEntry)
    {
        int idx = archiveTextEntry.LastIndexOf('/');
        if (idx > -1)
            ArchiveTextParentPath = archiveTextEntry[..(idx + 1)];
    }

    /// <summary>画像ファイルパスを取得</summary>
    public string GetImageFilePath(string fileName) => _srcParentPath + fileName;

    /// <summary>パストラバーサルを防止して画像ファイルパスを取得</summary>
    public string GetImageFilePathSafe(string fileName)
    {
        string baseDir = Path.GetFullPath(_srcParentPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string resolved = Path.GetFullPath(_srcParentPath + fileName);
        if (!resolved.StartsWith(baseDir + Path.DirectorySeparatorChar) &&
            !resolved.Equals(baseDir, StringComparison.Ordinal))
            throw new IOException($"画像パスが許可されたディレクトリ外です: {fileName}");
        return resolved;
    }

    /// <summary>画像出力順にファイル名を格納</summary>
    public void AddImageFileName(string imageFileName)
    {
        _imageFileNames.Add(ArchiveTextParentPath + imageFileName);
    }

    /// <summary>名前順で並び替え</summary>
    public void SortImageFileNames()
    {
        _imageFileNames.Sort(FileNameComparer.Instance);
    }

    /// <summary>指定位置の画像ファイル名を取得</summary>
    public string GetImageFileName(int idx) => _imageFileNames[idx];

    /// <summary>画像ファイル名のリストを取得</summary>
    public List<string> GetImageFileNames() => _imageFileNames;

    /// <summary>画像情報のカウント（zip内にある画像すべて）</summary>
    public int CountImageFileInfos() => _imageFileInfos.Count;

    /// <summary>テキスト内の画像順で格納された画像ファイル名の件数</summary>
    public int CountImageFileNames() => _imageFileNames.Count;

    /// <summary>指定した順番の画像情報を取得</summary>
    public ImageInfo? GetImageInfo(int idx)
    {
        if (_imageFileNames.Count - 1 < idx) return null;
        return GetImageInfo(_imageFileNames[idx]);
    }

    /// <summary>ImageInfoを取得（拡張子違いを吸収）</summary>
    public ImageInfo? GetImageInfo(string srcImageFileName)
    {
        return InternalGetImageInfo(srcImageFileName);
    }

    /// <summary>拡張子修正を含めてImageInfoを取得</summary>
    public ImageInfo? GetCollectImageInfo(string srcImageFileName)
    {
        ImageInfo? imageInfo = InternalGetImageInfo(srcImageFileName);
        if (imageInfo == null)
        {
            string? corrected = CorrectExt(srcImageFileName);
            if (corrected != null) imageInfo = InternalGetImageInfo(corrected);
        }
        return imageInfo;
    }

    /// <summary>拡張子修正 大文字小文字は3パターンのみ試行</summary>
    public string? CorrectExt(string srcImageFileName)
    {
        if (HasImage(srcImageFileName)) return srcImageFileName;

        string[] exts = [".png", ".jpg", ".jpeg", ".gif", ".PNG", ".JPG", ".JPEG", ".GIF", ".Png", ".Jpg", ".Jpeg", ".Gif"];
        foreach (var ext in exts)
        {
            string candidate = Regex.Replace(srcImageFileName, @"\.\w+$", ext);
            if (HasImage(candidate)) return candidate;
        }
        return null;
    }

    private bool HasImage(string srcImageFileName)
    {
        if (_imageFileInfos.ContainsKey(srcImageFileName)) return true;
        if (_isFile)
            return File.Exists(_srcParentPath + srcImageFileName);
        else
            return _imageFileInfos.ContainsKey(ArchiveTextParentPath + srcImageFileName);
    }

    private ImageInfo? InternalGetImageInfo(string? srcImageFileName)
    {
        if (srcImageFileName == null) return null;

        // 取得済みならそれを返す（zipならすべて取得済み）
        if (_imageFileInfos.TryGetValue(srcImageFileName, out var cached)) return cached;

        if (_isFile)
        {
            string path = _srcParentPath + srcImageFileName;
            if (File.Exists(path))
            {
                var imageInfo = ImageInfo.GetImageInfoFromFile(path);
                if (imageInfo != null)
                {
                    _imageFileInfos[srcImageFileName] = imageInfo;
                    return imageInfo;
                }
            }
        }
        else
        {
            // Zipのサブパスから取得
            if (_imageFileInfos.TryGetValue(ArchiveTextParentPath + srcImageFileName, out var info))
                return info;
        }
        return null;
    }

    /// <summary>zip内の画像情報をすべて読み込み</summary>
    public void LoadZipImageInfos(bool addFileName)
    {
        LoadArchiveImageInfos("zip", addFileName);
    }

    /// <summary>rar内の画像情報をすべて読み込み</summary>
    public void LoadRarImageInfos(bool addFileName)
    {
        LoadArchiveImageInfos("rar", addFileName);
    }

    private void LoadArchiveImageInfos(string ext, bool addFileName)
    {
        var cache = new ArchiveCache(_srcFile, ext);
        cache.Scan();

        var imageEntries = cache.GetImageEntries();
        int idx = 0;
        foreach (var entryName in imageEntries)
        {
            if (idx++ % 10 == 0) LogAppender.Append(".");

            string sanitized;
            try
            {
                sanitized = SanitizeArchiveEntryName(entryName);
            }
            catch (ArgumentException ex)
            {
                Console.Error.WriteLine($"Skipping suspicious archive entry: {ex.Message}");
                continue;
            }

            ImageInfo? imageInfo = null;
            try
            {
                imageInfo = ReadImageInfoFromArchive(ext, sanitized);
            }
            catch (Exception)
            {
                LogAppender.Println($"画像が読み込めませんでした: {sanitized}");
            }

            if (imageInfo != null)
            {
                _imageFileInfos[sanitized] = imageInfo;
                if (addFileName) AddImageFileName(sanitized);
            }
        }
        LogAppender.Println("");
    }

    private ImageInfo? ReadImageInfoFromArchive(string ext, string entryName)
    {
        if (ext == "zip" || ext == "txtz")
        {
            using var fs = new FileStream(_srcFile, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var zip = new ZipArchive(fs, ZipArchiveMode.Read);
            foreach (var entry in zip.Entries)
            {
                if (entry.FullName == entryName)
                {
                    // Zip entry streams don't support seeking, buffer to MemoryStream first
                    using var entryStream = entry.Open();
                    using var ms = new MemoryStream(ArchiveCache.ReadFully(entryStream));
                    return ImageInfo.GetImageInfo(ms);
                }
            }
        }
        else if (ext == "rar")
        {
            using var archive = RarArchive.OpenArchive(new FileInfo(_srcFile), null);
            foreach (var entry in archive.Entries)
            {
                if (entry.IsDirectory) continue;
                string rarEntryName = (entry.Key ?? "").Replace('\\', '/');
                if (rarEntryName == entryName)
                {
                    using var entryStream = entry.OpenEntryStream();
                    using var ms = new MemoryStream(ArchiveCache.ReadFully(entryStream));
                    return ImageInfo.GetImageInfo(ms);
                }
            }
        }
        return null;
    }

    /// <summary>圧縮ファイル内の画像で画像注記以外の画像も表紙に選択できるように追加</summary>
    public void AddNoNameImageFileName()
    {
        var existing = new HashSet<string>(_imageFileNames);
        var names = new List<string>();
        foreach (var name in _imageFileInfos.Keys)
        {
            if (!existing.Contains(name)) names.Add(name);
        }
        names.Sort(FileNameComparer.Instance);
        foreach (var name in names) _imageFileNames.Add(name);
    }

    /// <summary>
    /// Path Traversal 脆弱性対策。
    /// アーカイブentryのパス名を安全に正規化する。
    /// </summary>
    public static string SanitizeArchiveEntryName(string entryName)
    {
        if (string.IsNullOrEmpty(entryName))
            throw new ArgumentException("Entry name cannot be null or empty");

        if (entryName[0] == '/' || entryName[0] == '\\')
            throw new ArgumentException($"Absolute path detected in archive entry: {entryName}");

        if (entryName.Contains("..") || entryName.Contains('~') || entryName.Contains('$'))
            throw new ArgumentException($"Path traversal attempt detected: {entryName}");

        string normalized = entryName.Replace('\\', '/');
        while (normalized.Contains("//"))
            normalized = normalized.Replace("//", "/");

        return normalized;
    }

    /// <summary>指定した順番の画像データを取得</summary>
    public byte[]? GetImageBytes(int idx)
    {
        if (idx >= _imageFileNames.Count) return null;
        return GetImageBytes(_imageFileNames[idx]);
    }

    /// <summary>
    /// ファイル名から画像データを取得。
    /// ファイルシステムまたはZip/Rarファイルから指定されたファイル名の画像バイト列を返す。
    /// </summary>
    public byte[]? GetImageBytes(string srcImageFileName)
    {
        if (_isFile)
        {
            string path = _srcParentPath + srcImageFileName;
            if (!File.Exists(path))
            {
                string? corrected = CorrectExt(srcImageFileName);
                if (corrected == null) return null;
                path = _srcParentPath + corrected;
                if (!File.Exists(path)) return null;
            }
            return File.ReadAllBytes(path);
        }
        else
        {
            if (_srcFile.EndsWith(".rar", StringComparison.OrdinalIgnoreCase))
            {
                using var archive = RarArchive.OpenArchive(new FileInfo(_srcFile), null);
                foreach (var entry in archive.Entries)
                {
                    if (entry.IsDirectory) continue;
                    string entryName = (entry.Key ?? "").Replace('\\', '/');
                    if (srcImageFileName == entryName)
                    {
                        using var stream = entry.OpenEntryStream();
                        return ArchiveCache.ReadFully(stream);
                    }
                }
            }
            else
            {
                // ZIP
                using var fs = new FileStream(_srcFile, FileMode.Open, FileAccess.Read, FileShare.Read);
                using var zip = new ZipArchive(fs, ZipArchiveMode.Read);

                // 完全一致
                var entry = zip.GetEntry(srcImageFileName);
                if (entry != null)
                {
                    using var stream = entry.Open();
                    return ArchiveCache.ReadFully(stream);
                }

                // 大文字小文字を無視して検索
                foreach (var e in zip.Entries)
                {
                    if (string.Equals(e.FullName, srcImageFileName, StringComparison.OrdinalIgnoreCase))
                    {
                        using var stream = e.Open();
                        return ArchiveCache.ReadFully(stream);
                    }
                }
            }

            // 拡張子を修正して再試行
            string? correctedExt = CorrectExt(srcImageFileName);
            if (correctedExt != null && correctedExt != srcImageFileName)
                return GetImageBytes(correctedExt);
        }
        return null;
    }
}
