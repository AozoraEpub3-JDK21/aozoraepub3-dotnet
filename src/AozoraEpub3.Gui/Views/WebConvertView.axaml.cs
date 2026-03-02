using Avalonia.Controls;
using Avalonia.Interactivity;
using AozoraEpub3.Gui.ViewModels;

namespace AozoraEpub3.Gui.Views;

public partial class WebConvertView : UserControl
{
    public WebConvertView()
    {
        InitializeComponent();
        this.Loaded += OnLoaded;
    }

    private WebConvertViewModel? ViewModel => DataContext as WebConvertViewModel;

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        if (this.FindControl<Button>("OutputDirBrowseButton") is { } btn)
            btn.Click += OnOutputDirBrowseClick;

        if (this.FindControl<Button>("WebConfigDirBrowseButton") is { } wcBtn)
            wcBtn.Click += OnWebConfigDirBrowseClick;
    }

    private async void OnOutputDirBrowseClick(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null || ViewModel is null) return;

        var dirs = await topLevel.StorageProvider.OpenFolderPickerAsync(
            new Avalonia.Platform.Storage.FolderPickerOpenOptions
            {
                Title = "出力先ディレクトリを選択",
                AllowMultiple = false
            });

        if (dirs.Count > 0)
            ViewModel.OutputDirectory = dirs[0].Path.LocalPath;
    }

    private async void OnWebConfigDirBrowseClick(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null || ViewModel is null) return;

        var dirs = await topLevel.StorageProvider.OpenFolderPickerAsync(
            new Avalonia.Platform.Storage.FolderPickerOpenOptions
            {
                Title = "web設定ディレクトリを選択",
                AllowMultiple = false
            });

        if (dirs.Count > 0)
            ViewModel.WebConfigDirectory = dirs[0].Path.LocalPath;
    }
}
