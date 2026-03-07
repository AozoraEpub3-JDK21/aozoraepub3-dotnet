using AozoraEpub3.Core.Editor;

namespace AozoraEpub3.Tests;

public class NovelFormatterTests
{
    private readonly NovelFormatter _formatter = new(ConversionProfile.Default);

    // ════════════════════════════════════════════════════════
    // 三点リーダー
    // ════════════════════════════════════════════════════════

    [Theory]
    [InlineData("テスト...です", "テスト……です")]
    [InlineData("テスト。。。です", "テスト……です")]
    [InlineData("テスト・・・です", "テスト……です")]
    [InlineData("テスト....です", "テスト……です")]
    [InlineData("テスト......です", "テスト……です")]
    public void Format_Ellipsis(string input, string expected)
    {
        Assert.Equal(expected, _formatter.Format(input));
    }

    // ════════════════════════════════════════════════════════
    // ダッシュ
    // ════════════════════════════════════════════════════════

    [Theory]
    [InlineData("テスト--です", "テスト――です")]
    [InlineData("テスト──です", "テスト――です")]
    public void Format_Dash(string input, string expected)
    {
        Assert.Equal(expected, _formatter.Format(input));
    }

    [Fact]
    public void Format_Dash_TripleDashNotAffected()
    {
        // --- は改ページ記法なので、ダッシュ変換は -- のみ
        // 注意: ---は --(-) としてマッチする可能性があるが、
        // 否定先読み/後読みで排除している
        var result = _formatter.Format("---");
        // --- は -- にマッチしない（否定先読み (?!-) で排除）
        Assert.Equal("---", result);
    }

    // ════════════════════════════════════════════════════════
    // 感嘆符後スペース
    // ════════════════════════════════════════════════════════

    [Theory]
    [InlineData("すごい！次の文", "すごい！　次の文")]
    [InlineData("本当？次の文", "本当？　次の文")]
    [InlineData("えっ！？次の文", "えっ！？　次の文")]
    public void Format_ExclamationSpace(string input, string expected)
    {
        Assert.Equal(expected, _formatter.Format(input));
    }

    [Fact]
    public void Format_ExclamationSpace_NotAtEndOfLine()
    {
        // 行末の感嘆符にはスペースを入れない
        var result = _formatter.Format("すごい！\n次の文");
        Assert.Equal("すごい！\n次の文", result);
    }

    [Fact]
    public void Format_ExclamationSpace_NotBeforeCloseBracket()
    {
        var result = _formatter.Format("すごい！」と言った");
        Assert.Equal("すごい！」と言った", result);
    }

    [Fact]
    public void Format_ExclamationSpace_AlreadyHasSpace()
    {
        var result = _formatter.Format("すごい！　次の文");
        Assert.Equal("すごい！　次の文", result);
    }

    // ════════════════════════════════════════════════════════
    // Lint
    // ════════════════════════════════════════════════════════

    [Fact]
    public void Lint_ReportsEllipsis()
    {
        var warnings = _formatter.Lint("1行目\n2行目...テスト");
        Assert.Contains(warnings, w => w.Rule == "L1" && w.Line == 2);
    }

    [Fact]
    public void Lint_ReportsDash()
    {
        var warnings = _formatter.Lint("テスト--です");
        Assert.Contains(warnings, w => w.Rule == "L2");
    }

    [Fact]
    public void Lint_ReportsExclamationSpace()
    {
        var warnings = _formatter.Lint("すごい！次");
        Assert.Contains(warnings, w => w.Rule == "L3");
    }

    [Fact]
    public void Lint_NoWarningsForCleanText()
    {
        var warnings = _formatter.Lint("きれいなテキストです。\n問題ありません。");
        Assert.Empty(warnings);
    }

    // ════════════════════════════════════════════════════════
    // 設定無効化
    // ════════════════════════════════════════════════════════

    [Fact]
    public void Format_AllDisabled_NoChange()
    {
        var formatter = new NovelFormatter(new ConversionProfile
        {
            Mode = EditorMode.Generic,
            AutoEllipsis = false,
            AutoDash = false,
            AutoExclamationSpace = false
        });
        var text = "テスト...です。すごい！次の--文";
        Assert.Equal(text, formatter.Format(text));
    }

    // ════════════════════════════════════════════════════════
    // 空文字列
    // ════════════════════════════════════════════════════════

    [Fact]
    public void Format_Empty_ReturnsEmpty()
    {
        Assert.Equal("", _formatter.Format(""));
    }

    [Fact]
    public void Lint_Empty_ReturnsEmpty()
    {
        Assert.Empty(_formatter.Lint(""));
    }
}
