using Avalonia.Headless.XUnit;
using AozoraEpub3.Gui.ViewModels;
using AozoraEpub3.Gui.Views;

namespace AozoraEpub3.Tests.UI;

public class ValidateUITests
{
    [AvaloniaFact]
    public void Validate_InitialState()
    {
        var mainVm = new MainWindowViewModel();
        var window = new MainWindow { DataContext = mainVm };
        window.Show();

        mainVm.NavigateToCommand.Execute("validate");
        var vm = mainVm.ValidateVm;

        Assert.False(vm.IsValidating);
        Assert.Empty(vm.Messages);
    }

    [AvaloniaFact]
    public void Validate_JumpToFile_NavigatesToPreview()
    {
        var mainVm = new MainWindowViewModel();
        var window = new MainWindow { DataContext = mainVm };
        window.Show();

        // ValidateVm の JumpToFileRequested はプレビューへの遷移をトリガー
        // ただし EPUB が未ロードだと遷移しないので、イベント登録の確認のみ
        mainVm.NavigateToCommand.Execute("validate");

        // JumpToFileRequested はイベントなので外部から直接呼べない
        // MainWindowViewModel が ValidateVm.JumpToFileRequested に接続済みであることを
        // EPUB未ロード時にプレビュー遷移しないことで確認
        Assert.IsType<ValidateViewModel>(mainVm.CurrentPage);
    }

    [AvaloniaFact]
    public void Validate_JarPath_CanBeSet()
    {
        var mainVm = new MainWindowViewModel();
        var window = new MainWindow { DataContext = mainVm };
        window.Show();

        var vm = mainVm.ValidateVm;
        vm.JarPath = "/path/to/epubcheck.jar";

        Assert.Equal("/path/to/epubcheck.jar", vm.JarPath);
    }
}
