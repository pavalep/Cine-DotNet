using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Cine.Avalonia.Controls;

/// <summary>
/// Hosts a native Win32 child window inside an Avalonia control.
/// Used to embed D3D11Renderer's swap chain HWND for GPU-accelerated video playback.
/// </summary>
public class D3D11VideoHost : global::Avalonia.Controls.Control
{
    private IntPtr _childHwnd = IntPtr.Zero;
    private IntPtr _parentHwnd;
    #region debug-point vt-paint-counter
    private static int _wmPaintCount;
    #endregion
    #region debug-point videohost-log
    private static readonly string DebugLogFile = CreateLogFilePath();

    private static string CreateLogFilePath()
    {
        try
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Cine");
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, "cine_startup.log");
        }
        catch
        {
            return Path.Combine(Path.GetTempPath(), "cine_startup.log");
        }
    }

    private static void DebugLog(string message)
    {
        try
        {
            File.AppendAllText(DebugLogFile, $"[{DateTime.Now:HH:mm:ss.fff}] [VideoHost] {message}{Environment.NewLine}");
        }
        catch
        {
        }
    }
    #endregion

    /// <summary>Raised when the child HWND is successfully created.</summary>
    public event EventHandler? ChildWindowCreated;

    public static readonly StyledProperty<bool> IsFullscreenProperty =
        AvaloniaProperty.Register<D3D11VideoHost, bool>(nameof(IsFullscreen), false);

    public static readonly StyledProperty<bool> IsVideoSurfaceVisibleProperty =
        AvaloniaProperty.Register<D3D11VideoHost, bool>(nameof(IsVideoSurfaceVisible), false);

    public bool IsFullscreen
    {
        get => GetValue(IsFullscreenProperty);
        set => SetValue(IsFullscreenProperty, value);
    }

    public bool IsVideoSurfaceVisible
    {
        get => GetValue(IsVideoSurfaceVisibleProperty);
        set => SetValue(IsVideoSurfaceVisibleProperty, value);
    }

    /// <summary>The HWND of the native child window where D3D11 renders.</summary>
    public IntPtr VideoHwnd => _childHwnd;

    /// <summary>Set this to the parent window's HWND to create the child window.</summary>
    public IntPtr ParentHwnd
    {
        get => _parentHwnd;
        set { _parentHwnd = value; TryCreateNow(); }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (_childHwnd == IntPtr.Zero)
            return;

        if (change.Property == IsVisibleProperty || change.Property == IsVideoSurfaceVisibleProperty)
        {
            #region debug-point VT-G
            global::Cine.Avalonia.App.DebugReport("VT", "VideoHost.VisibilityChanged", "Visibility property changed.", new
            {
                changed = change.Property.Name,
                isVisible = IsVisible,
                surfaceVisible = IsVideoSurfaceVisible,
                child = _childHwnd.ToInt64()
            }, runId: "pre-fix");
            #endregion
            ShowWindow(_childHwnd, IsVisible && IsVideoSurfaceVisible ? ShowWindowCommand.Show : ShowWindowCommand.Hide);
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        #region debug-point videohost-attached
        DebugLog("OnAttachedToVisualTree");
        #endregion
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        #region debug-point videohost-detached
        DebugLog("OnDetachedFromVisualTree");
        #endregion
        DestroyChildWindow();
    }

    protected override global::Avalonia.Size ArrangeOverride(global::Avalonia.Size finalSize)
    {
        var result = base.ArrangeOverride(finalSize);
        TryCreateNow();
        if (_childHwnd != IntPtr.Zero && _parentHwnd != IntPtr.Zero)
        {
            double scaling = GetScaling();
            int w = (int)(finalSize.Width * scaling);
            int h = (int)(finalSize.Height * scaling);
            #region debug-point VT-E
            global::Cine.Avalonia.App.DebugReport("VT", "VideoHost.ArrangeOverride", "Resizing child hwnd.", new
            {
                parent = _parentHwnd.ToInt64(),
                child = _childHwnd.ToInt64(),
                finalSize = finalSize.ToString(),
                scaled = $"{w}x{h}",
                isVisible = IsVisible,
                surfaceVisible = IsVideoSurfaceVisible
            }, runId: "pre-fix");
            #endregion
            SetWindowPos(_childHwnd, IntPtr.Zero,
                0, 0, w, h,
                SetWindowPosFlags.SWP_NOZORDER | SetWindowPosFlags.SWP_NOACTIVATE);
        }
        return result;
    }

    private void TryCreateNow()
    {
        #region debug-point videohost-try-create
        DebugLog($"TryCreateNow parent={_parentHwnd} child={_childHwnd} bounds={Bounds.Width}x{Bounds.Height}");
        global::Cine.Avalonia.App.DebugReport("VT", "VideoHost.TryCreateNow", "TryCreateNow.", new
        {
            parent = _parentHwnd.ToInt64(),
            child = _childHwnd.ToInt64(),
            bounds = $"{Bounds.Width}x{Bounds.Height}",
            isVisible = IsVisible,
            surfaceVisible = IsVideoSurfaceVisible
        }, runId: "pre-fix");
        #endregion
        if (_parentHwnd != IntPtr.Zero && _childHwnd == IntPtr.Zero && Bounds.Width > 0 && Bounds.Height > 0)
        {
            CreateChildWindow();
            if (_childHwnd != IntPtr.Zero)
            {
                #region debug-point videohost-created
                DebugLog($"Child hwnd created={_childHwnd}");
                #endregion
                ChildWindowCreated?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    private void CreateChildWindow()
    {
        #region debug-point videohost-create-start
        DebugLog($"CreateChildWindow start parent={_parentHwnd}");
        global::Cine.Avalonia.App.DebugReport("VT", "VideoHost.CreateChildWindow.Start", "CreateChildWindow start.", new
        {
            parent = _parentHwnd.ToInt64(),
            bounds = $"{Bounds.Width}x{Bounds.Height}",
            isVisible = IsVisible,
            surfaceVisible = IsVideoSurfaceVisible
        }, runId: "pre-fix");
        #endregion
        if (_parentHwnd == IntPtr.Zero) return;

        var windowClass = $"CineD3D11Video_{Guid.NewGuid():N}";

        var wc = new WndClassEx
        {
            cbSize = (uint)Marshal.SizeOf<WndClassEx>(),
            style = 0,
            lpfnWndProc = WndProc,
            cbClsExtra = 0,
            cbWndExtra = 0,
            hInstance = GetModuleHandle(null),
            hCursor = LoadCursor(IntPtr.Zero, (int)IDC_ARROW),
            hbrBackground = GetStockObject(BLACK_BRUSH),
            lpszClassName = windowClass
        };

        RegisterClassEx(ref wc);

        double scaling = GetScaling();
        int width = Math.Max(1, (int)(Bounds.Width * scaling));
        int height = Math.Max(1, (int)(Bounds.Height * scaling));

        _childHwnd = CreateWindowEx(
            0, windowClass, "CineD3D11",
            WindowStyles.WS_CHILD,
            0, 0, width, height,
            _parentHwnd, IntPtr.Zero,
            GetModuleHandle(null), IntPtr.Zero);

        if (_childHwnd != IntPtr.Zero)
        {
            ShowWindow(_childHwnd, IsVisible && IsVideoSurfaceVisible ? ShowWindowCommand.Show : ShowWindowCommand.Hide);

            #region debug-point videohost-create-success
            DebugLog($"CreateChildWindow success hwnd={_childHwnd} size={width}x{height}");
            global::Cine.Avalonia.App.DebugReport("VT", "VideoHost.CreateChildWindow.Success", "CreateChildWindow success.", new
            {
                hwnd = _childHwnd.ToInt64(),
                size = $"{width}x{height}",
                isVisible = IsVisible,
                surfaceVisible = IsVideoSurfaceVisible
            }, runId: "pre-fix");
            #endregion
        }
        else
        {
            #region debug-point videohost-create-fail
            DebugLog($"CreateChildWindow failed lastError={Marshal.GetLastWin32Error()} size={width}x{height}");
            global::Cine.Avalonia.App.DebugReport("VT", "VideoHost.CreateChildWindow.Fail", "CreateChildWindow failed.", new
            {
                lastError = Marshal.GetLastWin32Error(),
                size = $"{width}x{height}"
            }, runId: "pre-fix");
            #endregion
        }
    }

    private void DestroyChildWindow()
    {
        if (_childHwnd != IntPtr.Zero)
        {
            DestroyWindow(_childHwnd);
            _childHwnd = IntPtr.Zero;
        }
        _parentHwnd = IntPtr.Zero;
    }

    private double GetScaling()
    {
        if (_parentHwnd != IntPtr.Zero)
        {
            try { return GetDpiForWindow(_parentHwnd) / 96.0; }
            catch { }
        }
        return 1.0;
    }

    private static readonly WndProcDelegate _wndProcDelegate = WndProc;

    private static IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        const uint WM_NCHITTEST = 0x0084;
        const uint WM_PAINT = 0x000F;
        const int HTTRANSPARENT = -1;
        if (msg == WM_NCHITTEST)
            return new IntPtr(HTTRANSPARENT);
        if (msg == WM_PAINT)
        {
            #region debug-point VT-F
            int c = System.Threading.Interlocked.Increment(ref _wmPaintCount);
            if (c <= 10)
            {
                global::Cine.Avalonia.App.DebugReport("VT", "VideoHost.WM_PAINT", "WM_PAINT received.", new
                {
                    hwnd = hWnd.ToInt64(),
                    count = c
                }, runId: "pre-fix");
            }
            #endregion
            var hdc = BeginPaint(hWnd, out var ps);
            try
            {
                GetClientRect(hWnd, out var rc);
                FillRect(hdc, ref rc, GetStockObject(BLACK_BRUSH));
            }
            finally
            {
                EndPaint(hWnd, ref ps);
            }
            return IntPtr.Zero;
        }
        return DefWindowProc(hWnd, msg, wParam, lParam);
    }

    #region P/Invoke

    private const int IDC_ARROW = 32512;
    private const int BLACK_BRUSH = 4;

    [Flags]
    private enum WindowStyles : uint
    {
        WS_OVERLAPPED = 0,
        WS_POPUP = 0x80000000,
        WS_CHILD = 0x40000000,
        WS_MINIMIZE = 0x20000000,
        WS_VISIBLE = 0x10000000,
        WS_DISABLED = 0x8000000,
        WS_CLIPSIBLINGS = 0x4000000,
        WS_CLIPCHILDREN = 0x2000000,
        WS_MAXIMIZE = 0x1000000,
    }

    [Flags]
    private enum SetWindowPosFlags : uint
    {
        SWP_NOSIZE = 0x0001,
        SWP_NOMOVE = 0x0002,
        SWP_NOZORDER = 0x0004,
        SWP_NOREDRAW = 0x0008,
        SWP_NOACTIVATE = 0x0010,
    }

    private enum ShowWindowCommand : int
    {
        Hide = 0,
        ShowNormal = 1,
        ShowMinimized = 2,
        Maximize = 3,
        ShowNoActivate = 4,
        Show = 5,
        Minimize = 6,
        ShowMinNoActive = 7,
        ShowNA = 8,
        Restore = 9,
        ShowDefault = 10,
        ForceMinimize = 11
    }

    private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
        int X, int Y, int cx, int cy, SetWindowPosFlags uFlags);

    [DllImport("gdi32.dll")]
    private static extern IntPtr GetStockObject(int fnObject);

    [DllImport("user32.dll")]
    private static extern IntPtr BeginPaint(IntPtr hWnd, out PAINTSTRUCT lpPaint);

    [DllImport("user32.dll")]
    private static extern bool EndPaint(IntPtr hWnd, [In] ref PAINTSTRUCT lpPaint);

    [DllImport("user32.dll")]
    private static extern int FillRect(IntPtr hDC, [In] ref RECT lprc, IntPtr hbr);

    [DllImport("user32.dll")]
    private static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateWindowEx(
        int dwExStyle, string lpClassName, string lpWindowName,
        WindowStyles dwStyle, int x, int y, int nWidth, int nHeight,
        IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern ushort RegisterClassEx(ref WndClassEx lpwcx);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, ShowWindowCommand nCmdShow);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr DefWindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern IntPtr LoadCursor(IntPtr hInstance, int lpCursorName);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetDpiForWindow(IntPtr hwnd);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WndClassEx
    {
        public uint cbSize;
        public uint style;
        public WndProcDelegate lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public IntPtr hInstance;
        public IntPtr hIcon;
        public IntPtr hCursor;
        public IntPtr hbrBackground;
        public string? lpszMenuName;
        public string? lpszClassName;
        public IntPtr hIconSm;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int left;
        public int top;
        public int right;
        public int bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PAINTSTRUCT
    {
        public IntPtr hdc;
        public bool fErase;
        public RECT rcPaint;
        public bool fRestore;
        public bool fIncUpdate;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
        public byte[] rgbReserved;
    }

    #endregion
}
