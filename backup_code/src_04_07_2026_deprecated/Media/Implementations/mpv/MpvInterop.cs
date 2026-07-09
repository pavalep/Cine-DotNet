using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Cine.Media.Implementations;

public static class MpvInterop
{
    private static readonly string[] CandidateNames =
    [
        "libmpv-2.dll",
        "mpv-2.dll",
        "libmpv.dll"
    ];

    private static readonly Lazy<bool> Availability = new(() => TryProbe());

    static MpvInterop()
    {
        NativeLibrary.SetDllImportResolver(typeof(MpvInterop).Assembly, ResolveMpvLibrary);
    }

    public static bool IsAvailable => Availability.Value;

    private static IntPtr ResolveMpvLibrary(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        // Resolve both "mpv-2" (MpvRender.cs) and "libmpv-2.dll" (MpvNative.cs)
        // P/Invoke passes the exact string from [DllImport()] attribute.
        if (!string.Equals(libraryName, "libmpv-2.dll", StringComparison.OrdinalIgnoreCase) && 
            !string.Equals(libraryName, "mpv-2", StringComparison.OrdinalIgnoreCase))
            return IntPtr.Zero;

        foreach (var name in CandidateNames)
        {
            if (NativeLibrary.TryLoad(name, assembly, searchPath, out var handle))
                return handle;
        }

        foreach (var name in CandidateNames)
        {
            var local = Path.Combine(AppContext.BaseDirectory, name);
            if (File.Exists(local) && NativeLibrary.TryLoad(local, out var handle))
                return handle;
        }

        // Also search %LOCALAPPDATA%\Cine\runtime\ for downloaded mpv DLLs
        var runtimeDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Cine", "runtime");
        foreach (var name in CandidateNames)
        {
            var runtime = Path.Combine(runtimeDir, name);
            if (File.Exists(runtime) && NativeLibrary.TryLoad(runtime, out var handle))
                return handle;
        }

        return IntPtr.Zero;
    }

    private static bool TryProbe()
    {
        foreach (var name in CandidateNames)
        {
            if (NativeLibrary.TryLoad(name, out var handle))
            {
                NativeLibrary.Free(handle);
                return true;
            }
        }

        foreach (var name in CandidateNames)
        {
            var local = Path.Combine(AppContext.BaseDirectory, name);
            if (File.Exists(local) && NativeLibrary.TryLoad(local, out var handle))
            {
                NativeLibrary.Free(handle);
                return true;
            }
        }

        // Check runtime download directory
        var runtimeDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Cine", "runtime");
        foreach (var name in CandidateNames)
        {
            var runtime = Path.Combine(runtimeDir, name);
            if (File.Exists(runtime) && NativeLibrary.TryLoad(runtime, out var handle))
            {
                NativeLibrary.Free(handle);
                return true;
            }
        }

        return false;
    }
}