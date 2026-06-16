using System;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Cine.Avalonia.ViewModels;
using Cine.Avalonia.Controls.Subtitle;
using DragEventArgs = Avalonia.Input.DragEventArgs;
using DragDropEffects = Avalonia.Input.DragDropEffects;

namespace Cine.Avalonia.Controls;

/// <summary>
/// Standalone subtitle overlay layer with its own button + flyout containing
/// subtitle style controls (track selection, font size, position, delay, visibility).
/// Supports drag-drop of external subtitle files (.srt, .ass, .vtt, .sub, .idx).
/// </summary>
public partial class SubtitleOverlayControl : AvaloniaUserControl
{
    private MainViewModel? _viewModel;
    private Flyout? _currentFlyout;
    private SubtitleStyleFlyout? _styleFlyout;

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

        // Clean up old flyout binding
        if (_currentFlyout != null && _styleFlyout != null)
        {
            _styleFlyout.Unbind();
            _styleFlyout = null;
        }
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
        if (_viewModel?.Subtitles == null) return;

        _styleFlyout = new SubtitleStyleFlyout();
        _styleFlyout.Bind(_viewModel.Subtitles);
        _styleFlyout.CloseAction = () => _currentFlyout?.Hide();

        _currentFlyout = new Flyout
        {
            Content = _styleFlyout,
            Placement = PlacementMode.TopEdgeAlignedLeft,
            ShowMode = FlyoutShowMode.Standard,
            OverlayDismissEventPassThrough = true
        };
        _currentFlyout.Closed += (_, _) => _styleFlyout?.Unbind();
        _currentFlyout.ShowAt(BtnSubtitles);
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
                _viewModel?.Subtitles?.LoadExternalSubtitle(path);
                ExternalFileDropped?.Invoke(this, path);
                e.Handled = true;
            }
        }
    }
}
