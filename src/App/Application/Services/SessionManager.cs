using System;
using System.IO;
using System.Text.Json;
using Cine.Core;

namespace Cine.Avalonia.Services;

/// <summary>
/// Manages session persistence (save/load/clear) for the media player.
/// Data is stored as JSON in <c>%LOCALAPPDATA%\Cine\session.json</c>.
/// </summary>
public class SessionManager : ISessionService
{
    private readonly string _sessionPath;

    public SessionManager()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Cine");
        Directory.CreateDirectory(dir);
        _sessionPath = Path.Combine(dir, "session.json");
    }

    /// <inheritdoc/>
    public void Save(string filePath, TimeSpan position, int subtitleTrackId, int audioTrackId,
                     float subtitleDelay, float audioDelay, string rendererMode)
    {
        try
        {
            var session = new
            {
                FilePath = filePath,
                Position = position.Ticks,
                SubtitleTrackId = subtitleTrackId,
                AudioTrackId = audioTrackId,
                SubtitleDelay = subtitleDelay,
                AudioDelay = audioDelay,
                RendererMode = rendererMode
            };
            File.WriteAllText(_sessionPath, JsonSerializer.Serialize(session));
        }
        catch (Exception ex)
        {
            Log.ForContext<SessionManager>().Error(ex, "Failed to save session");
        }
    }

    /// <inheritdoc/>
    public SessionData? Load()
    {
        try
        {
            if (!File.Exists(_sessionPath))
                return null;

            var json = File.ReadAllText(_sessionPath);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("FilePath", out var pathEl))
                return null;

            var filePath = pathEl.GetString() ?? string.Empty;
            var positionTicks = root.TryGetProperty("Position", out var posEl) ? posEl.GetInt64() : 0L;
            var subtitleTrackId = root.TryGetProperty("SubtitleTrackId", out var subEl) ? subEl.GetInt32() : -1;
            var audioTrackId = root.TryGetProperty("AudioTrackId", out var audEl) ? audEl.GetInt32() : -1;
            var subtitleDelay = root.TryGetProperty("SubtitleDelay", out var subDelayEl) ? (float)subDelayEl.GetDouble() : 0f;
            var audioDelay = root.TryGetProperty("AudioDelay", out var audDelayEl) ? (float)audDelayEl.GetDouble() : 0f;
            var rendererMode = root.TryGetProperty("RendererMode", out var rmEl) ? rmEl.GetString() ?? "Auto" : "Auto";

            return new SessionData(filePath, positionTicks, subtitleTrackId, audioTrackId,
                                   subtitleDelay, audioDelay, rendererMode);
        }
        catch (Exception ex)
        {
            Log.ForContext<SessionManager>().Warning("Failed to load session: {Error}", ex.Message);
            return null;
        }
    }

    /// <inheritdoc/>
    public void Clear()
    {
        try
        {
            if (File.Exists(_sessionPath))
                File.Delete(_sessionPath);
        }
        catch (Exception ex)
        {
            Log.ForContext<SessionManager>().Warning("Failed to clear session: {Error}", ex.Message);
        }
    }
}
