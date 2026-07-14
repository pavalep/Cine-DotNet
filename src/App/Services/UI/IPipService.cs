using System;

namespace Simba.Avalonia.Services;

/// <summary>
/// Abstraction for PipService — enables unit testing of PipWindowManager
/// without a real MpvVideoView or PipWindow.
/// </summary>
public interface IPipService : IDisposable
{
    bool IsActive { get; }
    IPipWindow? PipWindow { get; }

    event EventHandler? PlayPauseRequested;
    event EventHandler<double>? SeekRequested;
    event EventHandler? MuteToggled;
    event EventHandler? PipClosed;

    IPipWindow? EnterPip(IPipWindow? testWindow = null);
    void ExitPip();
}
