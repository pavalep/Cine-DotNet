namespace Simba.Media.Interfaces;

/// <summary>
/// Represents an active decoding session wrapping a player instance.
/// Provides role-specific accessors and diagnostics about the active codec.
/// </summary>
public interface IDecodingSession : IDisposable
{
    /// <summary>Playback control (play, pause, seek, speed, etc.).</summary>
    IPlaybackControl Playback { get; }

    /// <summary>Audio control (volume, tracks, device, etc.).</summary>
    IAudioControl Audio { get; }

    /// <summary>Video control (zoom, filters, tracks, etc.).</summary>
    IVideoControl Video { get; }

    /// <summary>Subtitle control (tracks, styling, etc.).</summary>
    ISubtitleControl Subtitles { get; }

    /// <summary>Chapter navigation.</summary>
    IChapterNavigation Chapters { get; }

    /// <summary>Playlist management.</summary>
    IPlaylistManagement Playlist { get; }

    /// <summary>The underlying composite player instance.</summary>
    IMediaPlayer Player { get; }

    /// <summary>Name of the codec provider responsible for this session.</summary>
    string ProviderName { get; }

    /// <summary>Whether hardware decoding is active in this session.</summary>
    bool IsHardwareDecoding { get; }

    /// <summary>Human-readable info about the video renderer / backend.</summary>
    string RendererInfo { get; }
}
