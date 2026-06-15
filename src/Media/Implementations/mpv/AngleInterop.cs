using System;
using System.Runtime.InteropServices;

namespace Cine.Media.Implementations;

/// <summary>
/// P/Invoke bindings for ANGLE EGL (libEGL.dll).
/// Used to create an OpenGL ES context that mpv can bind to.
/// </summary>
public static class AngleInterop
{
    private const string LibEgl = "libEGL.dll";

    // EGL constants
    public const int EGL_SUCCESS = 0;
    public const IntPtr EGL_NO_DISPLAY = 0;
    public const IntPtr EGL_NO_SURFACE = 0;
    public const IntPtr EGL_NO_CONTEXT = 0;
    public const int EGL_CONTEXT_CLIENT_VERSION = 0x3098;
    public const int EGL_OPENGL_ES2_BIT = 0x0004;
    public const int EGL_SURFACE_TYPE = 0x3033;
    public const int EGL_RENDERABLE_TYPE = 0x3040;
    public const int EGL_NONE = 0x3038;
    public const int EGL_PBUFFER_BIT = 0x0001;
    public const int EGL_WIDTH = 0x3057;
    public const int EGL_HEIGHT = 0x3058;
    public const int EGL_RED_SIZE = 0x3024;
    public const int EGL_GREEN_SIZE = 0x3023;
    public const int EGL_BLUE_SIZE = 0x3022;
    public const int EGL_ALPHA_SIZE = 0x3021;
    public const int EGL_DEPTH_SIZE = 0x3025;
    public const int EGL_STENCIL_SIZE = 0x3026;
    public const int EGL_SAMPLES = 0x3041;
    public const int EGL_CONFIG_ID = 0x3028;

    // EGL API constants
    public const int EGL_OPENGL_ES_API = 0x30A0;
    public const int EGL_OPENGL_ES3_BIT = 0x0040;

    private static readonly Lazy<bool> _isAvailable = new(TryProbe);
    /// <summary>Whether ANGLE EGL libraries (libEGL.dll) are loadable on this system.</summary>
    public static bool IsAvailable => _isAvailable.Value;

    private static bool TryProbe()
    {
        try
        {
            // Probe by getting the default display — if this fails, ANGLE isn't usable
            var display = eglGetDisplay(IntPtr.Zero);
            if (display == EGL_NO_DISPLAY)
                return false;

            if (eglInitialize(display, out _, out _) == 0)
                return false;

            eglTerminate(display);
            return true;
        }
        catch (DllNotFoundException)
        {
            return false;
        }
        catch
        {
            return false;
        }
    }

    [DllImport(LibEgl, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr eglGetDisplay(IntPtr display_id);

    [DllImport(LibEgl, CallingConvention = CallingConvention.Cdecl)]
    public static extern int eglInitialize(IntPtr dpy, out int major, out int minor);

    [DllImport(LibEgl, CallingConvention = CallingConvention.Cdecl)]
    public static extern int eglBindAPI(int api);

    [DllImport(LibEgl, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern IntPtr eglGetProcAddress(string procname);

    [DllImport(LibEgl, CallingConvention = CallingConvention.Cdecl)]
    public static extern int eglChooseConfig(
        IntPtr dpy,
        [In] int[]? attrib_list,
        [Out] IntPtr[]? configs,
        int config_size,
        out int num_config);

    [DllImport(LibEgl, CallingConvention = CallingConvention.Cdecl)]
    public static extern int eglGetConfigs(
        IntPtr dpy,
        [Out] IntPtr[]? configs,
        int config_size,
        out int num_config);

    [DllImport(LibEgl, CallingConvention = CallingConvention.Cdecl)]
    public static extern int eglCreateWindowSurface(
        IntPtr dpy,
        IntPtr config,
        IntPtr win,
        int[] attrib_list);

    [DllImport(LibEgl, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr eglCreatePbufferSurface(
        IntPtr dpy,
        IntPtr config,
        [In] int[]? attrib_list);

    [DllImport(LibEgl, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr eglCreateContext(
        IntPtr dpy,
        IntPtr config,
        IntPtr share_context,
        [In] int[]? attrib_list);

    [DllImport(LibEgl, CallingConvention = CallingConvention.Cdecl)]
    public static extern int eglMakeCurrent(
        IntPtr dpy,
        IntPtr draw,
        IntPtr read,
        IntPtr ctx);

    [DllImport(LibEgl, CallingConvention = CallingConvention.Cdecl)]
    public static extern int eglDestroyContext(IntPtr dpy, IntPtr ctx);

    [DllImport(LibEgl, CallingConvention = CallingConvention.Cdecl)]
    public static extern int eglDestroySurface(IntPtr dpy, IntPtr surface);

    [DllImport(LibEgl, CallingConvention = CallingConvention.Cdecl)]
    public static extern int eglTerminate(IntPtr dpy);

    [DllImport(LibEgl, CallingConvention = CallingConvention.Cdecl)]
    public static extern int eglGetError();

    [DllImport(LibEgl, CallingConvention = CallingConvention.Cdecl)]
    public static extern int eglSwapBuffers(IntPtr dpy, IntPtr surface);

    [DllImport(LibEgl, CallingConvention = CallingConvention.Cdecl)]
    public static extern int eglQuerySurface(IntPtr dpy, IntPtr surface, int attribute, out int value);
}