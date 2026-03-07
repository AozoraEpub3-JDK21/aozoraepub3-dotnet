using AozoraEpub3.Core.Editor;

namespace AozoraEpub3.Tests;

public class ChukiDictionaryTests
{
    [Fact]
    public void Constructor_LoadsEmbeddedResource()
    {
        var dict = new ChukiDictionary();
        Assert.NotEmpty(dict.All);
    }

    [Fact]
    public void Search_EmptyPrefix_ReturnsTopPriorityItems()
    {
        var dict = new ChukiDictionary();
        var items = dict.Search("");
        Assert.NotEmpty(items);
        // 傍点は最高優先度
        Assert.Equal("傍点", items[0].DisplayName);
    }

    [Fact]
    public void Search_PrefixMatch_FiltersCorrectly()
    {
        var dict = new ChukiDictionary();
        var items = dict.Search("改");
        Assert.NotEmpty(items);
        Assert.All(items, i => Assert.StartsWith("改", i.DisplayName));
    }

    [Fact]
    public void Search_NoMatch_ReturnsEmpty()
    {
        var dict = new ChukiDictionary();
        var items = dict.Search("存在しないXYZ");
        Assert.Empty(items);
    }

    [Fact]
    public void BuiltinItems_HaveCorrectInsertText()
    {
        var dict = new ChukiDictionary();

        // 傍点（範囲注記）
        var bouten = dict.Search("傍点").FirstOrDefault(i => i.DisplayName == "傍点");
        Assert.NotNull(bouten);
        Assert.Equal("［＃傍点］［＃傍点終わり］", bouten.InsertText);
        Assert.Equal("装飾", bouten.Category);

        // 改ページ（単体注記）
        var pageBreak = dict.Search("改ページ").FirstOrDefault(i => i.DisplayName == "改ページ");
        Assert.NotNull(pageBreak);
        Assert.Equal("［＃改ページ］", pageBreak.InsertText);
        Assert.Equal("構造", pageBreak.Category);
    }

    [Fact]
    public void BuiltinItems_RangeAnnotation_CursorBetweenPair()
    {
        var dict = new ChukiDictionary();
        var bouten = dict.Search("傍点").First(i => i.DisplayName == "傍点");

        // CursorOffset は ［＃傍点］ の直後（= 5文字）
        // ［(1) ＃(2) 傍(3) 点(4) ］(5)
        Assert.Equal(5, bouten.CursorOffset);
    }

    [Fact]
    public void Search_IncludesChukiTagEntries()
    {
        var dict = new ChukiDictionary();
        // chuki_tag.txt には「白ゴマ傍点」「丸傍点」等がある
        var items = dict.Search("白ゴマ傍点");
        Assert.NotEmpty(items);
    }

    [Fact]
    public void Constructor_WithCustomText_ParsesEntries()
    {
        var customText = "テスト注記\t<span class=\"test\">\t</span>\t2\n";
        var dict = new ChukiDictionary(customText);
        // 組み込み + カスタム
        var items = dict.Search("テスト注記");
        Assert.NotEmpty(items);
    }

    [Fact]
    public void NoDuplicates_BuiltinAndChukiTag()
    {
        var dict = new ChukiDictionary();
        // 「傍点」は組み込みにもchuki_tagにもあるが、重複しないはず
        var items = dict.All.Where(i => i.DisplayName == "傍点").ToList();
        Assert.Single(items);
    }
}
