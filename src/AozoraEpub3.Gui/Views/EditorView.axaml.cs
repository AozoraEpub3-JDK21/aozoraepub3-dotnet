using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using AvaloniaEdit;
using AozoraEpub3.Core.Editor;
using AozoraEpub3.Gui.Controls;
using AozoraEpub3.Gui.ViewModels;

namespace AozoraEpub3.Gui.Views;

public partial class EditorView : UserControl
{
    private TextEditor _textEditor = null!;
    private WebView2Host? _webView;
    private readonly EditorSuggestService _suggestService = new();
    private ListBox? _suggestList;
    private Popup? _suggestPopup;
    private string _suggestFilter = "";
    private bool _isSuggestActive;

    public EditorView()
    {
        InitializeComponent();
        InitTextEditor();
        DataContextChanged += OnDataContextChanged;
    }

    private void InitTextEditor()
    {
        _textEditor = new TextEditor
        {
            FontFamily = new FontFamily("BIZ UDGothic, MS Gothic, Consolas, monospace"),
            FontSize = 14,
            ShowLineNumbers = true,
            WordWrap = true,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
        };

        _textEditor.TextChanged += OnTextEditorTextChanged;
        _textEditor.TextArea.TextEntering += OnTextEntering;
        _textEditor.TextArea.TextEntered += OnTextEntered;

        EditorContainer.Children.Add(_textEditor);
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is EditorViewModel vm)
        {
            vm.PreviewUpdateRequested += OnPreviewUpdateRequested;
            vm.SnippetInsertRequested += OnSnippetInsertRequested;

            vm.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(EditorViewModel.EditorText) &&
                    _textEditor.Text != vm.EditorText)
                {
                    _textEditor.Text = vm.EditorText;
                }
            };
        }
    }

    // ── テキスト変更 → ViewModel に反映 ────────────────────────

    private void OnTextEditorTextChanged(object? sender, EventArgs e)
    {
        if (DataContext is EditorViewModel vm)
            vm.EditorText = _textEditor.Text;
    }

    // ── 括弧ペアリング + サジェストトリガー ───────────────────────

    private void OnTextEntering(object? sender, TextInputEventArgs e)
    {
        if (e.Text?.Length != 1) return;
        var ch = e.Text[0];

        if (_isSuggestActive) return;

        var charAfter = GetCharAfterCaret();
        var charBefore = GetCharBeforeCaret();
        var selectedText = _textEditor.TextArea.Selection.IsEmpty
            ? null
            : _textEditor.TextArea.Selection.GetText();

        var result = _suggestService.HandleBracketInput(ch, charAfter, charBefore, selectedText);
        if (result != null)
        {
            e.Handled = true;

            if (result.ShouldSkip)
            {
                _textEditor.TextArea.Caret.Offset++;
            }
            else if (!string.IsNullOrEmpty(selectedText))
            {
                var start = _textEditor.SelectionStart;
                _textEditor.Document.Replace(start, _textEditor.SelectionLength, result.TextToInsert);
                _textEditor.TextArea.Caret.Offset = start + result.CursorOffset;
            }
            else
            {
                var offset = _textEditor.TextArea.Caret.Offset;
                _textEditor.Document.Insert(offset, result.TextToInsert);
                _textEditor.TextArea.Caret.Offset = offset + result.CursorOffset;
            }
        }
    }

    private void OnTextEntered(object? sender, TextInputEventArgs e)
    {
        if (e.Text?.Length != 1) return;

        var textBeforeCaret = GetTextBeforeCaret(10);
        if (_suggestService.ShouldShowSuggest(textBeforeCaret))
        {
            _suggestFilter = "";
            _isSuggestActive = true;
            UpdateSuggestPopup();
            ShowSuggestPopup();
        }
        else if (_isSuggestActive)
        {
            _suggestFilter += e.Text;
            UpdateSuggestPopup();
        }
    }

    // ── サジェストポップアップ ──────────────────────────────────

    private void ShowSuggestPopup()
    {
        if (_suggestPopup == null)
        {
            _suggestList = new ListBox { MaxHeight = 200, MinWidth = 200 };
            _suggestList.DoubleTapped += (_, _) => ApplySelectedSuggestion();

            _suggestPopup = new Popup
            {
                Child = new Border
                {
                    Background = Brushes.White,
                    BorderBrush = Brushes.Gray,
                    BorderThickness = new Avalonia.Thickness(1),
                    CornerRadius = new Avalonia.CornerRadius(4),
                    Child = _suggestList
                },
                PlacementTarget = _textEditor,
                Placement = PlacementMode.Pointer,
                IsLightDismissEnabled = true,
            };
            _suggestPopup.Closed += (_, _) =>
            {
                _isSuggestActive = false;
                _suggestFilter = "";
            };

            ((Grid)Content!).Children.Add(_suggestPopup);
        }

        _suggestPopup.IsOpen = true;
    }

    private void UpdateSuggestPopup()
    {
        if (_suggestList == null) return;

        var items = _suggestService.GetSuggestions(_suggestFilter);
        _suggestList.ItemsSource = items.Select(i => i.DisplayName).ToList();

        if (items.Count == 0)
            CloseSuggestPopup();
        else if (_suggestList.ItemCount > 0)
            _suggestList.SelectedIndex = 0;
    }

    private void ApplySelectedSuggestion()
    {
        if (_suggestList?.SelectedItem is not string selectedName) return;

        var items = _suggestService.GetSuggestions(_suggestFilter);
        var item = items.FirstOrDefault(i => i.DisplayName == selectedName);
        if (item == null) return;

        var offset = _textEditor.TextArea.Caret.Offset;
        var deleteLen = 2 + _suggestFilter.Length; // ［＃ + filter
        var insertStart = Math.Max(0, offset - deleteLen);

        _textEditor.Document.Replace(insertStart, deleteLen, item.InsertText);
        _textEditor.TextArea.Caret.Offset = insertStart + item.CursorOffset;

        CloseSuggestPopup();
        _textEditor.Focus();
    }

    private void CloseSuggestPopup()
    {
        if (_suggestPopup != null)
            _suggestPopup.IsOpen = false;
        _isSuggestActive = false;
        _suggestFilter = "";
    }

    // ── WebView2 プレビュー ────────────────────────────────────

    protected override void OnAttachedToVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
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
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            _webView.NavigateToString(xhtml);
        });
    }

    // ── スニペット挿入 ──────────────────────────────────────────

    private void OnSnippetInsertRequested(SnippetInsertRequest request)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            var offset = _textEditor.TextArea.Caret.Offset;

            if (request.IsLineLevel)
            {
                var line = _textEditor.Document.GetLineByOffset(offset);
                _textEditor.Document.Insert(line.Offset, request.Text + "\n");
                _textEditor.TextArea.Caret.Offset = line.Offset + request.Text.Length + 1;
            }
            else
            {
                _textEditor.Document.Insert(offset, request.Text);
                _textEditor.TextArea.Caret.Offset = offset + request.CursorOffset;
            }

            _textEditor.Focus();
        });
    }

    // ── ヘルパー ──────────────────────────────────────────────

    private char? GetCharAfterCaret()
    {
        var offset = _textEditor.TextArea.Caret.Offset;
        return offset < _textEditor.Document.TextLength
            ? _textEditor.Document.GetCharAt(offset)
            : null;
    }

    private char? GetCharBeforeCaret()
    {
        var offset = _textEditor.TextArea.Caret.Offset;
        return offset > 0 ? _textEditor.Document.GetCharAt(offset - 1) : null;
    }

    private string GetTextBeforeCaret(int maxLength)
    {
        var offset = _textEditor.TextArea.Caret.Offset;
        var start = Math.Max(0, offset - maxLength);
        return _textEditor.Document.GetText(start, offset - start);
    }

    // ── キーボードショートカット ────────────────────────────────

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (_isSuggestActive && _suggestList != null)
        {
            switch (e.Key)
            {
                case Key.Down:
                    if (_suggestList.SelectedIndex < _suggestList.ItemCount - 1)
                        _suggestList.SelectedIndex++;
                    e.Handled = true;
                    return;
                case Key.Up:
                    if (_suggestList.SelectedIndex > 0)
                        _suggestList.SelectedIndex--;
                    e.Handled = true;
                    return;
                case Key.Enter or Key.Tab:
                    ApplySelectedSuggestion();
                    e.Handled = true;
                    return;
                case Key.Escape:
                    CloseSuggestPopup();
                    e.Handled = true;
                    return;
                case Key.Back:
                    if (_suggestFilter.Length > 0)
                        _suggestFilter = _suggestFilter[..^1];
                    else
                        CloseSuggestPopup();
                    UpdateSuggestPopup();
                    return;
            }
        }

        if (DataContext is not EditorViewModel vm) return;

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
