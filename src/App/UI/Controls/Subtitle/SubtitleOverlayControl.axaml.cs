using System;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Cine.Avalonia.Builders;
using Cine.Avalonia.ViewModels;
using DragEventArgs = Avalonia.Input.DragEventArgs;
using DragDropEffects = Avalonia.Input.DragDropEffects;

namespace Cine.Avalonia.Controls;

/// <summary>
/// Standalone subtitle overlay layer with its own button + flyout containing
/// subtitle track selection and subtitle delay controls. Supports drag-drop
/// of external subtitle files (.srt, .ass, .vtt, .sub, .idx) directly onto
/// the button for immediate loading.
/// </summary>
public partial class SubtitleOverlayControl : AvaloniaUserControl
{
    private MainViewModel? _viewModel;
    private Flyout? _currentFlyout;

    private static readonly string[] SubtitleExtensions = { ".srt", ".ass", ".ssa", ".vtt", ".sub", ".idx" };

    /// <summary>
    /// Fired when an external subtitle file is dropped onto the button.
    /// MainWindow subscribes to this to show OSD notifications.
    /// </summary>
    public event EventHandler<string>? ExternalFileDropped;

    public SubtitleOverlayControl()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;

        // Wire drag-drop on the button
        DragDrop.SetAllowDrop(BtnSubtitles, true);
        BtnSubtitles.AddHandler(DragDrop.DragOverEvent, OnBtnDragOver);
        BtnSubtitles.AddHandler(DragDrop.DropEvent, OnBtnDrop);
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        _viewModel = DataContext as MainViewModel;
    }

    /// <summary>Refreshes the subtitle icon to reflect enabled/disabled state.</summary>
    public void RefreshIcon()
    {
        if (_viewModel == null) return;
        if (SubtitleIcon == null) return;
        SubtitleIcon.Kind = _viewModel.IsSubtitleEnabled
            ? Material.Icons.MaterialIconKind.Subtitles
            : Material.Icons.MaterialIconKind.ClosedCaptionOutline;
    }

    public void SetVisibility(bool visible) => IsVisible = visible;

    /// <summary>Closes the flyout if it is currently open.</summary>
    public void HideFlyout()
    {
        if (_currentFlyout?.IsOpen == true)
            _currentFlyout.Hide();
    }

    private void OnSubtitlesClick(object? sender, RoutedEventArgs e)
    {
        if (_viewModel == null) return;
        _currentFlyout = BuildSubtitleFlyout();
        _currentFlyout.ShowAt(BtnSubtitles);
    }

    private Flyout BuildSubtitleFlyout()
    {
        if (_viewModel == null) return new Flyout();
        var vm = _viewModel;
        return TrackFlyoutBuilder.Build(
            vm.SubtitleTracks,
            "No subtitles available",
            "Subtitle Delay",
            () => vm.SubtitleDelayValue,
            v => vm.SubtitleDelayValue = (float)Math.Clamp(v, -10, 10),
            () => vm.ResetSubtitleDelay()
        );
    }

    // ── Drag-drop handlers ──────────────────────────────────────────

    private void OnBtnDragOver(object? sender, DragEventArgs e)
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

    private void OnBtnDrop(object? sender, DragEventArgs e)
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
                _viewModel?.LoadExternalSubtitle(path);
                ExternalFileDropped?.Invoke(this, path);
                e.Handled = true;
            }
        }
    }
}
