using System;

namespace Cine.Avalonia.Services;

/// <summary>
/// Abstraction for the Picture-in-Picture window.
/// Enables unit testing of PipService and PipWindowManager without creating a real window.
/// </summary>
public interface IPipWindow
{
    bool IsClosed { get; }

    event EventHandler? PlayPauseRequested;
    event EventHandler<double>? SeekRequested;
    event EventHandler? MuteToggled;
    event EventHandler? Closed;

    void Show();
    void Close();
    void SetFileName(string fileName, string folderOrCodec);
    void SetMuted(bool muted);
    void SetPlayingState(bool isPlaying);
    void SetReplayMode(bool showReplay);
    void UpdatePosition(double positionSec, double durationSec);
    void SetAspectRatio(double aspectRatio);
    void UpdateFrame(byte[] pixels, int width, int height);
    void ShowAllControls();
    void StartHoverTimer();
}
