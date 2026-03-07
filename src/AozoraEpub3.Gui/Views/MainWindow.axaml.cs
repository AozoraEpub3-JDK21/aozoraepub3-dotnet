using System.ComponentModel;
using Avalonia.Controls;
using AozoraEpub3.Gui.ViewModels;

namespace AozoraEpub3.Gui.Views;

public partial class MainWindow : Window
{
    private WindowState _savedState;

    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
            vm.PropertyChanged += OnVmPropertyChanged;
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(MainWindowViewModel.IsPreviewMaximized)) return;
        var vm = (MainWindowViewModel)sender!;

        if (vm.IsPreviewMaximized)
        {
            _savedState = WindowState;
            WindowState = WindowState.FullScreen;
        }
        else
        {
            WindowState = _savedState;
        }
    }
}
