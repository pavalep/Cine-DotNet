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

    private void OnViewAllShortcutsClick(object? sender, RoutedEventArgs e)
    {
        var dialog = new KeyboardShortcutsDialog();
        dialog.Show(this);
    }
}

