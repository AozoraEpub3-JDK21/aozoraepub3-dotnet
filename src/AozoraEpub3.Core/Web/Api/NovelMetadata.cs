namespace AozoraEpub3.Core.Web.Api;

/// <summary>なろう小説APIのレスポンスデータを格納するモデルクラス</summary>
public class NovelMetadata
{
    public string Ncode { get; set; } = "";
    public string Title { get; set; } = "";
    public string Writer { get; set; } = "";
    public string Story { get; set; } = "";
    public int GeneralAllNo { get; set; }
    public int End { get; set; }
    public int NovelType { get; set; }
    public int Length { get; set; }

    public override string ToString() =>
        $"NovelMetadata{{ncode={Ncode}, title={Title}, writer={Writer}, " +
        $"generalAllNo={GeneralAllNo}, end={(End == 0 ? "完結" : "連載中")}}}";
}
