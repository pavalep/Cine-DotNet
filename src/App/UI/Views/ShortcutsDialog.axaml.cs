using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace Cine.Avalonia;

public partial class ShortcutsDialog : global::Avalonia.Controls.Window
{
    public ShortcutsDialog()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();
}
