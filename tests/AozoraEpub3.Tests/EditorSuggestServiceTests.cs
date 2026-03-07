using AozoraEpub3.Core.Editor;

namespace AozoraEpub3.Tests;

public class EditorSuggestServiceTests
{
    private readonly EditorSuggestService _service = new();

    // ════════════════════════════════════════════════════════
    // 括弧ペアリング: 開き括弧
    // ════════════════════════════════════════════════════════

    [Theory]
    [InlineData('「', '」')]
    [InlineData('『', '』')]
    [InlineData('（', '）')]
    [InlineData('《', '》')]
    [InlineData('［', '］')]
    [InlineData('(', ')')]
    [InlineData('[', ']')]
    [InlineData('{', '}')]
    public void HandleBracketInput_OpenBracket_InsertsPair(char open, char close)
    {
        var result = _service.HandleBracketInput(open, null, null, null);
        Assert.NotNull(result);
        Assert.Equal($"{open}{close}", result.TextToInsert);
        Assert.Equal(1, result.CursorOffset);
        Assert.False(result.ShouldSkip);
    }

    [Fact]
    public void HandleBracketInput_OpenBracket_WithSelection_WrapsText()
    {
        var result = _service.HandleBracketInput('「', null, null, "こんにちは");
        Assert.NotNull(result);
        Assert.Equal("「こんにちは」", result.TextToInsert);
        Assert.Equal(7, result.CursorOffset); // "こんにちは".Length + 2
    }

    // ════════════════════════════════════════════════════════
    // 括弧ペアリング: 閉じ括弧スキップ
    // ════════════════════════════════════════════════════════

    [Fact]
    public void HandleBracketInput_CloseBracket_SkipsWhenMatching()
    {
        var result = _service.HandleBracketInput('」', '」', '字', null);
        Assert.NotNull(result);
        Assert.True(result.ShouldSkip);
        Assert.Equal(1, result.CursorOffset);
    }

    [Fact]
    public void HandleBracketInput_CloseBracket_InsertsWhenNotMatching()
    {
        var result = _service.HandleBracketInput('」', 'あ', '字', null);
        Assert.Null(result); // 通常入力
    }

    // ════════════════════════════════════════════════════════
    // 括弧ペアリング: バックスペース
    // ════════════════════════════════════════════════════════

    [Fact]
    public void HandleBackspace_EmptyPair_DeletesPair()
    {
        var result = _service.HandleBackspace('「', '」');
        Assert.NotNull(result);
        Assert.True(result.ShouldDeletePair);
    }

    [Fact]
    public void HandleBackspace_NonPair_ReturnsNull()
    {
        var result = _service.HandleBackspace('「', 'あ');
        Assert.Null(result);
    }

    [Fact]
    public void HandleBackspace_MismatchedPair_ReturnsNull()
    {
        var result = _service.HandleBackspace('「', '）');
        Assert.Null(result);
    }

    // ════════════════════════════════════════════════════════
    // 括弧ペアリング: 通常文字
    // ════════════════════════════════════════════════════════

    [Fact]
    public void HandleBracketInput_RegularChar_ReturnsNull()
    {
        var result = _service.HandleBracketInput('あ', null, null, null);
        Assert.Null(result);
    }

    // ════════════════════════════════════════════════════════
    // 注記補完: トリガー検出
    // ════════════════════════════════════════════════════════

    [Fact]
    public void DetectAnnotationTrigger_WithTrigger_ReturnsFilter()
    {
        var result = _service.DetectAnnotationTrigger("テスト［＃ぼう");
        Assert.Equal("ぼう", result);
    }

    [Fact]
    public void DetectAnnotationTrigger_JustTrigger_ReturnsEmpty()
    {
        var result = _service.DetectAnnotationTrigger("テスト［＃");
        Assert.Equal("", result);
    }

    [Fact]
    public void DetectAnnotationTrigger_NoTrigger_ReturnsNull()
    {
        var result = _service.DetectAnnotationTrigger("普通のテキスト");
        Assert.Null(result);
    }

    [Fact]
    public void DetectAnnotationTrigger_CompletedAnnotation_ReturnsNull()
    {
        var result = _service.DetectAnnotationTrigger("テスト［＃傍点］");
        Assert.Null(result);
    }

    // ════════════════════════════════════════════════════════
    // 注記補完: サジェスト候補
    // ════════════════════════════════════════════════════════

    [Fact]
    public void GetAnnotationSuggestions_EmptyFilter_ReturnsTopItems()
    {
        var items = _service.GetAnnotationSuggestions("");
        Assert.NotEmpty(items);
        // 高優先度の「傍点」が含まれるはず
        Assert.Contains(items, i => i.DisplayName == "傍点");
    }

