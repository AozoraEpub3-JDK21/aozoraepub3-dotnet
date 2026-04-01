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
    public async Task Editor_NewFile_ClearsState()
    {
        var mainVm = new MainWindowViewModel();
        var window = new MainWindow { DataContext = mainVm };
        window.Show();

        var vm = mainVm.EditorVm;
        vm.EditorText = "some text";
        await vm.NewFileCommand.ExecuteAsync(null);

        Assert.Empty(vm.EditorText);
        Assert.False(vm.IsDirty);
        Assert.Null(vm.CurrentFilePath);
    }

    [AvaloniaFact]
    public async Task Editor_NewFile_WhenDiscardRejected_KeepsState()
    {
        var vm = new EditorViewModel
        {
            EditorText = "keep me"
        };
        vm.ConfirmDiscardChangesRequested += () => Task.FromResult(false);

        await vm.NewFileCommand.ExecuteAsync(null);

        Assert.Equal("keep me", vm.EditorText);
        Assert.True(vm.IsDirty);
    }

    [AvaloniaFact]
    public async Task Editor_OpenFile_WhenDiscardRejected_KeepsState()
    {
        var tempFile = Path.GetTempFileName();
        await File.WriteAllTextAsync(tempFile, "new content");
        try
        {
            var vm = new EditorViewModel
            {
                EditorText = "original"
            };
            vm.ConfirmDiscardChangesRequested += () => Task.FromResult(false);
            vm.OpenFileRequested += () => Task.FromResult<string?>(tempFile);

            await vm.OpenFileCommand.ExecuteAsync(null);

            Assert.Equal("original", vm.EditorText);
            Assert.True(vm.IsDirty);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [AvaloniaFact]
    public async Task Editor_OpenFile_WhenDiscardAccepted_LoadsFile()
    {
        var tempFile = Path.GetTempFileName();
        await File.WriteAllTextAsync(tempFile, "new content");
        try
        {
            var vm = new EditorViewModel
            {
                EditorText = "original"
            };
            vm.ConfirmDiscardChangesRequested += () => Task.FromResult(true);
            vm.OpenFileRequested += () => Task.FromResult<string?>(tempFile);

            await vm.OpenFileCommand.ExecuteAsync(null);

            Assert.Equal("new content", vm.EditorText);
            Assert.Equal(tempFile, vm.CurrentFilePath);
            Assert.False(vm.IsDirty);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
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
