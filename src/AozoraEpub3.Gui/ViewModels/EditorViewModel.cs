using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AozoraEpub3.Core.Editor;
using AozoraEpub3.Gui.Services;

namespace AozoraEpub3.Gui.ViewModels;

/// <summary>
/// エディタ画面の ViewModel。
/// テキスト入力 → デバウンス → XHTML 生成 → WebView2 プレビューのパイプラインを管理する。
/// </summary>
public sealed partial class EditorViewModel : ViewModelBase
{
    private LivePreviewService _previewService;
    private CancellationTokenSource? _debounceCts;
    private const int DebounceMs = 400;

    // ───── エディタ状態 ──────────────────────────────────────────────────────

    [ObservableProperty]
    private string _editorText = "";

    [ObservableProperty]
    private string _previewHtml = "";

    [ObservableProperty]
    private int _selectedModeIndex;

    [ObservableProperty]
    private bool _isVertical = true;

    [ObservableProperty]
    private int _characterCount;

    [ObservableProperty]
    private int _lineCount;

    [ObservableProperty]
    private int _lintWarningCount;

    [ObservableProperty]
    private bool _isPreviewVisible = true;

    [ObservableProperty]
    private bool _isProofreadingPanelVisible;

    [ObservableProperty]
    private ObservableCollection<LintWarning> _proofreadingResults = [];

    [ObservableProperty]
    private string? _currentFilePath;

    [ObservableProperty]
    private bool _isDirty;

    // ───── テーマ・チートシート ────────────────────────────────────────────

    [ObservableProperty]
    private EditorTheme _currentTheme = EditorThemes.DarkDefault;

    /// <summary>チートシート ViewModel</summary>
    public CheatSheetViewModel CheatSheet { get; } = new();

    /// <summary>プレビュー更新要求イベント。引数は XHTML 文字列。</summary>
    public event Action<string>? PreviewUpdateRequested;

    /// <summary>テーマ変更通知イベント。View がエディタ色を更新する。</summary>
    public event Action<EditorTheme>? ThemeChanged;

    /// <summary>EPUB変換要求。MainWindowViewModel がハンドルして変換・プレビューする。</summary>
    public event Action<string>? EpubConversionRequested;

    public string[] ModeNames { get; } = ["標準", "なろう", "カクヨム"];

    private readonly ProofreadingService _proofreadingService = new();
    private readonly CustomDictionaryService _customDictionary = new();

    public EditorViewModel()
    {
        _previewService = new LivePreviewService(ConversionProfile.Default)
        {
            Theme = _currentTheme
        };
    }

    partial void OnCurrentThemeChanged(EditorTheme value)
    {
        _previewService.Theme = value;
        ThemeChanged?.Invoke(value);
        OnEditorTextChanged(EditorText);
    }

    // ───── テキスト変更時のデバウンス処理 ─────────────────────────────────────

