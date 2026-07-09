using System;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Cine.Avalonia;
using Cine.Avalonia.Services;
using Cine.Avalonia.Constants;
using Cine.Avalonia.ViewModels;
using Cine.Avalonia.Components;
using Cine.Avalonia.Dialogs;
using Cine.Core;
using Cine.Core.Services;
using Control = Avalonia.Controls.Control;
using DragEventArgs = Avalonia.Input.DragEventArgs;
using DragDropEffects = Avalonia.Input.DragDropEffects;
using AvaloniaLayout = Avalonia.Layout;
using Button = global::Avalonia.Controls.Button;
using Cursor = Avalonia.Input.Cursor;

namespace Cine.Avalonia.Components;

/// <summary>
/// Standalone subtitle overlay layer with its own button + flyout containing
/// subtitle track selection and delay controls. Styling settings have been
/// moved to <see cref="SubtitleSettingsDialog"/> (opened via the gear button).
/// Supports drag-drop of external subtitle files (.srt, .ass, .vtt, .sub, .idx).
/// </summary>
public partial class SubtitleOverlay : AvaloniaUserControl, IFlyoutSource
{
    private Cine.Core.Services.ILogger _log = Cine.Core.Log.ForContext<SubtitleOverlay>();
    private MainViewModel? _viewModel;
    private Border? _currentFlyoutContent; // overlay content (not a Flyout)
    private IFlyoutService? _flyoutManager;
    private FlyoutOverlay? _overlay; // cached window-level overlay

    string IFlyoutSource.FlyoutKey => "subtitle";
    Control IFlyoutSource.Anchor => BtnSubtitles;
    bool IFlyoutSource.CanOpen => _viewModel?.Subtitles != null;
    Border IFlyoutSource.BuildContent()
    {
        _currentFlyoutContent = FlyoutContentBuilder();
        return _currentFlyoutContent;
    }
    void IFlyoutSource.OnDismissed() => _currentFlyoutContent = null;

    private static readonly string[] SubtitleExtensions = { ".srt", ".ass", ".ssa", ".vtt", ".sub", ".idx" };

    /// <summary>
    /// Fired when an external subtitle file is dropped onto the button.
    /// MainWindow subscribes to this to show OSD notifications.
    /// </summary>
    public event EventHandler<string>? ExternalFileDropped;

    public SubtitleOverlay()
    {
        _log = global::Cine.Core.Log.ForContext<SubtitleOverlay>();
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        // Also capture DataContext if already set before handler was attached
        if (DataContext is MainViewModel vm) _viewModel = vm;

        // Wire drag-drop on the button
        DragDrop.SetAllowDrop(BtnSubtitles, true);
        BtnSubtitles.AddHandler(DragDrop.DragOverEvent, OnBtnDragOver);
        BtnSubtitles.AddHandler(DragDrop.DropEvent, OnBtnDrop);
        _log.Debug("Constructor: initialized with drag-drop support");
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        _viewModel = DataContext as MainViewModel;
        _log.Trace("OnDataContextChanged: vm={Vm}", _viewModel != null ? "set" : "null");
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
        if (_currentFlyoutContent != null)
        {
            _log.Trace("HideFlyout: hiding subtitle overlay");
            _overlay?.HideContent();
            _currentFlyoutContent = null;
        }
    }

    /// <summary>Reopens the subtitle flyout on its button (call after dialog completes).</summary>
    public void ReopenFlyout()
    {
        if (BtnSubtitles == null) return;
        _log.Trace("ReopenFlyout: rebuilding and reopening subtitle overlay");
        _overlay ??= MainWindow.GetOverlay(this);
        if (_overlay == null) return;
        if (_viewModel?.Subtitles == null) return;

        _flyoutManager?.ShowFlyoutFor(this, _overlay);
    }

    /// <summary>
    /// Flyout ecosystem manager. Registers this control for mutual exclusion
    /// so that opening another flyout auto-closes this one.
    /// </summary>
    public IFlyoutService? FlyoutManager
    {
        get => _flyoutManager;
        set
        {
            _flyoutManager = value;
            value?.Register("subtitle", () => _overlay?.HideContent());
        }
    }

    private void OnSubtitlesClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (_viewModel?.Subtitles == null)
            {
                _log.Warning("OnSubtitlesClick: _viewModel or Subtitles is null");
                return;
            }
            _log.Debug("OnSubtitlesClick: building and showing subtitle overlay");
            _overlay ??= MainWindow.GetOverlay(this);
            if (_overlay == null) return;

