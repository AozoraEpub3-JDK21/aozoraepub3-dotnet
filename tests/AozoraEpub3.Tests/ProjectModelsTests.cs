using AozoraEpub3.Core.Editor;

namespace AozoraEpub3.Tests;

public class ProjectModelsTests
{
    [Fact]
    public void ProjectData_TotalWordCount_SumsEpisodes()
    {
        var project = new ProjectData
        {
            Structure =
            [
                new StructureItem
                {
                    Type = CardType.Chapter, Title = "第一章",
                    Children =
                    [
                        new StructureItem { Type = CardType.Episode, WordCount = 1000 },
                        new StructureItem { Type = CardType.Episode, WordCount = 2000 }
                    ]
                },
                new StructureItem { Type = CardType.Synopsis, WordCount = 500 }
            ]
        };
        Assert.Equal(3500, project.TotalWordCount);
    }

    [Fact]
    public void ProjectData_TotalWordCount_ExcludesMemo()
    {
        var project = new ProjectData
        {
            Structure =
            [
                new StructureItem { Type = CardType.Episode, WordCount = 1000 },
                new StructureItem { Type = CardType.Memo, WordCount = 500, ExcludeFromEpub = true }
            ]
        };
        Assert.Equal(1000, project.TotalWordCount);
    }

    [Fact]
    public void ProjectData_TotalEpisodes_CountsCorrectly()
    {
        var project = new ProjectData
        {
            Structure =
            [
                new StructureItem { Type = CardType.Cover },
                new StructureItem
                {
                    Type = CardType.Chapter,
                    Children =
                    [
                        new StructureItem { Type = CardType.Episode },
                        new StructureItem { Type = CardType.Episode }
                    ]
                },
                new StructureItem { Type = CardType.Episode }
            ]
        };
        Assert.Equal(3, project.TotalEpisodes);
    }

    [Fact]
    public void ProjectData_ProgressPercent_Calculates()
    {
        var project = new ProjectData
        {
            TargetWordCount = 10000,
            Structure =
            [
                new StructureItem { Type = CardType.Episode, WordCount = 2500 }
            ]
        };
        Assert.Equal(25.0, project.ProgressPercent);
    }

    [Fact]
    public void ProjectData_ProgressPercent_NoTarget_ReturnsZero()
    {
        var project = new ProjectData
        {
            Structure = [new StructureItem { Type = CardType.Episode, WordCount = 1000 }]
        };
        Assert.Equal(0, project.ProgressPercent);
    }

    [Fact]
    public void CardItem_WordCount_ReturnsBodyLength()
    {
        var card = new CardItem { Body = "テスト文章" };
        Assert.Equal(5, card.WordCount);
    }

    [Fact]
    public void CardItem_StatusDisplay()
    {
        Assert.Equal("下書き", new CardItem { Status = ProjectCardStatus.Draft }.StatusDisplay);
        Assert.Equal("執筆中", new CardItem { Status = ProjectCardStatus.Writing }.StatusDisplay);
        Assert.Equal("完成", new CardItem { Status = ProjectCardStatus.Done }.StatusDisplay);
        Assert.Equal("改稿中", new CardItem { Status = ProjectCardStatus.Revision }.StatusDisplay);
    }

    [Fact]
    public void ProjectService_CreateNew_And_Load()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"aep3test_{Guid.NewGuid():N}");
        try
        {
            var service = new ProjectService();
            var projectDir = service.CreateNew(tempDir, "テスト作品", "テスト著者");

            Assert.True(Directory.Exists(projectDir));
            Assert.True(File.Exists(Path.Combine(projectDir, "project.json")));

            var loaded = service.Load(projectDir);
            Assert.NotNull(loaded);
            Assert.Equal("テスト作品", loaded.Title);
            Assert.Equal("テスト著者", loaded.Author);
            Assert.True(loaded.Structure.Count >= 3); // cover, synopsis, chapter
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void ProjectService_LoadCard_ReturnsContent()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"aep3test_{Guid.NewGuid():N}");
        try
        {
            var service = new ProjectService();
            var projectDir = service.CreateNew(tempDir, "テスト", "著者");

            var item = new StructureItem { Type = CardType.Episode, Title = "第1話", File = "ch01_ep01.md" };
            var card = service.LoadCard(projectDir, item);

            Assert.Equal("ch01_ep01.md", card.FileName);
            Assert.False(string.IsNullOrEmpty(card.Body));
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void ProjectCombineService_Combine_IncludesChaptersAndEpisodes()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"aep3test_{Guid.NewGuid():N}");
        try
        {
            var service = new ProjectService();
            var projectDir = service.CreateNew(tempDir, "結合テスト", "結合著者");

            // カードに内容を書き込む
            File.WriteAllText(Path.Combine(projectDir, "cards", "ch01_ep01.md"), "　第1話の本文です。");

            var project = service.Load(projectDir)!;
            var combiner = new ProjectCombineService();
            var result = combiner.Combine(projectDir, project);

            Assert.Contains("結合テスト", result);
            Assert.Contains("結合著者", result);
            Assert.Contains("第1話の本文です", result);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void ProjectService_ImportFromCardCollection()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"aep3test_{Guid.NewGuid():N}");
        try
        {
            var collection = new CardCollection
            {
                Title = "インポートテスト",
                Author = "インポート著者",
                Cards =
                [
                    new StoryCard { Title = "第1話", Body = "本文1", Status = CardStatus.Done },
                    new StoryCard { Title = "第2話", Body = "本文2", Status = CardStatus.Writing }
                ]
            };

            var service = new ProjectService();
            var projectDir = service.ImportFromCardCollection(tempDir, collection);

            var project = service.Load(projectDir)!;
            Assert.Equal("インポートテスト", project.Title);

            // 章の中にエピソードが2つあること
            var chapter = project.Structure.First(s => s.Type == CardType.Chapter);
            Assert.Equal(2, chapter.Children.Count);
            Assert.Equal(ProjectCardStatus.Done, chapter.Children[0].Status);
            Assert.Equal(ProjectCardStatus.Writing, chapter.Children[1].Status);

            // カードファイルが存在すること
            Assert.True(File.Exists(Path.Combine(projectDir, "cards", "ch01_ep01.md")));
            Assert.Equal("本文1", File.ReadAllText(Path.Combine(projectDir, "cards", "ch01_ep01.md")));
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }
}
