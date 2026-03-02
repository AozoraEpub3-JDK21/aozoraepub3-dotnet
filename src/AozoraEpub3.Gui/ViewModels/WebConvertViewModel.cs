using System.Collections.ObjectModel;
using System.IO;
using System.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AozoraEpub3.Core.Converter;
using AozoraEpub3.Core.Web;
using AozoraEpub3.Gui.Services;

namespace AozoraEpub3.Gui.ViewModels;

/// <summary>
/// Web変換画面の ViewModel。
/// URL からの小説取得・変換を担当する。
/// </summary>
public sealed partial class WebConvertViewModel : ViewModelBase
{
    private readonly ConversionService _conversionService = new();
    private CancellationTokenSource? _cts;

    // ───── 取得設定 ───────────────────────────────────────────────────────────

    /// <summary>変換対象 URL</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConvertCommand))]
    private string _url = "";

    /// <summary>ダウンロード間隔 (ms) — 最小 500ms をUIで強制</summary>
    [ObservableProperty] private int _downloadIntervalMs = 1500;

    /// <summary>web設定ディレクトリ</summary>
    [ObservableProperty] private string _webConfigDirectory = "";

    // ───── 出力設定 ───────────────────────────────────────────────────────────
    public LocalConvertSettingsViewModel Settings { get; } = new();

    /// <summary>出力先ディレクトリ</summary>
    [ObservableProperty] private string _outputDirectory = "";

    // ───── Web テキスト処理設定 ───────────────────────────────────────────────

    public NarouFormatSettings NarouSettings { get; } = new();

    // ───── 変換状態 ───────────────────────────────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsIdle))]
    [NotifyCanExecuteChangedFor(nameof(ConvertCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelCommand))]
    private bool _isConverting = false;

    public bool IsIdle => !IsConverting;

    [ObservableProperty] private double _progressValue = 0;
    [ObservableProperty] private string _statusMessage = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(OpenOutputFolderCommand))]
    private string? _lastOutputDirectory;

    // ───── ログ ───────────────────────────────────────────────────────────────

    [ObservableProperty] private bool _isLogVisible = false;
    public ObservableCollection<string> LogLines { get; } = [];

    // ───── コマンド ───────────────────────────────────────────────────────────

    [RelayCommand(CanExecute = nameof(CanConvert))]
    private async Task ConvertAsync()
    {
        if (string.IsNullOrWhiteSpace(Url)) return;

        IsConverting = true;
        ProgressValue = 0;
        LogLines.Clear();
        LastOutputDirectory = null;
        StatusMessage = "Web変換中...";

        LogAppender.OutputAction = msg =>
            Avalonia.Threading.Dispatcher.UIThread.Post(() => LogLines.Add(msg));

        _cts = new CancellationTokenSource();
        var ct = _cts.Token;

        try
        {
            var webConfigDir = string.IsNullOrEmpty(WebConfigDirectory)
                ? Path.Combine(AppContext.BaseDirectory, "web")
                : WebConfigDirectory;

            var progress = new Progress<int>(v => ProgressValue = v);

            await _conversionService.ConvertUrlAsync(
                Url,
                string.IsNullOrEmpty(OutputDirectory) ? null : OutputDirectory,
                Settings,
                NarouSettings,
                webConfigDir,
                Math.Max(500, DownloadIntervalMs),  // 500ms 未満を強制禁止
                progress,
                ct);

            LastOutputDirectory = string.IsNullOrEmpty(OutputDirectory) ? "." : OutputDirectory;
            StatusMessage = "変換完了";
            ProgressValue = 100;
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "キャンセルされました";
        }
        catch (Exception ex)
        {
            StatusMessage = $"エラー: {ex.Message}";
            LogLines.Add($"[ERROR] {ex}");
            IsLogVisible = true;
        }
        finally
        {
            IsConverting = false;
            _cts?.Dispose();
            _cts = null;
            LogAppender.OutputAction = Console.Error.WriteLine;
        }
    }

    private bool CanConvert() => !IsConverting && !string.IsNullOrWhiteSpace(Url);

    [RelayCommand(CanExecute = nameof(CanCancel))]
    private void Cancel() => _cts?.Cancel();

    private bool CanCancel() => IsConverting;

    [RelayCommand(CanExecute = nameof(CanOpenFolder))]
    private void OpenOutputFolder()
    {
        if (!string.IsNullOrEmpty(LastOutputDirectory) && Directory.Exists(LastOutputDirectory))
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = LastOutputDirectory,
                UseShellExecute = true
            });
        }
    }

    private bool CanOpenFolder() => !string.IsNullOrEmpty(LastOutputDirectory);

    [RelayCommand]
    private void ToggleLog() => IsLogVisible = !IsLogVisible;

    [RelayCommand]
    private void ClearLog() => LogLines.Clear();
}
