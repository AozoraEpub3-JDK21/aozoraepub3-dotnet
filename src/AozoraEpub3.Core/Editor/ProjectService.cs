using System.Text.Json;

namespace AozoraEpub3.Core.Editor;

/// <summary>
/// .aep3proj プロジェクトの読み込み/保存/新規作成を担当する。
/// プロジェクトはディレクトリで、project.json + cards/ + images/ から成る。
/// </summary>
public sealed class ProjectService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>プロジェクトディレクトリから project.json を読み込む</summary>
    public ProjectData? Load(string projectDir)
    {
        var jsonPath = Path.Combine(projectDir, "project.json");
        if (!File.Exists(jsonPath)) return null;

        var json = File.ReadAllText(jsonPath);
        return JsonSerializer.Deserialize<ProjectData>(json, JsonOptions);
    }

    /// <summary>プロジェクトを保存する</summary>
    public void Save(string projectDir, ProjectData project)
    {
        Directory.CreateDirectory(projectDir);
        Directory.CreateDirectory(Path.Combine(projectDir, "cards"));
        Directory.CreateDirectory(Path.Combine(projectDir, "images"));

        project.ModifiedAt = DateTime.Now;
        var json = JsonSerializer.Serialize(project, JsonOptions);
        File.WriteAllText(Path.Combine(projectDir, "project.json"), json);
    }

    /// <summary>新規プロジェクトを作成する</summary>
    public string CreateNew(string parentDir, string title, string author)
    {
        var safeName = SanitizeFileName(title);
        if (string.IsNullOrEmpty(safeName)) safeName = "NewProject";
        var projectDir = Path.Combine(parentDir, safeName + ".aep3proj");

        // ディレクトリが既に存在する場合は連番を付ける
        var baseDir = projectDir;
        var counter = 1;
        while (Directory.Exists(projectDir))
        {
            projectDir = $"{baseDir}_{counter++}";
        }

        var project = new ProjectData
        {
            Title = title,
            Author = author,
            Structure =
            [
                new StructureItem { Type = CardType.Cover, Title = "表紙", File = "cover.md" },
                new StructureItem { Type = CardType.Synopsis, Title = "あらすじ", File = "synopsis.md" },
                new StructureItem
                {
                    Type = CardType.Chapter,
                    Title = "第一章",
                    Children =
                    [
                        new StructureItem
                        {
                            Type = CardType.Episode,
                            Title = "第1話",
                            File = "ch01_ep01.md",
                            Status = ProjectCardStatus.Draft
                        }
                    ]
                }
            ]
        };

        Save(projectDir, project);

        // カードファイルを作成
        var cardsDir = Path.Combine(projectDir, "cards");
        File.WriteAllText(Path.Combine(cardsDir, "cover.md"), $"# {title}\n\n{author}\n");
        File.WriteAllText(Path.Combine(cardsDir, "synopsis.md"), "ここにあらすじを書いてください。\n");
        File.WriteAllText(Path.Combine(cardsDir, "ch01_ep01.md"), "　ここに本文を書いてください。\n");

        return projectDir;
    }

    /// <summary>カードのテキストを読み込む</summary>
    public CardItem LoadCard(string projectDir, StructureItem item)
    {
        var card = new CardItem
        {
            FileName = item.File,
            Type = item.Type,
            Title = item.Title,
            Status = item.Status,
            ExcludeFromEpub = item.ExcludeFromEpub
        };

        if (!string.IsNullOrEmpty(item.File))
        {
            var filePath = Path.Combine(projectDir, "cards", item.File);
            if (File.Exists(filePath))
                card.Body = File.ReadAllText(filePath);
        }

        return card;
    }

    /// <summary>カードのテキストを保存する</summary>
    public void SaveCard(string projectDir, CardItem card)
    {
        if (string.IsNullOrEmpty(card.FileName)) return;
        var cardsDir = Path.Combine(projectDir, "cards");
        Directory.CreateDirectory(cardsDir);
        File.WriteAllText(Path.Combine(cardsDir, card.FileName), card.Body);
    }

    /// <summary>プロジェクトに新しいエピソードを追加する</summary>
    public CardItem AddEpisode(ProjectData project, string projectDir, int chapterIndex, string title)
    {
        if (chapterIndex < 0 || chapterIndex >= project.Structure.Count)
            throw new ArgumentOutOfRangeException(nameof(chapterIndex));

        var chapter = project.Structure[chapterIndex];
        if (chapter.Type != CardType.Chapter)
            throw new ArgumentException("指定されたインデックスは章ではありません");

        var epNum = chapter.Children.Count + 1;
        var chNum = chapterIndex + 1 - project.Structure.Count(s => s.Type != CardType.Chapter && project.Structure.IndexOf(s) < chapterIndex);
        var fileName = $"ch{chNum:D2}_ep{epNum:D2}.md";

        var item = new StructureItem
        {
            Type = CardType.Episode,
            Title = string.IsNullOrEmpty(title) ? $"第{epNum}話" : title,
            File = fileName,
            Status = ProjectCardStatus.Draft
        };
        chapter.Children.Add(item);

        var card = new CardItem
        {
            FileName = fileName,
            Type = CardType.Episode,
            Title = item.Title,
            Body = ""
        };

        // ファイルを作成
        var cardsDir = Path.Combine(projectDir, "cards");
        Directory.CreateDirectory(cardsDir);
        File.WriteAllText(Path.Combine(cardsDir, fileName), "");

        return card;
    }

    /// <summary>プロジェクトに新しい章を追加する</summary>
    public void AddChapter(ProjectData project, string title)
    {
        var chapterNum = project.TotalChapters + 1;
        var item = new StructureItem
        {
            Type = CardType.Chapter,
            Title = string.IsNullOrEmpty(title) ? $"第{chapterNum}章" : title,
            Children = []
        };
        project.Structure.Add(item);
    }

    /// <summary>プロジェクトにフリーカード（メモ）を追加する</summary>
    public CardItem AddMemo(ProjectData project, string projectDir, string title)
    {
        var memoNum = project.Structure.Count(s => s.Type == CardType.Memo) + 1;
        var fileName = $"memo_{memoNum:D2}.md";

        var item = new StructureItem
        {
            Type = CardType.Memo,
            Title = string.IsNullOrEmpty(title) ? $"メモ {memoNum}" : title,
            File = fileName,
            ExcludeFromEpub = true
        };
        project.Structure.Add(item);

        var card = new CardItem
        {
            FileName = fileName,
            Type = CardType.Memo,
            Title = item.Title,
            ExcludeFromEpub = true
        };

        var cardsDir = Path.Combine(projectDir, "cards");
        Directory.CreateDirectory(cardsDir);
        File.WriteAllText(Path.Combine(cardsDir, fileName), "");

        return card;
    }

    /// <summary>初心者カード(CardCollection)から編集カードプロジェクトに変換する</summary>
    public string ImportFromCardCollection(string parentDir, CardCollection collection)
    {
        var title = string.IsNullOrEmpty(collection.Title) ? "インポート作品" : collection.Title;
        var projectDir = CreateNew(parentDir, title, collection.Author);

        // 既存のデフォルト構造を上書き
        var project = Load(projectDir)!;
        project.Structure.Clear();
        project.Structure.Add(new StructureItem { Type = CardType.Cover, Title = "表紙", File = "cover.md" });

        // 全カードを1章にまとめる
        var chapter = new StructureItem
        {
            Type = CardType.Chapter,
            Title = "第一章"
        };

        var cardsDir = Path.Combine(projectDir, "cards");
        for (int i = 0; i < collection.Cards.Count; i++)
        {
            var card = collection.Cards[i];
            var fileName = $"ch01_ep{i + 1:D2}.md";
            var episodeTitle = string.IsNullOrEmpty(card.Title) ? $"第{i + 1}話" : card.Title;

            chapter.Children.Add(new StructureItem
            {
                Type = CardType.Episode,
                Title = episodeTitle,
                File = fileName,
                WordCount = card.WordCount,
                Status = card.Status switch
                {
                    CardStatus.Draft => ProjectCardStatus.Draft,
                    CardStatus.Writing => ProjectCardStatus.Writing,
                    CardStatus.Done => ProjectCardStatus.Done,
                    _ => ProjectCardStatus.Draft
                }
            });

            File.WriteAllText(Path.Combine(cardsDir, fileName), card.Body);
        }

        project.Structure.Add(chapter);
        Save(projectDir, project);

        return projectDir;
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Concat(name.Where(c => !invalid.Contains(c))).Trim();
    }
}
