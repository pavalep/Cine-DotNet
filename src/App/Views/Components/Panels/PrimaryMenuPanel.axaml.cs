using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using MaterialIcon = global::Material.Icons.Avalonia.MaterialIcon;

namespace Simba.Avalonia.Views.Components.Panels;

public partial class PrimaryMenuPanel : UserControl
{
    public event EventHandler<RoutedEventArgs>? PipClicked;
    public event EventHandler<RoutedEventArgs>? AlwaysOnTopClicked;
    public event EventHandler<RoutedEventArgs>? LoopFileClicked;
    public event EventHandler<RoutedEventArgs>? LoopPlaylistClicked;
    public event EventHandler<RoutedEventArgs>? ShuffleClicked;
    public event EventHandler<RoutedEventArgs>? ShortcutsClicked;
    public event EventHandler<RoutedEventArgs>? PreferencesClicked;
    public event EventHandler<RoutedEventArgs>? AboutClicked;

    // Expose toggle icons for state sync
    public MaterialIcon LoopFileIconControl => LoopFileIcon;
    public MaterialIcon LoopPlaylistIconControl => LoopPlaylistIcon;
    public MaterialIcon ShuffleIconControl => ShuffleIcon;

    public PrimaryMenuPanel()
    {
        InitializeComponent();
    }

    private void OnPipClick(object? sender, RoutedEventArgs e) => PipClicked?.Invoke(sender, e);
    private void OnAlwaysOnTopClick(object? sender, RoutedEventArgs e) => AlwaysOnTopClicked?.Invoke(sender, e);
    private void OnLoopFileClick(object? sender, RoutedEventArgs e) => LoopFileClicked?.Invoke(sender, e);
    private void OnLoopPlaylistClick(object? sender, RoutedEventArgs e) => LoopPlaylistClicked?.Invoke(sender, e);
    private void OnShuffleClick(object? sender, RoutedEventArgs e) => ShuffleClicked?.Invoke(sender, e);
    private void OnShortcutsClick(object? sender, RoutedEventArgs e) => ShortcutsClicked?.Invoke(sender, e);
    private void OnPreferencesClick(object? sender, RoutedEventArgs e) => PreferencesClicked?.Invoke(sender, e);
    private void OnAboutClick(object? sender, RoutedEventArgs e) => AboutClicked?.Invoke(sender, e);
}
