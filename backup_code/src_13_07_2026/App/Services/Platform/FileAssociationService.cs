using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Cine.Core;

namespace Cine.Avalonia.Services;

/// <summary>
/// Registers Cine as the default player for supported video/audio formats on Windows.
/// Writes to HKEY_CURRENT_USER\Software\Classes (no admin required).
/// Per-format try-catch ensures one failed format doesn't block others.
/// </summary>
public sealed class FileAssociationService
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
    private const string SubAppId = "CineMediaPlayer.sub";

    private readonly IRegistryService _registry;
    private readonly string _executablePath;

    public FileAssociationService(IRegistryService registry, string? executablePath = null)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _executablePath = executablePath ?? GetExecutablePath();
    }

    public string ExecutablePath => _executablePath;

    /// <summary>Register all supported file types.</summary>
    public void Register()
    {
        int videoOk = 0, audioOk = 0, subOk = 0;
        int videoFail = 0, audioFail = 0, subFail = 0;

        videoFail = RegisterFormatList(VideoFormats, "video", ref videoOk);
        audioFail = RegisterFormatList(AudioFormats, "audio", ref audioOk);
        subFail = RegisterSubtitleFormats(ref subOk);

        NotifyShell();

        Log.ForContext("FileAssociation").Info(
            "File associations registered. Video: {0} ok/{1} fail, Audio: {2} ok/{3} fail, Subtitle: {4} ok/{5} fail",
            videoOk, videoFail, audioOk, audioFail, subOk, subFail);
    }

    /// <summary>Remove all registered file types.</summary>
    public void Unregister()
    {
        int removed = 0, failed = 0;
        foreach (var ext in VideoFormats)
            if (TryUnregister(ext)) removed++; else failed++;
        foreach (var ext in AudioFormats)
            if (TryUnregister(ext)) removed++; else failed++;
        foreach (var ext in SubtitleFormats)
            if (TryUnregister(ext)) removed++; else failed++;

        NotifyShell();
        Log.ForContext("FileAssociation").Info("File associations unregistered: {0} removed, {1} failed", removed, failed);
    }

    /// <summary>Check if Cine is the default for a given extension.</summary>
    public bool IsRegistered(string extension)
    {
        try
        {
            var keyPath = $@"{extension}\OpenWithProgids";
            return _registry.GetValue(keyPath, AppId) != null;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Optionally run registration on a background thread.
    /// Registry writes can block the UI thread briefly; use this at startup.
    /// </summary>
    public void RegisterOnStartup()
    {
        if (!IsExecutableValid(_executablePath))
        {
            Log.ForContext("FileAssociation").Warning(
                "Skipping file association registration — executable path does not end with .exe: {0}", _executablePath);
            return;
        }

        System.Threading.ThreadPool.QueueUserWorkItem(_ => Register());
    }

    // ── Private helpers ──

    private int RegisterFormatList(string[] formats, string type, ref int okCount)
    {
        int failCount = 0;
        foreach (var ext in formats)
        {
            try
            {
                var commandPath = $@"{AppId}\shell\open\command";
                _registry.SetValue(commandPath, "", $"\"{_executablePath}\" \"%1\"");

                _registry.SetValue(AppId, "", $"{AppName} {type} file");

                var progIdPath = $@"{ext}\OpenWithProgids";
                _registry.SetBinaryValue(progIdPath, AppId, Array.Empty<byte>());

                okCount++;
            }
            catch (Exception ex)
            {
                failCount++;
                Log.ForContext("FileAssociation").Error(ex, "Failed to register format {0} ({1})", ext, type);
            }
        }
        return failCount;
    }

    private int RegisterSubtitleFormats(ref int okCount)
    {
        int failCount = 0;
        foreach (var ext in SubtitleFormats)
        {
            try
            {
                var commandPath = $@"{SubAppId}\shell\open\command";
                _registry.SetValue(commandPath, "", $"\"{_executablePath}\" \"%1\"");

                var progIdPath = $@"{ext}\OpenWithProgids";
                _registry.SetBinaryValue(progIdPath, SubAppId, Array.Empty<byte>());

                okCount++;
            }
            catch (Exception ex)
            {
                failCount++;
                Log.ForContext("FileAssociation").Error(ex, "Failed to register subtitle format {0}", ext);
            }
        }
        return failCount;
    }

    private bool TryUnregister(string ext)
    {
        try
        {
            _registry.DeleteValue($@"{ext}\OpenWithProgids", AppId);
            _registry.DeleteValue($@"{ext}\OpenWithProgids", SubAppId);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private void NotifyShell()
    {
        try
        {
            SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST, IntPtr.Zero, IntPtr.Zero);
        }
        catch
        {
            // Non-critical — shell notification is best effort
        }
    }

    private static bool IsExecutableValid(string path)
    {
        return !string.IsNullOrEmpty(path) &&
               path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase);
    }

    private static string GetExecutablePath()
    {
        using var process = Process.GetCurrentProcess();
        return process.MainModule?.FileName ?? "Cine.exe";
    }

    [DllImport("shell32.dll", CharSet = CharSet.Auto)]
    private static extern void SHChangeNotify(uint wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);

    private const uint SHCNE_ASSOCCHANGED = 0x08000000;
    private const uint SHCNF_IDLIST = 0x0000;
}
