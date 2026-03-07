using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using AozoraEpub3.Gui.ViewModels;

namespace AozoraEpub3.Gui.Views;

public partial class ValidateView : UserControl
{
    public ValidateView()
    {
        InitializeComponent();
        this.Loaded += OnLoaded;
    }

    private ValidateViewModel? ViewModel => DataContext as ValidateViewModel;

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        if (this.FindControl<Button>("BrowseJarButton") is { } jarBtn)
            jarBtn.Click += OnBrowseJarClick;

        if (this.FindControl<Button>("BrowseEpubButton") is { } epubBtn)
            epubBtn.Click += OnBrowseEpubClick;
    }

    private async void OnBrowseJarClick(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null || ViewModel is null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select epubcheck.jar",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("JAR files") { Patterns = ["*.jar"] },
                new FilePickerFileType("All files") { Patterns = ["*"] }
            ]
        });

        if (files.Count > 0)
            ViewModel.JarPath = files[0].Path.LocalPath;
    }

    private async void OnBrowseEpubClick(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null || ViewModel is null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select EPUB",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("EPUB files") { Patterns = ["*.epub", "*.kepub.epub"] },
                new FilePickerFileType("All files") { Patterns = ["*"] }
            ]
        });

        if (files.Count > 0)
            ViewModel.EpubFilePath = files[0].Path.LocalPath;
    }
}
