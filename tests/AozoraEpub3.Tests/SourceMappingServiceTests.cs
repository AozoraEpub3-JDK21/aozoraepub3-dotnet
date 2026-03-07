using System.IO.Compression;
using AozoraEpub3.Gui.Services;

namespace AozoraEpub3.Tests;

public class SourceMappingServiceTests : IDisposable
{
    private readonly List<string> _tempFiles = [];
    private readonly List<string> _tempDirs = [];

    public void Dispose()
    {
        foreach (var f in _tempFiles)
            try { if (File.Exists(f)) File.Delete(f); } catch { }
        foreach (var d in _tempDirs)
            try { if (Directory.Exists(d)) Directory.Delete(d, true); } catch { }
    }

    private string CreateTestEpub()
    {
        var path = Path.Combine(Path.GetTempPath(), $"SourceMapTest_{Guid.NewGuid():N}.epub");
        _tempFiles.Add(path);

        using var fs = new FileStream(path, FileMode.Create, FileAccess.ReadWrite);
        using var zip = new ZipArchive(fs, ZipArchiveMode.Create, leaveOpen: false);

        var mimetypeEntry = zip.CreateEntry("mimetype", CompressionLevel.NoCompression);
        using (var w = new StreamWriter(mimetypeEntry.Open()))
            w.Write("application/epub+zip");

        AddEntry(zip, "META-INF/container.xml", """
            <?xml version="1.0" encoding="UTF-8"?>
            <container version="1.0" xmlns="urn:oasis:names:tc:opendocument:xmlns:container">
              <rootfiles>
                <rootfile full-path="OPS/content.opf" media-type="application/oebps-package+xml"/>
              </rootfiles>
            </container>
            """);

        AddEntry(zip, "OPS/content.opf", """
            <?xml version="1.0" encoding="UTF-8"?>
            <package xmlns="http://www.idpf.org/2007/opf" version="3.0" unique-identifier="uid">
              <metadata xmlns:dc="http://purl.org/dc/elements/1.1/">
                <dc:title>Test</dc:title>
                <dc:identifier id="uid">urn:uuid:test</dc:identifier>
                <dc:language>ja</dc:language>
              </metadata>
              <manifest>
                <item id="ch1" href="ch1.xhtml" media-type="application/xhtml+xml"/>
                <item id="ch2" href="ch2.xhtml" media-type="application/xhtml+xml"/>
                <item id="nav" href="nav.xhtml" media-type="application/xhtml+xml" properties="nav"/>
              </manifest>
              <spine>
                <itemref idref="ch1"/>
                <itemref idref="ch2"/>
              </spine>
            </package>
            """);

        AddEntry(zip, "OPS/nav.xhtml", """
            <?xml version="1.0" encoding="UTF-8"?>
            <html xmlns="http://www.w3.org/1999/xhtml" xmlns:epub="http://www.idpf.org/2007/ops">
            <head><title>Nav</title></head>
            <body>
              <nav epub:type="toc"><ol>
                <li><a href="ch1.xhtml">Chapter 1</a></li>
                <li><a href="ch2.xhtml">Chapter 2</a></li>
              </ol></nav>
            </body></html>
            """);

        AddEntry(zip, "OPS/ch1.xhtml", """
            <?xml version="1.0" encoding="UTF-8"?>
            <html xmlns="http://www.w3.org/1999/xhtml">
            <head><title>Ch1</title></head>
            <body>
            <h1 id="title1">Chapter 1</h1>
            <p>First paragraph.</p>
            <p>Second paragraph.</p>
            </body></html>
            """);

        AddEntry(zip, "OPS/ch2.xhtml", """
            <?xml version="1.0" encoding="UTF-8"?>
            <html xmlns="http://www.w3.org/1999/xhtml">
            <head><title>Ch2</title></head>
            <body>
            <p>Content of chapter 2.</p>
            <img src="image.png"/>
            </body></html>
            """);

        return path;
    }

