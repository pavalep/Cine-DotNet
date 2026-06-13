using Avalonia;
using Avalonia.Controls;
using Avalonia.OpenGL;
using Avalonia.OpenGL.Controls;
using Avalonia.Threading;
using Cine.Media.Implementations;
using Cine.Media.Models;

namespace Cine.Avalonia.Controls;

/// <summary>
/// OpenGL-based video view using Avalonia's OpenGlControlBase.
/// mpv renders directly into the FBO provided by Avalonia (zero-copy).
/// Matches the reference LibMpv-OpenGL.OpenGlView implementation.
/// </summary>
public class MpvVideoView : OpenGlControlBase
{
    private MpvPlayer? _player;
    private bool _initialized;
    private volatile bool _isIdle = true;

    public void AttachPlayer(MpvPlayer player)
    {
        _player = player;
        player.PlaybackStateChangedEvent += (_, e) =>
        {
            var wasIdle = _isIdle;
            _isIdle = e.State is not (PlaybackState.Playing or PlaybackState.Paused);
            if (wasIdle && !_isIdle)
                Dispatcher.UIThread.Post(RequestNextFrameRendering, DispatcherPriority.Background);
        };
        if (_initialized)
            RequestNextFrameRendering();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        RequestNextFrameRendering();
    }

    protected override void OnOpenGlInit(GlInterface gl)
    {
        if (_initialized) return;
        _initialized = true;

        if (_player == null) return;

        _player.InitializeRenderApi(
            name => gl.GetProcAddress(name),
            () => Dispatcher.UIThread.Post(RequestNextFrameRendering, DispatcherPriority.Background));
    }

    protected override unsafe void OnOpenGlRender(GlInterface gl, int fbo)
    {
        if (_player == null || _isIdle) return;

        var scaling = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1.0;
        var w = Math.Max(1, (int)(Bounds.Width * scaling));
        var h = Math.Max(1, (int)(Bounds.Height * scaling));

        _player.RenderFrame(fbo, w, h);
    }

    protected override void OnOpenGlDeinit(GlInterface gl)
    {
        _player?.DeinitializeRenderApi();
        _initialized = false;
    }
}
