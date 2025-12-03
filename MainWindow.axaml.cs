using Avalonia.Controls;
using Avalonia.Input;
using OpenTaskManager.ViewModels;

namespace OpenTaskManager;

public partial class MainWindow : Window
{
    private MainViewModel? _viewModel;

    public MainWindow()
    {
        InitializeComponent();
        
        _viewModel = new MainViewModel();
        DataContext = _viewModel;
        
        Loaded += async (_, _) =>
        {
            if (_viewModel != null)
                await _viewModel.InitializeAsync();
        };
        
        Closing += async (_, _) =>
        {
            if (_viewModel != null)
                await _viewModel.CleanupAsync();
        };
    }

    private void TitleBar_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginMoveDrag(e);
        }
    }
}