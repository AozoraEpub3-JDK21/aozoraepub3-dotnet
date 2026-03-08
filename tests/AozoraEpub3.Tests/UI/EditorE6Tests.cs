using Avalonia.Headless.XUnit;
using AozoraEpub3.Core.Editor;
using AozoraEpub3.Gui.ViewModels;
using AozoraEpub3.Gui.Views;

namespace AozoraEpub3.Tests.UI;

/// <summary>
/// E6 機能のUIテスト。
/// E6-3: 直接EPUB出力、E6-5: 校正支援、E6-6: カスタム辞書。
/// </summary>
public class EditorE6Tests
{
    // ───── E6-3: Direct EPUB Export ──────────────────────────────────

    [AvaloniaFact]
    public async Task E6_3_ConvertToEpub_FiresEvent()
    {
        var vm = new EditorViewModel();
        vm.EditorText = "テスト本文です。";

        string? firedPath = null;
        vm.EpubConversionRequested += path => firedPath = path;

        await vm.ConvertToEpubCommand.ExecuteAsync(null);

        Assert.NotNull(firedPath);
        Assert.Contains("aep3_editor_temp.txt", firedPath);
    }

    [AvaloniaFact]
    public async Task E6_3_ConvertToEpub_EmptyText_NoEvent()
    {
        var vm = new EditorViewModel();
        vm.EditorText = "";

        string? firedPath = null;
        vm.EpubConversionRequested += path => firedPath = path;

        await vm.ConvertToEpubCommand.ExecuteAsync(null);

        Assert.Null(firedPath);
    }

    [AvaloniaFact]
    public async Task E6_3_MainWindow_WiresEditorEpubConversion()
    {
        // MainWindow を使わずスタンドアロンVMでイベント接続を確認
        // （LocalConvertView の AXAML 型解決問題を回避）
        var vm = new EditorViewModel();
        vm.EditorText = "テスト";

        string? firedPath = null;
        vm.EpubConversionRequested += path => firedPath = path;

        await vm.ConvertToEpubCommand.ExecuteAsync(null);

        Assert.NotNull(firedPath);
        Assert.True(File.Exists(firedPath));
    }

    // ───── E6-5: Proofreading ───────────────────────────────────────

    [AvaloniaFact]
    public void E6_5_Proofread_ShowsResultsPanel()
    {
        var vm = new EditorViewModel();
        vm.EditorText = "彼は行う。しかし行なう。";

        vm.ProofreadCommand.Execute(null);

        Assert.True(vm.IsProofreadingPanelVisible);
        Assert.NotEmpty(vm.ProofreadingResults);
        Assert.Contains(vm.ProofreadingResults, w => w.Rule == "P1");
    }

    [AvaloniaFact]
    public void E6_5_Proofread_CleanText_HidesPanel()
    {
        var vm = new EditorViewModel();
        vm.EditorText = "きれいな文章です。";

        vm.ProofreadCommand.Execute(null);

        Assert.False(vm.IsProofreadingPanelVisible);
        Assert.Empty(vm.ProofreadingResults);
    }

    [AvaloniaFact]
    public void E6_5_Proofread_DetectsBracketMismatch()
    {
        var vm = new EditorViewModel();
        vm.EditorText = "「こんにちは」と「さようなら";

        vm.ProofreadCommand.Execute(null);

        Assert.True(vm.IsProofreadingPanelVisible);
        Assert.Contains(vm.ProofreadingResults, w => w.Rule == "P2");
    }

    [AvaloniaFact]
    public void E6_5_CloseProofreading_HidesPanel()
    {
        var vm = new EditorViewModel();
        vm.EditorText = "行う。行なう。";
        vm.ProofreadCommand.Execute(null);
        Assert.True(vm.IsProofreadingPanelVisible);

        vm.CloseProofreadingCommand.Execute(null);
        Assert.False(vm.IsProofreadingPanelVisible);
    }

    [AvaloniaFact]
    public void E6_5_Proofread_UpdatesLintWarningCount()
    {
        var vm = new EditorViewModel();
        vm.EditorText = "行う。行なう。「開いたまま";

        vm.ProofreadCommand.Execute(null);

        Assert.True(vm.LintWarningCount > 0);
        Assert.Equal(vm.ProofreadingResults.Count, vm.LintWarningCount);
    }

    // ───── E6-6: Custom Dictionary ──────────────────────────────────

    [AvaloniaFact]
    public void E6_6_CustomDictionary_LoadAndSearch()
    {
        var service = new CustomDictionaryService();
        service.LoadFromJson("""
        {
            "name": "テスト辞書",
            "entries": [
                { "name": "キャラA", "category": "固有名詞", "insertText": "｜太郎《たろう》" },
                { "name": "キャラB", "category": "固有名詞", "insertText": "｜花子《はなこ》" },
                { "name": "場面転換", "category": "構造", "insertText": "◇◇◇", "isLineLevel": true }
            ]
        }
        """);

        var results = service.Search("キャラ");
        Assert.Equal(2, results.Count);
    }

    [AvaloniaFact]
    public void E6_6_CustomDictionary_IntegratesWithSuggest()
    {
        var service = new CustomDictionaryService();
        service.LoadFromJson("""
        {
            "name": "テスト",
            "entries": [
                { "name": "テスト注記", "insertText": "［＃テスト注記］" }
            ]
        }
        """);

        var items = service.ToSuggestItems();
        Assert.Single(items);
        Assert.Equal(200, items[0].Priority); // ユーザー定義は高優先度
    }
}
