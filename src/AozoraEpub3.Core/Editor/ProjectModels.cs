using System.Text.Json.Serialization;

namespace AozoraEpub3.Core.Editor;

/// <summary>カードの種別</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CardType
{
    Cover,
    Synopsis,
    Chapter,
    Episode,
    Afterword,
    Memo
}

/// <summary>編集カードのステータス（4種）</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ProjectCardStatus
{
    Draft,
    Writing,
    Done,
    Revision
}

/// <summary>
/// project.json の構造要素。
/// Chapter は children を持ち、Episode/Cover/Synopsis 等はリーフ。
/// </summary>
public sealed class StructureItem
{
    public CardType Type { get; set; } = CardType.Episode;
    public string Title { get; set; } = "";
    public string File { get; set; } = "";
    public int WordCount { get; set; }
    public ProjectCardStatus Status { get; set; } = ProjectCardStatus.Draft;
    public bool ExcludeFromEpub { get; set; }
    public List<StructureItem> Children { get; set; } = [];
}

/// <summary>
/// project.json のルートデータモデル。
/// プロジェクト全体のメタデータと構造を保持する。
/// </summary>
public sealed class ProjectData
{
    public string Title { get; set; } = "";
    public string Author { get; set; } = "";
    public int TargetWordCount { get; set; }
    public string CoverImage { get; set; } = "";
    public List<StructureItem> Structure { get; set; } = [];
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime ModifiedAt { get; set; } = DateTime.Now;

    /// <summary>全エピソード/カードの合計文字数を計算する</summary>
    public int TotalWordCount => CountWords(Structure);

    /// <summary>全エピソード数を数える</summary>
    public int TotalEpisodes => CountEpisodes(Structure);

    /// <summary>全章数を数える</summary>
    public int TotalChapters => Structure.Count(s => s.Type == CardType.Chapter);

    /// <summary>目標文字数に対する進捗率（0-100）</summary>
    public double ProgressPercent => TargetWordCount > 0
        ? Math.Min(100.0, TotalWordCount * 100.0 / TargetWordCount)
        : 0;

    private static int CountWords(List<StructureItem> items)
    {
        int total = 0;
        foreach (var item in items)
        {
            if (item.Type == CardType.Chapter)
                total += CountWords(item.Children);
            else if (!item.ExcludeFromEpub)
                total += item.WordCount;
        }
        return total;
    }

    private static int CountEpisodes(List<StructureItem> items)
    {
        int total = 0;
        foreach (var item in items)
        {
            if (item.Type == CardType.Chapter)
                total += CountEpisodes(item.Children);
            else if (item.Type == CardType.Episode)
                total++;
        }
        return total;
    }
}
