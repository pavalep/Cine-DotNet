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
        if (!string.Equals(libraryName, "libmpv-2.dll", StringComparison.OrdinalIgnoreCase))
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

        return false;
    }
}