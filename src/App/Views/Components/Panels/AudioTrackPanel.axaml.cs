using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Cine.Avalonia.Models;
using Cine.Avalonia.Views.Components;

namespace Cine.Avalonia.Views.Components.Panels;

public partial class AudioTrackPanel : UserControl
{
    private Action? _dismiss;

    /// <summary>Fired when an external audio file is dropped onto the panel.</summary>
    public event EventHandler<string>? ExternalFileDropped;

    private static readonly string[] AudioExtensions = { ".mp3", ".aac", ".flac", ".ogg", ".wav", ".m4a", ".opus" };

    public AudioTrackPanel()
    {
        InitializeComponent();
        SetupDragDrop();
    }

    /// <summary>
    /// Populates the panel's content using TrackFlyoutBuilder.
    /// Call this each time the panel is shown to ensure fresh data.
    /// </summary>
    public void SetTrackData(
        ObservableCollection<TrackMenuItem> tracks,
        Func<double> getDelay,
        Action<double> setDelay,
        Action resetDelay,
        Action? dismiss = null)
    {
        _dismiss = dismiss;

        var content = TrackFlyoutBuilder.BuildContent(
            tracks: tracks,
            emptyMessage: "No audio tracks available",
            delayLabel: "Audio Delay",
            getDelay: getDelay,
            setDelay: setDelay,
            resetDelay: resetDelay,
            emptyIcon: global::Material.Icons.MaterialIconKind.MusicOff,
            dismissOverlay: dismiss
        );

        ContentContainer.Child = content;
    }

    private void SetupDragDrop()
    {
        DragDrop.SetAllowDrop(this, true);
        this.AddHandler(DragDrop.DragOverEvent, OnDragOver);
        this.AddHandler(DragDrop.DropEvent, OnDrop);
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        if (e.DataTransfer == null || !e.DataTransfer.Contains(DataFormat.File))
        {
            e.DragEffects = DragDropEffects.None;
            return;
        }

        var files = e.DataTransfer.TryGetFiles();
        if (files == null) return;

        var hasValidFile = files.Any(f =>
        {
            var ext = Path.GetExtension(f.Path.LocalPath)?.ToLowerInvariant();
            return ext != null && AudioExtensions.Contains(ext);
        });

        e.DragEffects = hasValidFile ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnDrop(object? sender, DragEventArgs e)
    {
        if (e.DataTransfer == null || !e.DataTransfer.Contains(DataFormat.File))
            return;

        var files = e.DataTransfer.TryGetFiles();
        if (files == null) return;

        foreach (var file in files)
        {
            var path = file.Path.LocalPath;
            var ext = Path.GetExtension(path)?.ToLowerInvariant();
            if (ext != null && AudioExtensions.Contains(ext))
            {
                ExternalFileDropped?.Invoke(this, path);
                e.Handled = true;
            }
        }
    }
}
