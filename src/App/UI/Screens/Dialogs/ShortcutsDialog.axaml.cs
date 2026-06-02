using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Cine.Avalonia.Views.Dialogs;

public partial class ShortcutsDialog : Window
{
    public ShortcutsDialog()
    {
        InitializeComponent();
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();
}

