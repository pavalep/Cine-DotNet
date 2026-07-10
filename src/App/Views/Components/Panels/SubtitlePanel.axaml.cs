using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Cine.Avalonia.Views.Components.Panels;

public partial class SubtitlePanel : UserControl
{
    public ItemsControl TrackListControl => TrackList;

    public event EventHandler<RoutedEventArgs>? PreferencesClicked;

    public SubtitlePanel()
    {
        InitializeComponent();
    }

    private void OnPreferencesClick(object? sender, RoutedEventArgs e)
    {
        PreferencesClicked?.Invoke(sender, e);
    }
}
