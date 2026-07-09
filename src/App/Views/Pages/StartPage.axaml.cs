using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Cine.Avalonia.Core.Navigation;
using Cine.Avalonia.Extensions;
using Cine.Avalonia.ViewModels;
using Cine.Avalonia.ViewModels.Pages;
using AvColor     = Avalonia.Media.Color;
using AvColors    = Avalonia.Media.Colors;
using AvBrushes   = Avalonia.Media.Brushes;
using AvBrush     = Avalonia.Media.IBrush;
using SolidBrush  = Avalonia.Media.SolidColorBrush;
using LinGrad     = Avalonia.Media.LinearGradientBrush;
using GradStop    = Avalonia.Media.GradientStop;
using AvGeometry  = Avalonia.Media.Geometry;
using AvStretch   = Avalonia.Media.Stretch;
using AvFontWeight= Avalonia.Media.FontWeight;
using AvTrimming  = Avalonia.Media.TextTrimming;
using AvTranslate = Avalonia.Media.TranslateTransform;
using DropShadow  = Avalonia.Media.DropShadowEffect;
using ShapesPath  = Avalonia.Controls.Shapes.Path;
using RelPoint    = Avalonia.RelativePoint;
using RelUnit     = Avalonia.RelativeUnit;

namespace Cine.Avalonia.Views.Components;

public partial class StartPage : global::Avalonia.Controls.UserControl, INavigable
{
    // ── Icon geometry ──────────────────────────────────────────────
    private static readonly AvGeometry PlayIcon  = AvGeometry.Parse("M8 5v14l11-7z");
    private static readonly AvGeometry MusicIcon = AvGeometry.Parse(
        "M12 3v10.55c-.59-.34-1.27-.55-2-.55-2.21 0-4 1.79-4 4s1.79 4 4 4 4-1.79 4-4V7h4V3h-6z");

    // Brushes resolved once on Loaded
    private AvBrush? _accentBrush;
    private AvBrush? _glassEdgeBrush;
    private AvBrush? _cardBgBrush;
    private AvBrush? _cardBorderBrush;
    private AvBrush? _accentBorderBrush;
    private AvBrush? _textPrimaryBrush;
    private AvBrush? _textHintBrush;

    // ── Responsive layout state ──
    private enum   LayoutMode { LargeDesktop, Wide, Tablet, Narrow }
    private const  double NarrowBp = 768;
    private const  double TabletBp = 1024;
    private const  double LargeBp  = 1600;
    private LayoutMode _layoutMode = LayoutMode.Wide;

    // Fluid sizes updated per-breakpoint (matching HTML clamp() values)
    private double _wordmarkFs   = 32;
    private double _taglineFs    = 14;
    private double _panelPadding = 28;
    private double _cardWidth    = 180;

    public StartPage()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Loaded += OnLoaded;
        SizeChanged += OnSizeChanged;

