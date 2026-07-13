using System.Runtime.InteropServices;

namespace Cine.Media.Implementations;

/// <summary>Matches reference LibMpv-OpenGL MarshalHelper.</summary>
public class MarshalHelper : IDisposable
{
    bool _disposed;
    struct AllocBlock { public bool IsHGlobal; public IntPtr IntPtr; }
    List<AllocBlock> _toBeFree = new();

    public IntPtr StringToHGlobalAnsi(string value)
    {
        var ptr = Marshal.StringToHGlobalAnsi(value);
        _toBeFree.Add(new AllocBlock { IsHGlobal = true, IntPtr = ptr });
        return ptr;
    }
    public IntPtr AllocHGlobalValue(int value)
    {
        var ptr = AllocHGlobal(sizeof(int));
        Marshal.WriteInt32(ptr, value);
        return ptr;
    }
    public IntPtr AllocHGlobal<T>(T instance) where T : struct
    {
        var ptr = AllocHGlobal(Marshal.SizeOf<T>());
        Marshal.StructureToPtr(instance, ptr, false);
        return ptr;
    }
    public IntPtr AllocHGlobal(int cb)
    {
        var ptr = Marshal.AllocHGlobal(cb);
        _toBeFree.Add(new AllocBlock { IsHGlobal = true, IntPtr = ptr });
        return ptr;
    }
    public void Dispose()
    {
        if (_disposed) return;
        foreach (var item in _toBeFree)
        {
            if (item.IsHGlobal) Marshal.FreeHGlobal(item.IntPtr);
            else Marshal.FreeCoTaskMem(item.IntPtr);
        }
        _disposed = true;
    }
}