    [Fact]
    public void GetAnnotationSuggestions_Filter_ReturnsMatches()
    {
        var items = _service.GetAnnotationSuggestions("傍");
        Assert.NotEmpty(items);
        Assert.All(items, i => Assert.StartsWith("傍", i.DisplayName));
    }

    [Fact]
    public void GetAnnotationSuggestions_NoMatch_ReturnsEmpty()
    {
        var items = _service.GetAnnotationSuggestions("zzz存在しない");
        Assert.Empty(items);
    }

    // ════════════════════════════════════════════════════════
    // ルビ入力アシスト
    // ════════════════════════════════════════════════════════

    [Fact]
    public void InsertRuby_NoSelection_InsertsTemplate()
    {
        var result = _service.InsertRuby(null);
        Assert.Equal("|《》", result.TextToInsert);
        Assert.Equal(1, result.CursorOffset);
    }

    [Fact]
    public void InsertRuby_WithSelection_WrapsText()
    {
        var result = _service.InsertRuby("漢字");
        Assert.Equal("|漢字《》", result.TextToInsert);
        Assert.Equal(4, result.CursorOffset); // "漢字".Length + 2
    }

    // ════════════════════════════════════════════════════════
    // スニペット
    // ════════════════════════════════════════════════════════

    [Fact]
    public void GetSnippet_Ruby_NoSelection()
    {
        var result = _service.GetSnippet("ruby", null);
        Assert.Equal("|《》", result.TextToInsert);
        Assert.Equal(1, result.CursorOffset);
    }

    [Fact]
    public void GetSnippet_Ruby_WithSelection()
    {
        var result = _service.GetSnippet("ruby", "漢字");
        Assert.Equal("|漢字《》", result.TextToInsert);
    }

    [Fact]
    public void GetSnippet_Emphasis_NoSelection()
    {
        var result = _service.GetSnippet("emphasis", null);
        Assert.Equal("《《》》", result.TextToInsert);
        Assert.Equal(2, result.CursorOffset);
    }

    [Fact]
    public void GetSnippet_Emphasis_WithSelection()
    {
        var result = _service.GetSnippet("emphasis", "強調");
        Assert.Equal("《《強調》》", result.TextToInsert);
    }

    [Fact]
    public void GetSnippet_Bold_NoSelection()
    {
        var result = _service.GetSnippet("bold", null);
        Assert.Equal("****", result.TextToInsert);
        Assert.Equal(2, result.CursorOffset);
    }

    [Fact]
    public void GetSnippet_Bold_WithSelection()
    {
        var result = _service.GetSnippet("bold", "太字");
        Assert.Equal("**太字**", result.TextToInsert);
    }

    [Fact]
    public void GetSnippet_Heading_NoSelection()
    {
        var result = _service.GetSnippet("heading", null);
        Assert.Equal("# ", result.TextToInsert);
        Assert.True(result.IsLineLevel);
    }

    [Fact]
    public void GetSnippet_PageBreak()
    {
        var result = _service.GetSnippet("pagebreak", null);
        Assert.Equal("---", result.TextToInsert);
        Assert.True(result.IsLineLevel);
        Assert.True(result.InsertNewlineAfter);
    }

    [Fact]
    public void GetSnippet_IndentStart()
    {
        var result = _service.GetSnippet("indent_start", null);
        Assert.Equal("［＃ここから１字下げ］", result.TextToInsert);
        Assert.True(result.IsLineLevel);
    }

    [Fact]
    public void GetSnippet_IndentEnd()
    {
        var result = _service.GetSnippet("indent_end", null);
        Assert.Equal("［＃ここで字下げ終わり］", result.TextToInsert);
    }

    [Fact]
    public void GetSnippet_Unknown_Throws()
    {
        Assert.Throws<ArgumentException>(() => _service.GetSnippet("unknown", null));
    }

    // ════════════════════════════════════════════════════════
    // ユーティリティ
    // ════════════════════════════════════════════════════════

    [Theory]
    [InlineData('「', true)]
    [InlineData('《', true)]
    [InlineData('(', true)]
    [InlineData('あ', false)]
    [InlineData('」', false)]
    public void IsOpenBracket(char c, bool expected)
    {
        Assert.Equal(expected, EditorSuggestService.IsOpenBracket(c));
    }

    [Theory]
    [InlineData('」', true)]
    [InlineData('》', true)]
    [InlineData(')', true)]
    [InlineData('あ', false)]
    [InlineData('「', false)]
    public void IsCloseBracket(char c, bool expected)
    {
        Assert.Equal(expected, EditorSuggestService.IsCloseBracket(c));
    }
}
