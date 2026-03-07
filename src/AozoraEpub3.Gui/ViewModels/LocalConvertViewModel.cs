using System.Collections.ObjectModel;
using System.IO;
using System.Threading;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AozoraEpub3.Core.Converter;
using AozoraEpub3.Gui.Models;
using AozoraEpub3.Gui.Services;

namespace AozoraEpub3.Gui.ViewModels;

/// <summary>
/// ローカルファイル変換画面の ViewModel。
/// <para>
/// 責務:
/// <list type="bullet">
///   <item>入力ファイル一覧の管理（D&amp;D + ダイアログ）</item>
///   <item>出力先ディレクトリ / 対象端末の基本設定</item>
///   <item>詳細設定ドロワーの開閉</item>
///   <item>変換実行 / キャンセル</item>
///   <item>進捗表示とログ収集</item>
/// </list>
/// </para>
/// </summary>
public sealed partial class LocalConvertViewModel : ViewModelBase
{
    private readonly ConversionService _conversionService = new();
    private CancellationTokenSource? _cts;

    /// <summary>変換完了後にプレビューを開くためのコールバック。MainWindowViewModel が設定する。</summary>
    public Action<string>? OnConversionCompleted { get; set; }

    public LocalConvertViewModel()
    {
        InputFiles.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasFiles));
    }

    // ───── 詳細設定（ドロワー内）──────────────────────────────────────────────
    public LocalConvertSettingsViewModel Settings { get; } = new();

    // ───── ファイル一覧 ───────────────────────────────────────────────────────

    /// <summary>変換対象ファイル一覧</summary>
    public ObservableCollection<InputFileItem> InputFiles { get; } = [];

    /// <summary>ファイルが1件以上あるか（リスト表示制御用）</summary>
    public bool HasFiles => InputFiles.Count > 0;

    /// <summary>選択中のファイルアイテム</summary>
    [ObservableProperty]
    private InputFileItem? _selectedFile;

    // ───── 基本設定（Easy モード） ────────────────────────────────────────────

    /// <summary>出力先ディレクトリ（空 = 入力ファイルと同じ）</summary>
    [ObservableProperty] private string _outputDirectory = "";

    // ───── ドロワー制御 ───────────────────────────────────────────────────────

    /// <summary>詳細設定ドロワーが開いているか</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DrawerWidth))]
    private bool _isDrawerOpen = false;

    /// <summary>ドロワーの幅（アニメーション用）</summary>
    public double DrawerWidth => IsDrawerOpen ? 520 : 0;

    [RelayCommand]
    private void OpenDrawer() => IsDrawerOpen = true;

    [RelayCommand]
    private void CloseDrawer() => IsDrawerOpen = false;

    // ───── ドラッグオーバー表示 ───────────────────────────────────────────────

    /// <summary>ファイルがドラッグオーバー中か（ドロップゾーンのハイライト用）</summary>
    [ObservableProperty] private bool _isDragOver = false;

    // ───── 変換実行 ───────────────────────────────────────────────────────────

    /// <summary>変換処理が実行中か</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsIdle))]
    [NotifyCanExecuteChangedFor(nameof(ConvertCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelCommand))]
    private bool _isConverting = false;

    public bool IsIdle => !IsConverting;

    /// <summary>進捗値 (0–100)</summary>
    [ObservableProperty] private double _progressValue = 0;

    /// <summary>ステータスメッセージ（1行）</summary>
    [ObservableProperty] private string _statusMessage = "";

    /// <summary>最後の出力ディレクトリ（完了後「フォルダを開く」用）</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(OpenOutputFolderCommand))]
    private string? _lastOutputDirectory;

    // ───── ログエリア ─────────────────────────────────────────────────────────

    /// <summary>ログエリアが表示されているか</summary>
    [ObservableProperty] private bool _isLogVisible = false;

    /// <summary>ログメッセージ行リスト</summary>
    public ObservableCollection<string> LogLines { get; } = [];

    // ───── ファイル操作コマンド ───────────────────────────────────────────────

    /// <summary>ファイルをコレクションに追加する（パスの重複をスキップ）</summary>
    public void AddFiles(IEnumerable<IStorageFile> files)
    {
        var existingPaths = InputFiles.Select(f => f.FullPath).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var file in files)
        {
            var path = file.Path.LocalPath;
            if (existingPaths.Contains(path)) continue;

            var item = new InputFileItem(path);
            if (item.IsUnsupported)
            {
                AppendLog($"[SKIP] .cbz は変換非対応のためスキップします: {item.FileName}");
                continue;
            }
            InputFiles.Add(item);
            existingPaths.Add(path);
        }
    }

    /// <summary>ファイルパス文字列から直接追加（CLI 互換用）</summary>
    public void AddFilePaths(IEnumerable<string> paths)
    {
        var existingPaths = InputFiles.Select(f => f.FullPath).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var path in paths)
        {
            if (!File.Exists(path) || existingPaths.Contains(path)) continue;
            var item = new InputFileItem(path);
            if (item.IsUnsupported)
            {
                AppendLog($"[SKIP] .cbz は変換非対応のためスキップします: {item.FileName}");
                continue;
            }
            InputFiles.Add(item);
            existingPaths.Add(path);
        }
    }

    [RelayCommand]
    private void RemoveFile(InputFileItem? item)
    {
        if (item is not null)
            InputFiles.Remove(item);
    }

    [RelayCommand]
    private void ClearAllFiles() => InputFiles.Clear();

    // ───── 出力先ディレクトリ ─────────────────────────────────────────────────
    // （ダイアログ起動は View のコードビハインドで実行し、結果をこのプロパティにセット）

    // ───── 変換実行 ───────────────────────────────────────────────────────────

    [RelayCommand(CanExecute = nameof(CanConvert))]
    private async Task ConvertAsync()
    {
        if (InputFiles.Count == 0) return;

        IsConverting = true;
        ProgressValue = 0;
        LogLines.Clear();
        LastOutputDirectory = null;

        // LogAppender を ViewModel に接続
        LogAppender.OutputAction = msg => Avalonia.Threading.Dispatcher.UIThread.Post(
            () => AppendLog(msg));

        _cts = new CancellationTokenSource();
        var ct = _cts.Token;

        try
        {
            var total = InputFiles.Count;
            var progress = new Progress<int>(v =>
                ProgressValue = v / 100.0 / total * 100);

            for (int i = 0; i < total; i++)
            {
                var file = InputFiles[i];
                SetStatus($"[{i + 1}/{total}] {file.FileName}");

                await _conversionService.ConvertFileAsync(
                    file.FullPath,
                    string.IsNullOrEmpty(OutputDirectory) ? null : OutputDirectory,
                    Settings,
                    progress,
                    ct);
            }

            LastOutputDirectory = string.IsNullOrEmpty(OutputDirectory)
                ? Path.GetDirectoryName(InputFiles[0].FullPath)
                : OutputDirectory;

            SetStatus("変換完了");
            ProgressValue = 100;

            // 最後に変換されたファイルの出力EPUBを探してプレビューを開く
            if (OnConversionCompleted != null && LastOutputDirectory != null)
            {
                var lastInput = InputFiles[^1].FullPath;
                var outDir = LastOutputDirectory;
                var epubFiles = Directory.GetFiles(outDir, "*" + Settings.OutputExtension)
                    .OrderByDescending(File.GetLastWriteTimeUtc)
                    .FirstOrDefault();
                if (epubFiles != null)
                {
                    Avalonia.Threading.Dispatcher.UIThread.Post(
                        () => OnConversionCompleted?.Invoke(epubFiles));
                }
            }
        }
        catch (OperationCanceledException)
        {
            SetStatus("キャンセルされました");
        }
        catch (Exception ex)
        {
            SetStatus($"エラー: {ex.Message}");
            AppendLog($"[ERROR] {ex}");
            IsLogVisible = true; // エラー時は自動でログを開く
        }
        finally
        {
            IsConverting = false;
            _cts?.Dispose();
            _cts = null;
            // LogAppender を元に戻す
            LogAppender.OutputAction = Console.Error.WriteLine;
        }
    }

    private bool CanConvert() => !IsConverting && InputFiles.Count > 0;

    [RelayCommand(CanExecute = nameof(CanCancel))]
    private void Cancel() => _cts?.Cancel();

    private bool CanCancel() => IsConverting;

    [RelayCommand(CanExecute = nameof(CanOpenFolder))]
    private void OpenOutputFolder()
    {
        if (!string.IsNullOrEmpty(LastOutputDirectory) && Directory.Exists(LastOutputDirectory))
        {
            // OS のファイラーで開く
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = LastOutputDirectory,
                UseShellExecute = true
            };
            System.Diagnostics.Process.Start(psi);
        }
    }

    private bool CanOpenFolder() => !string.IsNullOrEmpty(LastOutputDirectory);

    [RelayCommand]
    private void ToggleLog() => IsLogVisible = !IsLogVisible;

    [RelayCommand]
    private void ClearLog() => LogLines.Clear();

    // ───── ヘルパー ───────────────────────────────────────────────────────────

    private void SetStatus(string msg) =>
        Avalonia.Threading.Dispatcher.UIThread.Post(() => StatusMessage = msg);

    private void AppendLog(string msg) => LogLines.Add(msg);
}
