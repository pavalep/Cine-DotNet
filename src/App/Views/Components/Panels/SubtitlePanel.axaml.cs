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
using Cine.Avalonia.Services;

namespace Cine.Avalonia.Views.Components.Panels;

public partial class SubtitlePanel : UserControl
{
    private ISubtitleManager? _manager;
    private ObservableCollection<TrackMenuItem>? _tracks;
    private Action? _dismiss;

    /// <summary>Fired when an external subtitle file is dropped onto the panel.</summary>
    public event EventHandler<string>? ExternalFileDropped;

    /// <summary>Fired when the user clicks the Subtitle Settings gear button.</summary>
    public event EventHandler? SettingsClicked;

    private static readonly string[] SubtitleExtensions = { ".srt", ".ass", ".ssa", ".vtt", ".sub", ".idx" };

    public SubtitlePanel()
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
        ISubtitleManager? manager,
        Func<double> getDelay,
        Action<double> setDelay,
        Action resetDelay,
        Action? dismiss = null)
    {
        _tracks = tracks;
        _manager = manager;
        _dismiss = dismiss;

        var content = TrackFlyoutBuilder.BuildContent(
            tracks: tracks,
            emptyMessage: "No subtitles available",
            delayLabel: "Subtitle Delay",
            getDelay: getDelay,
            setDelay: setDelay,
            resetDelay: resetDelay,
            appendExtra: root => AppendFooter(root),
            emptyIcon: global::Material.Icons.MaterialIconKind.ClosedCaptionOutline,
            dismissOverlay: dismiss
        );

        ContentContainer.Child = content;
    }

    private void AppendFooter(StackPanel root)
    {
        // Thin separator
        root.Children.Add(new Border
        {
            Height = 1,
            Background = (IBrush?)Application.Current?.FindResource("PopoverBorder"),
            Margin = new Thickness(8, 4, 8, 4)
        });

        // Gear button → opens SubtitleSettingsDialog
        var gearBtn = new Button
        {
            Content = new TextBlock
            {
                Text = "\u2699 Subtitle Settings\u2026",
                FontSize = 12,
                Foreground = (IBrush?)Application.Current?.FindResource("TextTertiary"),
                HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Center
            },
            Background = global::Avalonia.Media.Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(8, 4),
            Cursor = new Cursor(StandardCursorType.Hand)
        };
        gearBtn.Click += (_, _) =>
        {
            _dismiss?.Invoke();
            SettingsClicked?.Invoke(this, EventArgs.Empty);
        };
        root.Children.Add(gearBtn);
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
            return ext != null && SubtitleExtensions.Contains(ext);
        });

        e.DragEffects = hasValidFile ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        if (e.DataTransfer == null || !e.DataTransfer.Contains(DataFormat.File))
            return;

        var files = e.DataTransfer.TryGetFiles();
        if (files == null) return;

        foreach (var file in files)
        {
            var path = file.Path.LocalPath;
            var ext = Path.GetExtension(path)?.ToLowerInvariant();
            if (ext != null && SubtitleExtensions.Contains(ext))
            {
                if (_manager != null)
                    await _manager.LoadExternalSubtitleAsync(path);
                ExternalFileDropped?.Invoke(this, path);
                e.Handled = true;
            }
        }
    }
}
