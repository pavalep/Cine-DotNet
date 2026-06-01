using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Cine.Avalonia.Controls;
using Cine.Media.Interfaces;

namespace Cine.Avalonia.Views;

public partial class PipWindow : Window
{
    private readonly IMediaPlayer _pipPlayer;
    private readonly IMediaPlayer _mainPlayer;
    private readonly string _filePath;
    private DispatcherTimer? _syncTimer;
    private D3D11VideoHost? _videoHost;
    private bool _initialized;

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

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
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

        Task.Run(() =>
        {
            try
            {
                _pipPlayer.InitializeRenderer(hwnd);
                _pipPlayer.Mute(true);
                _pipPlayer.Open(_filePath);

                var mainPos = _mainPlayer.Position;
                if (mainPos.TotalSeconds > 0)
                    _pipPlayer.Seek(mainPos);

                Dispatcher.UIThread.Post(() =>
                {
                    if (_videoHost != null)
                        _videoHost.IsVideoSurfaceVisible = true;
                });

                StartSyncTimer();
            }
            catch
            {
                Dispatcher.UIThread.Post(Close);
            }
        });
    }

    private void StartSyncTimer()
    {
        _syncTimer = new DispatcherTimer(
            TimeSpan.FromSeconds(2),
            DispatcherPriority.Background,
            (s, a) =>
            {
                try
                {
                    var mainPos = _mainPlayer.Position;
                    var pipPos = _pipPlayer.Position;
                    if (Math.Abs((mainPos - pipPos).TotalSeconds) > 1.0)
                        _pipPlayer.Seek(mainPos);
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

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();

    protected override void OnClosed(EventArgs e)
    {
        _syncTimer?.Stop();
        _syncTimer = null;

        try
        {
            _pipPlayer.Stop();
            (_pipPlayer as IDisposable)?.Dispose();
        }
        catch { }

        base.OnClosed(e);
    }
}
