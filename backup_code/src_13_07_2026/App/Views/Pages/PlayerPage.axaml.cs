using Avalonia.Controls;
using Cine.Avalonia.Core.Navigation;

namespace Cine.Avalonia.Views.Pages;

/// <summary>
/// Wraps the video surface and all playback-related overlays as a single
/// navigable page, toggled by NavigationService alongside StartPage.
/// All child controls are exposed as internal x:Name fields (generated
/// by the XAML compiler) and accessed directly from MainWindow.
/// </summary>
public partial class PlayerPage : UserControl, INavigable
{
    public PlayerPage()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Called when navigating to the player page. Restores UI visibility
    /// and starts the auto-hide timer for playback controls.
    /// </summary>
    public void OnNavigatedTo(object? parameter)
    {
        // ShowUiControls is called by MainWindow when media opens.
        // If parameter is a file path, MainWindow handles the open.
    }

    /// <summary>
    /// Called when navigating away from the player page.
    /// </summary>
    public void OnNavigatedFrom()
    {
        // Cancel any pending auto-hide or debounce timers if needed
    }
}
