using System.Collections.Specialized;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using AozoraEpub3.Gui.ViewModels;

namespace AozoraEpub3.Gui.Views;

public partial class WebConvertView : UserControl
{
    public WebConvertView()
    {
        InitializeComponent();
        this.Loaded += OnLoaded;
        this.DataContextChanged += OnDataContextChanged;

        var dropZone = this.FindControl<Border>("UrlDropZone")!;
        DragDrop.SetAllowDrop(dropZone, true);
        dropZone.AddHandler(DragDrop.DragEnterEvent, OnDragEnter);
        dropZone.AddHandler(DragDrop.DragLeaveEvent, OnDragLeave);
        dropZone.AddHandler(DragDrop.DropEvent,      OnDrop);
    }

    private void OnDragEnter(object? sender, DragEventArgs e)
    {
        if (e.Data.Contains(DataFormats.Text) || e.Data.Contains("text/uri-list"))
        {
            e.DragEffects = DragDropEffects.Copy;
            (sender as Border)?.Classes.Add("drag-over");
        }
        else
        {
            e.DragEffects = DragDropEffects.None;
        }
    }

    private void OnDragLeave(object? sender, DragEventArgs e)
        => (sender as Border)?.Classes.Remove("drag-over");

    private void OnDrop(object? sender, DragEventArgs e)
    {
        (sender as Border)?.Classes.Remove("drag-over");
        var url = ExtractUrl(e);
        if (url != null && DataContext is WebConvertViewModel vm)
            vm.Url = url;
    }

    private static string? ExtractUrl(DragEventArgs e)
    {
        if (e.Data.Get("text/uri-list") is string uriList)
        {
            var first = uriList.Split('\n', '\r')[0].Trim();
            if (!string.IsNullOrEmpty(first)) return first;
        }
        return e.Data.GetText();
    }

    private WebConvertViewModel? ViewModel => DataContext as WebConvertViewModel;

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (ViewModel is { } vm)
            vm.LogLines.CollectionChanged += OnLogLinesChanged;
    }

    private void OnLogLinesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action != NotifyCollectionChangedAction.Add) return;
        if (this.FindControl<ListBox>("WebLogListBox") is not { } lb) return;
        if (lb.ItemCount == 0) return;
        lb.ScrollIntoView(lb.ItemCount - 1);
    }

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
