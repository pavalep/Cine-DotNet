using Cine.Avalonia.Services;
using Cine.Media.Events;
using Cine.Media.Models;

namespace Cine.Avalonia;

/// <summary>
/// Picture-in-Picture orchestration — delegates to <see cref="PipWindowManager"/>.
/// </summary>
public partial class MainWindow
{

    /// <summary>Handle PiP toggle from header bar / keyboard shortcut.</summary>
    private void OnPipToggled(object? sender, EventArgs e)
    {
        _pipWindowManager?.OnPipToggled(sender, e);
    }

    /// <summary>Sync PiP position display from player position events.</summary>
    private void SyncPipPosition(object? sender, PositionChangedEventArgs e)
    {
        _pipWindowManager?.SyncPosition(sender, e);
    }

    /// <summary>Sync PiP play/pause state from player state changes.</summary>
    private void SyncPipPlayState(PlaybackState state)
    {
        _pipWindowManager?.SyncPlayState(state);
    }

    /// <summary>Sync PiP replay mode when media ends.</summary>
    private void SyncPipReplayMode(bool isEnded)
    {
        _pipWindowManager?.SyncReplayMode(isEnded);
    }
}
