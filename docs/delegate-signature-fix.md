# MPV Render API Delegate Signature Fix

## Date: 2026-06-13

## Problem
The app was crashing after loading videos due to incorrect delegate signatures for the mpv render API callbacks. The `get_proc_address` callback and update callback had mismatched signatures that didn't match the reference LibMpv-OpenGL implementation.

## Root Cause
1. **Wrong delegate signature**: Using `IntPtr ctx, IntPtr name` instead of `void* ctx, string name` for `MpvGetProcAddressDelegate`
2. **Missing wrapper structs**: The reference implementation wraps delegates in special structs with implicit conversion operators
3. **Manual marshaling**: Manually converting string pointers with `Marshal.PtrToStringAnsi` when P/Invoke should handle it automatically with proper marshaling attributes

## Changes Made

### 1. MpvRender.cs - Updated Delegate Signatures

**Before:**
```csharp
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
public delegate IntPtr MpvGetProcAddressDelegate(IntPtr ctx, IntPtr name);

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
public unsafe delegate void MpvRenderUpdateFnDelegate(void* ctx);
```

**After:**
```csharp
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
public unsafe delegate void* MpvGetProcAddressDelegate(void* ctx,
#if NET5_0_OR_GREATER
    [MarshalAs(UnmanagedType.LPUTF8Str)]
#else
    [MarshalAs(UnmanagedType.LPStr)]
#endif
    string name);

// Wrapper struct for GetProcAddress delegate (matches reference LibMpv-OpenGL pattern)
public struct MpvGetProcAddressFunc
{
    public IntPtr Pointer;
    public static implicit operator MpvGetProcAddressFunc(MpvGetProcAddressDelegate? func) =>
        new MpvGetProcAddressFunc { Pointer = func == null ? IntPtr.Zero : Marshal.GetFunctionPointerForDelegate(func) };
}

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
public unsafe delegate void MpvRenderUpdateFnDelegate(void* ctx);

// Wrapper struct for update callback delegate
public struct MpvRenderUpdateFnFunc
{
    public IntPtr Pointer;
    public static implicit operator MpvRenderUpdateFnFunc(MpvRenderUpdateFnDelegate? func) =>
        new MpvRenderUpdateFnFunc { Pointer = func == null ? IntPtr.Zero : Marshal.GetFunctionPointerForDelegate(func) };
}
```

### 2. MpvRender.cs - Updated MpvOpenglInitParams Struct

**Before:**
```csharp
[StructLayout(LayoutKind.Sequential)]
public struct MpvOpenglInitParams
{
    public IntPtr GetProcAddress;
    public IntPtr GetProcAddressCtx;
}
```

**After:**
```csharp
[StructLayout(LayoutKind.Sequential)]
public unsafe struct MpvOpenglInitParams
{
    public MpvGetProcAddressFunc GetProcAddress;
    public void* GetProcAddressCtx;
}
```

### 3. MpvPlayer.cs - Updated Callback Implementation

**Before:**
```csharp
private IntPtr GetProcAddressCallback(IntPtr ctx, IntPtr name)
{
    if (name == IntPtr.Zero) return IntPtr.Zero;
    var str = Marshal.PtrToStringAnsi(name);
    if (string.IsNullOrEmpty(str)) return IntPtr.Zero;
    return _getProcAddress?.Invoke(str) ?? IntPtr.Zero;
}
```

**After:**
```csharp
private unsafe void* GetProcAddressCallback(void* ctx, string name)
{
    if (string.IsNullOrEmpty(name)) return null;
    var ptr = _getProcAddress?.Invoke(name) ?? IntPtr.Zero;
    DebugLog($"GetProcAddress: {name} => 0x{ptr:X}");
    return (void*)ptr;
}
```

### 4. MpvPlayer.cs - Updated InitParams Assignment

**Before:**
```csharp
Data = (void*)mh.AllocHGlobal(new MpvRenderNative.MpvOpenglInitParams
{
    GetProcAddress = Marshal.GetFunctionPointerForDelegate(_getProcCb),
    GetProcAddressCtx = IntPtr.Zero
})
```

**After:**
```csharp
Data = (void*)mh.AllocHGlobal(new MpvRenderNative.MpvOpenglInitParams
{
    GetProcAddress = _getProcCb,  // Implicit conversion via wrapper struct
    GetProcAddressCtx = null
})
```

## Why This Fixes the Issue

1. **Correct ABI**: The `void* ctx, string name` signature matches what libmpv expects. P/Invoke automatically marshals the `const char*` name parameter to a C# string.

2. **Automatic String Marshaling**: Using `[MarshalAs(UnmanagedType.LPUTF8Str)]` (or `LPStr` for older .NET) tells P/Invoke to automatically handle the string conversion, eliminating manual pointer manipulation.

3. **Type Safety**: The wrapper structs provide type safety and ensure the function pointer is correctly converted to the format mpv expects.

4. **Matches Reference**: This implementation exactly matches the proven LibMpv-OpenGL reference implementation.

## Testing

Build succeeds with no errors:
```
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

## Next Steps

1. Run the app and test video playback
2. Check logs at `C:\Users\paval\AppData\Local\cine\MpvPlayer.log` for:
   - Successful `mpv_render_context_create` (should return 0)
   - `GetProcAddress` calls showing function names and resolved pointers
   - `mpv_render_context_render` errors (should be 0, not -4)
3. If still getting -4 error, investigate which OpenGL function is failing to resolve

## References

- Reference implementation: `x:\Development\Cine_CSharp_DotNet\reference\LibMpv-OpenGL-main\`
- LibMpv render API documentation: https://mpv.io/manual/master/#embedding-into-your-own-application-opengl-rendering
