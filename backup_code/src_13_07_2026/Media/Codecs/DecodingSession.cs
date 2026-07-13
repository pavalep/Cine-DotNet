using Cine.Media.Interfaces;

namespace Cine.Media.Codecs;

/// <summary>
/// Default implementation of <see cref="IDecodingSession"/>.
/// Wraps an <see cref="IMediaPlayer"/> and exposes role-specific accessors
/// alongside diagnostics about the active codec session.
/// </summary>
public sealed class DecodingSession : IDecodingSession
{
    private bool _disposed;

    public IPlaybackControl Playback { get; }
    public IAudioControl Audio { get; }
    public IVideoControl Video { get; }
    public ISubtitleControl Subtitles { get; }
    public IChapterNavigation Chapters { get; }
    public IPlaylistManagement Playlist { get; }
    public IMediaPlayer Player { get; }
    public string ProviderName { get; }
    public bool IsHardwareDecoding { get; }
    public string RendererInfo { get; }

    public DecodingSession(
        IMediaPlayer player,
        string providerName,
        bool isHardwareDecoding,
        string rendererInfo)
    {
        Player = player ?? throw new ArgumentNullException(nameof(player));
        ProviderName = providerName;
        IsHardwareDecoding = isHardwareDecoding;
        RendererInfo = rendererInfo;

        Playback = player;
        Audio = player;
        Video = player;
        Subtitles = player;
        Chapters = player;
        Playlist = player;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        (Player as IDisposable)?.Dispose();
    }
}
