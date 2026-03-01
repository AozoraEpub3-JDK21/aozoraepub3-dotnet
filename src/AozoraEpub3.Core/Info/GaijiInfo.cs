namespace AozoraEpub3.Core.Info;

/// <summary>外字フォントとクラス名を格納</summary>
public class GaijiInfo
{
    public string ClassName { get; set; }
    public string FilePath { get; set; }

    public GaijiInfo(string className, string filePath)
    {
        ClassName = className;
        FilePath = filePath;
    }

    public string FileName => Path.GetFileName(FilePath);
}
