using Avalonia.Controls;
using Avalonia.Input;
using AozoraEpub3.Gui.Controls;
using AozoraEpub3.Gui.ViewModels;

namespace AozoraEpub3.Gui.Views;

public partial class EditorView : UserControl
{
    private WebView2Host? _webView;

    public EditorView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is EditorViewModel vm)
        {
            vm.PreviewUpdateRequested += OnPreviewUpdateRequested;
            vm.SnippetInsertRequested += OnSnippetInsertRequested;
        }
    }

    protected override void OnAttachedToVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        InitWebView();
    }

    private void InitWebView()
    {
        if (_webView != null) return;

        // Windows のみ WebView2 を使用
        if (!OperatingSystem.IsWindows()) return;

        _webView = new WebView2Host();
        PreviewContainer.Children.Add(_webView);

        _webView.WebViewReady += (_, _) =>
        {
            // 初期コンテンツ表示
            if (DataContext is EditorViewModel vm && !string.IsNullOrEmpty(vm.PreviewHtml))
                _webView.NavigateToString(vm.PreviewHtml);
        };
    }

    private void OnPreviewUpdateRequested(string xhtml)
    {
        if (_webView == null || !_webView.IsWebViewReady) return;

        // UI スレッドで更新
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            _webView.NavigateToString(xhtml);
        });
    }

    private void OnSnippetInsertRequested(SnippetInsertRequest request)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            var textBox = EditorTextBox;
            var caretIndex = textBox.CaretIndex;
            var text = textBox.Text ?? "";

            if (request.IsLineLevel)
            {
                // 行頭に挿入: 現在行の先頭を探す
                var lineStart = text.LastIndexOf('\n', Math.Max(0, caretIndex - 1)) + 1;
                textBox.Text = text.Insert(lineStart, request.Text + "\n");
                textBox.CaretIndex = lineStart + request.Text.Length + 1;
            }
            else
            {
                textBox.Text = text.Insert(caretIndex, request.Text);
                textBox.CaretIndex = caretIndex + request.CursorOffset;
            }

            textBox.Focus();
        });
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (DataContext is not EditorViewModel vm) return;

        // ショートカットキー
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
    }
}
