using AozoraEpub3.Core.Converter;

namespace AozoraEpub3.Core.Info;

/// <summary>目次用の章の情報を格納</summary>
public class ChapterInfo
{
    /// <summary>xhtmlファイルのセクション毎の連番ID</summary>
    public string SectionId { get; set; }
    /// <summary>章ID 見出し行につけたspanのID</summary>
    public string? ChapterId { get; set; }
    /// <summary>章名称</summary>
    public string? ChapterName { get; set; }

    /// <summary>目次階層レベル</summary>
    public int ChapterLevel { get; set; }

    /// <summary>出力前に階層化開始タグを入れる回数</summary>
    public int LevelStart { get; set; } = 0;
    /// <summary>出力後に階層化終了タグを入れる回数</summary>
    public int LevelEnd { get; set; } = 0;
    /// <summary>navPointを閉じる回数</summary>
    public int NavClose { get; set; } = 1;

    public ChapterInfo(string sectionId, string? chapterId, string? chapterName, int chapterLevel)
    {
        SectionId = sectionId;
        ChapterId = chapterId;
        ChapterName = chapterName;
        ChapterLevel = chapterLevel;
    }

    public string? NoTagChapterName => ChapterName != null ? CharUtils.RemoveTag(ChapterName) : null;

    /// <summary>Scribanでループするために配列を返す（プロパティ）</summary>
    public int[]? GetLevelStart => LevelStart == 0 ? null : new int[LevelStart];
    /// <summary>Scribanでループするために配列を返す（プロパティ）</summary>
    public int[]? GetLevelEnd => LevelEnd == 0 ? null : new int[LevelEnd];
    /// <summary>Scribanでループするために配列を返す（プロパティ）</summary>
    public int[]? GetNavClose => NavClose <= 0 ? null : new int[NavClose];
}
