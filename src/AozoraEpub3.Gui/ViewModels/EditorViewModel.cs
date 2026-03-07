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
    private string? _currentFilePath;

    [ObservableProperty]
    private bool _isDirty;

    /// <summary>プレビュー更新要求イベント。引数は XHTML 文字列。</summary>
    public event Action<string>? PreviewUpdateRequested;

    public string[] ModeNames { get; } = ["汎用", "なろう", "カクヨム"];

    public EditorViewModel()
    {
        _previewService = new LivePreviewService(ConversionProfile.Default);
    }

    // ───── テキスト変更時のデバウンス処理 ─────────────────────────────────────

    partial void OnEditorTextChanged(string value)
    {
        IsDirty = true;
        CharacterCount = value.Length;
        LineCount = value.Split('\n').Length;

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
        // 再変換トリガー
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
    private void ForceRefreshPreview()
    {
        OnEditorTextChanged(EditorText);
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
