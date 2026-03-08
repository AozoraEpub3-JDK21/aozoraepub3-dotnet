using Avalonia.Headless.XUnit;
using AozoraEpub3.Gui.ViewModels;
using AozoraEpub3.Gui.Views;

namespace AozoraEpub3.Tests.UI;

public class LocalConvertUITests
{
    [AvaloniaFact]
    public void LocalConvert_InitialState_NoFiles()
    {
        var mainVm = new MainWindowViewModel();
        var window = new MainWindow { DataContext = mainVm };
        window.Show();

        var vm = mainVm.LocalConvertVm;
        Assert.False(vm.HasFiles);
        Assert.False(vm.IsConverting);
    }

    [AvaloniaFact]
    public void LocalConvert_AddFilePaths_AddsToList()
    {
        var mainVm = new MainWindowViewModel();
        var window = new MainWindow { DataContext = mainVm };
        window.Show();

        // LocalConvert ページから離脱してからファイル追加（ContentControl の描画競合回避）
        mainVm.NavigateToCommand.Execute("settings");
        var vm = mainVm.LocalConvertVm;

        var tempFile = Path.GetTempFileName();
        File.WriteAllText(tempFile, "テスト");
        var txtFile = Path.ChangeExtension(tempFile, ".txt");
        if (File.Exists(txtFile)) File.Delete(txtFile);
        File.Move(tempFile, txtFile);

        try
        {
            vm.AddFilePaths([txtFile]);
            Assert.True(vm.HasFiles);
            Assert.Single(vm.InputFiles);
        }
        finally
        {
            if (File.Exists(txtFile)) File.Delete(txtFile);
        }
    }

    [AvaloniaFact]
    public void LocalConvert_ClearAll_RemovesAllFiles()
    {
        var mainVm = new MainWindowViewModel();
        var window = new MainWindow { DataContext = mainVm };
        window.Show();

        // LocalConvert ページから離脱してからファイル操作
        mainVm.NavigateToCommand.Execute("settings");
        var vm = mainVm.LocalConvertVm;

        var tempFile = Path.GetTempFileName();
        File.WriteAllText(tempFile, "テスト");
        var txtFile = Path.ChangeExtension(tempFile, ".txt");
        if (File.Exists(txtFile)) File.Delete(txtFile);
        File.Move(tempFile, txtFile);

        try
        {
            vm.AddFilePaths([txtFile]);
            Assert.True(vm.HasFiles);

            vm.ClearAllFilesCommand.Execute(null);
            Assert.False(vm.HasFiles);
            Assert.Empty(vm.InputFiles);
        }
        finally
        {
            if (File.Exists(txtFile)) File.Delete(txtFile);
        }
    }

    [AvaloniaFact]
    public void LocalConvert_DrawerToggle_Works()
    {
        var mainVm = new MainWindowViewModel();
        var window = new MainWindow { DataContext = mainVm };
        window.Show();

        var vm = mainVm.LocalConvertVm;
        Assert.False(vm.IsDrawerOpen);

        vm.OpenDrawerCommand.Execute(null);
        Assert.True(vm.IsDrawerOpen);

        vm.CloseDrawerCommand.Execute(null);
        Assert.False(vm.IsDrawerOpen);
    }
}
