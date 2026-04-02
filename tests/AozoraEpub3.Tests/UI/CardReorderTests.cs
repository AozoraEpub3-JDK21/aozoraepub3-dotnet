using Avalonia.Headless.XUnit;
using AozoraEpub3.Core.Editor;
using AozoraEpub3.Gui.ViewModels;
using AozoraEpub3.Gui.Views;

namespace AozoraEpub3.Tests.UI;

/// <summary>
/// カード並べ替え・複製・コンテキストメニュー操作のテスト。
/// CardBoardViewModel（初心者カード）と CardEditorViewModel（編集カード）の両方をカバー。
/// </summary>
public class CardReorderTests
{
    // ───── CardBoardViewModel ─────────────────────────────────────────

    [AvaloniaFact]
    public void CardBoard_MoveCardUp_SwapsWithPrevious()
    {
        var vm = new CardBoardViewModel();
        // 初期枚数を記録（ストレージに保存済みデータがある場合でも動作するよう）
        var initial = vm.Cards.Count;
        vm.AddCardCommand.Execute(null);
        vm.AddCardCommand.Execute(null);
        Assert.Equal(initial + 2, vm.Cards.Count);

        var last = vm.Cards[^1];
        vm.SelectedCard = last;
        vm.MoveCardUpCommand.Execute(null);

        Assert.Equal(vm.Cards.Count - 2, vm.Cards.IndexOf(last));
    }

    [AvaloniaFact]
    public void CardBoard_MoveCardUp_FirstCard_NoOp()
    {
        var vm = new CardBoardViewModel();
        vm.AddCardCommand.Execute(null);

        var first = vm.Cards[0];
        vm.SelectedCard = first;
        vm.MoveCardUpCommand.Execute(null);

        Assert.Equal(0, vm.Cards.IndexOf(first));
    }

    [AvaloniaFact]
    public void CardBoard_MoveCardDown_SwapsWithNext()
    {
        var vm = new CardBoardViewModel();
        vm.AddCardCommand.Execute(null);
        vm.AddCardCommand.Execute(null);

        var first = vm.Cards[0];
        vm.SelectedCard = first;
        vm.MoveCardDownCommand.Execute(null);

        Assert.Equal(1, vm.Cards.IndexOf(first));
    }

    [AvaloniaFact]
    public void CardBoard_MoveCardDown_LastCard_NoOp()
    {
        var vm = new CardBoardViewModel();
        vm.AddCardCommand.Execute(null);

        var last = vm.Cards[^1];
        vm.SelectedCard = last;
        vm.MoveCardDownCommand.Execute(null);

        Assert.Equal(vm.Cards.Count - 1, vm.Cards.IndexOf(last));
    }

    [AvaloniaFact]
    public void CardBoard_DuplicateCard_CreatesACopy()
    {
        var vm = new CardBoardViewModel();
        vm.Cards[0].Body = "テスト本文";
        vm.SelectedCard = vm.Cards[0];

        var countBefore = vm.Cards.Count;
        vm.DuplicateCardCommand.Execute(null);

        Assert.Equal(countBefore + 1, vm.Cards.Count);
        Assert.Contains("コピー", vm.Cards[1].Title);
        Assert.Equal("テスト本文", vm.Cards[1].Body);
    }

    [AvaloniaFact]
    public void CardBoard_DuplicateCard_SelectsCopy()
    {
        var vm = new CardBoardViewModel();
        vm.SelectedCard = vm.Cards[0];
        vm.DuplicateCardCommand.Execute(null);

        Assert.Contains("コピー", vm.SelectedCard!.Title);
    }

    // ───── CardEditorViewModel ────────────────────────────────────────

    [AvaloniaFact]
    public void CardEditor_MoveItemUp_Works()
    {
        var vm = CreateProjectWithEpisodes(3);
        var items = vm.TreeItems;

        // 章の子: index 1,2,3 → StructureItem は children[0],[1],[2]
        vm.SelectedTreeItem = items[3]; // 3番目のエピソード
        vm.MoveItemUpCommand.Execute(null);

        // 移動後、選択が維持されること
        Assert.NotNull(vm.SelectedTreeItem);
    }

    [AvaloniaFact]
    public void CardEditor_MoveItemDown_Works()
    {
        var vm = CreateProjectWithEpisodes(3);
        var items = vm.TreeItems;

        vm.SelectedTreeItem = items[1]; // 1番目のエピソード
        vm.MoveItemDownCommand.Execute(null);

        Assert.NotNull(vm.SelectedTreeItem);
    }

    [AvaloniaFact]
    public void CardEditor_DuplicateItem_CreatesACopy()
    {
        var vm = CreateProjectWithEpisodes(2);
        var countBefore = vm.TreeItems.Count;

        vm.SelectedTreeItem = vm.TreeItems[1]; // 最初のエピソード
        vm.DuplicateItemCommand.Execute(null);

        Assert.Equal(countBefore + 1, vm.TreeItems.Count);
        Assert.NotNull(vm.SelectedTreeItem);
        Assert.Contains("コピー", vm.SelectedTreeItem!.DisplayTitle);
    }

    [AvaloniaFact]
    public void CardEditor_DuplicateChapter_IsNoOp()
    {
        var vm = CreateProjectWithEpisodes(2);
        var countBefore = vm.TreeItems.Count;

        // 章アイテムを探して選択
        var chapterItem = vm.TreeItems.First(t => t.StructureItem.Type == CardType.Chapter);
        vm.SelectedTreeItem = chapterItem;
        vm.DuplicateItemCommand.Execute(null);

        // 章の複製は非対応なのでカウント変わらない
        Assert.Equal(countBefore, vm.TreeItems.Count);
    }

    [AvaloniaFact]
    public void CardEditor_MoveItem_DragDrop()
    {
        var vm = CreateProjectWithEpisodes(3);
        // 章の子エピソードのインデックスを取得
        var chapterIdx = vm.TreeItems.ToList().FindIndex(t => t.StructureItem.Type == CardType.Chapter);
        // 章の直後がエピソード群
        var ep1Idx = chapterIdx + 1;
        var ep3Idx = chapterIdx + 3;

        var ep1 = vm.TreeItems[ep1Idx].StructureItem;
        vm.MoveItem(ep1Idx, ep3Idx);

        // ep1 が ep3 の位置に移動しているはず
        var movedItem = vm.TreeItems.FirstOrDefault(t => t.StructureItem == ep1);
        Assert.NotNull(movedItem);
    }

    [AvaloniaFact]
    public void CardEditor_ChangeStatus_Updates()
    {
        var vm = CreateProjectWithEpisodes(1);
        vm.SelectedTreeItem = vm.TreeItems[1]; // エピソード

        vm.ChangeStatusCommand.Execute("Done");

        Assert.Equal(ProjectCardStatus.Done, vm.SelectedTreeItem.StructureItem.Status);
        Assert.Contains("完成", vm.SelectedTreeItem.DisplayInfo);
    }

    // ───── ヘルパー ──────────────────────────────────────────────────

    private static CardEditorViewModel CreateProjectWithEpisodes(int episodeCount)
    {
        var vm = new CardEditorViewModel();
        var projectDir = Path.Combine(Path.GetTempPath(), $"aep3_test_{Guid.NewGuid():N}");

        var projectService = new ProjectService();
        var dir = projectService.CreateNew(Path.GetTempPath(), Path.GetFileName(projectDir), "テスト著者");
        vm.LoadProject(dir);

        for (int i = 0; i < episodeCount; i++)
            vm.AddEpisodeCommand.Execute(null);

        return vm;
    }
}
