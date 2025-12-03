using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using OpenTaskManager.Models;
using OpenTaskManager.ViewModels;

namespace OpenTaskManager.Views;

public partial class UsersView : UserControl
{
    public UsersView()
    {
        InitializeComponent();
    }

    private void OnUserRowPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Border border && border.DataContext is UserInfo user)
        {
            if (DataContext is MainViewModel vm)
            {
                vm.SelectedUser = user;
            }
        }
    }
}
