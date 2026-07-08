using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Cine.Core;
using Cine.Media.Interfaces;
using Cine.Media.Implementations;

namespace Cine.Avalonia.Services;

/// <summary>
/// Service that wraps the active player backend for use by Avalonia ViewModels.
/// Provides lifecycle management (init with timeout, shutdown with timeout)
/// and exposes platform-agnostic playback functionality via <see cref="Player"/>.
/// </summary>
public class PlayerService : IDisposable
{
    private IMediaPlayer? _player;
    private IDecodingSession? _session;
    private bool _disposed;
    private readonly IPlayerFactory _factory;
    private readonly CodecManager _codecManager;
    private static readonly string DebugLogFile = CreateLogFilePath();

    private static string CreateLogFilePath()
    {
        try
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Cine");
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, "cine_startup.log");
        }
        catch (Exception ex)
        {
            Log.ForContext<PlayerService>().Error(ex, "Log path creation failed");
            return Path.Combine(Path.GetTempPath(), "cine_startup.log");
        }
    }

    private static void DebugLog(string message)
    {
        try
        {
            File.AppendAllText(DebugLogFile, $"[{DateTime.Now:HH:mm:ss.fff}] [PlayerService] {message}{Environment.NewLine}");
        }
        catch
        {
            Log.ForContext<PlayerService>().Trace("Debug log append failed (best-effort)");
        }
    }

    public PlayerService(CodecManager codecManager, IPlayerFactory? factory = null)
    {
        _codecManager = codecManager ?? throw new ArgumentNullException(nameof(codecManager));
        _factory = factory ?? new MpvPlayerFactory();
    }

    public IMediaPlayer? Player => _player;

    /// <summary>Current decoding session with codec diagnostics.</summary>
    public IDecodingSession? Session => _session;

    /// <summary>The active codec provider selected at startup.</summary>
    public ICodecProvider? ActiveProvider => _codecManager.ActiveProvider;

    // ── Role-specific accessors (ISP) — narrow dependency surface ──
    public IPlaybackControl? Playback => _player;
    public IAudioControl? Audio => _player;
    public IVideoControl? Video => _player;
    public ISubtitleControl? Subtitles => _player;
    public IChapterNavigation? Chapters => _player;
    public IPlaylistManagement? Playlist => _player;

    public event EventHandler<string>? Error;

    /// <summary>Initialize the player. Gracefully handles double-init (no-op).</summary>
    public void Initialize()
    {
        if (_player != null)
        {
            DebugLog("Initialize skipped — already initialized");
            return;
        }

        try
        {
            DebugLog("Initialize start");
            _player = _factory.CreatePlayer();
            DebugLog($"{_player.GetType().Name} created");

            // Configure the player using the selected codec provider
            _codecManager.ActiveProvider.Configure(_player);
            DebugLog($"Configured with codec provider: {_codecManager.ActiveProvider.Name}");

            // Create a decoding session wrapping the player
            _session = _codecManager.CreateSession(_player);
            DebugLog("Decoding session created");

            _player.Error += OnError;
            DebugLog("Initialize finish");
        }
        catch (Exception ex)
        {
            DebugLog($"Initialize failed: {ex}");
            Log.ForContext<PlayerService>().Error(ex, "Player creation FAILED");
            throw;
        }
    }

    /// <summary>
    /// Shutdown player with a timeout. Calls Stop(), unsubscribes events,
    /// disposes, and waits up to 3 seconds for native cleanup.
    /// </summary>
    public async Task ShutdownAsync(CancellationToken ct = default)
    {
        if (_player == null) return;

        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

        _player.Stop();

        if (_player is IDisposable disposable)
        {
            try
            {
                await Task.Run(() => disposable.Dispose(), linked.Token);
            }
            catch (OperationCanceledException)
            {
                DebugLog("Player dispose timed out after 3s");
            }
        }

        _player = null;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        try
        {
            if (_player != null)
            {
                _player.Error -= OnError;
                _player.Stop();
                (_player as IDisposable)?.Dispose();
                _player = null;
            }
        }
        catch (Exception ex)
        {
            DebugLog($"Dispose error: {ex.Message}");
        }

        GC.SuppressFinalize(this);
    }

    private void OnError(object? sender, string error)
    {
        DebugLog($"[Error] {error}");
        Error?.Invoke(this, error);
    }
}

/// <summary>Creates player instances. Testable via mock.</summary>
public interface IPlayerFactory
{
    IMediaPlayer CreatePlayer();
}

/// <summary>Default production factory — creates MpvPlayer.</summary>
public class MpvPlayerFactory : IPlayerFactory
{
    public IMediaPlayer CreatePlayer() => new MpvPlayer();
}

