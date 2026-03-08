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

public partial class CardEditorView : UserControl
{
    private WebView2Host? _webView;
    private ScrollViewer? _textBoxScrollViewer;
    private Point _dragStartPoint;
    private bool _isDragging;

    public CardEditorView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;

        EditorTextBox.PropertyChanged += OnEditorTextBoxPropertyChanged;
        EditorTextBox.TextChanged += OnEditorTextBoxTextChanged;
        EditorTextBox.TemplateApplied += OnTextBoxTemplateApplied;

        // ドラッグ＆ドロップ
        TreeListBox.AddHandler(DragDrop.DropEvent, OnTreeDrop);
        TreeListBox.AddHandler(DragDrop.DragOverEvent, OnTreeDragOver);
        TreeListBox.AddHandler(InputElement.PointerPressedEvent, OnTreePointerPressed, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        TreeListBox.AddHandler(InputElement.PointerMovedEvent, OnTreePointerMoved, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        TreeListBox.AddHandler(InputElement.PointerReleasedEvent, OnTreePointerReleased, Avalonia.Interactivity.RoutingStrategies.Tunnel);
    }

    // ── ドラッグ＆ドロップ ─────────────────────────────────────────

    private void OnTreePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(TreeListBox).Properties.IsLeftButtonPressed)
        {
            _dragStartPoint = e.GetPosition(TreeListBox);
            _isDragging = false;
        }
    }

    private async void OnTreePointerMoved(object? sender, PointerEventArgs e)
    {
        if (!e.GetCurrentPoint(TreeListBox).Properties.IsLeftButtonPressed) return;
        if (_isDragging) return;

        var pos = e.GetPosition(TreeListBox);
        var diff = pos - _dragStartPoint;
        if (Math.Abs(diff.Y) < 10) return;

        if (DataContext is not CardEditorViewModel vm || vm.SelectedTreeItem == null) return;

        _isDragging = true;
        var data = new DataObject();
        data.Set("TreeIndex", vm.TreeItems.IndexOf(vm.SelectedTreeItem));
        await DragDrop.DoDragDrop(e, data, DragDropEffects.Move);
        _isDragging = false;
    }

    private void OnTreePointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _isDragging = false;
    }

    private void OnTreeDragOver(object? sender, DragEventArgs e)
    {
#pragma warning disable CS0618
        e.DragEffects = e.Data.Contains("TreeIndex") ? DragDropEffects.Move : DragDropEffects.None;
#pragma warning restore CS0618
        e.Handled = true;
    }

    private void OnTreeDrop(object? sender, DragEventArgs e)
    {
        if (DataContext is not CardEditorViewModel vm) return;
#pragma warning disable CS0618
        if (!e.Data.Contains("TreeIndex")) return;
        var sourceIndex = (int)e.Data.Get("TreeIndex")!;
#pragma warning restore CS0618

        var pos = e.GetPosition(TreeListBox);
        var targetIndex = GetDropTargetIndex(pos, vm);
        if (targetIndex < 0) targetIndex = vm.TreeItems.Count - 1;

        vm.MoveItem(sourceIndex, targetIndex);
        e.Handled = true;
    }

    private int GetDropTargetIndex(Point position, CardEditorViewModel vm)
    {
        for (int i = 0; i < TreeListBox.ItemCount; i++)
        {
            var container = TreeListBox.ContainerFromIndex(i);
            if (container is not Control ctrl) continue;

            var itemPos = ctrl.TranslatePoint(new Point(0, 0), TreeListBox);
            if (itemPos == null) continue;

            var midY = itemPos.Value.Y + ctrl.Bounds.Height / 2;
            if (position.Y < midY) return i;
        }
        return vm.TreeItems.Count - 1;
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
        => UpdateLineNumberScroll();

    private void UpdateLineNumberScroll()
    {
        if (_textBoxScrollViewer == null) return;
        var offset = _textBoxScrollViewer.Offset;
        LineNumberBlock.Margin = new Thickness(0, -offset.Y + 4, 0, 0);
    }

    private void OnEditorTextBoxPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property.Name == "Text") UpdateLineNumbers();
    }

    private void OnEditorTextBoxTextChanged(object? sender, TextChangedEventArgs e)
        => UpdateLineNumbers();

    private void UpdateLineNumbers()
    {
        var text = EditorTextBox.Text ?? "";
        var lineCount = text.Length == 0 ? 1 : text.Split('\n').Length;
        LineNumberBlock.Text = string.Join('\n', Enumerable.Range(1, lineCount));
    }

    // ── ViewModel 接続 ───────────────────────────────────────────

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is CardEditorViewModel vm)
        {
            vm.PreviewUpdateRequested += OnPreviewUpdateRequested;
            vm.SnippetInsertRequested += OnSnippetInsertRequested;
            vm.ThemeChanged += OnThemeChanged;
            vm.OpenProjectRequested += OnOpenProjectRequested;
            vm.SaveProjectAsRequested += OnSaveProjectAsRequested;
            ApplyTheme(vm.CurrentTheme);

            vm.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(CardEditorViewModel.SelectedTreeItem))
                    Dispatcher.UIThread.Post(() => EditorTextBox.Focus(), DispatcherPriority.Loaded);
            };
        }
    }

    private async Task<string?> OnOpenProjectRequested()
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return null;

        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(
            new Avalonia.Platform.Storage.FolderPickerOpenOptions
            {
                Title = "プロジェクトフォルダを開く",
                AllowMultiple = false
            });

        return folders.Count > 0 ? folders[0].Path.LocalPath : null;
    }

    private async Task<string?> OnSaveProjectAsRequested(string? currentPath)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return null;

        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(
            new Avalonia.Platform.Storage.FolderPickerOpenOptions
            {
                Title = "プロジェクトの保存先フォルダを選択",
                AllowMultiple = false
            });

        return folders.Count > 0 ? folders[0].Path.LocalPath : null;
    }

    // ── テーマ適用 ──────────────────────────────────────────────

    private void OnThemeChanged(EditorTheme theme)
        => Dispatcher.UIThread.Post(() => ApplyTheme(theme));

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

        if (EditorTextBox.Parent is Grid editorGrid &&
            editorGrid.Parent is Grid rowGrid &&
            rowGrid.Parent is Border editorBorder)
            editorBorder.Background = ParseBrush(theme.EditorBackground);
    }

    private static IBrush ParseBrush(string hex) => new SolidColorBrush(Color.Parse(hex));

    // ── WebView2 プレビュー ────────────────────────────────────

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        Dispatcher.UIThread.Post(() => EditorTextBox.Focus(), DispatcherPriority.Loaded);

        if (!OperatingSystem.IsWindows()) return;
        if (_webView != null) return;

        _webView = new WebView2Host();
        PreviewContainer.Children.Add(_webView);

        _webView.WebViewReady += (_, _) =>
        {
            if (DataContext is CardEditorViewModel vm && !string.IsNullOrEmpty(vm.PreviewHtml))
                _webView.NavigateToString(vm.PreviewHtml);
        };
    }

    private void OnPreviewUpdateRequested(string xhtml)
    {
        if (_webView == null || !_webView.IsWebViewReady) return;
        Dispatcher.UIThread.Post(() => _webView.NavigateToString(xhtml));
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

        if (DataContext is not CardEditorViewModel vm) return;

        if (e.KeyModifiers == KeyModifiers.Control)
        {
            switch (e.Key)
            {
                case Key.S:
                    vm.SaveProjectCommand.Execute(null);
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
                case Key.Delete:
                    vm.DeleteItemCommand.Execute(null);
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
