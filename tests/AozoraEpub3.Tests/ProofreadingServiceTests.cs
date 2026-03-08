using AozoraEpub3.Core.Editor;

namespace AozoraEpub3.Tests;

public class ProofreadingServiceTests
{
    private readonly ProofreadingService _service = new();

    // ───── P1: 表記ゆれ検出 ─────────────────────────────────────────

    [Fact]
    public void P1_DetectsNotationVariant_行う_行なう()
    {
        var text = "彼は仕事を行う。そして調査を行なう。";
        var warnings = _service.Check(text);
        Assert.Contains(warnings, w => w.Rule == "P1" && w.Message.Contains("表記ゆれ"));
    }

    [Fact]
    public void P1_NoWarning_WhenConsistent()
    {
        var text = "彼は仕事を行う。調査も行う。";
        var warnings = _service.Check(text);
        Assert.DoesNotContain(warnings, w => w.Rule == "P1");
    }

    [Fact]
    public void P1_DetectsVariant_できる_出来る()
    {
        var text = "それはできる。しかし出来ることは限られる。";
        var warnings = _service.Check(text);
        Assert.Contains(warnings, w => w.Rule == "P1" && w.Message.Contains("表記ゆれ"));
    }

    [Fact]
    public void P1_SuggestsMinorityToMajority()
    {
        // 「行う」3回、「行なう」1回 → 「行なう」に警告、「行う」をSuggestion
        var text = "行う。行う。行う。行なう。";
        var warnings = _service.Check(text);
        var w = warnings.First(w => w.Rule == "P1");
        Assert.Equal("行う", w.Suggestion);
    }

    // ───── P2: 括弧不整合検出 ───────────────────────────────────────

    [Fact]
    public void P2_DetectsMismatchedBrackets()
    {
        var text = "「こんにちは」と「さようなら";
        var warnings = _service.Check(text);
        Assert.Contains(warnings, w => w.Rule == "P2" && w.Message.Contains("括弧の不整合"));
    }

    [Fact]
    public void P2_NoWarning_WhenMatched()
    {
        var text = "「こんにちは」と「さようなら」";
        var warnings = _service.Check(text);
        Assert.DoesNotContain(warnings, w => w.Rule == "P2");
    }

    [Fact]
    public void P2_DetectsNestedMismatch()
    {
        var text = "「『二重括弧」";
        var warnings = _service.Check(text);
        Assert.Contains(warnings, w => w.Rule == "P2" && w.Message.Contains("『"));
    }

    [Fact]
    public void P2_DetectsAnnotationBracketMismatch()
    {
        var text = "［＃傍点ここから";
        var warnings = _service.Check(text);
        Assert.Contains(warnings, w => w.Rule == "P2" && w.Message.Contains("［"));
    }

    // ───── P3: 連続重複表現検出 ─────────────────────────────────────

    [Fact]
    public void P3_DetectsConsecutiveDuplicate()
    {
        var text = "彼女はそのときそのとき考えていた。";
        var warnings = _service.Check(text);
        Assert.Contains(warnings, w => w.Rule == "P3" && w.Message.Contains("連続重複"));
    }

    [Fact]
    public void P3_IgnoresIntentionalRepetition()
    {
        // 擬音語は除外
        var text = "心臓がどきどきした。";
        var warnings = _service.Check(text);
        Assert.DoesNotContain(warnings, w => w.Rule == "P3");
    }

    [Fact]
    public void P3_IgnoresSingleCharRepetition()
    {
        var text = "ははは";
        var warnings = _service.Check(text);
        Assert.DoesNotContain(warnings, w => w.Rule == "P3");
    }

    // ───── 総合 ─────────────────────────────────────────────────────

    [Fact]
    public void Check_EmptyText_ReturnsNoWarnings()
    {
        Assert.Empty(_service.Check(""));
        Assert.Empty(_service.Check(null!));
    }

    [Fact]
    public void Check_CleanText_ReturnsNoWarnings()
    {
        var text = "　駅のホームで、見覚えのない手紙を拾った。\n　宛名も差出人もない。";
        var warnings = _service.Check(text);
        Assert.Empty(warnings);
    }

    [Fact]
    public void Check_ReportsLineAndColumn()
    {
        var text = "行う。\n行なう。";
        var warnings = _service.Check(text);
        var w = warnings.First(w => w.Rule == "P1");
        Assert.True(w.Line >= 1);
        Assert.True(w.Column >= 1);
    }
}
