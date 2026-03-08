using AozoraEpub3.Core.Editor;

namespace AozoraEpub3.Tests;

public class CustomDictionaryTests
{
    [Fact]
    public void LoadFromJson_ParsesEntries()
    {
        var json = """
        {
            "name": "テスト辞書",
            "entries": [
                {
                    "name": "ルビテスト",
                    "category": "テスト",
                    "insertText": "｜漢字《かんじ》",
                    "cursorOffset": 9
                },
                {
                    "name": "改ページ",
                    "category": "構造",
                    "insertText": "---",
                    "isLineLevel": true
                }
            ]
        }
        """;

        var service = new CustomDictionaryService();
        service.LoadFromJson(json);

        Assert.Equal(2, service.Entries.Count);
        Assert.Equal("ルビテスト", service.Entries[0].Name);
        Assert.Equal("テスト", service.Entries[0].Category);
        Assert.Equal(9, service.Entries[0].CursorOffset);
    }

    [Fact]
    public void LoadFromJson_DefaultsCursorOffset()
    {
        var json = """
        {
            "name": "テスト",
            "entries": [
                {
                    "name": "テスト",
                    "insertText": "ABCDE"
                }
            ]
        }
        """;

        var service = new CustomDictionaryService();
        service.LoadFromJson(json);

        // CursorOffset が 0 のとき、InsertText.Length に自動設定
        Assert.Equal(5, service.Entries[0].CursorOffset);
    }

    [Fact]
    public void LoadFromJson_SkipsEmptyNames()
    {
        var json = """
        {
            "name": "テスト",
            "entries": [
                { "name": "", "insertText": "skip" },
                { "name": "有効", "insertText": "OK" }
            ]
        }
        """;

        var service = new CustomDictionaryService();
        service.LoadFromJson(json);

        Assert.Single(service.Entries);
        Assert.Equal("有効", service.Entries[0].Name);
    }

    [Fact]
    public void ToSuggestItems_ConvertsToChukiSuggestItems()
    {
        var json = """
        {
            "name": "テスト",
            "entries": [
                {
                    "name": "テスト注記",
                    "category": "カスタム",
                    "insertText": "テスト"
                }
            ]
        }
        """;

        var service = new CustomDictionaryService();
        service.LoadFromJson(json);

        var items = service.ToSuggestItems();
        Assert.Single(items);
        Assert.Equal("テスト注記", items[0].DisplayName);
        Assert.Equal("カスタム", items[0].Category);
        Assert.Equal(200, items[0].Priority); // ユーザー定義は高優先度
    }

    [Fact]
    public void GetSnippet_ReturnsMatchingEntry()
    {
        var json = """
        {
            "name": "テスト",
            "entries": [
                {
                    "name": "場面転換",
                    "insertText": "◇　◇　◇",
                    "isLineLevel": true
                }
            ]
        }
        """;

        var service = new CustomDictionaryService();
        service.LoadFromJson(json);

        var snippet = service.GetSnippet("場面転換");
        Assert.NotNull(snippet);
        Assert.Equal("◇　◇　◇", snippet.TextToInsert);
        Assert.True(snippet.IsLineLevel);
    }

    [Fact]
    public void GetSnippet_ReturnsNull_WhenNotFound()
    {
        var service = new CustomDictionaryService();
        Assert.Null(service.GetSnippet("存在しない"));
    }

    [Fact]
    public void Search_FiltersByPrefix()
    {
        var json = """
        {
            "name": "テスト",
            "entries": [
                { "name": "キャラA", "insertText": "A" },
                { "name": "キャラB", "insertText": "B" },
                { "name": "場面転換", "insertText": "C" }
            ]
        }
        """;

        var service = new CustomDictionaryService();
        service.LoadFromJson(json);

        var results = service.Search("キャラ");
        Assert.Equal(2, results.Count);
    }

    [Fact]
    public void Clear_RemovesAllEntries()
    {
        var json = """
        {
            "name": "テスト",
            "entries": [{ "name": "テスト", "insertText": "T" }]
        }
        """;

        var service = new CustomDictionaryService();
        service.LoadFromJson(json);
        Assert.Single(service.Entries);

        service.Clear();
        Assert.Empty(service.Entries);
    }

    [Fact]
    public void GenerateSampleJson_ProducesValidJson()
    {
        var json = CustomDictionaryService.GenerateSampleJson();

        var service = new CustomDictionaryService();
        service.LoadFromJson(json);

        Assert.True(service.Entries.Count >= 3);
        Assert.Contains(service.Entries, e => e.Name == "場面転換");
    }

    [Fact]
    public void LoadFromFile_NonExistent_DoesNotThrow()
    {
        var service = new CustomDictionaryService();
        service.LoadFromFile("/non/existent/path.json");
        Assert.Empty(service.Entries);
    }

    [Fact]
    public void LoadFromFile_ValidFile_LoadsEntries()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempFile, CustomDictionaryService.GenerateSampleJson());
            var service = new CustomDictionaryService();
            service.LoadFromFile(tempFile);
            Assert.True(service.Entries.Count >= 3);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }
}
