using Avalonia.Headless.XUnit;
using AozoraEpub3.Gui.ViewModels;
using AozoraEpub3.Gui.Views;

namespace AozoraEpub3.Tests.UI;

public class PreviewUITests
{
    [AvaloniaFact]
    public void Preview_InitialState_NotLoaded()
    {
        var mainVm = new MainWindowViewModel();
        var window = new MainWindow { DataContext = mainVm };
        window.Show();

        mainVm.NavigateToCommand.Execute("preview");
        var vm = mainVm.PreviewVm;

        Assert.False(vm.IsEpubLoaded);
        Assert.Empty(vm.EpubFilePath);
    }

    [AvaloniaFact]
    public void Preview_TocToggle_ChangesVisibility()
    {
        var mainVm = new MainWindowViewModel();
        var window = new MainWindow { DataContext = mainVm };
        window.Show();

        var vm = mainVm.PreviewVm;
        var before = vm.IsTocOpen;
        vm.ToggleTocCommand.Execute(null);

        Assert.NotEqual(before, vm.IsTocOpen);
    }

    [AvaloniaFact]
    public void Preview_NavigateToCards_RaisesEvent()
    {
        var mainVm = new MainWindowViewModel();
        var window = new MainWindow { DataContext = mainVm };
        window.Show();

        // NavigateToCards は NavigateToCardsRequested イベントを発火
        // MainWindowViewModel がそれを受けて cards に遷移する
        mainVm.NavigateToCommand.Execute("preview");
        mainVm.PreviewVm.NavigateToCardsCommand.Execute(null);

        Assert.IsType<CardBoardViewModel>(mainVm.CurrentPage);
    }

    [AvaloniaFact]
    public void Preview_MaximizeToggle_HidesHeaderAndSidebar()
    {
        var mainVm = new MainWindowViewModel();
        var window = new MainWindow { DataContext = mainVm };
        window.Show();

        mainVm.NavigateToCommand.Execute("preview");

        Assert.True(mainVm.IsHeaderVisible);
        Assert.True(mainVm.IsSidebarVisible);

        // IsPreviewMaximized を直接操作して確認（ToggleMaximizeRequested はイベント）
        mainVm.IsPreviewMaximized = true;

        Assert.True(mainVm.IsPreviewMaximized);
        Assert.False(mainVm.IsHeaderVisible);
        Assert.False(mainVm.IsSidebarVisible);
    }
}