    partial void OnEditorTextChanged(string value)
    {
        IsDirty = true;
        CharacterCount = value.Length;
        LineCount = value.Split('\n').Length;
        CheatSheet.NotifyInput(value.Length);

        // デバウンスしてプレビュー更新
        _debounceCts?.Cancel();
        _debounceCts = new CancellationTokenSource();
        var ct = _debounceCts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(DebounceMs, ct);
                if (ct.IsCancellationRequested) return;

                var xhtml = _previewService.ConvertToXhtml(value, IsVertical);
                var warnings = _previewService.GetLintWarnings(value);

                // UI スレッドに戻して更新
                LintWarningCount = warnings.Count;
                PreviewHtml = xhtml;
                PreviewUpdateRequested?.Invoke(xhtml);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Preview error: {ex.Message}");
            }
        }, ct);
    }

    partial void OnSelectedModeIndexChanged(int value)
    {
        var profile = value switch
        {
            1 => ConversionProfile.Narou,
            2 => ConversionProfile.Kakuyomu,
            _ => ConversionProfile.Default
        };
        _previewService = new LivePreviewService(profile);
        OnEditorTextChanged(EditorText);
    }

    partial void OnIsVerticalChanged(bool value)
    {
        OnEditorTextChanged(EditorText);
    }

    // ───── ツールバーコマンド ──────────────────────────────────────────────────

    /// <summary>スニペット挿入要求イベント。View 側で TextBox に反映する。</summary>
    public event Action<SnippetInsertRequest>? SnippetInsertRequested;

    [RelayCommand]
    private void InsertSnippet(string snippetId)
    {
        var service = new EditorSuggestService();
        var result = service.GetSnippet(snippetId, null);
        SnippetInsertRequested?.Invoke(new SnippetInsertRequest(
            result.TextToInsert, result.CursorOffset, result.IsLineLevel));
    }

    [RelayCommand]
    private void TogglePreview()
    {
        IsPreviewVisible = !IsPreviewVisible;
    }

    [RelayCommand]
    private void FormatText()
    {
        var formatter = new NovelFormatter(GetCurrentProfile());
        EditorText = formatter.Format(EditorText);
    }

    [RelayCommand]
    private async Task ConvertToEpub()
    {
        if (string.IsNullOrWhiteSpace(EditorText)) return;

        var engine = new EditorConversionEngine(GetCurrentProfile());
        var formatter = new NovelFormatter(GetCurrentProfile());
        var formattedText = formatter.Format(EditorText);
        var aozoraText = engine.Convert(formattedText);

        var tempPath = Path.Combine(Path.GetTempPath(), "aep3_editor_temp.txt");
        await File.WriteAllTextAsync(tempPath, aozoraText);

        EpubConversionRequested?.Invoke(tempPath);
    }

    [RelayCommand]
    private void Proofread()
    {
        var results = _proofreadingService.Check(EditorText);
        ProofreadingResults = new ObservableCollection<LintWarning>(results);
        IsProofreadingPanelVisible = results.Count > 0;
        LintWarningCount = results.Count;
    }

    [RelayCommand]
    private void CloseProofreading()
    {
        IsProofreadingPanelVisible = false;
    }

    [RelayCommand]
    private void ForceRefreshPreview()
    {
        OnEditorTextChanged(EditorText);
    }

    // ───── ファイル操作 ──────────────────────────────────────────────────────

    /// <summary>ファイルを開くダイアログ要求。View がハンドルする。</summary>
    public event Func<Task<string?>>? OpenFileRequested;

    /// <summary>名前を付けて保存ダイアログ要求。View がハンドルする。</summary>
    public event Func<string?, Task<string?>>? SaveFileRequested;

    [RelayCommand]
    private void NewFile()
    {
        if (IsDirty)
        {
            // TODO: 未保存確認ダイアログ
        }
        EditorText = "";
        CurrentFilePath = null;
        IsDirty = false;
    }

    [RelayCommand]
    private async Task OpenFile()
    {
        if (OpenFileRequested == null) return;
        var path = await OpenFileRequested.Invoke();
        if (path == null) return;

        try
        {
            EditorText = File.ReadAllText(path);
            CurrentFilePath = path;
            IsDirty = false;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Open file error: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task SaveFile()
    {
        if (CurrentFilePath != null)
        {
            File.WriteAllText(CurrentFilePath, EditorText);
            IsDirty = false;
        }
        else
        {
            await SaveFileAs();
        }
    }

    [RelayCommand]
    private async Task SaveFileAs()
    {
        if (SaveFileRequested == null) return;
        var path = await SaveFileRequested.Invoke(CurrentFilePath);
        if (path == null) return;

        try
        {
            File.WriteAllText(path, EditorText);
            CurrentFilePath = path;
            IsDirty = false;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Save file error: {ex.Message}");
        }
    }

    /// <summary>タイトルバー表示用のファイル名</summary>
    public string WindowTitle
    {
        get
        {
            var name = CurrentFilePath != null ? Path.GetFileName(CurrentFilePath) : "新規";
            var dirty = IsDirty ? " *" : "";
            return $"{name}{dirty}";
        }
    }

    partial void OnCurrentFilePathChanged(string? value) => OnPropertyChanged(nameof(WindowTitle));
    partial void OnIsDirtyChanged(bool value) => OnPropertyChanged(nameof(WindowTitle));

    private ConversionProfile GetCurrentProfile() => SelectedModeIndex switch
    {
        1 => ConversionProfile.Narou,
        2 => ConversionProfile.Kakuyomu,
        _ => ConversionProfile.Default
    };
}

/// <summary>スニペット挿入要求</summary>
public record SnippetInsertRequest(string Text, int CursorOffset, bool IsLineLevel);
