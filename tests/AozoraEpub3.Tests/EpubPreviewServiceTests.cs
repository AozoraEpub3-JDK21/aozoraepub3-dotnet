using System.IO.Compression;
using AozoraEpub3.Gui.Services;

namespace AozoraEpub3.Tests;

public class EpubPreviewServiceTests : IDisposable
{
    private readonly List<string> _tempFiles = [];

    public void Dispose()
    {
        foreach (var f in _tempFiles)
        {
            try { if (File.Exists(f)) File.Delete(f); } catch { }
        }
    }

    /// <summary>
    /// Creates a minimal valid EPUB file for testing and returns its path.
    /// If <paramref name="includeNav"/> is false, the nav.xhtml and its manifest/spine entry are omitted
    /// so that the service falls back to toc.ncx parsing.
    /// </summary>
    private string CreateTestEpub(bool includeNav = true)
    {
        var path = Path.Combine(Path.GetTempPath(), $"EpubPreviewTest_{Guid.NewGuid():N}.epub");
        _tempFiles.Add(path);

        using var fs = new FileStream(path, FileMode.Create, FileAccess.ReadWrite);
        using var zip = new ZipArchive(fs, ZipArchiveMode.Create, leaveOpen: false);

        // mimetype (must be first, uncompressed)
        var mimetypeEntry = zip.CreateEntry("mimetype", CompressionLevel.NoCompression);
        using (var w = new StreamWriter(mimetypeEntry.Open()))
            w.Write("application/epub+zip");

        // META-INF/container.xml
        AddTextEntry(zip, "META-INF/container.xml", """
            <?xml version="1.0" encoding="UTF-8"?>
            <container version="1.0" xmlns="urn:oasis:names:tc:opendocument:xmlns:container">
              <rootfiles>
                <rootfile full-path="OPS/content.opf" media-type="application/oebps-package+xml"/>
              </rootfiles>
            </container>
            """);

        // OPS/content.opf
        var navManifestItem = includeNav
            ? """<item id="nav" href="nav.xhtml" media-type="application/xhtml+xml" properties="nav"/>"""
            : "";

        AddTextEntry(zip, "OPS/content.opf", $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <package xmlns="http://www.idpf.org/2007/opf" version="3.0" unique-identifier="uid">
              <metadata xmlns:dc="http://purl.org/dc/elements/1.1/">
                <dc:title>Test Book</dc:title>
                <dc:identifier id="uid">urn:uuid:12345</dc:identifier>
                <dc:language>ja</dc:language>
              </metadata>
              <manifest>
                <item id="cover" href="cover.xhtml" media-type="application/xhtml+xml"/>
                <item id="ch1" href="chapter1.xhtml" media-type="application/xhtml+xml"/>
                <item id="ch2" href="chapter2.xhtml" media-type="application/xhtml+xml"/>
                <item id="ncx" href="toc.ncx" media-type="application/x-dtbncx+xml"/>
                {navManifestItem}
              </manifest>
              <spine toc="ncx">
                <itemref idref="cover"/>
                <itemref idref="ch1"/>
                <itemref idref="ch2"/>
              </spine>
            </package>
            """);

        // OPS/toc.ncx
        AddTextEntry(zip, "OPS/toc.ncx", """
            <?xml version="1.0" encoding="UTF-8"?>
            <ncx xmlns="http://www.daisy.org/z3986/2005/ncx/" version="2005-1">
              <head>
                <meta name="dtb:uid" content="urn:uuid:12345"/>
              </head>
              <docTitle><text>Test Book</text></docTitle>
              <navMap>
                <navPoint id="np1" playOrder="1">
                  <navLabel><text>Chapter 1</text></navLabel>
                  <content src="chapter1.xhtml"/>
                </navPoint>
                <navPoint id="np2" playOrder="2">
                  <navLabel><text>Chapter 2</text></navLabel>
                  <content src="chapter2.xhtml"/>
                </navPoint>
              </navMap>
            </ncx>
            """);

        // OPS/nav.xhtml
        if (includeNav)
        {
            AddTextEntry(zip, "OPS/nav.xhtml", """
                <?xml version="1.0" encoding="UTF-8"?>
                <html xmlns="http://www.w3.org/1999/xhtml" xmlns:epub="http://www.idpf.org/2007/ops">
                <head><title>Navigation</title></head>
                <body>
                  <nav epub:type="toc">
                    <ol>
                      <li><a href="chapter1.xhtml">Chapter 1</a></li>
                      <li><a href="chapter2.xhtml">Chapter 2</a></li>
                    </ol>
                  </nav>
                </body>
                </html>
                """);
        }

        // Minimal XHTML content pages
        AddXhtmlPage(zip, "OPS/cover.xhtml", "Cover");
        AddXhtmlPage(zip, "OPS/chapter1.xhtml", "Chapter 1 Content");
        AddXhtmlPage(zip, "OPS/chapter2.xhtml", "Chapter 2 Content");

        return path;
    }

    private static void AddTextEntry(ZipArchive zip, string entryName, string content)
    {
        var entry = zip.CreateEntry(entryName);
        using var w = new StreamWriter(entry.Open());
        w.Write(content);
    }

    private static void AddXhtmlPage(ZipArchive zip, string entryName, string bodyText)
    {
        AddTextEntry(zip, entryName, $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <html xmlns="http://www.w3.org/1999/xhtml">
            <head><title>{bodyText}</title></head>
            <body><p>{bodyText}</p></body>
            </html>
            """);
    }

