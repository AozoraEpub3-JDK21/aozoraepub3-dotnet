using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using AozoraEpub3.Gui.Controls;
using AozoraEpub3.Gui.Services;
using AozoraEpub3.Gui.ViewModels;

namespace AozoraEpub3.Gui.Views;

/// <summary>
/// EPUB プレビュー画面のコードビハインド。
/// WebView2Host へのナビゲーション仲介、目次クリック、ファイルダイアログを担当する。
/// </summary>
public partial class PreviewView : UserControl
{
    private WebView2Host? _webViewHost;
    private Border? _webViewContainer;
    private PreviewViewModel? _currentVm;

    public PreviewView()
    {
        InitializeComponent();
        this.Loaded += OnLoaded;
        this.DataContextChanged += OnDataContextChanged;
    }

    private PreviewViewModel? ViewModel => DataContext as PreviewViewModel;

    // ───── DataContext 変更時: ViewModel イベント購読 ─────────────────────

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        // 古い VM の購読解除
        if (_currentVm != null)
        {
            _currentVm.NavigationRequested -= OnNavigationRequested;
            _currentVm.PropertyChanged -= OnViewModelPropertyChanged;
            _currentVm.FontChanged -= OnFontChanged;
        }

        _currentVm = ViewModel;

        if (_currentVm != null)
        {
            _currentVm.NavigationRequested += OnNavigationRequested;
            _currentVm.PropertyChanged += OnViewModelPropertyChanged;
            _currentVm.FontChanged += OnFontChanged;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PreviewViewModel.IsEpubLoaded) && _currentVm?.IsEpubLoaded == true)
        {
            // EPUB が読み込まれたら WebView2 を確実に作成
            EnsureWebViewCreated();
        }
    }

    private void OnNavigationRequested(Uri uri)
    {
        EnsureWebViewCreated();
        _webViewHost?.Navigate(uri);
    }

    private void OnFontChanged(string fontFamily)
    {
        _webViewHost?.SetFont(fontFamily);
    }

    // ───── Loaded ─────────────────────────────────────────────────────────

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        _webViewContainer = this.FindControl<Border>("WebViewContainer");

        // ウェルカム画面の「Open EPUB」ボタン
        if (this.FindControl<Button>("WelcomeOpenButton") is { } welcomeBtn)
            welcomeBtn.Click += OnOpenEpubClick;

        // ツールバーの「Open EPUB」ボタン
        if (this.FindControl<Button>("ToolbarOpenButton") is { } toolbarBtn)
            toolbarBtn.Click += OnOpenEpubClick;

        // 最大化ボタン
        if (this.FindControl<Button>("MaximizeButton") is { } maxBtn)
            maxBtn.Click += (_, _) => _currentVm?.RequestToggleMaximize();

        // 目次リストの選択変更 + キー操作
        if (this.FindControl<ListBox>("TocListBox") is { } tocList)
        {
            tocList.SelectionChanged += OnTocSelectionChanged;
            tocList.KeyDown += OnTocListKeyDown;
        }
    }

    // ───── WebView2Host の遅延生成 ────────────────────────────────────────

    private void EnsureWebViewCreated()
    {
        if (_webViewHost != null || _webViewContainer == null) return;

        _webViewHost = new WebView2Host();
        _webViewHost.SpineNavigationRequested += OnSpineNavigationRequested;

        // 現在選択中のフォントを設定
        if (_currentVm != null)
            _webViewHost.SetFont(_currentVm.SelectedFont);

        _webViewContainer.Child = _webViewHost;
    }

    private void OnSpineNavigationRequested(string direction)
    {
        if (_currentVm == null) return;
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            if (direction == "prev")
                _currentVm.GoPrevCommand.Execute(null);
            else
                _currentVm.GoNextCommand.Execute(null);
        });
    }

    // ───── 目次リストのキー操作 → WebView2 横スクロール ────────────────

    private void OnTocListKeyDown(object? sender, KeyEventArgs e)
    {
        if (_webViewHost == null) return;

        switch (e.Key)
        {
            case Key.Left:
                _webViewHost.ScrollHorizontal(-80);
                e.Handled = true;
                break;
            case Key.Right:
                _webViewHost.ScrollHorizontal(80);
                e.Handled = true;
                break;
            case Key.PageUp:
                _webViewHost.ScrollPage(1); // 縦書き: 右方向=前ページ
                e.Handled = true;
                break;
            case Key.PageDown:
                _webViewHost.ScrollPage(-1); // 縦書き: 左方向=次ページ
                e.Handled = true;
                break;
        }
    }

    // ───── 目次クリックでページ移動 ──────────────────────────────────────

    private void OnTocSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is not ListBox lb) return;
        if (lb.SelectedItem is TocEntry entry && ViewModel is { } vm)
        {
            vm.GoToPageCommand.Execute(entry.SpineIndex);
        }
    }

    // ───── ファイルダイアログ ────────────────────────────────────────────

    private async void OnOpenEpubClick(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null || ViewModel is null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open EPUB",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("EPUB")
                {
                    Patterns = ["*.epub", "*.kepub.epub"]
                },
                new FilePickerFileType("All files") { Patterns = ["*"] }
            ]
        });

        if (files.Count > 0)
        {
            var path = files[0].Path.LocalPath;
            ViewModel.OpenEpubCommand.Execute(path);
        }
    }
}
