using AozoraEpub3.Core.Editor;

namespace AozoraEpub3.Tests;

public class CardModelsTests
{
    [Fact]
    public void StoryCard_WordCount_ReturnsBodyLength()
    {
        var card = new StoryCard { Body = "テスト文章です" };
        Assert.Equal(7, card.WordCount);
    }

    [Fact]
    public void StoryCard_WordCount_EmptyBody_ReturnsZero()
    {
        var card = new StoryCard();
        Assert.Equal(0, card.WordCount);
    }

    [Fact]
    public void StoryCard_GetPreviewText_ShortBody_ReturnsFullText()
    {
        var card = new StoryCard { Body = "短いテキスト" };
        Assert.Equal("短いテキスト", card.GetPreviewText());
    }

    [Fact]
    public void StoryCard_GetPreviewText_LongBody_TruncatesWithEllipsis()
    {
        var card = new StoryCard { Body = new string('あ', 50) };
        var preview = card.GetPreviewText(40);
        Assert.Equal(41, preview.Length); // 40 chars + "…"
        Assert.EndsWith("…", preview);
    }

    [Fact]
    public void StoryCard_GetPreviewText_ReplacesNewlines()
    {
        var card = new StoryCard { Body = "行1\n行2\n行3" };
        var preview = card.GetPreviewText();
        Assert.DoesNotContain("\n", preview);
        Assert.Contains("行1 行2 行3", preview);
    }

    [Fact]
    public void StoryCard_DefaultValues()
    {
        var card = new StoryCard();
        Assert.Equal("", card.Title);
        Assert.Equal("", card.Body);
        Assert.Equal(CardStatus.Draft, card.Status);
        Assert.Equal(8, card.Id.Length);
    }

    [Fact]
    public void CardCollection_TotalWordCount_SumsAllCards()
    {
        var collection = new CardCollection
        {
            Cards =
            [
                new StoryCard { Body = "12345" },      // 5
                new StoryCard { Body = "あいうえお" },   // 5
                new StoryCard { Body = "" }             // 0
            ]
        };
        Assert.Equal(10, collection.TotalWordCount);
    }

    [Fact]
    public void CardCollection_TotalWordCount_EmptyCards_ReturnsZero()
    {
        var collection = new CardCollection();
        Assert.Equal(0, collection.TotalWordCount);
    }

    [Fact]
    public void CardCollection_DefaultValues()
    {
        var collection = new CardCollection();
        Assert.Equal("", collection.Title);
        Assert.Equal("", collection.Author);
        Assert.Empty(collection.Cards);
    }
}
