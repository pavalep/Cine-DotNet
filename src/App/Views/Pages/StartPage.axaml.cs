using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.VisualTree;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Simba.Avalonia.Core.Navigation;
using Simba.Avalonia.Extensions;
using Simba.Avalonia.ViewModels;
using Simba.Avalonia.ViewModels.Pages;

namespace Simba.Avalonia.Views.Components;

public partial class StartPage : global::Avalonia.Controls.UserControl, INavigable
{
    private CancellationTokenSource? _entranceCts;

    // ── Responsive card dimension properties ───────────────────────
    // These are bound via ElementName from the DataTemplate so that
    // every card item updates in real time during window resize.
    public static readonly StyledProperty<double> CardWidthProperty =
        AvaloniaProperty.Register<StartPage, double>(nameof(CardWidth), 180.0);

    public static readonly StyledProperty<double> ThumbWidthProperty =
        AvaloniaProperty.Register<StartPage, double>(nameof(ThumbWidth), 156.0);

    public static readonly StyledProperty<double> ThumbHeightProperty =
        AvaloniaProperty.Register<StartPage, double>(nameof(ThumbHeight), 97.5);

    public double CardWidth
    {
        get => GetValue(CardWidthProperty);
        set => SetValue(CardWidthProperty, value);
    }

    public double ThumbWidth
    {
        get => GetValue(ThumbWidthProperty);
        set => SetValue(ThumbWidthProperty, value);
    }

    public double ThumbHeight
    {
        get => GetValue(ThumbHeightProperty);
        set => SetValue(ThumbHeightProperty, value);
    }

    public StartPage()
    {
        InitializeComponent();

        // Set initial responsive layout at mid-range (860px equivalent, t = 0.5)
        UpdateResponsiveLayout(860, 860);

        Loaded += OnLoaded;

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
        // Platform-appropriate keyboard hint
        var isMac = System.OperatingSystem.IsMacOS();
        KbdModifierText.Text = isMac ? "\u2318" : "Ctrl";
    }

    /// <summary>
    /// Directly sets sizing properties on all named elements — no DynamicResource needed.
    /// Called from MainWindow.OnSizeChanged on every resize event.
    /// Uses Math.Min(width, height) as the driving dimension (CSS vmin-style).
    /// </summary>
    public void UpdateResponsiveLayout(double width, double height)
    {
        const double minW = 320, maxW = 1400;
        var dim = Math.Min(width, height);
        var clamped = Math.Clamp(dim, minW, maxW);
        var t = (clamped - minW) / (maxW - minW);

        static double Lerp(double min, double max, double t) =>
            min + t * (max - min);

        // Glow orb
        GlowOrb.Width = GlowOrb.Height = Lerp(180, 360, t);

        // Logo
        var logoSize = Lerp(48, 80, t);
        BrandLogo.Width = BrandLogo.Height = logoSize;
        BrandLogoBorder.Width = BrandLogoBorder.Height = logoSize;

        // Wordmark
        WordmarkText.FontSize = Lerp(22, 40, t);
        WordmarkText.LetterSpacing = Lerp(4, 8, t);

        // Tagline
        TaglineText.FontSize = Lerp(11, 16, t);

        // Brand panel spacing
        BrandPanel.Spacing = Lerp(6, 16, t);

        // Buttons
        var btnW = Lerp(150, 220, t);
        var btnH = Lerp(38, 46, t);
        BtnOpenFile.Width = btnW;
        BtnOpenFile.Height = btnH;
        BtnOpenFolder.Width = btnW;
        BtnOpenFolder.Height = btnH;

        // Button content
        var iconSize = Lerp(15, 18, t);
        BtnOpenFileIcon.Width = BtnOpenFileIcon.Height = iconSize;
        BtnOpenFolderIcon.Width = BtnOpenFolderIcon.Height = iconSize;

        var btnFont = Lerp(12, 14, t);
        BtnOpenFileLabel.FontSize = btnFont;
        BtnOpenFolderLabel.FontSize = btnFont;

        // Button panel spacing
        ButtonPanel.Spacing = Lerp(8, 12, t);

        // Recent section header
        RecentHeaderText.FontSize = Lerp(10, 11, t);
        RecentCountText.FontSize = Lerp(10, 11, t);

        // Recent media cards — these are bound via ElementName from the DataTemplate
        CardWidth = Lerp(140, 220, t);
        ThumbWidth = Lerp(120, 196, t);
        ThumbHeight = Lerp(75, 122, t);

        // Keyboard hint
        var kbdFont = Lerp(9, 10, t);
        KbdModifierText.FontSize = kbdFont;
        KbdPlusText.FontSize = kbdFont;
        KbdOLetterText.FontSize = kbdFont;
        KbdToOpenText.FontSize = kbdFont;
    }

    public void Dispose() => _entranceCts?.Cancel();

    // ── Window dragging ────────────────────────────────────────────
    private void OnDragPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            var window = TopLevel.GetTopLevel(this) as Window;
            if (window != null)
                window.BeginMoveDrag(e);
        }
    }

    // ── Drag-drop ──────────────────────────────────────────────────

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

    // ── Public API ─────────────────────────────────────────────────
    /// <summary>
    /// Refreshes the recent files list. Called by MainWindow
    /// when returning to the StartPage (e.g., after closing a video).
    /// </summary>
    public void RefreshRecentList()
    {
        // ItemsRepeater is bound to RecentFileItems via ItemsSource;
        // the ViewModel keeps it synced with RecentFiles automatically.
    }

    // ── INavigable ─────────────────────────────────────────────────
    public void OnNavigatedTo(object? parameter)
    {
        IsVisible = true;
        RefreshRecentList();

        // Cancel any pending entrance from a previous navigation cycle
        _entranceCts?.Cancel();
        _entranceCts = new CancellationTokenSource();
        var token = _entranceCts.Token;

        // Reset opacity → XAML entrance animations replay
        Opacity = 0;
        Dispatcher.UIThread.Post(() =>
        {
            if (!token.IsCancellationRequested && this.IsAttachedToVisualTree())
                Opacity = 1;
        }, DispatcherPriority.Render);
    }

    public void OnNavigatedFrom()
    {
        _entranceCts?.Cancel();
        _entranceCts = null;

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
