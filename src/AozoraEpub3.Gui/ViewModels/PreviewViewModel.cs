using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AozoraEpub3.Gui.Services;

namespace AozoraEpub3.Gui.ViewModels;

/// <summary>
/// EPUB プレビュー画面の ViewModel。
/// 目次ナビゲーション、ページ送り、EPUB展開を管理する。
/// </summary>
public sealed partial class PreviewViewModel : ViewModelBase, IDisposable
{
    private readonly EpubPreviewService _previewService = new();

    // ───── CSS エディタ ────────────────────────────────────────────────

    /// <summary>CSS エディタの ViewModel</summary>
    public CssEditorViewModel CssEditor { get; } = new();

    public PreviewViewModel()
    {
        CssEditor.CssChanged += OnCssChanged;
    }

    private void OnCssChanged(string cssText)
    {
        // CSS ファイルパスを取得して WebView2 に反映
        var cssFiles = _previewService.GetCssFiles();
        var target = cssFiles.FirstOrDefault(c =>
            c.Href.Contains("vertical") || c.Href.Contains("horizontal"))
            ?? cssFiles.FirstOrDefault();
        if (target != null)
            CssInjectionRequested?.Invoke(cssText, target.AbsolutePath);
    }

    // ───── プロパティ ──────────────────────────────────────────────────

    /// <summary>現在開いている EPUB ファイルパス</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEpubLoaded))]
    [NotifyPropertyChangedFor(nameof(WindowTitle))]
    private string _epubFilePath = "";

    /// <summary>現在のスパインインデックス（0-based）</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentPageDisplay))]
    [NotifyCanExecuteChangedFor(nameof(GoPrevCommand))]
    [NotifyCanExecuteChangedFor(nameof(GoNextCommand))]
    private int _currentSpineIndex;

    /// <summary>ステータスメッセージ</summary>
    [ObservableProperty]
    private string _statusMessage = "";

    /// <summary>目次パネルが開いているか</summary>
    [ObservableProperty]
    private bool _isTocOpen = true;

    /// <summary>プレビュー用フォント名</summary>
    [ObservableProperty]
    private string _selectedFont = "BIZ UDPMincho";

    /// <summary>選択可能なフォント一覧</summary>
    public string[] FontChoices { get; } =
    [
        "BIZ UDPMincho",
        "BIZ UDPGothic",
        "游明朝",
        "游ゴシック",
        "メイリオ",
        "ＭＳ 明朝",
        "ＭＳ ゴシック",
        "Noto Serif JP",
        "Noto Sans JP",
    ];

    /// <summary>EPUB が読み込まれているか</summary>
    public bool IsEpubLoaded => !string.IsNullOrEmpty(EpubFilePath) && _previewService.SpineItems.Count > 0;

    /// <summary>ページ表示（例: "3 / 15"）</summary>
    public string CurrentPageDisplay => IsEpubLoaded
        ? $"{CurrentSpineIndex + 1} / {_previewService.SpineItems.Count}"
        : "";

    /// <summary>ウィンドウタイトル用</summary>
    public string WindowTitle => IsEpubLoaded
        ? _previewService.Title
        : "";

    /// <summary>目次項目リスト（バインディング用に新規リストを返す）</summary>
    public ObservableCollection<TocEntry> TocItems { get; } = [];

    /// <summary>目次の選択中項目（スパイン移動と連動）</summary>
    [ObservableProperty]
    private TocEntry? _selectedTocItem;

    // ───── ナビゲーション要求イベント ───────────────────────────────

    /// <summary>View 側の WebView2 にナビゲーションを要求するイベント。</summary>
    public event Action<Uri>? NavigationRequested;

    /// <summary>フォント変更を通知するイベント。</summary>
    public event Action<string>? FontChanged;

    partial void OnSelectedFontChanged(string value)
    {
        FontChanged?.Invoke(value);
    }

    partial void OnCurrentSpineIndexChanged(int value)
    {
        // 目次の選択をスパインインデックスに連動
        var match = TocItems.LastOrDefault(t => t.SpineIndex <= value);
        if (match != null && match != SelectedTocItem)
            SelectedTocItem = match;
    }

    // ───── コマンド ────────────────────────────────────────────────────

    /// <summary>EPUB ファイルを開いてプレビューする。</summary>
    [RelayCommand]
    private void OpenEpub(string path)
    {
        try
        {
            _previewService.Open(path);
            EpubFilePath = path;
            CurrentSpineIndex = 0;

            // 目次を更新（新しいコレクションに入れ替え）
            TocItems.Clear();
            foreach (var entry in _previewService.TocEntries)
                TocItems.Add(entry);

            StatusMessage = $"Loaded: {Path.GetFileName(path)}";

            // CSS エディタに EPUB の CSS を読み込む
            CssEditor.LoadFromEpub(_previewService);

            // ソースマッピング生成
            try
            {
                SourceMappingService.GenerateAndSave(_previewService, path);
            }
            catch { /* ソースマッピング生成失敗は無視 */ }

            NavigateToCurrentPage();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
    }

    [RelayCommand(CanExecute = nameof(CanGoPrev))]
    private void GoPrev()
    {
        CurrentSpineIndex--;
        NavigateToCurrentPage();
    }

    private bool CanGoPrev() => IsEpubLoaded && CurrentSpineIndex > 0;

    [RelayCommand(CanExecute = nameof(CanGoNext))]
    private void GoNext()
    {
        CurrentSpineIndex++;
        NavigateToCurrentPage();
    }

    private bool CanGoNext() => IsEpubLoaded && CurrentSpineIndex < _previewService.SpineItems.Count - 1;

    [RelayCommand]
    private void GoToPage(int spineIndex)
    {
        if (spineIndex >= 0 && spineIndex < _previewService.SpineItems.Count)
        {
            CurrentSpineIndex = spineIndex;
            NavigateToCurrentPage();
        }
    }

    [RelayCommand]
    private void ToggleToc() => IsTocOpen = !IsTocOpen;

    [RelayCommand]
    private void Validate()
    {
        if (IsEpubLoaded)
            ValidateRequested?.Invoke(EpubFilePath);
    }

    /// <summary>CSS 変更を通知するイベント。引数: CSS テキスト, CSS ファイルパス。</summary>
    public event Action<string, string>? CssInjectionRequested;

    /// <summary>プレビュー中の EPUB を検証する要求。引数: EPUB ファイルパス。</summary>
    public event Action<string>? ValidateRequested;

    /// <summary>最大化モードのトグルを親に要求するイベント。</summary>
    public event Action? ToggleMaximizeRequested;

    /// <summary>最大化トグルを外部から発火する。</summary>
    public void RequestToggleMaximize() => ToggleMaximizeRequested?.Invoke();

    // ───── 内部 ────────────────────────────────────────────────────────

    private void NavigateToCurrentPage()
    {
        var uri = _previewService.GetPageUri(CurrentSpineIndex);
        if (uri != null)
            NavigationRequested?.Invoke(uri);
    }

    public void Dispose()
    {
        _previewService.Dispose();
    }
}
