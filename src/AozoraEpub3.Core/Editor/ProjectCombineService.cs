using System.Text;

namespace AozoraEpub3.Core.Editor;

/// <summary>
/// プロジェクトのカードを結合して青空文庫テキストを生成する。
/// </summary>
public sealed class ProjectCombineService
{
    private readonly ProjectService _projectService = new();

    /// <summary>
    /// プロジェクト全体を1つの青空文庫テキストに結合する。
    /// ハイブリッド記法で出力し、呼び出し側で EditorConversionEngine で変換する。
    /// </summary>
    public string Combine(string projectDir, ProjectData project)
    {
        var sb = new StringBuilder();

        // タイトル・著者
        sb.AppendLine(project.Title);
        sb.AppendLine(project.Author);
        sb.AppendLine();

        foreach (var item in project.Structure)
        {
            if (item.ExcludeFromEpub) continue;

            switch (item.Type)
            {
                case CardType.Cover:
                    // 表紙は BookInfo で処理されるため、テキストには含めない
                    break;

                case CardType.Synopsis:
                    var synopsisCard = _projectService.LoadCard(projectDir, item);
                    if (!string.IsNullOrWhiteSpace(synopsisCard.Body))
                    {
                        sb.AppendLine("---");
                        sb.AppendLine("## はじめに");
                        sb.AppendLine();
                        sb.AppendLine(synopsisCard.Body);
                        sb.AppendLine();
                    }
                    break;

                case CardType.Chapter:
                    sb.AppendLine("---");
                    sb.AppendLine($"# {item.Title}");
                    sb.AppendLine();
                    foreach (var child in item.Children)
                    {
                        if (child.ExcludeFromEpub) continue;
                        var episodeCard = _projectService.LoadCard(projectDir, child);
                        if (!string.IsNullOrEmpty(child.Title))
                        {
                            sb.AppendLine($"## {child.Title}");
                            sb.AppendLine();
                        }
                        sb.AppendLine(episodeCard.Body);
                        sb.AppendLine();
                    }
                    break;

                case CardType.Episode:
                    // 章に属さないエピソード
                    sb.AppendLine("---");
                    if (!string.IsNullOrEmpty(item.Title))
                    {
                        sb.AppendLine($"# {item.Title}");
                        sb.AppendLine();
                    }
                    var epCard = _projectService.LoadCard(projectDir, item);
                    sb.AppendLine(epCard.Body);
                    sb.AppendLine();
                    break;

                case CardType.Afterword:
                    var afterCard = _projectService.LoadCard(projectDir, item);
                    if (!string.IsNullOrWhiteSpace(afterCard.Body))
                    {
                        sb.AppendLine("---");
                        sb.AppendLine("## あとがき");
                        sb.AppendLine();
                        sb.AppendLine(afterCard.Body);
                        sb.AppendLine();
                    }
                    break;
            }
        }

        return sb.ToString();
    }
}
