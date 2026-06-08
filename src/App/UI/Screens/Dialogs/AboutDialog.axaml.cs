using Avalonia.Input;
using Avalonia.Interactivity;

namespace Cine.Avalonia.Views.Dialogs;

public partial class AboutDialog : global::Avalonia.Controls.Window
{
    public AboutDialog()
    {
        InitializeComponent();
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) { Close(); e.Handled = true; }
        };
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();
}

