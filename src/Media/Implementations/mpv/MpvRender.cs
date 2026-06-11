using System;
using System.Runtime.InteropServices;

namespace Cine.Media.Implementations;

/// <summary>
/// mpv render API P/Invoke bindings (render_gl.h + render.h).
/// Requires libmpv built with --enable-libmpv-render.
/// Uses OpenGL render backend (supported in all standard builds).
/// </summary>
public static class MpvRenderNative
{
    private const string MpvDll = "libmpv-2.dll";

    // ── Render context lifecycle ──

    [DllImport(MpvDll, CallingConvention = CallingConvention.Cdecl)]
    public static extern int mpv_render_context_create(
        out IntPtr renderContext,
        IntPtr mpvHandle,
        [In] mpv_render_param[]? parameters);

    [DllImport(MpvDll, CallingConvention = CallingConvention.Cdecl)]
    public static extern void mpv_render_context_free(IntPtr renderContext);

    // ── Framebuffer access ──

    [DllImport(MpvDll, CallingConvention = CallingConvention.Cdecl)]
    public static extern ulong mpv_render_context_update(IntPtr renderContext);

    [DllImport(MpvDll, CallingConvention = CallingConvention.Cdecl)]
    public static extern int mpv_render_context_render(
        IntPtr renderContext,
        [In] mpv_render_param[]? parameters);

    [DllImport(MpvDll, CallingConvention = CallingConvention.Cdecl)]
    public static extern void mpv_render_context_report_swap(IntPtr renderContext);

    // ── Update callback ──

    [DllImport(MpvDll, CallingConvention = CallingConvention.Cdecl)]
    public static extern void mpv_render_context_set_update_callback(
        IntPtr renderContext,
        mpv_render_update_fn callback,
        IntPtr cb_ctx);

    // ── Structs ──

    [StructLayout(LayoutKind.Sequential)]
    public struct mpv_render_param
    {
        public int type;
        public IntPtr data;
    }

    /// <summary>
    /// OpenGL init params. Based on mpv/render_gl.h: mpv_opengl_init_params.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct mpv_opengl_init_params
    {
        public IntPtr get_proc_address; // void *(*)(void *ctx, const char *name)
        public IntPtr get_proc_address_ctx;
    }

    /// <summary>
    /// OpenGL FBO params. Based on mpv/render_gl.h: mpv_opengl_fbo.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct mpv_opengl_fbo
    {
        public int fbo;
        public int w;
        public int h;
        public int internal_format;
    }

    // ── Delegates ──

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate IntPtr mpv_get_proc_address_fn(IntPtr ctx, IntPtr name);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void mpv_render_update_fn(IntPtr cb_ctx);

    // ── Constants ──

    public const int MPV_RENDER_PARAM_INVALID = 0;
    public const int MPV_RENDER_PARAM_API_TYPE = 1;
    public const int MPV_RENDER_PARAM_OPENGL_INIT_PARAMS = 2;
    public const int MPV_RENDER_PARAM_OPENGL_FBO = 5;
    public const int MPV_RENDER_PARAM_FLIP_Y = 4;

    public const ulong MPV_RENDER_UPDATE_FRAME = 1;

    public const string MPV_RENDER_API_TYPE_OPENGL = "opengl";
}
