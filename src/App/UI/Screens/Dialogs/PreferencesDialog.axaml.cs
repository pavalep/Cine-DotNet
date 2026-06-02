using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Cine.Avalonia.Views.Dialogs;

public partial class PreferencesDialog : Window
{
    public PreferencesDialog()
    {
        InitializeComponent();
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();
}

