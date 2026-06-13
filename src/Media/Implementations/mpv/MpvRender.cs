using System.Runtime.InteropServices;

namespace Cine.Media.Implementations;

/// <summary>
/// P/Invoke bindings for the mpv render API (libmpv OpenGL rendering).
/// Matches the reference LibMpv-OpenGL implementation exactly.
/// </summary>
public static unsafe partial class MpvRenderNative
{
    public const string MpvDll = "mpv-2";
    public const string MPV_RENDER_API_TYPE_OPENGL = "opengl";

    // ── Render parameter type constants (MpvRenderParamType enum) ──
    public const int MPV_RENDER_PARAM_INVALID = 0;
    public const int MPV_RENDER_PARAM_API_TYPE = 1;
    public const int MPV_RENDER_PARAM_OPENGL_INIT_PARAMS = 2;
    public const int MPV_RENDER_PARAM_OPENGL_FBO = 3;
    public const int MPV_RENDER_PARAM_FLIP_Y = 4;
    public const int MPV_RENDER_PARAM_DEPTH = 5;
    public const int MPV_RENDER_PARAM_ICC_PROFILE = 6;
    public const int MPV_RENDER_PARAM_AMBIENT_LIGHT = 7;
    public const int MPV_RENDER_PARAM_X11_DISPLAY = 8;
    public const int MPV_RENDER_PARAM_WL_DISPLAY = 9;
    public const int MPV_RENDER_PARAM_ADVANCED_CONTROL = 10;
    public const int MPV_RENDER_PARAM_NEXT_FRAME_INFO = 11;
    public const int MPV_RENDER_PARAM_BLOCK_FOR_TARGET_TIME = 12;
    public const int MPV_RENDER_PARAM_SKIP_RENDERING = 13;
    public const int MPV_RENDER_PARAM_DRM_DISPLAY = 14;
    public const int MPV_RENDER_PARAM_DRM_DRAW_SURFACE_SIZE = 15;
    public const int MPV_RENDER_PARAM_DRM_DISPLAY_V2 = 16;
    public const int MPV_RENDER_PARAM_SW_SIZE = 17;
    public const int MPV_RENDER_PARAM_SW_FORMAT = 18;
    public const int MPV_RENDER_PARAM_SW_STRIDE = 19;
    public const int MPV_RENDER_PARAM_SW_POINTER = 20;

    public const ulong MPV_RENDER_UPDATE_FRAME = 1;

    // ── Structs ──

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct MpvOpenglInitParams
    {
        public MpvGetProcAddressFunc GetProcAddress;
        public void* GetProcAddressCtx;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MpvOpenglFbo
    {
        public int Fbo;
        public int W;
        public int H;
        public int InternalFormat;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MpvRenderParam
    {
        public int Type;
        public void* Data;
    }

    // ── Managed delegates (stored as fields in MpvPlayer) ──
    // P/Invoke marshals these to native function pointers automatically.
    // Matches reference: void* ctx, string name, returns void*

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

    // Wrapper struct for update callback delegate (matches reference LibMpv-OpenGL pattern)
    public struct MpvRenderUpdateFnFunc
    {
        public IntPtr Pointer;
        public static implicit operator MpvRenderUpdateFnFunc(MpvRenderUpdateFnDelegate? func) =>
            new MpvRenderUpdateFnFunc { Pointer = func == null ? IntPtr.Zero : Marshal.GetFunctionPointerForDelegate(func) };
    }

    // ── P/Invoke (typed pointer params) ──

    [DllImport(MpvDll, CallingConvention = CallingConvention.Cdecl)]
    public static extern int mpv_render_context_create(
        out IntPtr renderContext, IntPtr mpvHandle, IntPtr parameters);

    [DllImport(MpvDll, CallingConvention = CallingConvention.Cdecl)]
    public static extern int mpv_render_context_render(
        IntPtr renderContext, IntPtr parameters);

    [DllImport(MpvDll, CallingConvention = CallingConvention.Cdecl)]
    public static extern void mpv_render_context_report_swap(
        IntPtr renderContext);

    [DllImport(MpvDll, CallingConvention = CallingConvention.Cdecl)]
    public unsafe static extern void mpv_render_context_set_update_callback(
        IntPtr renderContext, MpvRenderUpdateFnDelegate callback, void* callbackCtx);

    [DllImport(MpvDll, CallingConvention = CallingConvention.Cdecl)]
    public static extern ulong mpv_render_context_update(
        IntPtr renderContext);

    [DllImport(MpvDll, CallingConvention = CallingConvention.Cdecl)]
    public static extern void mpv_render_context_free(
        IntPtr renderContext);
}
