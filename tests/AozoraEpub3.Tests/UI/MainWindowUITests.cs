using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using AozoraEpub3.Gui.ViewModels;
using AozoraEpub3.Gui.Views;

namespace AozoraEpub3.Tests.UI;

public class MainWindowUITests
{
    [AvaloniaFact]
    public void MainWindow_Opens_WithLocalConvertPage()
    {
        var vm = new MainWindowViewModel();
        var window = new MainWindow { DataContext = vm };
        window.Show();

        Assert.IsType<LocalConvertViewModel>(vm.CurrentPage);
    }

    [AvaloniaFact]
    public void NavigateTo_Settings_ChangesCurrentPage()
    {
        var vm = new MainWindowViewModel();
        var window = new MainWindow { DataContext = vm };
        window.Show();

        vm.NavigateToCommand.Execute("settings");

        Assert.IsType<SettingsPageViewModel>(vm.CurrentPage);
    }

    [AvaloniaFact]
    public void NavigateTo_Cards_ChangesCurrentPage()
    {
        var vm = new MainWindowViewModel();
        var window = new MainWindow { DataContext = vm };
        window.Show();

        vm.NavigateToCommand.Execute("cards");

        Assert.IsType<CardBoardViewModel>(vm.CurrentPage);
    }

    [AvaloniaFact]
    public void NavigateTo_Editor_ChangesCurrentPage()
    {
        var vm = new MainWindowViewModel();
        var window = new MainWindow { DataContext = vm };
        window.Show();

        vm.NavigateToCommand.Execute("editor");

        Assert.IsType<EditorViewModel>(vm.CurrentPage);
    }

    [AvaloniaFact]
    public void NavigateTo_Project_ChangesCurrentPage()
    {
        var vm = new MainWindowViewModel();
        var window = new MainWindow { DataContext = vm };
        window.Show();

        vm.NavigateToCommand.Execute("project");

        Assert.IsType<CardEditorViewModel>(vm.CurrentPage);
    }

    [AvaloniaFact]
    public void LanguageToggle_ChangesLanguage()
    {
        var vm = new MainWindowViewModel();
        var window = new MainWindow { DataContext = vm };
        window.Show();

        Assert.True(vm.IsJapanese);

        vm.SetLanguageCommand.Execute("en");
        Assert.False(vm.IsJapanese);
        Assert.Equal("en", vm.CurrentLanguage);

        vm.SetLanguageCommand.Execute("ja");
        Assert.True(vm.IsJapanese);
    }

    [AvaloniaFact]
    public void MainWindow_HasSidebarButtons()
    {
        var vm = new MainWindowViewModel();
        var window = new MainWindow { DataContext = vm };
        window.Show();

        // サイドバーのナビボタンが存在するか確認
        var navButtons = window.GetVisualDescendants()
            .OfType<Button>()
            .Where(b => b.Classes.Contains("nav-item"))
            .ToList();

        // 7 nav items + 1 settings = at least 7 nav-item buttons
        Assert.True(navButtons.Count >= 7, $"Expected at least 7 nav-item buttons, found {navButtons.Count}");
    }

    [AvaloniaFact]
    public void ContentControl_ResolvesView_ForLocalConvert()
    {
        var vm = new MainWindowViewModel();
        var window = new MainWindow { DataContext = vm };
        window.Show();

        // ContentControl が正しい View を解決しているか
        var contentControl = window.GetVisualDescendants()
            .OfType<ContentControl>()
            .FirstOrDefault(c => c.Content is LocalConvertViewModel);

        Assert.NotNull(contentControl);
    }
}
