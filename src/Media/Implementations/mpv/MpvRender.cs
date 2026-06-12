using System;
using System.Runtime.InteropServices;

namespace Cine.Media.Implementations;

/// <summary>
/// mpv render API P/Invoke bindings (render_gl.h + render.h).
/// Modelled after the LibMpv-OpenGL reference implementation — uses unsafe void*
/// so struct layout matches the C ABI exactly (no padding ambiguity).
/// </summary>
public static unsafe class MpvRenderNative
{
    private const string MpvDll = "libmpv-2.dll";

    // ── Render context lifecycle ──

    [DllImport(MpvDll, CallingConvention = CallingConvention.Cdecl)]
    public static extern int mpv_render_context_create(
        out IntPtr renderContext,
        IntPtr mpvHandle,
        MpvRenderParam* parameters);

    [DllImport(MpvDll, CallingConvention = CallingConvention.Cdecl)]
    public static extern void mpv_render_context_free(IntPtr renderContext);

    // ── Frame access ──

    [DllImport(MpvDll, CallingConvention = CallingConvention.Cdecl)]
    public static extern ulong mpv_render_context_update(IntPtr renderContext);

    [DllImport(MpvDll, CallingConvention = CallingConvention.Cdecl)]
    public static extern int mpv_render_context_render(
        IntPtr renderContext,
        MpvRenderParam* parameters);

    [DllImport(MpvDll, CallingConvention = CallingConvention.Cdecl)]
    public static extern void mpv_render_context_report_swap(IntPtr renderContext);

    // ── Update callback ──

    [DllImport(MpvDll, CallingConvention = CallingConvention.Cdecl)]
    public static extern void mpv_render_context_set_update_callback(
        IntPtr renderContext,
        MpvRenderUpdateFn callback,
        IntPtr cb_ctx);

    // ─────────────────────────────────────────────────────────────────────
    // Structs — all use void* so layout matches the C ABI on x64 exactly.
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// { int type; void* data; } — 16 bytes on x64, matching native layout.
    /// </summary>
    public struct MpvRenderParam
    {
        public int Type;
        public void* Data;
    }

    /// <summary>
    /// mpv_opengl_init_params.
    /// GetProcAddress is stored as a struct-wrapped IntPtr (see MpvGetProcAddressFunc)
    /// — the same pattern used by the LibMpv-OpenGL reference library so the
    /// function pointer is embedded correctly inside the struct.
    /// </summary>
    public struct MpvOpenglInitParams
    {
        public MpvGetProcAddressFunc GetProcAddress;
        public void* GetProcAddressCtx;
    }

    /// <summary>
    /// Wraps a Cdecl function pointer for get_proc_address so it can be
    /// embedded inside MpvOpenglInitParams without any marshaling issues.
    /// Implicit conversion from delegate calls Marshal.GetFunctionPointerForDelegate.
    /// </summary>
    public struct MpvGetProcAddressFunc
    {
        public IntPtr Pointer;
        public static implicit operator MpvGetProcAddressFunc(MpvGetProcAddressDelegate? fn)
            => new() { Pointer = fn is null ? IntPtr.Zero : Marshal.GetFunctionPointerForDelegate(fn) };
    }

    /// <summary>mpv_opengl_fbo — 4 ints = 16 bytes, no padding needed.</summary>
    public struct MpvOpenglFbo
    {
        public int Fbo;
        public int W;
        public int H;
        public int InternalFormat;
    }

    // ── Delegates ──

    /// <summary>get_proc_address: void*(*)(void* ctx, const char* name)</summary>
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public unsafe delegate void* MpvGetProcAddressDelegate(
        void* ctx,
        [MarshalAs(UnmanagedType.LPStr)] string name);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void MpvRenderUpdateFn(IntPtr cb_ctx);

    // ── Param type constants ──

    public const int MPV_RENDER_PARAM_INVALID          = 0;
    public const int MPV_RENDER_PARAM_API_TYPE         = 1;
    public const int MPV_RENDER_PARAM_OPENGL_INIT_PARAMS = 2;
    public const int MPV_RENDER_PARAM_FLIP_Y           = 4;
    public const int MPV_RENDER_PARAM_OPENGL_FBO       = 5;
    public const int MPV_RENDER_PARAM_ADVANCED_CONTROL = 10;

    public const ulong MPV_RENDER_UPDATE_FRAME = 1;

    public const string MPV_RENDER_API_TYPE_OPENGL = "opengl";
}
