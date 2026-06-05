using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Key = Avalonia.Input.Key;
using KeyEventArgs = Avalonia.Input.KeyEventArgs;
using Cine.Avalonia.Helpers;
using PointerPressedEventArgs = Avalonia.Input.PointerPressedEventArgs;
using Cine.Media.Interfaces;

namespace Cine.Avalonia.Views.Dialogs;

public partial class PipWindow : Window
{
    private readonly IMediaPlayer _mainPlayer;
    private bool _disposed;
    private bool _isPinned;
    private bool _isClosing;
    private CancellationTokenSource? _pollCts;
    private int _frameCount;

    // ────── State persistence ──────
    private static readonly string PipStatePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Cine", "pip_state.json");

    private record PipState(int X, int Y, int W, int H, bool Pinned);

    public PipWindow(IMediaPlayer mainPlayer)
    {
        _mainPlayer = mainPlayer;
        InitializeComponent();
        TitleBar.PointerPressed += OnTitleBarPointerPressed;
        KeyDown += OnKeyDown;
    }

    // ═══════════════════════════════════════════════════════════════
    // LIFECYCLE
    // ═══════════════════════════════════════════════════════════════

    public void StartPolling()
    {
        _pollCts = new CancellationTokenSource();
        _ = PollFramesAsync(_pollCts.Token);
    }

    public void StopPolling()
    {
        _pollCts?.Cancel();
        _pollCts?.Dispose();
        _pollCts = null;
    }

    private async Task PollFramesAsync(CancellationToken ct)
    {
        var timerInterval = TimeSpan.FromMilliseconds(16); // ~60fps
        var sw = new System.Diagnostics.Stopwatch();

        while (!ct.IsCancellationRequested && !_disposed)
        {
            sw.Restart();
            var raw = _mainPlayer.ScreenshotRaw(out var w, out var h);

            if (raw != null && w > 0 && h > 0 && !ct.IsCancellationRequested)
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (!_disposed && PipFrameImage != null)
                    {
                        try
                        {
                            var size = new global::Avalonia.PixelSize(w, h);
                            var dpi = new global::Avalonia.Vector(96, 96);
                            int stride = w * 4;
                            var wb = new WriteableBitmap(size, dpi);

                            using (var fb = wb.Lock())
                            {
                                // Copy raw BGRA data row by row (handles stride differences)
                                int srcStride = raw.Length / h;
                                for (int y = 0; y < h; y++)
                                {
                                    var srcOffset = y * srcStride;
                                    var dstOffset = y * stride;
                                    if (srcOffset + stride <= raw.Length)
                                    {
                                        System.Runtime.InteropServices.Marshal.Copy(
                                            raw, srcOffset,
                                            IntPtr.Add(fb.Address, dstOffset),
                                            Math.Min(stride, raw.Length - srcOffset));
                                    }
                                }
                            }

                            PipFrameImage.Source = wb;
                            _frameCount++;
                        }
                        catch { }
                    }
                }, DispatcherPriority.Normal, ct);
            }

            var elapsed = sw.ElapsedMilliseconds;
            var delay = (int)(timerInterval.TotalMilliseconds - elapsed);
            if (delay > 0)
                await Task.Delay(delay, ct).ConfigureAwait(false);
            else
                await Task.Yield();
        }
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);

        if (PipFileNameLabel != null)
            PipFileNameLabel.Text = "Cine PIP";

        RestoreState();
        StartPolling();
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        if (_isClosing) return;
        _isClosing = true;

        StopPolling();
        SaveState();
        PipFrameImage.Source = null;

        base.OnClosing(e);
    }

    // ═══════════════════════════════════════════════════════════════
    // STATE PERSISTENCE
    // ═══════════════════════════════════════════════════════════════

    private void SaveState()
    {
        try
        {
            var state = new PipState(Position.X, Position.Y, (int)Width, (int)Height, _isPinned);
            var json = JsonSerializer.Serialize(state);
            File.WriteAllText(PipStatePath, json);
        }
        catch { }
    }

    private void RestoreState()
    {
        try
        {
            if (!File.Exists(PipStatePath)) return;
            var json = File.ReadAllText(PipStatePath);
            var state = JsonSerializer.Deserialize<PipState>(json);
            if (state != null)
            {
                Position = new PixelPoint(state.X, state.Y);
                Width = state.W;
                Height = state.H;
                _isPinned = state.Pinned;
                Topmost = _isPinned;
                UpdatePinIcon();
            }
        }
        catch { }
    }

    // ═══════════════════════════════════════════════════════════════
    // UI HANDLERS
    // ═══════════════════════════════════════════════════════════════

    private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginMoveDrag(e);
    }

    private void OnPinToggle(object? sender, RoutedEventArgs e)
    {
        _isPinned = !_isPinned;
        Topmost = _isPinned;
        UpdatePinIcon();
        SaveState();
    }

    private void UpdatePinIcon()
    {
        if (PinIcon != null)
            PinIcon.Opacity = _isPinned ? 1.0 : 0.4;
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        StopPolling();
        SaveState();
        Close();
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
            OnCloseClick(sender, e);
        if (e.Key == Key.F)
            OnCloseClick(sender, e);
    }

    public new void Close()
    {
        if (!_isClosing)
        {
            _isClosing = true;
            StopPolling();
            SaveState();
            PipFrameImage.Source = null;
        }
        base.Close();
    }
}
