using System;
using Cine.Media.Models;

namespace Cine.Media.Events;

public class PlaybackStateChangedEventArgs : EventArgs
{
    public PlaybackState State { get; }
    public bool IsPaused { get; }

    public PlaybackStateChangedEventArgs(bool isPaused)
        : this(isPaused ? PlaybackState.Paused : PlaybackState.Playing)
    {
    }

    public PlaybackStateChangedEventArgs(PlaybackState state)
    {
        State = state;
        IsPaused = state == PlaybackState.Paused;
    }
}
