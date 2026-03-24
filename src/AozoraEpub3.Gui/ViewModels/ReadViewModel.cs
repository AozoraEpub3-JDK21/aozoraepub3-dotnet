using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AozoraEpub3.Gui.ViewModels;

/// <summary>
/// 層1「読む」のコンテナ ViewModel。
/// URL変換（WebConvertViewModel）とファイル変換（LocalConvertViewModel）を
/// タブ切り替えで統合する。
/// </summary>
public sealed partial class ReadViewModel : ViewModelBase
{
    // ── 子 ViewModel ───────────────────────────────────────────────────────
    public LocalConvertViewModel LocalConvertVm { get; }
    public WebConvertViewModel WebConvertVm { get; }

    /// <summary>変換完了時に MainWindowViewModel へ通知する。</summary>
    public Action<string>? OnConversionCompleted { get; set; }

    // ── モード切り替え ─────────────────────────────────────────────────────

    /// <summary>true = URLから読む / false = ファイルから読む</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentSubPage))]
    [NotifyPropertyChangedFor(nameof(IsFileMode))]
    private bool _isUrlMode = true;

    public bool IsFileMode => !IsUrlMode;

    /// <summary>ContentControl にバインドするサブページ VM。</summary>
    public ViewModelBase CurrentSubPage => IsUrlMode ? (ViewModelBase)WebConvertVm : LocalConvertVm;

    public ReadViewModel()
    {
        WebConvertVm   = new WebConvertViewModel();
        LocalConvertVm = new LocalConvertViewModel();
        LocalConvertVm.OnConversionCompleted = path => OnConversionCompleted?.Invoke(path);
    }

    [RelayCommand]
    private void SwitchToUrl()  => IsUrlMode = true;

    [RelayCommand]
    private void SwitchToFile() => IsUrlMode = false;

    /// <summary>「既存の EPUB をプレビューする」導線。MainWindowViewModel が接続する。</summary>
    public event Action? OpenPreviewRequested;

    [RelayCommand]
    private void OpenPreview() => OpenPreviewRequested?.Invoke();
}
