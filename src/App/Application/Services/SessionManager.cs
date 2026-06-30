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

    public SessionManager(string? storePath = null)
    {
        if (storePath != null)
        {
            var dir = Path.GetDirectoryName(storePath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
            _sessionPath = storePath;
        }
        else
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Cine");
            Directory.CreateDirectory(dir);
            _sessionPath = Path.Combine(dir, "session.json");
        }
    }

    /// <inheritdoc/>
    public void Save(string filePath, TimeSpan position, int subtitleTrackId, int audioTrackId,
                     float subtitleDelay, float audioDelay, string rendererMode)
    {
        try
        {
            var session = new
            {
                Version = 1, // schema version for future migrations
                FilePath = filePath,
                Position = position.Ticks,
                SubtitleTrackId = subtitleTrackId,
                AudioTrackId = audioTrackId,
                SubtitleDelay = subtitleDelay,
                AudioDelay = audioDelay,
                RendererMode = rendererMode
            };

            var json = JsonSerializer.Serialize(session);

            // Backup existing session before overwriting (crash recovery)
            if (File.Exists(_sessionPath))
            {
                var backupPath = _sessionPath + ".bak";
                try { File.Copy(_sessionPath, backupPath, overwrite: true); }
                catch { /* best-effort backup — not critical */ }
            }

            // Atomic write: temp → rename (prevents half-written files on crash)
            var tempPath = _sessionPath + ".tmp";
            File.WriteAllText(tempPath, json);
            File.Move(tempPath, _sessionPath, overwrite: true);
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
            {
                // Try backup if main session is missing (crash recovery)
                var backupPath = _sessionPath + ".bak";
                if (File.Exists(backupPath))
                {
                    try { File.Copy(backupPath, _sessionPath, overwrite: true); }
                    catch { /* best-effort — backup may be stale */ }
                }
                else
                {
                    return null;
                }
            }

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
        catch (JsonException)
        {
            // Session file is corrupted — try backup
            Log.ForContext<SessionManager>().Warning("Session file corrupted, trying backup...");
            try
            {
                var backupPath = _sessionPath + ".bak";
                if (File.Exists(backupPath))
                {
                    File.Copy(backupPath, _sessionPath, overwrite: true);
                    // Recursive call to parse the restored session
                    return Load();
                }
            }
            catch { /* best-effort — no backup available */ }
            return null;
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
