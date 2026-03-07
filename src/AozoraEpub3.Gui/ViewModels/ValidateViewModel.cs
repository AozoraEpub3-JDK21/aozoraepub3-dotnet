using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AozoraEpub3.Gui.Services;

namespace AozoraEpub3.Gui.ViewModels;

/// <summary>
/// EPUB 検証（epubcheck）画面の ViewModel。
/// 独立タブとしてもプレビュー画面のボタンからも使用可能。
/// </summary>
public sealed partial class ValidateViewModel : ViewModelBase
{
    // ───── プロパティ ─────────────────────────────────────────────

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RunValidationCommand))]
    private string _epubFilePath = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RunValidationCommand))]
    private string _jarPath = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RunValidationCommand))]
    private bool _isValidating;

    [ObservableProperty]
    private string _statusMessage = "";

    [ObservableProperty]
    private string _summaryMessage = "";

    /// <summary>検証結果メッセージ一覧</summary>
    public ObservableCollection<EpubcheckMessage> Messages { get; } = [];

    /// <summary>選択中のメッセージ</summary>
    [ObservableProperty]
    private EpubcheckMessage? _selectedMessage;

    /// <summary>エラー箇所のプレビューページジャンプを要求するイベント。引数: ファイル名。</summary>
    public event Action<string>? JumpToFileRequested;

    // ───── コマンド ──────────────────────────────────────────────

    [RelayCommand(CanExecute = nameof(CanRunValidation))]
    private async Task RunValidationAsync()
    {
        if (string.IsNullOrEmpty(JarPath) || string.IsNullOrEmpty(EpubFilePath)) return;

        IsValidating = true;
        StatusMessage = "Validating...";
        Messages.Clear();
        SummaryMessage = "";

        try
        {
            var result = await Task.Run(() =>
                EpubcheckService.RunAsync(EpubFilePath, JarPath));

            SummaryMessage = result.Summary;
            foreach (var msg in result.Messages)
                Messages.Add(msg);

            StatusMessage = $"Done (exit code: {result.ExitCode})";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsValidating = false;
        }
    }

    private bool CanRunValidation() =>
        !IsValidating &&
        !string.IsNullOrEmpty(EpubFilePath) &&
        !string.IsNullOrEmpty(JarPath);

    partial void OnSelectedMessageChanged(EpubcheckMessage? value)
    {
        if (value != null && !string.IsNullOrEmpty(value.FileName))
            JumpToFileRequested?.Invoke(value.FileName);
    }

    /// <summary>プレビュー画面から呼び出し: 現在のEPUBを検証する。</summary>
    public void ValidateCurrentEpub(string epubPath, string jarPath)
    {
        EpubFilePath = epubPath;
        JarPath = jarPath;
        if (CanRunValidation())
            RunValidationCommand.Execute(null);
    }
}
