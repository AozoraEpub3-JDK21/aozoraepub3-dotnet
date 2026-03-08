using Avalonia.Headless.XUnit;
using AozoraEpub3.Gui.ViewModels;
using AozoraEpub3.Gui.Views;

namespace AozoraEpub3.Tests.UI;

public class EditorUITests
{
    [AvaloniaFact]
    public void Editor_InitialState_IsEmpty()
    {
        var mainVm = new MainWindowViewModel();
        var window = new MainWindow { DataContext = mainVm };
        window.Show();

        mainVm.NavigateToCommand.Execute("editor");
        var vm = mainVm.EditorVm;

        Assert.Empty(vm.EditorText);
        Assert.False(vm.IsDirty);
        Assert.Null(vm.CurrentFilePath);
    }

    [AvaloniaFact]
    public void Editor_SetText_UpdatesCharAndLineCount()
    {
        var mainVm = new MainWindowViewModel();
        var window = new MainWindow { DataContext = mainVm };
        window.Show();

        var vm = mainVm.EditorVm;
        vm.EditorText = "テスト文章\n二行目";

        Assert.True(vm.CharacterCount > 0);
        Assert.Equal(2, vm.LineCount);
    }

    [AvaloniaFact]
    public void Editor_SetText_SetsDirty()
    {
        var mainVm = new MainWindowViewModel();
        var window = new MainWindow { DataContext = mainVm };
        window.Show();

        var vm = mainVm.EditorVm;
        vm.EditorText = "something";

        Assert.True(vm.IsDirty);
    }

    [AvaloniaFact]
    public void Editor_NewFile_ClearsState()
    {
        var mainVm = new MainWindowViewModel();
        var window = new MainWindow { DataContext = mainVm };
        window.Show();

        var vm = mainVm.EditorVm;
        vm.EditorText = "some text";
        vm.NewFileCommand.Execute(null);

        Assert.Empty(vm.EditorText);
        Assert.False(vm.IsDirty);
        Assert.Null(vm.CurrentFilePath);
    }

    [AvaloniaFact]
    public void Editor_TogglePreview_ChangesVisibility()
    {
        var mainVm = new MainWindowViewModel();
        var window = new MainWindow { DataContext = mainVm };
        window.Show();

        var vm = mainVm.EditorVm;
        var before = vm.IsPreviewVisible;
        vm.TogglePreviewCommand.Execute(null);

        Assert.NotEqual(before, vm.IsPreviewVisible);
    }

    [AvaloniaFact]
    public void Editor_ModeSwitch_ChangesIndex()
    {
        var mainVm = new MainWindowViewModel();
        var window = new MainWindow { DataContext = mainVm };
        window.Show();

        var vm = mainVm.EditorVm;
        vm.SelectedModeIndex = 1; // なろう

        Assert.Equal(1, vm.SelectedModeIndex);
        Assert.Equal("なろう", vm.ModeNames[vm.SelectedModeIndex]);
    }

    [AvaloniaFact]
    public void Editor_CheatSheet_TogglesVisibility()
    {
        var mainVm = new MainWindowViewModel();
        var window = new MainWindow { DataContext = mainVm };
        window.Show();

        var vm = mainVm.EditorVm;
        var before = vm.CheatSheet.IsVisible;
        vm.CheatSheet.ToggleCommand.Execute(null);

        Assert.NotEqual(before, vm.CheatSheet.IsVisible);
    }
}
