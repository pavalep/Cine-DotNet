using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace Cine.Avalonia.Views;

public partial class AboutDialog : global::Avalonia.Controls.Window
{
    public AboutDialog()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();
}
