using System;
using System.IO;

namespace Cine.Core.Services;

/// <summary>
/// Centralized settings directory path management.
/// All application settings stores should use this instead of constructing paths independently.
/// </summary>
public static class SettingsPath
{
    private static readonly string BaseDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Cine");

    static SettingsPath()
    {
        Directory.CreateDirectory(BaseDir);
    }

    /// <summary>Base directory: %LOCALAPPDATA%\Cine\</summary>
    public static string Base => BaseDir;

    /// <summary>Settings directory: %LOCALAPPDATA%\Cine\Settings\</summary>
    public static string Settings => GetSubDir("Settings");

    /// <summary>Logs directory: %LOCALAPPDATA%\Cine\logs\</summary>
    public static string Logs => GetSubDir("logs");

    /// <summary>Subtitles settings directory: %LOCALAPPDATA%\Cine\subtitles\</summary>
    public static string Subtitles => GetSubDir("subtitles");

    /// <summary>Playlist settings directory: %LOCALAPPDATA%\Cine\playlist\</summary>
    public static string Playlist => GetSubDir("playlist");

    private static string GetSubDir(string name)
    {
        var dir = Path.Combine(BaseDir, name);
        Directory.CreateDirectory(dir);
        return dir;
    }
}
