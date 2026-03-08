using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using AozoraEpub3.Gui.ViewModels;
using AozoraEpub3.Gui.Views;

namespace AozoraEpub3.Tests.UI;

public class CardBoardUITests
{
    [AvaloniaFact]
    public void CardBoard_AddCard_IncreasesCardCount()
    {
        var mainVm = new MainWindowViewModel();
        var window = new MainWindow { DataContext = mainVm };
        window.Show();

        mainVm.NavigateToCommand.Execute("cards");
        var vm = mainVm.CardBoardVm;

        var initialCount = vm.Cards.Count;
        vm.AddCardCommand.Execute(null);

        Assert.Equal(initialCount + 1, vm.Cards.Count);
    }

    [AvaloniaFact]
    public void CardBoard_AddCard_SelectsNewCard()
    {
        var mainVm = new MainWindowViewModel();
        var window = new MainWindow { DataContext = mainVm };
        window.Show();

        mainVm.NavigateToCommand.Execute("cards");
        var vm = mainVm.CardBoardVm;

        vm.AddCardCommand.Execute(null);

        Assert.NotNull(vm.SelectedCard);
        Assert.NotEmpty(vm.SelectedCard!.Title);
    }

    [AvaloniaFact]
    public void CardBoard_DeleteCard_RemovesCard()
    {
        var mainVm = new MainWindowViewModel();
        var window = new MainWindow { DataContext = mainVm };
        window.Show();

        var vm = mainVm.CardBoardVm;
        vm.AddCardCommand.Execute(null);
        vm.AddCardCommand.Execute(null);

        var countBefore = vm.Cards.Count;
        vm.SelectedCard = vm.Cards[0];
        vm.DeleteCardCommand.Execute(null);

        Assert.Equal(countBefore - 1, vm.Cards.Count);
    }

    [AvaloniaFact]
    public void CardBoard_StatsText_UpdatesAfterAddCard()
    {
        var mainVm = new MainWindowViewModel();
        var window = new MainWindow { DataContext = mainVm };
        window.Show();

        var vm = mainVm.CardBoardVm;
        vm.AddCardCommand.Execute(null);

        Assert.Contains("全", vm.StatsText);
        Assert.Contains("字", vm.StatsText);
    }

    [AvaloniaFact]
    public void CardBoard_TogglePreview_ChangesVisibility()
    {
        var mainVm = new MainWindowViewModel();
        var window = new MainWindow { DataContext = mainVm };
        window.Show();

        var vm = mainVm.CardBoardVm;
        var before = vm.IsPreviewVisible;

        vm.TogglePreviewCommand.Execute(null);

        Assert.NotEqual(before, vm.IsPreviewVisible);
    }
}
