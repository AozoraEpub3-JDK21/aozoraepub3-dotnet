using AozoraEpub3.Core.Editor;

namespace AozoraEpub3.Tests;

public class CustomConversionRuleTests
{
    [Fact]
    public void LoadFromJson_ParsesRules()
    {
        var json = """
        {
            "name": "テストルール",
            "rules": [
                {
                    "name": "ダッシュ統一",
                    "pattern": "─+",
                    "replacement": "――"
                },
                {
                    "name": "感嘆符統一",
                    "pattern": "！{3,}",
                    "replacement": "！！"
                }
            ]
        }
        """;

        var service = new CustomConversionRuleService();
        service.LoadFromJson(json);

        Assert.Equal(2, service.Rules.Count);
        Assert.Equal("ダッシュ統一", service.Rules[0].Name);
    }

    [Fact]
    public void Apply_ReplacesMatchingText()
    {
        var json = """
        {
            "name": "テスト",
            "rules": [
                { "name": "感嘆符", "pattern": "！{3,}", "replacement": "！！" }
            ]
        }
        """;

        var service = new CustomConversionRuleService();
        service.LoadFromJson(json);

        var result = service.Apply("すごい！！！！");
        Assert.Equal("すごい！！", result);
    }

    [Fact]
    public void Apply_MultipleRules_InOrder()
    {
        var json = """
        {
            "name": "テスト",
            "rules": [
                { "name": "rule1", "pattern": "AAA", "replacement": "BBB" },
                { "name": "rule2", "pattern": "BBB", "replacement": "CCC" }
            ]
        }
        """;

        var service = new CustomConversionRuleService();
        service.LoadFromJson(json);

        var result = service.Apply("AAA");
        Assert.Equal("CCC", result); // AAA→BBB→CCC
    }

    [Fact]
    public void Apply_DisabledRule_IsSkipped()
    {
        var json = """
        {
            "name": "テスト",
            "rules": [
                { "name": "disabled", "pattern": "ABC", "replacement": "XYZ", "enabled": false }
            ]
        }
        """;

        var service = new CustomConversionRuleService();
        service.LoadFromJson(json);

        Assert.Equal("ABC", service.Apply("ABC"));
    }

    [Fact]
    public void Apply_IgnoreCase()
    {
        var json = """
        {
            "name": "テスト",
            "rules": [
                { "name": "case", "pattern": "hello", "replacement": "HELLO", "ignoreCase": true }
            ]
        }
        """;

        var service = new CustomConversionRuleService();
        service.LoadFromJson(json);

        Assert.Equal("HELLO world", service.Apply("Hello world"));
    }

    [Fact]
    public void Apply_EmptyText_ReturnsEmpty()
    {
        var service = new CustomConversionRuleService();
        Assert.Equal("", service.Apply(""));
    }

    [Fact]
    public void Apply_NoRules_ReturnsOriginal()
    {
        var service = new CustomConversionRuleService();
        Assert.Equal("テスト", service.Apply("テスト"));
    }

    [Fact]
    public void Apply_InvalidRegex_IsIgnored()
    {
        var json = """
        {
            "name": "テスト",
            "rules": [
                { "name": "invalid", "pattern": "[invalid", "replacement": "x" },
                { "name": "valid", "pattern": "OK", "replacement": "YES" }
            ]
        }
        """;

        var service = new CustomConversionRuleService();
        service.LoadFromJson(json);

        Assert.Equal("YES", service.Apply("OK"));
    }

    [Fact]
    public void Apply_RegexGroupCapture()
    {
        var json = """
        {
            "name": "テスト",
            "rules": [
                {
                    "name": "ルビ付与",
                    "pattern": "鉄腕アトム",
                    "replacement": "｜鉄腕アトム《てつわんあとむ》"
                }
            ]
        }
        """;

        var service = new CustomConversionRuleService();
        service.LoadFromJson(json);

        var result = service.Apply("鉄腕アトムが走った。");
        Assert.Equal("｜鉄腕アトム《てつわんあとむ》が走った。", result);
    }

    [Fact]
    public void Preview_ShowsMatchResults()
    {
        var json = """
        {
            "name": "テスト",
            "rules": [
                { "name": "感嘆符", "pattern": "！{3,}", "replacement": "！！" }
            ]
        }
        """;

        var service = new CustomConversionRuleService();
        service.LoadFromJson(json);

        var results = service.Preview("すごい！！！！ やばい！！！");
        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.Equal("感嘆符", r.RuleName));
    }

    [Fact]
    public void Clear_RemovesAll()
    {
        var json = """
        {
            "name": "テスト",
            "rules": [{ "name": "r", "pattern": "x", "replacement": "y" }]
        }
        """;

        var service = new CustomConversionRuleService();
        service.LoadFromJson(json);
        service.Clear();

        Assert.Empty(service.Rules);
        Assert.Equal("x", service.Apply("x"));
    }

    [Fact]
    public void GenerateSampleJson_ProducesValidJson()
    {
        var json = CustomConversionRuleService.GenerateSampleJson();

        var service = new CustomConversionRuleService();
        service.LoadFromJson(json);

        Assert.True(service.Rules.Count >= 2);
    }

    [Fact]
    public void LoadFromFile_NonExistent_DoesNotThrow()
    {
        var service = new CustomConversionRuleService();
        service.LoadFromFile("/non/existent/path.json");
        Assert.Empty(service.Rules);
    }

    [Fact]
    public void LoadFromFile_ValidFile_LoadsRules()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempFile, CustomConversionRuleService.GenerateSampleJson());
            var service = new CustomConversionRuleService();
            service.LoadFromFile(tempFile);
            Assert.True(service.Rules.Count >= 2);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }
}
