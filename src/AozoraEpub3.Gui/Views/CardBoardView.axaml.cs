using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AozoraEpub3.Core.Editor;
using AozoraEpub3.Gui.Controls;
using AozoraEpub3.Gui.ViewModels;

namespace AozoraEpub3.Gui.Views;

public partial class CardBoardView : UserControl
{
    private WebView2Host? _webView;
    private ScrollViewer? _textBoxScrollViewer;

    public CardBoardView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;

        EditorTextBox.PropertyChanged += OnEditorTextBoxPropertyChanged;
        EditorTextBox.TextChanged += OnEditorTextBoxTextChanged;
        EditorTextBox.TemplateApplied += OnTextBoxTemplateApplied;
    }

    // ── 行番号同期 ──────────────────────────────────────────────

    private void OnTextBoxTemplateApplied(object? sender, TemplateAppliedEventArgs e)
    {
        _textBoxScrollViewer = EditorTextBox.FindDescendantOfType<ScrollViewer>();
        if (_textBoxScrollViewer != null)
        {
            _textBoxScrollViewer.ScrollChanged += OnTextBoxScrollChanged;
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
        var offset = _textBoxScrollViewer.Offset;
        LineNumberBlock.Margin = new Avalonia.Thickness(0, -offset.Y + 4, 0, 0);
    }

    private void OnEditorTextBoxPropertyChanged(object? sender, Avalonia.AvaloniaPropertyChangedEventArgs e)
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
        if (DataContext is CardBoardViewModel vm)
        {
            vm.PreviewUpdateRequested += OnPreviewUpdateRequested;
            vm.SnippetInsertRequested += OnSnippetInsertRequested;
            vm.ThemeChanged += OnThemeChanged;
            ApplyTheme(vm.CurrentTheme);

            vm.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(CardBoardViewModel.SelectedCard))
                {
                    Dispatcher.UIThread.Post(() => EditorTextBox.Focus(),
                        DispatcherPriority.Loaded);
                }
                else if (args.PropertyName == nameof(CardBoardViewModel.PreviewHtml))
                {
                    OnPreviewUpdateRequested(vm.PreviewHtml);
                }
            };
        }
    }

    private void OnThemeChanged(EditorTheme theme)
    {
        Dispatcher.UIThread.Post(() => ApplyTheme(theme));
    }

    private void ApplyTheme(EditorTheme theme)
    {
        EditorTextBox.Background = ParseBrush(theme.EditorBackground);
        EditorTextBox.Foreground = ParseBrush(theme.EditorForeground);
        EditorTextBox.CaretBrush = ParseBrush(theme.EditorCaretColor);
        EditorTextBox.SelectionBrush = ParseBrush(theme.EditorSelectionBg);
        EditorTextBox.FontFamily = new FontFamily(theme.EditorFontFamily);
        EditorTextBox.FontSize = theme.EditorFontSize;
        LineNumberBlock.Foreground = ParseBrush(theme.EditorLineNumberFg);
        LineNumberBlock.FontFamily = new FontFamily(theme.EditorFontFamily);
        LineNumberBlock.FontSize = theme.EditorFontSize;

        if (LineNumberBlock.Parent is Border lineNumBorder)
            lineNumBorder.Background = ParseBrush(theme.EditorLineNumberBg);

        if (EditorTextBox.Parent is Grid editorGrid && editorGrid.Parent is Border editorBorder)
            editorBorder.Background = ParseBrush(theme.EditorBackground);
    }

    private static IBrush ParseBrush(string hex)
        => new SolidColorBrush(Color.Parse(hex));

    // ── WebView2 プレビュー ────────────────────────────────────

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        if (!OperatingSystem.IsWindows()) return;
        if (_webView != null) return;

        _webView = new WebView2Host();
        PreviewContainer.Children.Add(_webView);

        _webView.WebViewReady += (_, _) =>
        {
            if (DataContext is CardBoardViewModel vm && !string.IsNullOrEmpty(vm.PreviewHtml))
                _webView.NavigateToString(vm.PreviewHtml);
        };
    }

    private void OnPreviewUpdateRequested(string xhtml)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (_webView == null || !_webView.IsWebViewReady) return;
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

        if (DataContext is not CardBoardViewModel vm) return;

        if (e.Key == Key.Escape && vm.IsEditorMode)
        {
            vm.BackToGalleryCommand.Execute(null);
            e.Handled = true;
            return;
        }

        if (e.KeyModifiers == KeyModifiers.Control)
        {
            switch (e.Key)
            {
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
                case Key.Delete:
                    vm.DeleteCardCommand.Execute(null);
                    e.Handled = true;
                    break;
            }
        }
        else if (e.KeyModifiers == (KeyModifiers.Control | KeyModifiers.Shift))
        {
            switch (e.Key)
            {
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
        else if (e.Key == Key.F1)
        {
            vm.CheatSheet.ToggleCommand.Execute(null);
            e.Handled = true;
        }
    }
}
