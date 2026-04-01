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

    /// <summary>原稿用紙換算（400字詰め）</summary>
    public double ManuscriptPageCount => Math.Round(CharacterCount / 400.0, 1);

    /// <summary>選択中の文字数（選択なしは空文字）</summary>
    [ObservableProperty]
    private string _selectionInfo = "";

    [ObservableProperty]
    private int _lintWarningCount;

    [ObservableProperty]
    private bool _isPreviewVisible = true;

    [ObservableProperty]
    private bool _isProofreadingPanelVisible;

    // ───── 検索・置換 ─────────────────────────────────────────────────────────

    [ObservableProperty]
    private bool _isFindReplaceVisible;

    [ObservableProperty]
    private string _findText = "";

    [ObservableProperty]
    private string _replaceText = "";

    [ObservableProperty]
    private string _findResultInfo = "";

    /// <summary>検索ヒット位置リスト（開始インデックス）</summary>
    private List<int> _findMatches = [];
    private int _findMatchIndex = -1;

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

    // ───── 検索・置換コマンド ────────────────────────────────────────────────

    /// <summary>検索パネル表示切替。View から SelectAll 等を行うためのイベント。</summary>
    public event Action? FindPanelOpened;

    [RelayCommand]
    private void OpenFindReplace()
    {
        IsFindReplaceVisible = true;
        FindPanelOpened?.Invoke();
    }

    [RelayCommand]
    private void CloseFindReplace()
    {
        IsFindReplaceVisible = false;
        FindResultInfo = "";
        _findMatches.Clear();
        _findMatchIndex = -1;
    }

    partial void OnFindTextChanged(string value) => RefreshMatches();

    private void RefreshMatches()
    {
        _findMatches.Clear();
        _findMatchIndex = -1;
        if (string.IsNullOrEmpty(FindText))
        {
            FindResultInfo = "";
            return;
        }
        var text = EditorText;
        var idx = 0;
        while (true)
        {
            idx = text.IndexOf(FindText, idx, StringComparison.OrdinalIgnoreCase);
            if (idx < 0) break;
            _findMatches.Add(idx);
            idx += FindText.Length;
        }
        UpdateFindResultInfo();
    }

    private void UpdateFindResultInfo()
    {
        FindResultInfo = _findMatches.Count == 0
            ? (string.IsNullOrEmpty(FindText) ? "" : "見つかりません")
            : $"{(_findMatchIndex >= 0 ? _findMatchIndex + 1 : 1)}/{_findMatches.Count}";
    }

    /// <summary>検索ジャンプ要求（開始位置と長さ）。View 側がキャレット移動を行う。</summary>
    public event Action<int, int>? FindJumpRequested;

    [RelayCommand]
    private void FindNext()
    {
        if (_findMatches.Count == 0) { RefreshMatches(); return; }
        _findMatchIndex = (_findMatchIndex + 1) % _findMatches.Count;
        UpdateFindResultInfo();
        FindJumpRequested?.Invoke(_findMatches[_findMatchIndex], FindText.Length);
    }

    [RelayCommand]
    private void FindPrev()
    {
        if (_findMatches.Count == 0) { RefreshMatches(); return; }
        _findMatchIndex = (_findMatchIndex - 1 + _findMatches.Count) % _findMatches.Count;
        UpdateFindResultInfo();
        FindJumpRequested?.Invoke(_findMatches[_findMatchIndex], FindText.Length);
    }

    [RelayCommand]
    private void Replace()
    {
        if (_findMatches.Count == 0 || _findMatchIndex < 0) { FindNext(); return; }
        var pos = _findMatches[_findMatchIndex];
        var text = EditorText;
        EditorText = text[..pos] + ReplaceText + text[(pos + FindText.Length)..];
        RefreshMatches();
        if (_findMatches.Count > 0)
        {
            _findMatchIndex = Math.Min(_findMatchIndex, _findMatches.Count - 1);
            UpdateFindResultInfo();
            FindJumpRequested?.Invoke(_findMatches[_findMatchIndex], ReplaceText.Length);
        }
    }

    [RelayCommand]
    private void ReplaceAll()
    {
        if (string.IsNullOrEmpty(FindText)) return;
        var count = 0;
        var text = EditorText;
        var sb = new System.Text.StringBuilder();
        var idx = 0;
        while (true)
        {
            var next = text.IndexOf(FindText, idx, StringComparison.OrdinalIgnoreCase);
            if (next < 0) { sb.Append(text[idx..]); break; }
            sb.Append(text[idx..next]);
            sb.Append(ReplaceText);
            idx = next + FindText.Length;
            count++;
        }
        if (count > 0)
        {
            EditorText = sb.ToString();
            FindResultInfo = $"{count}件置換しました";
        }
        else
        {
            FindResultInfo = "見つかりません";
        }
        _findMatches.Clear();
        _findMatchIndex = -1;
    }

    // ───── ファイル操作 ──────────────────────────────────────────────────────

    /// <summary>ファイルを開くダイアログ要求。View がハンドルする。</summary>
    public event Func<Task<string?>>? OpenFileRequested;

    /// <summary>名前を付けて保存ダイアログ要求。View がハンドルする。</summary>
    public event Func<string?, Task<string?>>? SaveFileRequested;

    /// <summary>未保存変更の破棄確認要求。trueで続行、falseでキャンセル。</summary>
    public event Func<Task<bool>>? ConfirmDiscardChangesRequested;

    private async Task<bool> ConfirmDiscardChangesAsync()
    {
        if (!IsDirty) return true;
        if (ConfirmDiscardChangesRequested == null) return true;
        return await ConfirmDiscardChangesRequested.Invoke();
    }

    [RelayCommand]
    private async Task NewFile()
    {
        if (!await ConfirmDiscardChangesAsync()) return;
        EditorText = "";
        CurrentFilePath = null;
        IsDirty = false;
    }

    [RelayCommand]
    private async Task OpenFile()
    {
        if (!await ConfirmDiscardChangesAsync()) return;
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

    partial void OnCharacterCountChanged(int value)
    {
        OnPropertyChanged(nameof(ManuscriptPageCount));
    }

    private ConversionProfile GetCurrentProfile() => SelectedModeIndex switch
    {
        1 => ConversionProfile.Narou,
        2 => ConversionProfile.Kakuyomu,
        _ => ConversionProfile.Default
    };
}

/// <summary>スニペット挿入要求</summary>
public record SnippetInsertRequest(string Text, int CursorOffset, bool IsLineLevel);
