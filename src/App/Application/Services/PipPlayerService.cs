using System;
using System.IO;
using System.Runtime.InteropServices;
using Cine.Media.Interfaces;
using Cine.Media.Implementations;

namespace Cine.Avalonia.ViewModels
{
    /// <summary>
    /// Manages the secondary MpvPlayer instance used for PiP (Picture-in-Picture).
    /// Uses the mpv render API (DXGI) directly — no hidden windows, no DWM thumbnails.
    ///
    /// Architecture:
    ///   PipWindow (Avalonia, renders controls)
    ///     └─ Child video HWND (Win32, covers video area)
    ///          └─ D3D11 swap chain ← mpv renders here via DXGI render API
    ///
    /// Controls render naturally on top of the video child window.
    /// </summary>
    public class PipPlayerService : IDisposable
    {
        private MpvPlayer? _player;
        private IntPtr _childVideoHwnd;
        private bool _childWindowCreated;
        private IntPtr _pipWindowHwnd;
        private bool _disposed;

        private static readonly string WindowClassName = "CinePipVideo_" + Guid.NewGuid().ToString("N")[..8];

        /// <summary>The secondary PiP player instance, if initialized.</summary>
        public IMediaPlayer? Player => _player;

        /// <summary>The child video HWND (the Win32 window mpv renders to).</summary>
        public IntPtr ChildVideoHwnd => _childVideoHwnd;

        /// <summary>Fired when an error occurs on the secondary player.</summary>
        public event EventHandler<string>? Error;

        /// <summary>
        /// Creates a child video HWND and mpv player using native HWND rendering.
        /// </summary>
        public bool Initialize(IntPtr pipWindowHwnd)
        {
            if (_disposed)
            {
                Error?.Invoke(this, "PipPlayerService is disposed");
                return false;
            }

            if (_player != null)
            {
                // Already initialized
                return true;
            }

            _pipWindowHwnd = pipWindowHwnd;

            try
            {
                // 1. Create child video HWND inside the PiP window
                if (!CreateChildVideoWindow())
                {
                    Error?.Invoke(this, "Failed to create child video window");
                    return false;
                }

                // 2. Create secondary mpv player using native HWND rendering
                var player = new MpvPlayer();
                player.Error += OnSecondaryError;
                player.HighQualityRendering = false;   // low-quality profile
                player.InitializeRenderer(_childVideoHwnd);
                player.Mute(true);
                _player = player;

                PipLog("Initialize success (render API)");
                return true;
            }
            catch (Exception ex)
            {
                Error?.Invoke(this, $"Failed to create secondary player: {ex.Message}");
                Cleanup();
                return false;
            }
        }

        /// <summary>
        /// Resize the child video HWND to match the PiP video area.
        /// Call on every PipWindow resize, with offsets for control areas.
        /// </summary>
        public void ResizeVideoArea(int left, int top, int width, int height)
        {
            if (_childVideoHwnd == IntPtr.Zero) return;

            SetWindowPos(_childVideoHwnd, IntPtr.Zero,
                left, top, Math.Max(1, width), Math.Max(1, height),
                SWP_NOACTIVATE | SWP_NOZORDER);
        }

        /// <summary>
        /// Opens a file in the secondary player (must be called after <see cref="Initialize"/>).
        /// </summary>
        public void Open(string path)
        {
            _player?.Open(path);
        }

        /// <summary>
        /// Seeks the secondary player to the specified position.
        /// </summary>
        public void Seek(TimeSpan position)
        {
            _player?.Seek(position);
        }

        /// <summary>
        /// Sets the secondary player's mute state (should always be muted for PiP).
        /// </summary>
        public void SetMuted(bool muted)
        {
            _player?.Mute(muted);
        }

        /// <summary>
        /// Stops and disposes the secondary player. Safe to call multiple times.
        /// </summary>
        public void Stop()
        {
            Cleanup();
        }

