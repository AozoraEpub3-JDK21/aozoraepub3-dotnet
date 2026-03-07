using System.IO;
using System.IO.Compression;
using System.Xml.Linq;

namespace AozoraEpub3.Gui.Services;

/// <summary>
/// EPUB ファイルを一時ディレクトリに展開し、目次・コンテンツ情報を提供する。
/// </summary>
public sealed class EpubPreviewService : IDisposable
{
    private string? _extractDir;

    /// <summary>展開先ディレクトリ</summary>
    public string? ExtractDir => _extractDir;

    /// <summary>EPUB内のスパイン順コンテンツファイルパス（絶対パス）</summary>
    public List<SpineItem> SpineItems { get; } = [];

    /// <summary>目次項目</summary>
    public List<TocEntry> TocEntries { get; } = [];

    /// <summary>書籍タイトル</summary>
    public string Title { get; private set; } = "";

    /// <summary>
    /// EPUB を展開してメタデータを読み込む。
    /// </summary>
    public void Open(string epubPath)
    {
        Close();

        _extractDir = Path.Combine(Path.GetTempPath(), "AozoraEpub3Preview", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_extractDir);

        ZipFile.ExtractToDirectory(epubPath, _extractDir);

        // container.xml からルートファイル（OPF）を特定
        var containerPath = Path.Combine(_extractDir, "META-INF", "container.xml");
        if (!File.Exists(containerPath))
            throw new InvalidOperationException("container.xml not found in EPUB.");

        var containerDoc = XDocument.Load(containerPath);
        XNamespace cns = "urn:oasis:names:tc:opendocument:xmlns:container";
        var rootFilePath = containerDoc.Descendants(cns + "rootfile")
            .FirstOrDefault()?.Attribute("full-path")?.Value
            ?? throw new InvalidOperationException("rootfile not found in container.xml.");

        var opfFullPath = Path.Combine(_extractDir, rootFilePath.Replace('/', Path.DirectorySeparatorChar));
        var opfDir = Path.GetDirectoryName(opfFullPath)!;

        // OPF（package document）を解析
        var opfDoc = XDocument.Load(opfFullPath);
        XNamespace opfNs = "http://www.idpf.org/2007/opf";
        XNamespace dcNs = "http://purl.org/dc/elements/1.1/";

        // タイトル取得
        Title = opfDoc.Descendants(dcNs + "title").FirstOrDefault()?.Value ?? "";

        // manifest: id → href マッピング
        var manifest = new Dictionary<string, string>();
        var manifestMediaTypes = new Dictionary<string, string>();
        foreach (var item in opfDoc.Descendants(opfNs + "item"))
        {
            var id = item.Attribute("id")?.Value;
            var href = item.Attribute("href")?.Value;
            var mediaType = item.Attribute("media-type")?.Value;
            if (id != null && href != null)
            {
                manifest[id] = href;
                if (mediaType != null)
                    manifestMediaTypes[id] = mediaType;
            }
        }

        // spine: 読み順でXHTMLファイルを列挙
        SpineItems.Clear();
        foreach (var itemref in opfDoc.Descendants(opfNs + "itemref"))
        {
            var idref = itemref.Attribute("idref")?.Value;
            if (idref != null && manifest.TryGetValue(idref, out var href))
            {
                var absPath = Path.GetFullPath(Path.Combine(opfDir, href.Replace('/', Path.DirectorySeparatorChar)));
                SpineItems.Add(new SpineItem(idref, href, absPath));
            }
        }

        // nav.xhtml（EPUB3 目次）を探す
        TocEntries.Clear();
        var navId = opfDoc.Descendants(opfNs + "item")
            .FirstOrDefault(e => e.Attribute("properties")?.Value?.Contains("nav") == true)
            ?.Attribute("id")?.Value;

        if (navId != null && manifest.TryGetValue(navId, out var navHref))
        {
            var navPath = Path.GetFullPath(Path.Combine(opfDir, navHref.Replace('/', Path.DirectorySeparatorChar)));
            if (File.Exists(navPath))
                ParseNavDocument(navPath, opfDir);
        }

        // nav.xhtml が無い場合は toc.ncx にフォールバック
        if (TocEntries.Count == 0)
        {
            var ncxId = opfDoc.Descendants(opfNs + "spine")
                .FirstOrDefault()?.Attribute("toc")?.Value;
            if (ncxId != null && manifest.TryGetValue(ncxId, out var ncxHref))
            {
                var ncxPath = Path.GetFullPath(Path.Combine(opfDir, ncxHref.Replace('/', Path.DirectorySeparatorChar)));
                if (File.Exists(ncxPath))
                    ParseNcx(ncxPath, opfDir);
            }
        }

        // 目次が空の場合はスパインからファイル名で生成
        if (TocEntries.Count == 0)
        {
            for (int i = 0; i < SpineItems.Count; i++)
                TocEntries.Add(new TocEntry(Path.GetFileNameWithoutExtension(SpineItems[i].Href), SpineItems[i].Href, i));
        }
        else
        {
            // nav に無いスパインページを補完（目次ページ、あらすじ等）
            FillMissingSpineEntries();
        }
    }

