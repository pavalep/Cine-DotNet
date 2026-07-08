using System;
using Cine.Media.Events;
using Cine.Media.Models;

namespace Cine.Media.Interfaces;

/// <summary>
/// Composite media player interface aggregating all role-specific interfaces
/// plus low-level rendering and command members.
/// </summary>
public interface IMediaPlayer : IPlaybackControl, IAudioControl, IVideoControl,
    ISubtitleControl, IChapterNavigation, IPlaylistManagement
{
    // ── Low-level / native rendering (not in role interfaces) ──
    void InitializeRenderer(IntPtr hwnd);
    void NotifyResize(int width, int height);
    void Command(string command, params string[] args);
}