        private void OnSecondaryError(object? sender, string message)
        {
            Error?.Invoke(this, message);
        }

        // ── Child video window (Win32) ──

        private bool CreateChildVideoWindow()
        {
            if (_childWindowCreated) return true;

            RegisterWindowClass();

            _childVideoHwnd = CreateWindowEx(
                0,  // No extended style (child window doesn't need toolwindow)
                WindowClassName, "CinePiPVideo",
                WS_CHILD | WS_VISIBLE | WS_CLIPSIBLINGS,
                0, 0, 640, 360,   // Position/size updated on resize
                _pipWindowHwnd,   // Parent = PipWindow
                IntPtr.Zero,
                Marshal.GetHINSTANCE(typeof(PipPlayerService).Module),
                IntPtr.Zero);

            if (_childVideoHwnd != IntPtr.Zero)
            {
                _childWindowCreated = true;
                PipLog($"Child video window created: hwnd=0x{_childVideoHwnd:X} parent=0x{_pipWindowHwnd:X}");
                return true;
            }
            else
            {
                int err = Marshal.GetLastWin32Error();
                PipLog($"CreateWindowEx FAILED: error={err}");
                return false;
            }
        }

        private static void RegisterWindowClass()
        {
            var wc = new WNDCLASS
            {
                style = 0,
                lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_wndProcDelegate),
                hInstance = Marshal.GetHINSTANCE(typeof(PipPlayerService).Module),
                lpszClassName = WindowClassName,
                hbrBackground = IntPtr.Zero,
                hCursor = IntPtr.Zero
            };

            if (RegisterClass(ref wc) == 0)
            {
                int err = Marshal.GetLastWin32Error();
                if (err != 1410) // Class already exists
                    PipLog($"RegisterClass returned error={err}");
            }
        }

        private static IntPtr StaticWndProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam)
            => DefWindowProc(hwnd, msg, wParam, lParam);

        // ── Cleanup ──

        private void Cleanup()
        {
            // Dispose mpv player first
            if (_player != null)
            {
                _player.Error -= OnSecondaryError;
                _player.Dispose();
                _player = null;
            }

            // Destroy child video window
            if (_childVideoHwnd != IntPtr.Zero)
            {
                DestroyWindow(_childVideoHwnd);
                _childVideoHwnd = IntPtr.Zero;
                _childWindowCreated = false;
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Cleanup();
        }

        // ── Logging ──

        private static void PipLog(string msg)
        {
            try
            {
                var path = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Cine", "cine_pip_video.log");
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.AppendAllText(path, $"[{DateTime.Now:HH:mm:ss.fff}] {msg}{Environment.NewLine}");
            }
            catch { }
        }

        // ── Win32 P/Invoke ──

        private static readonly WndProcDelegate _wndProcDelegate = StaticWndProc;

        private const uint WS_CHILD = 0x40000000;
        private const uint WS_VISIBLE = 0x10000000;
        private const uint WS_CLIPSIBLINGS = 0x04000000;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_NOZORDER = 0x0004;

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern IntPtr CreateWindowEx(uint exStyle, string className, string windowName,
            uint style, int x, int y, int width, int height, IntPtr parent, IntPtr menu,
            IntPtr instance, IntPtr param);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DestroyWindow(IntPtr hwnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hwnd, IntPtr hwndInsertAfter,
            int x, int Y, int cx, int cy, uint flags);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode, EntryPoint = "RegisterClassW")]
        private static extern ushort RegisterClass(ref WNDCLASS wc);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern IntPtr DefWindowProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        private struct WNDCLASS
        {
            public uint style;
            public IntPtr lpfnWndProc;
            public int cbClsExtra;
            public int cbWndExtra;
            public IntPtr hInstance;
            public IntPtr hIcon;
            public IntPtr hCursor;
            public IntPtr hbrBackground;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string lpszMenuName;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string lpszClassName;
        }

        private delegate IntPtr WndProcDelegate(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);
    }
}