    private static void AddEntry(ZipArchive zip, string name, string content)
    {
        using var w = new StreamWriter(zip.CreateEntry(name).Open());
        w.Write(content);
    }

    [Fact]
    public void GetMetaFilePath_AppendsMetaJson()
    {
        var result = SourceMappingService.GetMetaFilePath("/path/to/book.epub");
        Assert.Equal("/path/to/book.epub.meta.json", result);
    }

    [Fact]
    public void GenerateFromEpub_CreatesChapterMappings()
    {
        var epub = CreateTestEpub();
        using var service = new EpubPreviewService();
        service.Open(epub);

        var map = SourceMappingService.GenerateFromEpub(service, epub);

        Assert.Equal(2, map.Chapters.Count);
        Assert.Equal("ch1", map.Chapters[0].Id);
        Assert.Equal("ch1.xhtml", map.Chapters[0].Href);
        Assert.Equal(0, map.Chapters[0].SpineIndex);
        Assert.Equal("ch2", map.Chapters[1].Id);
        Assert.Equal(1, map.Chapters[1].SpineIndex);
    }

    [Fact]
    public void GenerateFromEpub_IncludesTocLabels()
    {
        var epub = CreateTestEpub();
        using var service = new EpubPreviewService();
        service.Open(epub);

        var map = SourceMappingService.GenerateFromEpub(service, epub);

        Assert.Equal("Chapter 1", map.Chapters[0].TocLabel);
        Assert.Equal("Chapter 2", map.Chapters[1].TocLabel);
    }

    [Fact]
    public void GenerateFromEpub_ExtractsElements()
    {
        var epub = CreateTestEpub();
        using var service = new EpubPreviewService();
        service.Open(epub);

        var map = SourceMappingService.GenerateFromEpub(service, epub);

        // Ch1 should have h1 + 2 p elements
        var ch1Elements = map.Chapters[0].Elements;
        Assert.NotNull(ch1Elements);
        Assert.True(ch1Elements!.Count >= 3);
        Assert.Contains(ch1Elements, e => e.Tag == "h1" && e.Id == "title1");
        Assert.Contains(ch1Elements, e => e.Tag == "p" && e.Text == "First paragraph.");

        // Ch2 should have p + img
        var ch2Elements = map.Chapters[1].Elements;
        Assert.NotNull(ch2Elements);
        Assert.Contains(ch2Elements!, e => e.Tag == "p");
        Assert.Contains(ch2Elements!, e => e.Tag == "img" && e.Text == "image.png");
    }

    [Fact]
    public void SaveAndLoad_Roundtrip()
    {
        var epub = CreateTestEpub();
        using var service = new EpubPreviewService();
        service.Open(epub);

        var map = SourceMappingService.GenerateFromEpub(service, epub);
        var metaPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid():N}.meta.json");
        _tempFiles.Add(metaPath);

        SourceMappingService.Save(map, metaPath);
        var loaded = SourceMappingService.Load(metaPath);

        Assert.NotNull(loaded);
        Assert.Equal(map.Chapters.Count, loaded!.Chapters.Count);
        Assert.Equal(map.Chapters[0].Id, loaded.Chapters[0].Id);
        Assert.Equal(map.Chapters[0].Href, loaded.Chapters[0].Href);
        Assert.Equal(map.EpubFileName, loaded.EpubFileName);
    }

    [Fact]
    public void Load_ReturnsNull_WhenFileNotFound()
    {
        var result = SourceMappingService.Load("/nonexistent/path.meta.json");
        Assert.Null(result);
    }

    [Fact]
    public void GenerateFromEpub_SetsLineCount()
    {
        var epub = CreateTestEpub();
        using var service = new EpubPreviewService();
        service.Open(epub);

        var map = SourceMappingService.GenerateFromEpub(service, epub);

        Assert.True(map.Chapters[0].LineCount > 0);
        Assert.True(map.Chapters[1].LineCount > 0);
    }
}