    [Fact]
    public void Open_ParsesSpineCorrectly()
    {
        // Arrange
        var epubPath = CreateTestEpub();
        using var service = new EpubPreviewService();

        // Act
        service.Open(epubPath);

        // Assert
        Assert.Equal(3, service.SpineItems.Count);
        Assert.Equal("cover", service.SpineItems[0].Id);
        Assert.Equal("cover.xhtml", service.SpineItems[0].Href);
        Assert.Equal("ch1", service.SpineItems[1].Id);
        Assert.Equal("chapter1.xhtml", service.SpineItems[1].Href);
        Assert.Equal("ch2", service.SpineItems[2].Id);
        Assert.Equal("chapter2.xhtml", service.SpineItems[2].Href);
    }

    [Fact]
    public void Open_ParsesTocFromNav()
    {
        // Arrange
        var epubPath = CreateTestEpub(includeNav: true);
        using var service = new EpubPreviewService();

        // Act
        service.Open(epubPath);

        // Assert — nav に2件 + スパイン補完1件（cover）= 3件
        Assert.Equal(3, service.TocEntries.Count);
        Assert.Equal("[cover]", service.TocEntries[0].Label);
        Assert.Equal(0, service.TocEntries[0].SpineIndex);
        Assert.Equal("Chapter 1", service.TocEntries[1].Label);
        Assert.Equal("chapter1.xhtml", service.TocEntries[1].Href);
        Assert.Equal(1, service.TocEntries[1].SpineIndex);
        Assert.Equal("Chapter 2", service.TocEntries[2].Label);
        Assert.Equal("chapter2.xhtml", service.TocEntries[2].Href);
        Assert.Equal(2, service.TocEntries[2].SpineIndex);
    }

    [Fact]
    public void Open_ParsesTitle()
    {
        // Arrange
        var epubPath = CreateTestEpub();
        using var service = new EpubPreviewService();

        // Act
        service.Open(epubPath);

        // Assert
        Assert.Equal("Test Book", service.Title);
    }

    [Fact]
    public void GetPageUri_ReturnsValidUri()
    {
        // Arrange
        var epubPath = CreateTestEpub();
        using var service = new EpubPreviewService();
        service.Open(epubPath);

        // Act & Assert — each spine item should produce a file:// URI
        for (int i = 0; i < service.SpineItems.Count; i++)
        {
            var uri = service.GetPageUri(i);
            Assert.NotNull(uri);
            Assert.Equal("file", uri!.Scheme);
            Assert.True(File.Exists(uri.LocalPath), $"File should exist at {uri.LocalPath}");
        }

        // Out-of-range indices return null
        Assert.Null(service.GetPageUri(-1));
        Assert.Null(service.GetPageUri(service.SpineItems.Count));
    }

    [Fact]
    public void Open_FallbackToNcx_WhenNoNav()
    {
        // Arrange — create EPUB without nav.xhtml
        var epubPath = CreateTestEpub(includeNav: false);
        using var service = new EpubPreviewService();

        // Act
        service.Open(epubPath);

        // Assert — ncx に2件 + スパイン補完1件（cover）= 3件
        Assert.Equal(3, service.TocEntries.Count);
        Assert.Equal("[cover]", service.TocEntries[0].Label);
        Assert.Equal(0, service.TocEntries[0].SpineIndex);
        Assert.Equal("Chapter 1", service.TocEntries[1].Label);
        Assert.Equal("chapter1.xhtml", service.TocEntries[1].Href);
        Assert.Equal(1, service.TocEntries[1].SpineIndex);
        Assert.Equal("Chapter 2", service.TocEntries[2].Label);
        Assert.Equal("chapter2.xhtml", service.TocEntries[2].Href);
        Assert.Equal(2, service.TocEntries[2].SpineIndex);
    }

    [Fact]
    public void Close_CleansUpTempDirectory()
    {
        // Arrange
        var epubPath = CreateTestEpub();
        var service = new EpubPreviewService();
        service.Open(epubPath);

        var extractDir = service.ExtractDir;
        Assert.NotNull(extractDir);
        Assert.True(Directory.Exists(extractDir), "Extract dir should exist after Open.");

        // Act
        service.Close();

        // Assert
        Assert.False(Directory.Exists(extractDir), "Extract dir should be deleted after Close.");
        Assert.Null(service.ExtractDir);
        Assert.Empty(service.SpineItems);
        Assert.Empty(service.TocEntries);
        Assert.Equal("", service.Title);
    }
}
