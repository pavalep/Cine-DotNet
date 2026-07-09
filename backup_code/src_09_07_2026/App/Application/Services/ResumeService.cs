using System;
using System.IO;
using System.Text.Json;
using Cine.Core;

namespace Cine.Avalonia.Services;

/// <summary>
/// Orchestrates session resume — loading saved state, validating, and
/// applying it safely. Separates resume logic from MainViewModel.
///
/// Handles:
///   - Corrupt JSON → return null (fresh start)
///   - Missing file → return null (fresh start)
///   - Future schema version → return null (fresh start)
///   - Position clamping → position >= 0
/// </summary>
public class ResumeService
{
    private readonly SessionManager _session;

    public ResumeService(SessionManager session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
    }

    /// <summary>
    /// Try to load session data. Returns null if no valid session exists.
    /// </summary>
    public SessionData? TryResume()
    {
        try
        {
            return _session.Load();
        }
        catch (JsonException)
        {
            // Corrupt JSON → delete the corrupt file and return null
            _session.Clear();
            return null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// Validates that the session data has a file that still exists on disk.
    /// </summary>
    public bool IsValid(SessionData? data)
    {
        if (data == null) return false;
        if (data.PositionTicks < 0) return false;
        if (string.IsNullOrWhiteSpace(data.FilePath)) return false;
        return File.Exists(data.FilePath);
    }
}
