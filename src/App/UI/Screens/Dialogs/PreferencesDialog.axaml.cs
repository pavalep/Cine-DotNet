using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Cine.Avalonia.Views.Dialogs;

public partial class PreferencesDialog : Window
{
    public PreferencesDialog()
    {
        InitializeComponent();
        KeyDown += OnGlobalKeyDown;
    }

    private void OnGlobalKeyDown(object? sender, global::Avalonia.Input.KeyEventArgs e)
    {
        if (e.Key == global::Avalonia.Input.Key.Escape)
        {
            Close();
            e.Handled = true;
        }
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();

    private void OnViewAllShortcutsClick(object? sender, RoutedEventArgs e)
    {
        var dialog = new KeyboardShortcutsDialog();
        dialog.ShowDialog(this);
    }
}

