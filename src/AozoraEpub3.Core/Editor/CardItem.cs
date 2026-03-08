namespace AozoraEpub3.Core.Editor;

/// <summary>
/// 編集カード1枚分のデータ。
/// StructureItem がプロジェクト構造上のメタデータ、
/// CardItem が実際のテキスト内容を保持する。
/// </summary>
public sealed class CardItem
{
    public string FileName { get; set; } = "";
    public CardType Type { get; set; } = CardType.Episode;
    public string Title { get; set; } = "";
    public string Body { get; set; } = "";
    public ProjectCardStatus Status { get; set; } = ProjectCardStatus.Draft;
    public bool ExcludeFromEpub { get; set; }
    public int WordCount => Body.Length;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime ModifiedAt { get; set; } = DateTime.Now;

    /// <summary>本文プレビュー（先頭N文字）</summary>
    public string GetPreviewText(int maxChars = 40)
        => Body.Length <= maxChars ? Body.Replace("\n", " ")
            : Body[..maxChars].Replace("\n", " ") + "…";

    /// <summary>ステータスの表示テキスト</summary>
    public string StatusDisplay => Status switch
    {
        ProjectCardStatus.Draft => "下書き",
        ProjectCardStatus.Writing => "執筆中",
        ProjectCardStatus.Done => "完成",
        ProjectCardStatus.Revision => "改稿中",
        _ => ""
    };

    /// <summary>種別の表示テキスト</summary>
    public string TypeDisplay => Type switch
    {
        CardType.Cover => "表紙",
        CardType.Synopsis => "あらすじ",
        CardType.Chapter => "章",
        CardType.Episode => "本文",
        CardType.Afterword => "あとがき",
        CardType.Memo => "メモ",
        _ => ""
    };
}