    private void ParseNavDocument(string navPath, string opfDir)
    {
        var doc = XDocument.Load(navPath);
        XNamespace xhtml = "http://www.w3.org/1999/xhtml";
        XNamespace epub = "http://www.idpf.org/2007/ops";

        // <nav epub:type="toc"> 内の <a> 要素を取得
        var tocNav = doc.Descendants(xhtml + "nav")
            .FirstOrDefault(n => n.Attribute(epub + "type")?.Value == "toc");

        if (tocNav == null) return;

        foreach (var a in tocNav.Descendants(xhtml + "a"))
        {
            var href = a.Attribute("href")?.Value;
            var label = a.Value.Trim();
            if (string.IsNullOrEmpty(href)) continue;

            // href からフラグメントを除去してスパインインデックスを特定
            var hrefNoFragment = href.Contains('#') ? href[..href.IndexOf('#')] : href;
            var navDir = Path.GetDirectoryName(navPath)!;
            var resolvedHref = Path.GetRelativePath(opfDir,
                Path.GetFullPath(Path.Combine(navDir, hrefNoFragment.Replace('/', Path.DirectorySeparatorChar))));
            resolvedHref = resolvedHref.Replace(Path.DirectorySeparatorChar, '/');

            var spineIndex = SpineItems.FindIndex(s => s.Href == resolvedHref);
            TocEntries.Add(new TocEntry(label, href, spineIndex >= 0 ? spineIndex : 0));
        }
    }

    private void ParseNcx(string ncxPath, string opfDir)
    {
        var doc = XDocument.Load(ncxPath);
        XNamespace ncxNs = "http://www.daisy.org/z3986/2005/ncx/";

        foreach (var navPoint in doc.Descendants(ncxNs + "navPoint"))
        {
            var label = navPoint.Element(ncxNs + "navLabel")?.Element(ncxNs + "text")?.Value?.Trim() ?? "";
            var src = navPoint.Element(ncxNs + "content")?.Attribute("src")?.Value;
            if (string.IsNullOrEmpty(src)) continue;

            var srcNoFragment = src.Contains('#') ? src[..src.IndexOf('#')] : src;
            var ncxDir = Path.GetDirectoryName(ncxPath)!;
            var resolvedHref = Path.GetRelativePath(opfDir,
                Path.GetFullPath(Path.Combine(ncxDir, srcNoFragment.Replace('/', Path.DirectorySeparatorChar))));
            resolvedHref = resolvedHref.Replace(Path.DirectorySeparatorChar, '/');

            var spineIndex = SpineItems.FindIndex(s => s.Href == resolvedHref);
            TocEntries.Add(new TocEntry(label, src, spineIndex >= 0 ? spineIndex : 0));
        }
    }

    /// <summary>nav に含まれないスパインページを目次に挿入する。</summary>
    private void FillMissingSpineEntries()
    {
        var coveredIndices = TocEntries.Select(t => t.SpineIndex).ToHashSet();
        // 既知のファイル名→表示ラベルのマッピング
        var knownLabels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["nav"] = "目次",
            ["toc"] = "目次",
        };

