using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Cine.Avalonia.Controls;
using Cine.Media.Interfaces;

namespace Cine.Avalonia.Views.Dialogs;

public partial class PipWindow : Window
{
    private readonly IMediaPlayer _pipPlayer;
    private readonly IMediaPlayer _mainPlayer;
    private readonly string _filePath;
    private DispatcherTimer? _syncTimer;
    private D3D11VideoHost? _videoHost;
    private bool _initialized;
    private CancellationTokenSource? _initCts;

    public PipWindow()
    {
        _pipPlayer = null!;
        _mainPlayer = null!;
        _filePath = string.Empty;
        InitializeComponent();
    }

    public PipWindow(IMediaPlayer pipPlayer, IMediaPlayer mainPlayer, string filePath) : this()
    {
        _pipPlayer = pipPlayer;
        _mainPlayer = mainPlayer;
        _filePath = filePath;
    }

    private void OnOpened(object? sender, EventArgs e)
    {
        _videoHost = this.FindControl<D3D11VideoHost>("PipVideoHost");
        if (_videoHost == null) return;

        _videoHost.ChildWindowCreated += OnPipVideoHostReady;
    }

    private void OnPipVideoHostReady(object? sender, EventArgs e)
    {
        if (_initialized || _videoHost == null) return;
        _initialized = true;

        var hwnd = _videoHost.VideoHwnd;
        if (hwnd == IntPtr.Zero) return;

        _initCts = new CancellationTokenSource();
        var ct = _initCts.Token;

        Task.Run(() =>
        {
            try
            {
                ct.ThrowIfCancellationRequested();

                _pipPlayer.InitializeRenderer(hwnd);
                ct.ThrowIfCancellationRequested();

                _pipPlayer.Mute(true);
                _pipPlayer.Open(_filePath);
                ct.ThrowIfCancellationRequested();

                var mainPos = _mainPlayer.Position;
                if (mainPos.TotalSeconds > 0)
                    _pipPlayer.Seek(mainPos);

                Dispatcher.UIThread.Post(() =>
                {
                    if (!ct.IsCancellationRequested && _videoHost != null)
                        _videoHost.IsVideoSurfaceVisible = true;
                });

                StartSyncTimer();
            }
            catch (OperationCanceledException)
            {
            }
            catch
            {
                Dispatcher.UIThread.Post(Close);
            }
        }, ct);
    }

    private void StartSyncTimer()
    {
        _syncTimer = new DispatcherTimer(
            TimeSpan.FromMilliseconds(100),
            DispatcherPriority.Background,
            (s, a) =>
            {
                try
                {
                    var mainPos = _mainPlayer.Position;
                    var pipPos = _pipPlayer.Position;
                    var diff = Math.Abs((mainPos - pipPos).TotalSeconds);
                    if (diff > 0.5)
                        _pipPlayer.Seek(mainPos);

                    var dur = _pipPlayer.Duration;
                    if (dur.TotalSeconds > 0)
                    {
                        var width = PipSeekTrack?.Bounds.Width ?? 0;
                        if (width > 0)
                        {
                            var pct = Math.Clamp(pipPos.TotalSeconds / dur.TotalSeconds, 0.0, 1.0);
                            if (PipSeekFill != null)
                                PipSeekFill.Width = pct * width;
                        }
                    }
                    if (PipTimeLabel != null)
                        PipTimeLabel.Text = $"{(int)pipPos.TotalMinutes:D2}:{pipPos.Seconds:D2} / {(int)dur.TotalMinutes:D2}:{dur.Seconds:D2}";
                }
                catch { }
            });
        _syncTimer.Start();
    }

    private void OnPipPlayPause(object? sender, RoutedEventArgs e)
    {
        if (_pipPlayer.IsPlaying) _pipPlayer.Pause();
        else _pipPlayer.Play();
    }

    private void OnPipPrevious(object? sender, RoutedEventArgs e)
    {
        try { _mainPlayer.SeekBackward(30); SyncFromMain(); } catch { }
    }

    private void OnPipNext(object? sender, RoutedEventArgs e)
    {
        try { _mainPlayer.SeekForward(30); SyncFromMain(); } catch { }
    }

    public void SyncFromMain()
    {
        try
        {
            var mainPos = _mainPlayer.Position;
            var pipPos = _pipPlayer.Position;
            if (Math.Abs((mainPos - pipPos).TotalSeconds) > 0.3)
                _pipPlayer.Seek(mainPos);
        }
        catch { }
    }

    private async void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        _syncTimer?.Stop();
        var startW = Width;
        var startH = Height;
        var startX = Position.X;
        var startY = Position.Y;
        var centerX = startX + startW / 2;
        var centerY = startY + startH / 2;
        var targetW = startW * 0.3;
        var targetH = startH * 0.3;
        var steps = 10;
        for (int i = 1; i <= steps; i++)
        {
            var t = (double)i / steps;
            var ease = 1 - Math.Pow(1 - t, 3);
            Width = startW - (startW - targetW) * ease;
            Height = startH - (startH - targetH) * ease;
            Opacity = 1 - ease;
            var px = centerX - Width / 2;
            var py = centerY - Height / 2;
            Position = new global::Avalonia.PixelPoint((int)px, (int)py);
            await Task.Delay(20);
        }
        Close();
    }

    protected override void OnClosed(EventArgs e)
    {
        _syncTimer?.Stop();
        _syncTimer = null;

        _initCts?.Cancel();
        _initCts?.Dispose();
        _initCts = null;

        try
        {
            _pipPlayer.Stop();
            (_pipPlayer as IDisposable)?.Dispose();
        }
        catch { }

        base.OnClosed(e);
    }
}

