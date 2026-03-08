using Avalonia.Headless.XUnit;
using AozoraEpub3.Core.Editor;
using AozoraEpub3.Gui.ViewModels;
using AozoraEpub3.Gui.Views;

namespace AozoraEpub3.Tests.UI;

public class MigrationProposalUITests
{
    [AvaloniaFact]
    public void Migration_NotShown_Initially()
    {
        var mainVm = new MainWindowViewModel();
        var window = new MainWindow { DataContext = mainVm };
        window.Show();
        mainVm.NavigateToCommand.Execute("cards");

        Assert.False(mainVm.CardBoardVm.IsMigrationProposalVisible);
    }

    [AvaloniaFact]
    public void Migration_ShownAfter6Cards()
    {
        var mainVm = new MainWindowViewModel();
        var window = new MainWindow { DataContext = mainVm };
        window.Show();
        mainVm.NavigateToCommand.Execute("cards");

        var vm = mainVm.CardBoardVm;

        // サンプル含めて6話超えるまで追加
        while (vm.Cards.Count <= 5)
            vm.AddCardCommand.Execute(null);

        // 6話超えた時点の次の追加でトリガー
        vm.AddCardCommand.Execute(null);

        Assert.True(vm.IsMigrationProposalVisible);
        Assert.Contains("章分け", vm.MigrationProposalText);
    }

    [AvaloniaFact]
    public void Migration_Dismiss_HidesProposal()
    {
        var mainVm = new MainWindowViewModel();
        var window = new MainWindow { DataContext = mainVm };
        window.Show();

        var vm = mainVm.CardBoardVm;
        while (vm.Cards.Count <= 6)
            vm.AddCardCommand.Execute(null);

        Assert.True(vm.IsMigrationProposalVisible);

        vm.DismissMigrationProposalCommand.Execute(null);
        Assert.False(vm.IsMigrationProposalVisible);
    }

    [AvaloniaFact]
    public void Migration_Suppress_PermanentlyHides()
    {
        var mainVm = new MainWindowViewModel();
        var window = new MainWindow { DataContext = mainVm };
        window.Show();

        var vm = mainVm.CardBoardVm;
        while (vm.Cards.Count <= 6)
            vm.AddCardCommand.Execute(null);

        vm.SuppressMigrationProposals_CommandCommand.Execute(null);

        Assert.True(vm.SuppressMigrationProposals);
        Assert.False(vm.IsMigrationProposalVisible);

        // 再度カードを追加しても提案されない
        vm.AddCardCommand.Execute(null);
        Assert.False(vm.IsMigrationProposalVisible);
    }

    [AvaloniaFact]
    public void Migration_EpubConvert_Trigger_ShowsProposal()
    {
        // ConvertToEpub は EpubConversionRequested → LocalConvert へ遷移し
        // ヘッドレス環境で型解決エラーになるため、
        // EpubConversionRequested 未接続の独立 ViewModel でテスト
        var vm = new CardBoardViewModel();

        // 3話追加（サンプル1 + 追加2 = 3）
        vm.AddCardCommand.Execute(null);
        vm.AddCardCommand.Execute(null);

        Assert.True(vm.Cards.Count >= 3);
        Assert.False(vm.IsMigrationProposalVisible);

        // EPUB変換（EpubConversionRequested 未接続なので遷移しない）
        _ = vm.ConvertToEpubCommand.ExecuteAsync(null);

        // 提案が表示される
        Assert.True(vm.IsMigrationProposalVisible);
        Assert.Contains("構成", vm.MigrationProposalText);
    }

    [AvaloniaFact]
    public void Migration_Accept_NavigatesToProject()
    {
        var mainVm = new MainWindowViewModel();
        var window = new MainWindow { DataContext = mainVm };
        window.Show();
        mainVm.NavigateToCommand.Execute("cards");

        var vm = mainVm.CardBoardVm;
        // テスト用カードを追加
        vm.AddCardCommand.Execute(null);
        vm.AddCardCommand.Execute(null);

        // マイグレーション実行
        vm.AcceptMigrationCommand.Execute(null);

        // CardEditorVm に遷移していること
        Assert.IsType<CardEditorViewModel>(mainVm.CurrentPage);
        Assert.True(mainVm.CardEditorVm.IsProjectLoaded);
    }

    [AvaloniaFact]
    public void Migration_Accept_ImportsAllCards()
    {
        var mainVm = new MainWindowViewModel();
        var window = new MainWindow { DataContext = mainVm };
        window.Show();

        var vm = mainVm.CardBoardVm;
        vm.Cards[0].Body = "最初の話のテスト本文";
        vm.AddCardCommand.Execute(null);
        vm.SelectedCard = vm.Cards[1];
        vm.ActiveCardBody = "二番目の話のテスト本文";

        var cardCount = vm.Cards.Count;

        vm.AcceptMigrationCommand.Execute(null);

        // プロジェクトにカードが移行されている
        var editorVm = mainVm.CardEditorVm;
        Assert.True(editorVm.IsProjectLoaded);

        // ツリーアイテム数 >= カード数 + 表紙 + 章ヘッダ
        Assert.True(editorVm.TreeItems.Count >= cardCount);
    }
}
