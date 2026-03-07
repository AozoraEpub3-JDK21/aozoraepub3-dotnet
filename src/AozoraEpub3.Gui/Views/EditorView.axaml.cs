using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AozoraEpub3.Core.Editor;
using AozoraEpub3.Gui.Controls;
using AozoraEpub3.Gui.ViewModels;

namespace AozoraEpub3.Gui.Views;

public partial class EditorView : UserControl
{
    private WebView2Host? _webView;
    private readonly EditorSuggestService _suggestService = new();
    private ListBox? _suggestList;
    private Popup? _suggestPopup;
    private string _suggestFilter = "";
    private bool _isSuggestActive;
    private ScrollViewer? _textBoxScrollViewer;

    public EditorView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;

        EditorTextBox.PropertyChanged += OnEditorTextBoxPropertyChanged;
        EditorTextBox.TextChanged += OnEditorTextBoxTextChanged;

        // スクロール同期のため ScrollViewer を取得
        EditorTextBox.TemplateApplied += OnTextBoxTemplateApplied;
    }

    // ── 行番号同期 ──────────────────────────────────────────────

    private void OnTextBoxTemplateApplied(object? sender, TemplateAppliedEventArgs e)
    {
        // TextBox 内部の ScrollViewer を取得
        _textBoxScrollViewer = EditorTextBox.FindDescendantOfType<ScrollViewer>();
        if (_textBoxScrollViewer != null)
        {
            _textBoxScrollViewer.ScrollChanged += OnTextBoxScrollChanged;
            // 行番号パネルの Margin を ScrollViewer のオフセットに同期
            UpdateLineNumberScroll();
        }
    }

    private void OnTextBoxScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        UpdateLineNumberScroll();
    }

    private void UpdateLineNumberScroll()
    {
        if (_textBoxScrollViewer == null) return;
        // 行番号パネルの垂直位置を TextBox のスクロールに同期
        var offset = _textBoxScrollViewer.Offset;
        LineNumberBlock.Margin = new Thickness(0, -offset.Y + 4, 0, 0);
    }

    private void OnEditorTextBoxPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property.Name == "Text")
            UpdateLineNumbers();
    }

    private void OnEditorTextBoxTextChanged(object? sender, TextChangedEventArgs e)
    {
        UpdateLineNumbers();
    }

    private void UpdateLineNumbers()
    {
        var text = EditorTextBox.Text ?? "";
        var lineCount = text.Length == 0 ? 1 : text.Split('\n').Length;
        var numbers = string.Join('\n', Enumerable.Range(1, lineCount));
        LineNumberBlock.Text = numbers;
    }

    // ── ViewModel 接続 ───────────────────────────────────────────

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is EditorViewModel vm)
        {
            vm.PreviewUpdateRequested += OnPreviewUpdateRequested;
            vm.SnippetInsertRequested += OnSnippetInsertRequested;
            vm.OpenFileRequested += OnOpenFileRequested;
            vm.SaveFileRequested += OnSaveFileRequested;
        }
    }

    private async Task<string?> OnOpenFileRequested()
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return null;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions
        {
            Title = "テキストファイルを開く",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new Avalonia.Platform.Storage.FilePickerFileType("テキストファイル") { Patterns = ["*.txt", "*.md"] },
                new Avalonia.Platform.Storage.FilePickerFileType("すべてのファイル") { Patterns = ["*"] }
            ]
        });

        return files.Count > 0 ? files[0].Path.LocalPath : null;
    }

    private async Task<string?> OnSaveFileRequested(string? currentPath)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return null;

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new Avalonia.Platform.Storage.FilePickerSaveOptions
        {
            Title = "名前を付けて保存",
            SuggestedFileName = currentPath != null ? Path.GetFileName(currentPath) : "novel.txt",
            FileTypeChoices =
            [
                new Avalonia.Platform.Storage.FilePickerFileType("テキストファイル") { Patterns = ["*.txt"] },
                new Avalonia.Platform.Storage.FilePickerFileType("Markdown") { Patterns = ["*.md"] }
            ]
        });

        return file?.Path.LocalPath;
    }

    // ── WebView2 プレビュー ────────────────────────────────────

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        Dispatcher.UIThread.Post(() => EditorTextBox.Focus(),
            DispatcherPriority.Loaded);

        if (!OperatingSystem.IsWindows()) return;
        if (_webView != null) return;

        _webView = new WebView2Host();
        PreviewContainer.Children.Add(_webView);

        _webView.WebViewReady += (_, _) =>
        {
            if (DataContext is EditorViewModel vm && !string.IsNullOrEmpty(vm.PreviewHtml))
                _webView.NavigateToString(vm.PreviewHtml);
        };
    }

    private void OnPreviewUpdateRequested(string xhtml)
    {
        if (_webView == null || !_webView.IsWebViewReady) return;
        Dispatcher.UIThread.Post(() =>
        {
            _webView.NavigateToString(xhtml);
        });
    }

    // ── スニペット挿入 ──────────────────────────────────────────

    private void OnSnippetInsertRequested(SnippetInsertRequest request)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var selStart = EditorTextBox.SelectionStart;
            var text = EditorTextBox.Text ?? "";

            if (request.IsLineLevel)
            {
                // 行頭を探す
                var lineStart = text.LastIndexOf('\n', Math.Max(0, selStart - 1)) + 1;
                var newText = text.Insert(lineStart, request.Text + "\n");
                EditorTextBox.Text = newText;
                EditorTextBox.SelectionStart = lineStart + request.Text.Length + 1;
                EditorTextBox.SelectionEnd = EditorTextBox.SelectionStart;
            }
            else
            {
                var newText = text.Insert(selStart, request.Text);
                EditorTextBox.Text = newText;
                EditorTextBox.SelectionStart = selStart + request.CursorOffset;
                EditorTextBox.SelectionEnd = EditorTextBox.SelectionStart;
            }

            EditorTextBox.Focus();
        });
    }

    // ── キーボードショートカット ────────────────────────────────

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (DataContext is not EditorViewModel vm) return;

        if (e.KeyModifiers == KeyModifiers.Control)
        {
            switch (e.Key)
            {
                case Key.N:
                    vm.NewFileCommand.Execute(null);
                    e.Handled = true;
                    break;
                case Key.O:
                    _ = vm.OpenFileCommand.ExecuteAsync(null);
                    e.Handled = true;
                    break;
                case Key.S:
                    _ = vm.SaveFileCommand.ExecuteAsync(null);
                    e.Handled = true;
                    break;
                case Key.R:
                    vm.InsertSnippetCommand.Execute("ruby");
                    e.Handled = true;
                    break;
                case Key.E:
                    vm.InsertSnippetCommand.Execute("emphasis");
                    e.Handled = true;
                    break;
                case Key.B:
                    vm.InsertSnippetCommand.Execute("bold");
                    e.Handled = true;
                    break;
                case Key.H:
                    vm.InsertSnippetCommand.Execute("heading");
                    e.Handled = true;
                    break;
            }
        }
        else if (e.KeyModifiers == (KeyModifiers.Control | KeyModifiers.Shift))
        {
            switch (e.Key)
            {
                case Key.S:
                    _ = vm.SaveFileAsCommand.ExecuteAsync(null);
                    e.Handled = true;
                    break;
                case Key.F:
                    vm.FormatTextCommand.Execute(null);
                    e.Handled = true;
                    break;
                case Key.P:
                    vm.InsertSnippetCommand.Execute("pagebreak");
                    e.Handled = true;
                    break;
            }
        }
        else if (e.Key == Key.F5)
        {
            vm.TogglePreviewCommand.Execute(null);
            e.Handled = true;
        }
    }
}