        // Global drag visual wiring
        AddHandler(DragDrop.DragEnterEvent, OnGlobalDragEnter, handledEventsToo: true);
        AddHandler(DragDrop.DragOverEvent,  OnGlobalDragOver,  handledEventsToo: true);
        AddHandler(DragDrop.DragLeaveEvent, OnGlobalDragLeave, handledEventsToo: true);
        AddHandler(DragDrop.DropEvent,      OnGlobalDrop,      handledEventsToo: true);
        DragDrop.SetAllowDrop(this, true);
        KeyDown += OnPageKeyDown;
    }

    private void OnLoaded(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        _accentBrush       = TryGetBrush("StartAccent",       new SolidBrush(AvColor.FromArgb(255, 201, 169, 110)));
        _glassEdgeBrush    = TryGetBrush("StartGlassBorder",  new SolidBrush(AvColor.FromArgb(13,  255, 255, 255)));
        _cardBgBrush       = TryGetBrush("CardGlassBg",       new SolidBrush(AvColor.FromArgb(22,  255, 255, 255)));
        _cardBorderBrush   = TryGetBrush("CardGlassBorder",   new SolidBrush(AvColor.FromArgb(25,  255, 255, 255)));
        _accentBorderBrush = TryGetBrush("StartAccentBorder", new SolidBrush(AvColor.FromArgb(51,  201, 169, 110)));
        _textPrimaryBrush  = TryGetBrush("AppTextPrimary",    new SolidBrush(AvColor.FromArgb(204, 255, 255, 255)));
        _textHintBrush     = TryGetBrush("AppTextOnDarkHint", new SolidBrush(AvColor.FromArgb(153, 255, 255, 255)));

        // Apply initial layout based on current width
        ApplyLayout(Bounds.Width);

        if (DataContext is StartPageViewModel vm)
            RebuildRecentFiles(vm);

        // Platform-appropriate keyboard hint
        var isMac = System.OperatingSystem.IsMacOS();
        KbdModifierText.Text = isMac ? "⌘" : "Ctrl";
    }

    // ── Responsive layout ──────────────────────────────────────────
    private void OnSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        ApplyLayout(e.NewSize.Width);
    }

    private void ApplyLayout(double width)
    {
        // Determine layout mode matching HTML breakpoints
        LayoutMode mode = width >= LargeBp ? LayoutMode.LargeDesktop
                        : width >  TabletBp ? LayoutMode.Wide
                        : width >= NarrowBp ? LayoutMode.Tablet
                        :                     LayoutMode.Narrow;

        // Only apply mode-switch changes (visibility, glow orb) when mode actually changes
        if (mode != _layoutMode)
        {
            _layoutMode = mode;

            // Show/hide layouts
            bool isNarrowOrTablet = mode is LayoutMode.Narrow or LayoutMode.Tablet;
            WideLayout.IsVisible     = !isNarrowOrTablet;
            RecentSection.IsVisible  = !isNarrowOrTablet;
            NarrowLayout.IsVisible   =  isNarrowOrTablet;

            // Kbd hint visible only on Wide / LargeDesktop
            KbdHint.IsVisible = mode is LayoutMode.Wide or LayoutMode.LargeDesktop;

            // Glow orb responsive size (HTML: 500→300→200)
            if (GlowOrb != null)
            {
                double orbSize = isNarrowOrTablet ? (mode == LayoutMode.Narrow ? 200 : 300) : 500;
                GlowOrb.Width  = orbSize;
                GlowOrb.Height = orbSize;
                // Reposition for smaller sizes
                GlowOrb.Margin = new Thickness(0, isNarrowOrTablet ? -40 : -100,
                                                  isNarrowOrTablet ? -60 : -150, 0);
            }

            // Rebuild cards when layout mode changes (different card sizes per mode)
            if (DataContext is StartPageViewModel vm1)
                RebuildRecentFiles(vm1);
        }

        // Update fluid sizes on EVERY resize (these change continuously within a mode)
        UpdateFluidSizes(width);

        // Panel MaxWidth per breakpoint — applies on every resize so sub-thresholds (e.g. 1200px in Wide) take effect
        switch (_layoutMode)
        {
            case LayoutMode.LargeDesktop:
                if (GlassPanelWrapper != null) GlassPanelWrapper.MaxWidth = 560;
                if (NarrowLayout != null)      NarrowLayout.MaxWidth       = 560;
                break;
            case LayoutMode.Wide:
                if (GlassPanelWrapper != null)
                    GlassPanelWrapper.MaxWidth = width <= 1200 ? 420 : 500;
                break;
            case LayoutMode.Tablet:
                if (NarrowLayout != null) NarrowLayout.MaxWidth = 460;
                break;
            case LayoutMode.Narrow:
                if (NarrowLayout != null) NarrowLayout.MaxWidth = double.PositiveInfinity;
                break;
        }

        // Rebuild cards if fluid card width changed (handles 1200px sub-threshold in Wide mode)
        if (DataContext is StartPageViewModel vm)
            RebuildRecentFiles(vm);
    }

    private void UpdateFluidSizes(double width)
    {
        // Wordmark: clamp(28px, 3.5vw, 52px)
        _wordmarkFs = Math.Clamp(width * 0.035, 28.0, 52.0);
        // Tagline: clamp(14px, 1.2vw, 18px)
        _taglineFs = Math.Clamp(width * 0.012, 14.0, 18.0);

        // Panel inner padding for glass panel grid: clamp(28px, 3vw, 40px)
        _panelPadding = Math.Clamp(width * 0.03, 28.0, 40.0);

        // Per-mode overrides (HTML breakpoints)
        switch (_layoutMode)
        {
            case LayoutMode.LargeDesktop:
                _cardWidth = 200;
                break;
            case LayoutMode.Wide:
                _cardWidth = 180;
                // At ≤1200px HTML shrinks cards to 160px
                if (width <= 1200) _cardWidth = 160;
                break;
            case LayoutMode.Tablet:
                _cardWidth = 160;
                break;
            case LayoutMode.Narrow:
                _cardWidth = 140;
                break;
        }

        // Push fluid values into XAML resources so bindings pick them up
        if (Resources.TryGetResource("WordmarkFontSize", null, out object? _))
            Resources["WordmarkFontSize"] = _wordmarkFs;
        if (Resources.TryGetResource("TaglineFontSize", null, out object? _))
            Resources["TaglineFontSize"] = _taglineFs;

        // Fluid panel padding: clamp(28px, 3vw, 40px)
        var pad = new Thickness(_panelPadding, Math.Min(_panelPadding + 4, 48), _panelPadding, _panelPadding);
        if (GlassPanelGrid != null)       GlassPanelGrid.Margin       = pad;
        if (GlassPanelGridNarrow != null) GlassPanelGridNarrow.Margin = pad;
    }

    // ── DataContext / Collection ────────────────────────────────────
    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is StartPageViewModel vm)
        {
            vm.RecentFiles.CollectionChanged += OnRecentFilesChanged;
            RebuildRecentFiles(vm);
        }
    }

    private void OnRecentFilesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (DataContext is StartPageViewModel vm)
            RebuildRecentFiles(vm);
    }

    private double _lastCardWidth;   // avoid unnecessary card rebuilds

    private void RebuildRecentFiles(StartPageViewModel vm)
    {
        var countText = $"{vm.RecentFiles.Count}";

        // Only rebuild cards when width changes enough to affect card sizes
        bool needRebuild = Math.Abs(_cardWidth - _lastCardWidth) > 1;
        _lastCardWidth = _cardWidth;

        // Wide layout
        RecentCountText.Text = countText;
        if (needRebuild)
        {
            RecentTracks.Children.Clear();
            for (int i = 0; i < vm.RecentFiles.Count; i++)
                RecentTracks.Children.Add(CreateRecentCard(vm.RecentFiles[i], vm, i));
        }

        // Narrow layout
        RecentCountTextNarrow.Text = countText;
        if (needRebuild)
        {
            RecentTracksNarrow.Children.Clear();
            for (int i = 0; i < vm.RecentFiles.Count; i++)
                RecentTracksNarrow.Children.Add(CreateRecentCard(vm.RecentFiles[i], vm, i));
        }

        // Empty state visibility
        bool hasCards = vm.RecentFiles.Count > 0;
        if (EmptyStateText != null)       EmptyStateText.IsVisible       = !hasCards;
        if (EmptyStateTextNarrow != null) EmptyStateTextNarrow.IsVisible = !hasCards;
        if (RecentTracks != null)         RecentTracks.IsVisible         =  hasCards;
        if (RecentTracksNarrow != null)   RecentTracksNarrow.IsVisible   =  hasCards;
    }


    // ── Card factory ───────────────────────────────────────────────
    private Border CreateRecentCard(string filePath, StartPageViewModel vm, int index)
    {
        var fileName = System.IO.Path.GetFileName(filePath);
        var ext      = System.IO.Path.GetExtension(filePath).ToLowerInvariant();
        var isVideo = vm.MediaFileService.IsVideoFile(filePath);

        // Clean title: filename without extension
        var title = System.IO.Path.GetFileNameWithoutExtension(filePath);
        var extUpper = ext.TrimStart('.').ToUpperInvariant();

        // Thumb icon (play or music shape)
        var icon = new ShapesPath
        {
            Data    = isVideo ? PlayIcon : MusicIcon,
            Width   = 24,
            Height  = 24,
            Stretch = AvStretch.Uniform,
            Opacity = 0.65,
            Fill    = new SolidBrush(AvColors.White),
            Effect  = new DropShadow
            {
                BlurRadius = 8,
                OffsetX    = 0,
                OffsetY    = 2,
                Color      = AvColors.Black,
                Opacity    = 0.5
            }
        };

        // Thumb area — gradient mimicking a dark frosted cover tile
        var thumbBg = new LinGrad
        {
            StartPoint = new RelPoint(0, 0, RelUnit.Relative),
            EndPoint   = new RelPoint(1, 1, RelUnit.Relative),
            GradientStops =
            {
                new GradStop(AvColor.FromArgb(30, 255, 255, 255), 0),
                new GradStop(AvColor.FromArgb(8,  255, 255, 255), 1),
            }
        };

        var thumbBorder = new Border
        {
            Width        = _cardWidth - 24,
            Height       = (_cardWidth - 24) / 16.0 * 10.0,
            CornerRadius = new CornerRadius(8),
            Background   = thumbBg,
            ClipToBounds = true,
            Child = new Panel
            {
                Children =
                {
                    // Dark overlay for legibility
                    new Border { Background = new SolidBrush(AvColor.FromArgb(10, 0, 0, 0)), IsHitTestVisible = false },
                    // Icon centred
                    new Panel
                    {
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment   = VerticalAlignment.Center,
                        Children            = { icon }
                    }
                }
            }
        };

        // Title — clean name without extension
        var nameBlock = new TextBlock
        {
            Text         = title,
            FontSize     = 12,
            FontWeight   = AvFontWeight.SemiBold,
            Foreground   = _textPrimaryBrush,
            TextTrimming = AvTrimming.CharacterEllipsis,
            MaxWidth     = _cardWidth - 24,
            Margin       = new Thickness(0, 7, 0, 2)
        };

        // Meta row: type badge + extension pill
        var metaRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing     = 4,
            Children =
            {
                // Type badge
                new Border
                {
                    CornerRadius    = new CornerRadius(3),
                    Background      = new SolidBrush(AvColor.FromArgb(20, 255, 255, 255)),
                    Padding         = new Thickness(4, 1, 4, 1),
                    Child = new TextBlock
                    {
                        Text       = isVideo ? "Video" : "Audio",
                        FontSize   = 9,
                        FontWeight = AvFontWeight.Medium,
                        Foreground = _textHintBrush
                    }
                },
                // Extension pill
                new Border
                {
                    CornerRadius    = new CornerRadius(3),
                    Background      = new SolidBrush(isVideo
                        ? AvColor.FromArgb(25, 201, 169, 110)
                        : AvColor.FromArgb(25, 110, 201, 169)),
                    Padding         = new Thickness(4, 1, 4, 1),
                    Child = new TextBlock
                    {
                        Text       = extUpper,
                        FontSize   = 9,
                        FontWeight = AvFontWeight.Medium,
                        Foreground = _textHintBrush
                    }
                }
            }
        };

        // Card shell
        var card = new Border
        {
            Width           = _cardWidth,
            Padding         = new Thickness(12),
            CornerRadius    = new CornerRadius(12),
            Background      = _cardBgBrush,
            BorderBrush     = _cardBorderBrush,
            BorderThickness = new Thickness(0.5),
            ClipToBounds    = true,
            Cursor          = new Cursor(StandardCursorType.Hand),
            Child           = new StackPanel { Children = { thumbBorder, nameBlock, metaRow } },
            RenderTransformOrigin = new RelPoint(0.5, 0.5, RelUnit.Relative),
            RenderTransform = new AvTranslate(0, 0),
            Opacity         = 0,
        };

        card.Transitions = new Transitions
        {
            new TransformOperationsTransition
            {
                Property = Border.RenderTransformProperty,
                Duration = TimeSpan.FromMilliseconds(300),
                Easing   = new CubicEaseOut()
            },
            new DoubleTransition
            {
                Property = Border.OpacityProperty,
                Duration = TimeSpan.FromMilliseconds(300),
                Easing   = new CubicEaseOut()
            }
        };

        // Hover lift
        card.PointerEntered += (_, _) =>
        {
            card.RenderTransform = new AvTranslate(0, -4);
            card.BorderBrush     = _accentBorderBrush;
        };
        card.PointerExited += (_, _) =>
        {
            card.RenderTransform = new AvTranslate(0, 0);
            card.BorderBrush     = _cardBorderBrush;
        };

        card.PointerPressed += (_, _) => vm.OpenRecentFile(filePath);

        // Stagger fade-in
        var staggerMs = 600 + index * 60;
        var timer = new System.Threading.Timer(_ =>
            global::Avalonia.Threading.Dispatcher.UIThread.Post(() => card.Opacity = 1),
            null, staggerMs, System.Threading.Timeout.Infinite);
        // Keep timer alive until fired (prevent GC)
        card.Tag = timer;

        return card;
    }


    private void OnGlobalDragEnter(object? sender, DragEventArgs e)
    {
        // Drag-drop is handled silently — no visual feedback
    }

    private void OnGlobalDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = e.DataTransfer != null && e.DataTransfer.Contains(DataFormat.File)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnGlobalDragLeave(object? sender, DragEventArgs e)
    {
        // No visual state to reset
    }

    private async void OnGlobalDrop(object? sender, DragEventArgs e)
    {
        // Process the dropped files through the ViewModel
        var files = e.DataTransfer.TryGetFiles();
        if (files == null) return;

        var paths = new List<string>();
        foreach (var item in files)
        {
            var path = item.TryGetLocalPath();
            if (!string.IsNullOrEmpty(path))
                paths.Add(path);
        }

        if (paths.Count > 0 && DataContext is MainViewModel vm)
            await vm.OpenDroppedFilesAsync(paths.ToArray());
    }

    // ── Resource helper ────────────────────────────────────────────
    private AvBrush? TryGetBrush(string key, AvBrush? fallback)
    {
        if (this.TryFindResource(key, out var value) && value is AvBrush brush)
            return brush;
        return fallback;
    }

    // ── Public API ─────────────────────────────────────────────────
    /// <summary>
    /// Forces a rebuild of the recent files list. Called by MainWindow
    /// when returning to the StartPage (e.g., after closing a video).
    /// </summary>
    public void RefreshRecentList()
    {
        // Reset card-width tracking so RebuildRecentFiles always performs a full rebuild
        _lastCardWidth = -1;
        if (DataContext is StartPageViewModel vm)
            RebuildRecentFiles(vm);
    }

    // ── INavigable ─────────────────────────────────────────────────
    /// <summary>
    /// Called when the StartPage becomes the active navigation target.
    /// Refreshes recent files and triggers the entrance fade animation.
    /// </summary>
    public void OnNavigatedTo(object? parameter)
    {
        IsVisible = true;
        RefreshRecentList();

        // Trigger entrance animation: fade from transparent
        Opacity = 0;
        Dispatcher.UIThread.Post(() =>
        {
            if (this.IsAttachedToVisualTree())
                Opacity = 1;
        }, DispatcherPriority.Render);
    }

    /// <summary>
    /// Called when the StartPage is no longer the active navigation target.
    /// Cancels drag state, resets visual properties, and fades out.
    /// </summary>
    public void OnNavigatedFrom()
    {
        // Graceful fade-out
        Opacity = 0;
        _ = Task.Run(async () =>
        {
            await Task.Delay(350);
            await Dispatcher.UIThread.OnUiThreadAsync(() =>
            {
                IsVisible = false;
            });
        });
    }

    // ── Button handlers ────────────────────────────────────────────
    private void BtnOpenFile_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
            vm.OpenFilesCommand.Execute(null);
    }

    private void BtnOpenFolder_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
            vm.OpenFolderCommand.Execute(null);
    }

    private async void OnPageKeyDown(object? sender, KeyEventArgs e)
    {
        if ((e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Meta)) != 0 && e.Key == Key.O)
        {
            e.Handled = true;
            if (DataContext is StartPageViewModel vm)
                vm.OpenFiles();
        }
    }

    // ── Window Controls ────────────────────────────────────────────

    private Window? GetParentWindow() => TopLevel.GetTopLevel(this) as Window;

    private void OnStartMinimizeClick(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        GetParentWindow()!.WindowState = WindowState.Minimized;
    }

    private void OnStartMaximizeRestoreClick(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        var w = GetParentWindow();
        if (w == null) return;
        w.WindowState = w.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    private void OnStartCloseClick(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        GetParentWindow()?.Close();
    }

    /// <summary>
    /// Updates the maximize/restore icon to match the current window state.
    /// Called from MainWindow when window state changes.
    /// </summary>
    public void UpdateMaximizeIcon(bool isMaximized)
    {
        var kind = isMaximized ? Material.Icons.MaterialIconKind.WindowRestore : Material.Icons.MaterialIconKind.WindowMaximize;
        if (StartMaximizeRestoreIcon != null)
            StartMaximizeRestoreIcon.Kind = kind;
    }
}
