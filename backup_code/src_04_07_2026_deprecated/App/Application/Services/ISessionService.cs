using System;
using System.Threading.Tasks;

namespace Cine.Avalonia.Services;

/// <summary>
/// Persists and restores playback session state across application restarts.
/// </summary>
public interface ISessionService
{
    /// <summary>Save current playback state to disk.</summary>
    void Save(string filePath, TimeSpan position, int subtitleTrackId, int audioTrackId,
              float subtitleDelay, float audioDelay, string rendererMode);

    /// <summary>Load and deserialize the saved session (synchronous).</summary>
    SessionData? Load();

    /// <summary>Delete the persisted session file.</summary>
    void Clear();
}

/// <summary>Data transfer object for session state.</summary>
public record SessionData(
    string FilePath,
    long PositionTicks,
    int SubtitleTrackId,
    int AudioTrackId,
    float SubtitleDelay,
    float AudioDelay,
    string RendererMode);
