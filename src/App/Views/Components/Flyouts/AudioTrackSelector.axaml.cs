using System;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Cine.Avalonia.Core;
using Cine.Avalonia.Services;
using Cine.Avalonia.ViewModels;
using Cine.Avalonia.Views.Shell;
using Control = Avalonia.Controls.Control;
using DragEventArgs = Avalonia.Input.DragEventArgs;
using DragDropEffects = Avalonia.Input.DragDropEffects;

namespace Cine.Avalonia.Views.Components;

/// <summary>
/// Standalone audio track selector layer with its own button + flyout overlay
/// containing audio track selection and audio delay controls. Supports drag-drop
/// of external audio files (.mp3, .aac, .flac, .ogg, .wav, .m4a) directly onto
/// the button for immediate loading.
/// </summary>
public partial class AudioTrackSelector : AvaloniaUserControl, IFlyoutSource
{
    private MainViewModel? _viewModel;
    private Border? _currentFlyoutContent;
    private IFlyoutService? _flyoutManager;
    private FlyoutOverlay? _overlay;

    string IFlyoutSource.FlyoutKey => "audio";
    Control IFlyoutSource.Anchor => BtnAudio;
    bool IFlyoutSource.CanOpen => _viewModel != null;
    Border IFlyoutSource.BuildContent()
    {
        _currentFlyoutContent = BuildAudioFlyoutContent();
        return _currentFlyoutContent;
    }
    void IFlyoutSource.OnDismissed() => _currentFlyoutContent = null;

    private static readonly string[] AudioExtensions = { ".mp3", ".aac", ".flac", ".ogg", ".wav", ".m4a", ".opus" };

    /// <summary>
    /// Fired when an external audio file is dropped onto the button.
    /// MainWindow subscribes to this to show OSD notifications.
    /// </summary>
    public event EventHandler<string>? ExternalFileDropped;

    public AudioTrackSelector()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;

        // Wire drag-drop on the button
        DragDrop.SetAllowDrop(BtnAudio, true);
        BtnAudio.AddHandler(DragDrop.DragOverEvent, OnBtnDragOver);
        BtnAudio.AddHandler(DragDrop.DropEvent, OnBtnDrop);
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        _viewModel = DataContext as MainViewModel;
    }

    /// <summary>Refreshes the audio icon to reflect enabled/disabled state.</summary>
    public void RefreshIcon()
    {
        if (_viewModel == null) return;
        if (AudioIcon == null) return;
        AudioIcon.Kind = _viewModel.IsAudioEnabled
            ? Material.Icons.MaterialIconKind.Music
            : Material.Icons.MaterialIconKind.MusicOff;
    }

    public void SetVisibility(bool visible) => IsVisible = visible;

    /// <summary>Closes the flyout if it is currently open.</summary>
    public void HideFlyout()
    {
        if (_currentFlyoutContent != null)
        {
            _overlay?.HideContent();
            _currentFlyoutContent = null;
        }
    }

    /// <summary>Reopens the flyout (for file dialog reopen cycle).</summary>
    public void ReopenFlyout()
    {
        if (BtnAudio == null) return;
        _overlay ??= MainWindow.GetOverlay(this);
        if (_overlay == null) return;

        _flyoutManager?.ShowFlyoutFor(this, _overlay);
    }

    /// <summary>
    /// Flyout ecosystem manager. Registers this control for mutual exclusion.
    /// </summary>
    public IFlyoutService? FlyoutManager
    {
        get => _flyoutManager;
        set
        {
            _flyoutManager = value;
            value?.Register("audio", () => HideFlyout());
        }
    }

    public IEventBus? EventBus { get; set; }

    private void OnAudioClick(object? sender, RoutedEventArgs e)
    {
        if (_viewModel == null) return;
        _overlay ??= MainWindow.GetOverlay(this);
        if (_overlay == null) return;

        _flyoutManager?.ShowFlyoutFor(this, _overlay);
    }

    private Border BuildAudioFlyoutContent()
    {
        if (_viewModel == null) return new Border();
        var vm = _viewModel;
        return TrackFlyoutBuilder.BuildContent(
            vm.AudioTracks,
            "No audio tracks available",
            "Audio Delay",
            () => vm.AudioDelayValue,
            v => vm.AudioDelayValue = (float)Math.Clamp(v, -10, 10),
            () => vm.ResetAudioDelay(),
            emptyIcon: global::Material.Icons.MaterialIconKind.MusicOff,
            dismissOverlay: () => _flyoutManager?.CloseAll()
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
            return ext != null && AudioExtensions.Contains(ext);
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
            if (ext != null && AudioExtensions.Contains(ext))
            {
                _viewModel?.LoadExternalAudio(path);
                EventBus?.Publish(new ExternalTrackLoadedEvent(path, "Audio"));
                ExternalFileDropped?.Invoke(this, path);
                e.Handled = true;
            }
        }
    }
}
