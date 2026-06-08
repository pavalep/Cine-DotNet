using System;
using System.Runtime.InteropServices;
using Cine.Core;

namespace Cine.Avalonia.Services;

/// <summary>
/// Registers Cine as the default player for supported video/audio formats on Windows.
/// Writes to HKEY_CURRENT_USER\Software\Classes (no admin required).
/// </summary>
public static class FileAssociationService
{
    private static readonly string[] VideoFormats =
    {
        ".mp4", ".mkv", ".avi", ".mov", ".wmv", ".flv", ".webm",
        ".m4v", ".mpg", ".mpeg", ".mts", ".ts", ".m2ts", ".3gp",
        ".ogv", ".divx", ".vob"
    };

    private static readonly string[] AudioFormats =
    {
        ".mp3", ".wav", ".flac", ".aac", ".ogg", ".m4a", ".wma",
        ".opus", ".alac"
    };

    private static readonly string[] SubtitleFormats =
    {
        ".srt", ".vtt", ".ass", ".ssa", ".sub", ".idx"
    };

    private const string AppId = "CineMediaPlayer";
    private const string AppName = "Cine Media Player";

    /// <summary>Register all supported file types.</summary>
    public static void Register()
    {
        try
        {
            RegisterAllFormats(VideoFormats, "video");
            RegisterAllFormats(AudioFormats, "audio");
            RegisterSubtitleFormats();

            // Notify Windows shell
            SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST, IntPtr.Zero, IntPtr.Zero);

            Log.ForContext("FileAssociation").Info("File associations registered ({0} video, {1} audio, {2} subtitle)",
                VideoFormats.Length, AudioFormats.Length, SubtitleFormats.Length);
        }
        catch (Exception ex)
        {
            Log.ForContext("FileAssociation").Error(ex, "Failed to register file associations");
        }
    }

    /// <summary>Remove all registered file types.</summary>
    public static void Unregister()
    {
        try
        {
            foreach (var ext in VideoFormats)
                RemoveAssociation(ext);
            foreach (var ext in AudioFormats)
                RemoveAssociation(ext);
            foreach (var ext in SubtitleFormats)
                RemoveAssociation(ext);

            SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST, IntPtr.Zero, IntPtr.Zero);
        }
        catch (Exception ex)
        {
            Log.ForContext("FileAssociation").Error(ex, "Failed to unregister file associations");
        }
    }

    /// <summary>Check if Cine is the default for a given extension.</summary>
    public static bool IsRegistered(string extension)
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                $@"Software\Classes\{extension}\OpenWithProgids");
            return key?.GetValue(AppId) != null;
        }
        catch
        {
            return false;
        }
    }

    private static void RegisterAllFormats(string[] formats, string type)
    {
        foreach (var ext in formats)
        {
            // Create progid key
            using var progid = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(
                $@"Software\Classes\{AppId}\shell\open\command");
            progid.SetValue("", $"\"{GetExecutablePath()}\" \"%1\"");

            // Set friendly name
            using var progidName = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(
                $@"Software\Classes\{AppId}");
            progidName.SetValue("", $"{AppName} {type} file");

            // Associate extension
            using var extKey = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(
                $@"Software\Classes\{ext}\OpenWithProgids");
            extKey.SetValue(AppId, Array.Empty<byte>(), Microsoft.Win32.RegistryValueKind.None);
        }
    }

    private static void RegisterSubtitleFormats()
    {
        foreach (var ext in SubtitleFormats)
        {
            using var progid = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(
                $@"Software\Classes\{AppId}.sub\shell\open\command");
            progid.SetValue("", $"\"{GetExecutablePath()}\" \"%1\"");

            using var extKey = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(
                $@"Software\Classes\{ext}\OpenWithProgids");
            extKey.SetValue(AppId + ".sub", Array.Empty<byte>(), Microsoft.Win32.RegistryValueKind.None);
        }
    }

    private static void RemoveAssociation(string ext)
    {
        try
        {
            using var extKey = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                $@"Software\Classes\{ext}\OpenWithProgids", writable: true);
            extKey?.DeleteValue(AppId, throwOnMissingValue: false);
            extKey?.DeleteValue(AppId + ".sub", throwOnMissingValue: false);
        }
        catch { /* Not registered */ }
    }

    private static string GetExecutablePath()
    {
        using var process = System.Diagnostics.Process.GetCurrentProcess();
        return process.MainModule?.FileName ?? "Cine.exe";
    }

    [DllImport("shell32.dll", CharSet = CharSet.Auto)]
    private static extern void SHChangeNotify(uint wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);

    private const uint SHCNE_ASSOCCHANGED = 0x08000000;
    private const uint SHCNF_IDLIST = 0x0000;
}
