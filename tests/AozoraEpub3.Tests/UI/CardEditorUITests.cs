using Avalonia.Headless.XUnit;
using AozoraEpub3.Core.Editor;
using AozoraEpub3.Gui.ViewModels;
using AozoraEpub3.Gui.Views;

namespace AozoraEpub3.Tests.UI;

public class CardEditorUITests
{
    [AvaloniaFact]
    public void CardEditor_InitialState_NoProjectLoaded()
    {
        var mainVm = new MainWindowViewModel();
        var window = new MainWindow { DataContext = mainVm };
        window.Show();

        mainVm.NavigateToCommand.Execute("project");
        var vm = mainVm.CardEditorVm;

        Assert.False(vm.IsProjectLoaded);
        Assert.Empty(vm.TreeItems);
    }

    [AvaloniaFact]
    public void CardEditor_TogglePreview_Works()
    {
        var mainVm = new MainWindowViewModel();
        var window = new MainWindow { DataContext = mainVm };
        window.Show();

        var vm = mainVm.CardEditorVm;
        var before = vm.IsPreviewVisible;
        vm.TogglePreviewCommand.Execute(null);

        Assert.NotEqual(before, vm.IsPreviewVisible);
    }

    [AvaloniaFact]
    public void CardEditor_ModeNames_HasThreeEntries()
    {
        var mainVm = new MainWindowViewModel();
        var window = new MainWindow { DataContext = mainVm };
        window.Show();

        var vm = mainVm.CardEditorVm;
        Assert.Equal(3, vm.ModeNames.Length);
        Assert.Equal("標準", vm.ModeNames[0]);
    }

    [AvaloniaFact]
    public void CardEditor_CheatSheet_Toggles()
    {
        var mainVm = new MainWindowViewModel();
        var window = new MainWindow { DataContext = mainVm };
        window.Show();

        var vm = mainVm.CardEditorVm;
        var before = vm.CheatSheet.IsVisible;
        vm.CheatSheet.ToggleCommand.Execute(null);

        Assert.NotEqual(before, vm.CheatSheet.IsVisible);
    }

    [AvaloniaFact]
    public void CardEditor_NewProject_WithMockedDialog()
    {
        var mainVm = new MainWindowViewModel();
        var window = new MainWindow { DataContext = mainVm };
        window.Show();

        var vm = mainVm.CardEditorVm;
        // テスト用一時ディレクトリでプロジェクト作成
        var tempDir = Path.Combine(Path.GetTempPath(), "aep3_test_" + Guid.NewGuid().ToString("N")[..8]);

        // SaveProjectAsRequested を差し替えてダイアログなしでパスを返す
        vm.SaveProjectAsRequested += _ => Task.FromResult<string?>(tempDir);

        vm.NewProjectCommand.Execute(null);

        // 少し待ってからチェック（非同期コマンドのため）
        Thread.Sleep(500);

        try
        {
            Assert.True(vm.IsProjectLoaded);
            Assert.NotEmpty(vm.TreeItems);
        }
        finally
        {
            // テスト後クリーンアップ
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [AvaloniaFact]
    public void CardEditor_SetTargetWordCount_RequiresProject()
    {
        var mainVm = new MainWindowViewModel();
        var window = new MainWindow { DataContext = mainVm };
        window.Show();

        var vm = mainVm.CardEditorVm;
        // プロジェクト未ロード時は無視される
        vm.SetTargetWordCountCommand.Execute("10000");

        Assert.Equal(0, vm.TargetWordCount);
    }
}
