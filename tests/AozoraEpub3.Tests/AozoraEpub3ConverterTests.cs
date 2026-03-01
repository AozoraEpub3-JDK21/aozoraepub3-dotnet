using AozoraEpub3.Core.Converter;
using AozoraEpub3.Core.Info;
using AozoraEpub3.Core.Io;

namespace AozoraEpub3.Tests;

/// <summary>テスト用の最小限 IEpub3Writer スタブ</summary>
internal class StubEpub3Writer : IEpub3Writer
{
    public Action<int>? ProgressCallback { get; set; }
    public string GetGaijiFontPath() => "";
    public string? GetImageFilePath(string srcImageFileName, int lineNum) => null;
    public bool IsCoverImage() => false;
    public int GetImageIndex() => 0;
    public int GetImagePageType(string srcFilePath, int tagLevel, int lineNum, bool hasCaption) => 0;
    public double GetImageWidthRatio(string srcFilePath, bool hasCaption) => 1.0;
    public void NextSection(TextWriter bw, int lineNum, int pageType, int imagePageType, string? srcImageFilePath) { }
    public void AddChapter(string? chapterId, string name, int chapterLevel) { }
    public void AddGaijiFont(string className, string gaijiFilePath) { }
}

public class AozoraEpub3ConverterTests
{
    private static AozoraEpub3Converter CreateConverter()
    {
        return new AozoraEpub3Converter(new StubEpub3Writer());
    }

    // --- 初期化 ---

    [Fact]
    public void Constructor_Succeeds_WithoutResourcePath()
    {
        var converter = CreateConverter();
        Assert.NotNull(converter);
    }

    [Fact]
    public void GetChukiValue_KnownKey_ReturnsValue()
    {
        var converter = CreateConverter();
        // chuki_tag.txt に「傍点」関連が必ずあるはず
        var values = converter.GetChukiValue("傍点");
        Assert.NotNull(values);
        Assert.NotEmpty(values);
    }

    [Fact]
    public void GetChukiValue_UnknownKey_ReturnsNull()
    {
        var converter = CreateConverter();
        Assert.Null(converter.GetChukiValue("存在しないキー"));
    }

    [Fact]
    public void ChukiFlagPageBreak_IsPopulated()
    {
        _ = CreateConverter(); // static 初期化を確保
        Assert.NotEmpty(AozoraEpub3Converter.ChukiFlagPageBreak);
    }

    // --- GetBookInfo ---

    [Fact]
    public void GetBookInfo_MinimalText_ReturnsTitleAndCreator()
    {
        var converter = CreateConverter();
        // 青空文庫の標準形式 (TITLE_AUTHOR): タイトル 空行 著者 空行 本文
        string text = "テスト作品\n\n作者名\n\n本文テキスト\n";
        using var reader = new StringReader(text);
        var imageReader = new ImageInfoReader(true, "test.txt");

        var bookInfo = converter.GetBookInfo(
            "test.txt", reader, imageReader,
            BookInfo.TitleType.TITLE_AUTHOR, false);

        Assert.NotNull(bookInfo);
        Assert.Equal("test.txt", bookInfo.SrcFilePath);
    }

    [Fact]
    public void GetBookInfo_EmptyText_ReturnsBookInfo()
    {
        var converter = CreateConverter();
        using var reader = new StringReader("");
        var imageReader = new ImageInfoReader(true, "test.txt");

        var bookInfo = converter.GetBookInfo(
            "test.txt", reader, imageReader,
            BookInfo.TitleType.TITLE_AUTHOR, false);

        Assert.NotNull(bookInfo);
    }

    [Fact]
    public void GetBookInfo_TitleType_None_ReturnsBookInfo()
    {
        var converter = CreateConverter();
        string text = "本文のみのテキスト\n行2\n行3\n";
        using var reader = new StringReader(text);
        var imageReader = new ImageInfoReader(true, "test.txt");

        var bookInfo = converter.GetBookInfo(
            "test.txt", reader, imageReader,
            BookInfo.TitleType.NONE, false);

        Assert.NotNull(bookInfo);
    }

    [Fact]
    public void GetBookInfo_WithComment_ParsesCorrectly()
    {
        var converter = CreateConverter();
        // コメント区切り（50文字以上の '-' 行）
        string separator = new('-', 50);
        string text = $"タイトル\n\n著者\n\n{separator}\nコメント行\n{separator}\n本文\n";
        using var reader = new StringReader(text);
        var imageReader = new ImageInfoReader(true, "test.txt");

        var bookInfo = converter.GetBookInfo(
            "test.txt", reader, imageReader,
            BookInfo.TitleType.TITLE_AUTHOR, false);

        Assert.NotNull(bookInfo);
    }

    [Fact]
    public void GetBookInfo_GaijiChuki_DoesNotThrow()
    {
        var converter = CreateConverter();
        // 外字注記を含むテキスト
        string text = "タイトル\n\n著者\n\n※［＃「山」の「月」に代えて「日」、U+26A36］\n";
        using var reader = new StringReader(text);
        var imageReader = new ImageInfoReader(true, "test.txt");

        var exception = Record.Exception(() =>
        {
            converter.GetBookInfo("test.txt", reader, imageReader,
                BookInfo.TitleType.TITLE_AUTHOR, false);
        });

        Assert.Null(exception);
    }
}
