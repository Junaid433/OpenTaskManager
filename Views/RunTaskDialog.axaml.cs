using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

namespace OpenTaskManager.Views;

public partial class RunTaskDialog : Window
{
    public string? CommandText { get; private set; }
    public bool RunAsSudo { get; private set; }

    public RunTaskDialog()
    {
        InitializeComponent();
    }

    private async void OnBrowseClick(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select a file to run",
            AllowMultiple = false
        });

        if (files != null && files.Count > 0)
        {
            var textBox = this.FindControl<TextBox>("InputTextBox");
            if (textBox != null)
            {
                textBox.Text = files[0].Path.LocalPath;
            }
        }
    }

    private void OnOkClick(object? sender, RoutedEventArgs e)
    {
        var textBox = this.FindControl<TextBox>("InputTextBox");
        var sudoCheckBox = this.FindControl<CheckBox>("SudoCheckBox");
        if (textBox != null)
        {
            CommandText = textBox.Text;
            RunAsSudo = sudoCheckBox?.IsChecked ?? false;
        }
        Close(true);
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }
}
