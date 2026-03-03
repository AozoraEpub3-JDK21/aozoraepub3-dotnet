using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using AozoraEpub3.Gui.ViewModels;

namespace AozoraEpub3.Gui.Views;

/// <summary>
/// LocalConvertView のコードビハインド。
/// ドラッグ＆ドロップ、ファイルダイアログ、ログ自動スクロールを担当する。
/// </summary>
public partial class LocalConvertView : UserControl
{
    public LocalConvertView()
    {
        InitializeComponent();

        // ビュー全体で D&D を受け付ける
        DragDrop.SetAllowDrop(this, true);

        // D&D ハンドラの登録
        AddHandler(DragDrop.DropEvent, OnDrop);
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DragLeaveEvent, OnDragLeave);

        // ボタンクリックのセットアップ（Loaded 後に行う）
        this.Loaded += OnLoaded;

        // DataContext が設定されたらログ購読を開始
        this.DataContextChanged += OnDataContextChanged;
    }

    private LocalConvertViewModel? ViewModel => DataContext as LocalConvertViewModel;

    // ───── Loaded ─────────────────────────────────────────────────────────────

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        // DropZone への D&D 許可
        if (this.FindControl<Border>("DropZoneBorder") is { } dropZone)
        {
            DragDrop.SetAllowDrop(dropZone, true);
        }

        // 「ファイルを開く」ボタン
        if (this.FindControl<Button>("BrowseButton") is { } browse)
            browse.Click += OnBrowseButtonClick;

        // 出力先ディレクトリ参照ボタン
        if (this.FindControl<Button>("OutputDirBrowseButton") is { } outDirBtn)
            outDirBtn.Click += OnOutputDirBrowseClick;

        // 表紙画像参照ボタン
        if (this.FindControl<Button>("CoverImageBrowseButton") is { } coverBtn)
            coverBtn.Click += OnCoverImageBrowseClick;
    }

    // ───── ログ自動スクロール ──────────────────────────────────────────────────

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (ViewModel is { } vm)
        {
            vm.LogLines.CollectionChanged += OnLogLinesChanged;
        }
    }

    private void OnLogLinesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action != NotifyCollectionChangedAction.Add) return;
        if (this.FindControl<ListBox>("LogListBox") is not { } lb) return;
        if (lb.ItemCount == 0) return;

        // 最後のアイテムにスクロール
        lb.ScrollIntoView(lb.ItemCount - 1);
    }

    // ───── D&D ───────────────────────────────────────────────────────────────

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        if (e.Data.Contains(DataFormats.Files))
        {
            e.DragEffects = DragDropEffects.Copy;
            if (ViewModel is not null) ViewModel.IsDragOver = true;

            // DropZone のビジュアル状態更新
            if (this.FindControl<Border>("DropZoneBorder") is { } dz)
                dz.Classes.Set("drag-over", true);
        }
        else
        {
            e.DragEffects = DragDropEffects.None;
        }
        e.Handled = true;
    }

    private void OnDragLeave(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is not null) ViewModel.IsDragOver = false;
        if (this.FindControl<Border>("DropZoneBorder") is { } dz)
            dz.Classes.Set("drag-over", false);
    }

    private void OnDrop(object? sender, DragEventArgs e)
    {
        if (ViewModel is not null) ViewModel.IsDragOver = false;
        if (this.FindControl<Border>("DropZoneBorder") is { } dz)
            dz.Classes.Set("drag-over", false);

        if (e.Data.Contains(DataFormats.Files))
        {
            var files = e.Data.GetFiles()?.OfType<IStorageFile>() ?? [];
            ViewModel?.AddFiles(files);
        }
        e.Handled = true;
    }

    // ───── ダイアログ ─────────────────────────────────────────────────────────

    private async void OnBrowseButtonClick(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "変換するファイルを選択",
            AllowMultiple = true,
            FileTypeFilter =
            [
                new FilePickerFileType("テキスト/アーカイブ")
                {
                    Patterns = ["*.txt", "*.zip", "*.rar", "*.txtz"]
                },
                new FilePickerFileType("すべてのファイル") { Patterns = ["*"] }
            ]
        });

        ViewModel?.AddFiles(files);
    }

    private async void OnOutputDirBrowseClick(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null || ViewModel is null) return;

        var dirs = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "出力先ディレクトリを選択",
            AllowMultiple = false
        });

        if (dirs.Count > 0)
            ViewModel.OutputDirectory = dirs[0].Path.LocalPath;
    }

    private async void OnCoverImageBrowseClick(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null || ViewModel is null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "表紙画像を選択",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("画像ファイル")
                {
                    Patterns = ["*.png", "*.jpg", "*.jpeg", "*.gif", "*.bmp", "*.webp"]
                }
            ]
        });

        if (files.Count > 0)
            ViewModel.Settings.CoverImagePath = files[0].Path.LocalPath;
    }
}
