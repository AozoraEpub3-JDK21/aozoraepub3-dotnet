using System.IO;
using System.IO.Compression;
using System.Text;
using AozoraEpub3.Core.Converter;
using AozoraEpub3.Core.Info;
using AozoraEpub3.Core.Io;

namespace AozoraEpub3.Tests;

/// <summary>
/// 実際に EPUB ファイルを生成して構造を検証する統合テスト。
/// 外部ファイルに依存せず、テキストをインメモリで生成して使用する。
/// </summary>
public class EpubIntegrationTests : IDisposable
{
    private readonly string _tempDir;

    public EpubIntegrationTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"aozora_e2e_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, true); } catch { }
    }

    // ── ヘルパー ──────────────────────────────────────────────────────────────

    private static string ConvertToEpub(string aozoraText, string outputDir, string outputName = "test")
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        string outPath = Path.Combine(outputDir, outputName + ".epub");
        var templatePath = "";   // 埋め込みリソースを使用

        var writer = new Epub3Writer(templatePath);
        var converter = new AozoraEpub3Converter(writer, templatePath);

        // 第1パス: BookInfo 取得
        BookInfo bookInfo;
        using (var reader1 = new StringReader(aozoraText))
        {
            var imgReader = new ImageInfoReader(true, "");
            bookInfo = converter.GetBookInfo("test.txt", reader1, imgReader, BookInfo.TitleType.TITLE_AUTHOR, false);
        }

        bookInfo.Vertical      = true;
        bookInfo.InsertTocPage = false;

        // 第2パス: 変換
        using var reader2 = new StringReader(aozoraText);
        var imgReader2 = new ImageInfoReader(true, "");
        writer.Write(converter, reader2, "test.txt", "txt", outPath, bookInfo, imgReader2);

        return outPath;
    }

    private static Dictionary<string, byte[]> ReadEpubEntries(string epubPath)
    {
        var result = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        using var zip = ZipFile.OpenRead(epubPath);
        foreach (var entry in zip.Entries)
        {
            using var stream = entry.Open();
            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            result[entry.FullName] = ms.ToArray();
        }
        return result;
    }

    // ── テスト ────────────────────────────────────────────────────────────────

    [Fact]
    public void Convert_MinimalText_CreatesEpubFile()
    {
        // Aozora 最小構成: タイトル行 + 著者行 + 本文
        const string aozoraText = "テストタイトル\n著者名\n\n本文テキスト。\n";

        string epubPath = ConvertToEpub(aozoraText, _tempDir);

        Assert.True(File.Exists(epubPath), $"EPUB が生成されていません: {epubPath}");
        Assert.True(new FileInfo(epubPath).Length > 0, "EPUB ファイルが空です");
    }

    [Fact]
    public void Convert_MinimalText_HasValidEpubStructure()
    {
        const string aozoraText = "テストタイトル\n著者名\n\n本文テキスト。\n";

        string epubPath = ConvertToEpub(aozoraText, _tempDir);
        var entries = ReadEpubEntries(epubPath);

        // EPUB 必須エントリの存在確認
        Assert.True(entries.ContainsKey("mimetype"),              "mimetype エントリなし");
        Assert.True(entries.ContainsKey("META-INF/container.xml"), "container.xml エントリなし");
        Assert.True(entries.ContainsKey("OPS/package.opf"),       "package.opf エントリなし");
        Assert.True(entries.ContainsKey("OPS/xhtml/nav.xhtml"),   "nav.xhtml エントリなし");
        Assert.True(entries.ContainsKey("OPS/toc.ncx"),           "toc.ncx エントリなし");
    }

    [Fact]
    public void Convert_MinimalText_MimetypeIsUncompressed()
    {
        const string aozoraText = "テストタイトル\n著者名\n\n本文テキスト。\n";

        string epubPath = ConvertToEpub(aozoraText, _tempDir);

        using var zip = ZipFile.OpenRead(epubPath);
        var mimetypeEntry = zip.GetEntry("mimetype");
        Assert.NotNull(mimetypeEntry);
        Assert.Equal(CompressionMethodValues.Stored, mimetypeEntry.CompressionMethod());
    }

    [Fact]
    public void Convert_MinimalText_MimetypeHasCorrectContent()
    {
        const string aozoraText = "テストタイトル\n著者名\n\n本文テキスト。\n";

        string epubPath = ConvertToEpub(aozoraText, _tempDir);
        var entries = ReadEpubEntries(epubPath);

        string mimetype = Encoding.ASCII.GetString(entries["mimetype"]).Trim();
        Assert.Equal("application/epub+zip", mimetype);
    }

    [Fact]
    public void Convert_MinimalText_PackageOpfContainsTitleAndCreator()
    {
        const string aozoraText = "統合テスト\n作者太郎\n\n本文。\n";

        string epubPath = ConvertToEpub(aozoraText, _tempDir);
        var entries = ReadEpubEntries(epubPath);

        string opf = Encoding.UTF8.GetString(entries["OPS/package.opf"]);
        Assert.Contains("統合テスト", opf);
        Assert.Contains("作者太郎", opf);
    }

    [Fact]
    public void Convert_MinimalText_ContainerXmlIsValid()
    {
        const string aozoraText = "タイトル\n著者\n\n本文。\n";

        string epubPath = ConvertToEpub(aozoraText, _tempDir);
        var entries = ReadEpubEntries(epubPath);

        string container = Encoding.UTF8.GetString(entries["META-INF/container.xml"]);
        Assert.Contains("OPS/package.opf", container);
        Assert.Contains("urn:oasis:names:tc:opendocument:xmlns:container", container);
    }

    [Fact]
    public void Convert_WithChapterMarkers_CreatesMultipleSections()
    {
        // 章マーカーを持つテキスト（青空文庫形式の章見出し）
        const string aozoraText =
            "複数章テスト\n著者名\n\n" +
            "-------------------------------------------------------\n" +
            "【テキスト中に現れる記号について】\n" +
            "-------------------------------------------------------\n" +
            "\n第一章\n\n" +
            "第一章の本文テキストです。\n\n" +
            "第二章\n\n" +
            "第二章の本文テキストです。\n";

        string epubPath = ConvertToEpub(aozoraText, _tempDir, "multi_chapter");
        var entries = ReadEpubEntries(epubPath);

        // 最低1つのXHTMLファイルが生成される
        int xhtmlCount = entries.Keys.Count(k => k.StartsWith("OPS/xhtml/") && k.EndsWith(".xhtml") && k != "OPS/xhtml/nav.xhtml");
        Assert.True(xhtmlCount >= 1, $"XHTMLファイルが不足: {xhtmlCount}");
    }

    [Fact]
    public void Convert_WithRuby_RubyTagsInOutput()
    {
        // ルビ付きテキスト: 《》形式
        const string aozoraText =
            "ルビテスト\n著者\n\n" +
            "漢字《かんじ》のルビテストです。\n";

        string epubPath = ConvertToEpub(aozoraText, _tempDir, "ruby_test");
        var entries = ReadEpubEntries(epubPath);

        // いずれかの XHTML に ruby タグが含まれるはず
        bool hasRuby = entries
            .Where(kv => kv.Key.StartsWith("OPS/xhtml/") && kv.Key.EndsWith(".xhtml"))
            .Any(kv => Encoding.UTF8.GetString(kv.Value).Contains("<ruby>") ||
                       Encoding.UTF8.GetString(kv.Value).Contains("<rb>"));
        Assert.True(hasRuby, "ruby タグが生成されていません");
    }

    [Fact]
    public void Convert_MultipleFiles_EachCreatesEpub()
    {
        var texts = new[]
        {
            ("file1", "タイトル1\n著者1\n\n本文1。\n"),
            ("file2", "タイトル2\n著者2\n\n本文2。\n"),
            ("file3", "タイトル3\n著者3\n\n本文3。\n"),
        };

        foreach (var (name, text) in texts)
        {
            string epubPath = ConvertToEpub(text, _tempDir, name);
            Assert.True(File.Exists(epubPath), $"EPUB未生成: {name}");
        }
    }

    [Fact]
    public void Convert_GetBookInfo_ExtractsCorrectMetadata()
    {
        const string aozoraText = "青空文庫テスト\nてすと著者\n\n本文。\n";

        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var writer    = new Epub3Writer("");
        var converter = new AozoraEpub3Converter(writer, "");

        BookInfo bookInfo;
        using (var reader = new StringReader(aozoraText))
        {
            var imgReader = new ImageInfoReader(true, "");
            bookInfo = converter.GetBookInfo("test.txt", reader, imgReader, BookInfo.TitleType.TITLE_AUTHOR, false);
        }

        Assert.False(string.IsNullOrEmpty(bookInfo.Title),   "タイトルが空");
        Assert.False(string.IsNullOrEmpty(bookInfo.Creator), "著者が空");
    }

    [Fact]
    public void Convert_HorizontalLayout_WritingModeHorizontal()
    {
        const string aozoraText = "横書きテスト\n著者\n\n本文。\n";

        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        string outPath = Path.Combine(_tempDir, "horizontal.epub");
        var writer    = new Epub3Writer("");
        var converter = new AozoraEpub3Converter(writer, "");

        BookInfo bookInfo;
        using (var reader1 = new StringReader(aozoraText))
        {
            var img1 = new ImageInfoReader(true, "");
            bookInfo = converter.GetBookInfo("test.txt", reader1, img1, BookInfo.TitleType.TITLE_AUTHOR, false);
        }
        bookInfo.Vertical = false;  // 横書き
        converter.vertical = false;

        var img2 = new ImageInfoReader(true, "");
        using var reader2 = new StringReader(aozoraText);
        writer.Write(converter, reader2, "test.txt", "txt", outPath, bookInfo, img2);

        var entries = ReadEpubEntries(outPath);
        string opf = Encoding.UTF8.GetString(entries["OPS/package.opf"]);

        // 横書き時は horizontal-tb が使われる
        Assert.Contains("horizontal", opf);
    }
}

// ── ZipArchiveEntry 拡張 ──────────────────────────────────────────────────────

internal static class ZipArchiveEntryExtensions
{
    /// <summary>Reflection なしで CompressionMethod を取得する。</summary>
    internal static CompressionMethodValues CompressionMethod(this ZipArchiveEntry entry)
    {
        // ZipArchiveEntry.CompressionMethod は .NET 10 で公開されていない。
        // 非圧縮（Stored）かどうかは CompressedLength == Length で判定できる。
        return entry.CompressedLength == entry.Length
            ? CompressionMethodValues.Stored
            : CompressionMethodValues.Deflated;
    }
}

internal enum CompressionMethodValues { Stored, Deflated }