        for (int i = 0; i < SpineItems.Count; i++)
        {
            if (coveredIndices.Contains(i)) continue;

            var fileName = Path.GetFileNameWithoutExtension(SpineItems[i].Href);
            var label = knownLabels.TryGetValue(fileName, out var known)
                ? known
                : fileName; // "0001" 等のファイル名をそのまま使う

            // 適切な挿入位置を探す（スパインインデックス順を維持）
            var insertAt = 0;
            for (int j = 0; j < TocEntries.Count; j++)
            {
                if (TocEntries[j].SpineIndex > i) break;
                insertAt = j + 1;
            }
            TocEntries.Insert(insertAt, new TocEntry($"[{label}]", SpineItems[i].Href, i));
        }
    }

    /// <summary>指定スパインインデックスのXHTMLファイルの file:// URI を返す。</summary>
    public Uri? GetPageUri(int spineIndex)
    {
        if (spineIndex < 0 || spineIndex >= SpineItems.Count) return null;
        var path = SpineItems[spineIndex].AbsolutePath;
        return File.Exists(path) ? new Uri(path) : null;
    }

    /// <summary>展開データをクリーンアップする。</summary>
    public void Close()
    {
        SpineItems.Clear();
        TocEntries.Clear();
        Title = "";

        if (_extractDir != null && Directory.Exists(_extractDir))
        {
            try { Directory.Delete(_extractDir, recursive: true); } catch { }
            _extractDir = null;
        }
    }

    /// <summary>展開済み EPUB 内の CSS ファイルパスを返す（OPS/css/ 配下）。</summary>
    public List<CssFileInfo> GetCssFiles()
    {
        var result = new List<CssFileInfo>();
        if (_extractDir == null) return result;

        // OPF から CSS manifest items を探す
        var containerPath = Path.Combine(_extractDir, "META-INF", "container.xml");
        if (!File.Exists(containerPath)) return result;

        var containerDoc = XDocument.Load(containerPath);
        XNamespace cns = "urn:oasis:names:tc:opendocument:xmlns:container";
        var rootFilePath = containerDoc.Descendants(cns + "rootfile")
            .FirstOrDefault()?.Attribute("full-path")?.Value;
        if (rootFilePath == null) return result;

        var opfFullPath = Path.Combine(_extractDir, rootFilePath.Replace('/', Path.DirectorySeparatorChar));
        var opfDir = Path.GetDirectoryName(opfFullPath)!;

        var opfDoc = XDocument.Load(opfFullPath);
        XNamespace opfNs = "http://www.idpf.org/2007/opf";

        foreach (var item in opfDoc.Descendants(opfNs + "item"))
        {
            var mediaType = item.Attribute("media-type")?.Value;
            var href = item.Attribute("href")?.Value;
            if (mediaType == "text/css" && href != null)
            {
                var absPath = Path.GetFullPath(Path.Combine(opfDir, href.Replace('/', Path.DirectorySeparatorChar)));
                if (File.Exists(absPath))
                    result.Add(new CssFileInfo(href, absPath));
            }
        }

        // OPF manifest に無い場合でも css/ ディレクトリ内を探す
        if (result.Count == 0)
        {
            var cssDir = Path.Combine(opfDir, "css");
            if (Directory.Exists(cssDir))
            {
                foreach (var f in Directory.GetFiles(cssDir, "*.css"))
                    result.Add(new CssFileInfo(Path.GetFileName(f), f));
            }
        }

        return result;
    }

    /// <summary>EPUB が縦書きかどうかを CSS から判定する。</summary>
    public bool IsVertical()
    {
        var cssFiles = GetCssFiles();
        foreach (var css in cssFiles)
        {
            var content = File.ReadAllText(css.AbsolutePath);
            if (content.Contains("vertical-rl"))
                return true;
        }
        return false;
    }

    public void Dispose() => Close();
}

/// <summary>スパインの1項目</summary>
public record SpineItem(string Id, string Href, string AbsolutePath);

/// <summary>目次の1項目</summary>
public record TocEntry(string Label, string Href, int SpineIndex);

/// <summary>EPUB内CSSファイル情報</summary>
public record CssFileInfo(string Href, string AbsolutePath);