            _flyoutManager?.ShowFlyoutFor(this, _overlay);
        }
        catch (Exception ex)
        {
            _log.Error(ex, "OnSubtitlesClick: exception building/showing overlay");
        }
    }

    private Border FlyoutContentBuilder()
    {
        if (_viewModel?.Subtitles == null) return new Border();

        return TrackFlyoutBuilder.BuildContent(
            tracks: _viewModel.Subtitles.SubtitleTracks,
            "No subtitles available",
            "Subtitle Delay",
            () => _viewModel.Subtitles.SubtitleDelay,
            v => _viewModel.Subtitles.SubtitleDelay = (float)Math.Clamp(v, -10, 10),
            () => _viewModel.Subtitles.SubtitleDelay = 0,
            appendExtra: root => AppendFlyoutFooter(root, _viewModel.Subtitles),
            emptyIcon: global::Material.Icons.MaterialIconKind.ClosedCaptionOutline,
            dismissOverlay: () => _flyoutManager?.CloseAll()
        );
    }

    // ═══════════════════════════════════════════════
    //  Flyout Footer
    // ═══════════════════════════════════════════════

    /// <summary>
    /// Adds a thin separator + gear button at the bottom of the flyout.
    /// The gear button opens the standalone Subtitle Settings dialog.
    /// </summary>
    private void AppendFlyoutFooter(StackPanel root, ISubtitleManager mgr)
    {
        // Thin separator
        root.Children.Add(new Border
        {
            Height = 1,
            Background = GetThemeBrush("PopoverBorder"),
            Margin = new Thickness(8, 4, 8, 4)
        });

        // Gear button → opens SubtitleSettingsDialog
        var gearBtn = new Button
        {
            Content = new TextBlock
            {
                Text = "\u2699 Subtitle Settings\u2026",
                FontSize = Token.Size("font-size-caption"),
                Foreground = AppColors.TextTertiary,
                HorizontalAlignment = AvaloniaLayout.HorizontalAlignment.Center
            },
            Background = AppColors.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(8, 4),
            Cursor = new Cursor(StandardCursorType.Arrow)
        };
        global::Avalonia.Controls.ToolTip.SetTip(gearBtn, "Subtitle settings \u2014 font, size, color, outline, encoding");
        gearBtn.PointerEntered += (_, _) => { if (gearBtn.Content is TextBlock tb) tb.Foreground = AppColors.TextPrimary; };
        gearBtn.PointerExited += (_, _) => { if (gearBtn.Content is TextBlock tb) tb.Foreground = AppColors.TextTertiary; };
        gearBtn.Click += (_, _) =>
        {
            _flyoutManager?.HideFlyout("subtitle", () => _overlay?.HideContent());
            var w = TopLevel.GetTopLevel(this) as Window;
            if (w != null)
                new SubtitleSettingsDialog(mgr).Show(w);
        };
        root.Children.Add(gearBtn);
    }

    /// <summary>
    /// Safely resolves a theme resource brush. Returns null (instead of
    /// throwing InvalidCastException) when the resource key is missing or
    /// its value is not an IBrush.
    /// </summary>
    private static IBrush? GetThemeBrush(string key)
    {
        var result = global::Avalonia.Application.Current?.FindResource(key);
        return result as IBrush;
    }

    // ═══════════════════════════════════════════════
    //  Drag-drop handlers
    // ═══════════════════════════════════════════════

    private void OnBtnDragOver(object? sender, DragEventArgs e)
    {
        if (e.DataTransfer == null || !e.DataTransfer.Contains(DataFormat.File))
        {
            e.DragEffects = DragDropEffects.None;
            ClearDragVisual();
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

        // F11: Visual feedback — accent glow on the button when valid subtitle file is dragged over
        if (hasValidFile)
            ShowDragVisual();
        else
            ClearDragVisual();

        _log.Trace("OnBtnDragOver: validFile={ValidFile}", hasValidFile);
    }

    private void ShowDragVisual()
    {
        BtnSubtitles.Background = new SolidColorBrush(global::Avalonia.Media.Color.FromArgb(0x22, 0x5B, 0xDB, 0xFF)); // faint accent
    }

    private void ClearDragVisual()
    {
        BtnSubtitles.Background = AppColors.Transparent;
    }

    private async void OnBtnDrop(object? sender, DragEventArgs e)
    {
        if (e.DataTransfer == null || !e.DataTransfer.Contains(DataFormat.File))
            return;

        var files = e.DataTransfer.TryGetFiles();
        if (files == null) return;

        try
        {
        foreach (var file in files)
        {
            var path = file.Path.LocalPath;
            var ext = Path.GetExtension(path)?.ToLowerInvariant();
            if (ext != null && SubtitleExtensions.Contains(ext))
            {
                _log.Info("OnBtnDrop: dropping subtitle file {Path}", path);
                if (_viewModel?.Subtitles != null)
                    await _viewModel.Subtitles.LoadExternalSubtitleAsync(path);
                ExternalFileDropped?.Invoke(this, path);
                e.Handled = true;
            }
            else
            {
                _log.Trace("OnBtnDrop: ignored unsupported file {Path} (ext={Ext})", path, ext);
            }
        }
        }
        catch (Exception ex)
        {
            _log.Error(ex, "OnBtnDrop: exception during subtitle drop");
        }
    }
}
