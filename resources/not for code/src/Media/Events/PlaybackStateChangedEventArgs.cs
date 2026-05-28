using System;

namespace Cine.Media.Events;

public class PlaybackStateChangedEventArgs : EventArgs
{
    public bool IsPaused { get; }

    public PlaybackStateChangedEventArgs(bool isPaused)
    {
        IsPaused = isPaused;
    }
}