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
    public LocalConvertViewModel LocalConvertVm { get; }
    public WebConvertViewModel WebConvertVm { get; }

    public Action<string>? OnConversionCompleted { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentSubPage))]
    [NotifyPropertyChangedFor(nameof(IsFileMode))]
    private bool _isUrlMode = true;

    public bool IsFileMode => !IsUrlMode;

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
}
