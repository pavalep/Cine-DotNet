using Avalonia.Interactivity;

namespace Cine.Avalonia.Views.Dialogs;

public partial class AboutDialog : global::Avalonia.Controls.Window
{
    public AboutDialog()
    {
        InitializeComponent();
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();
}

