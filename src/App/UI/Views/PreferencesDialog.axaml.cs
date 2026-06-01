using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace Cine.Avalonia;

public partial class PreferencesDialog : global::Avalonia.Controls.Window
{
    public PreferencesDialog()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();
}
