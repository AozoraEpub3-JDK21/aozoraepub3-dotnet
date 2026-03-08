namespace AozoraEpub3.Core.Editor;

/// <summary>カードのステータス</summary>
public enum CardStatus { Draft, Writing, Done }

/// <summary>1話分のカード</summary>
public sealed class StoryCard
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string Title { get; set; } = "";
    public string Body { get; set; } = "";
    public CardStatus Status { get; set; } = CardStatus.Draft;
    public int WordCount => Body.Length;  // 日本語なので文字数≒単語数
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime ModifiedAt { get; set; } = DateTime.Now;

    /// <summary>本文の先頭N文字を返す（カード表示用）</summary>
    public string GetPreviewText(int maxChars = 40)
        => Body.Length <= maxChars ? Body.Replace("\n", " ")
            : Body[..maxChars].Replace("\n", " ") + "…";
}

/// <summary>カードの集合（1作品分）</summary>
public sealed class CardCollection
{
    public string Title { get; set; } = "";
    public string Author { get; set; } = "";
    public List<StoryCard> Cards { get; set; } = [];
    public int TotalWordCount => Cards.Sum(c => c.WordCount);
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
