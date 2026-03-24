using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AozoraEpub3.Gui.ViewModels;

/// <summary>
/// 層2「書く」のコンテナ ViewModel。
/// カードボード（デフォルト）とフルテキストエディタをタブ切り替えで統合する。
/// </summary>
public sealed partial class WriteViewModel : ViewModelBase
{
    public CardBoardViewModel CardBoardVm { get; }
    public EditorViewModel    EditorVm    { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentSubPage))]
    [NotifyPropertyChangedFor(nameof(IsEditorMode))]
    private bool _isCardMode = true;

    public bool IsEditorMode => !IsCardMode;

    public ViewModelBase CurrentSubPage =>
        IsCardMode ? (ViewModelBase)CardBoardVm : EditorVm;

    public WriteViewModel(CardBoardViewModel cardBoardVm, EditorViewModel editorVm)
    {
        CardBoardVm = cardBoardVm;
        EditorVm    = editorVm;
    }

    [RelayCommand] private void SwitchToCard()   => IsCardMode = true;
    [RelayCommand] private void SwitchToEditor() => IsCardMode = false;
}
