using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Controls.Primitives;
using Cine.Avalonia.ViewModels;
using Cine.Avalonia.Dialogs;
using Cine.Media.Interfaces;
using MaterialIcon = global::Material.Icons.Avalonia.MaterialIcon;
using MenuItem = Avalonia.Controls.MenuItem;

namespace Cine.Avalonia.Components;

/// <summary>
/// Builds the right-click video context menu with Material icons and selection state.
/// Extracted from MainWindow.Input.cs to keep the window code clean.
/// </summary>
public class VideoContextMenuBuilder
{
    private readonly MainViewModel? _viewModel;
    private readonly IMediaPlayer? _player;
    private readonly Window _window;

    // Captured state at build time — ensures consistency while the menu is open
    private readonly bool _topmost;
    private readonly double _aspectRatio;
    private readonly double _cropValue;
    private readonly double _speedValue;

    public VideoContextMenuBuilder(Window window, MainViewModel? vm, IMediaPlayer? player)
    {
        _window = window;
        _viewModel = vm;
        _player = player;

        _topmost = window.Topmost;
        _aspectRatio = vm?.AspectRatioValue ?? -1;
        _cropValue = vm?.CropValue ?? -1;
        _speedValue = vm?.SpeedValue ?? 1.0;
    }

    public MenuFlyout Build()
    {
        Cine.Core.Log.ForContext<VideoContextMenuBuilder>().Debug("Building video context menu: topmost={Topmost} aspect={Aspect} crop={Crop} speed={Speed}", _topmost, _aspectRatio, _cropValue, _speedValue);
        var menu = new MenuFlyout { Placement = PlacementMode.Pointer };

        // ── Playback ──
        menu.Items.Add(Item("Play / Pause", "Space", () => _viewModel?.PlayPause(), Icon("Play")));
        menu.Items.Add(Item("Stop", "Ctrl+S", () => _viewModel?.Stop(), Icon("Stop")));
        menu.Items.Add(new Separator());

        // ── Navigate ──
        var nav = SubMenu("Navigate", Icon("ChevronRight"));
        nav.Items.Add(Item("Seek Backward", "Left", () => _viewModel?.SeekBackward(), Icon("ChevronDoubleLeft")));
        nav.Items.Add(Item("Seek Forward", "Right", () => _viewModel?.SeekForward(), Icon("ChevronDoubleRight")));
        menu.Items.Add(nav);

        // ── Video ──
        var video = SubMenu("Video", Icon("Filmstrip"));
        video.Items.Add(Item("Fullscreen", "F", () => _viewModel?.ToggleFullscreen(), Icon("Fullscreen")));
        video.Items.Add(Item("Always on Top", null, () => _window.Topmost = !_window.Topmost, Icon("PinOutline"), _topmost));
        video.Items.Add(new Separator());

        // Aspect Ratio — common ratios inline, advanced behind "More…"
        video.Items.Add(Header("ASPECT RATIO"));
        video.Items.Add(SelectItem("Original", () => _viewModel?.SetAspectRatio(-1), _aspectRatio < 0));
        video.Items.Add(SelectItem("16:9", () => _viewModel?.SetAspectRatio(1.7778), Math.Abs(_aspectRatio - 1.7778) < 0.01));
        video.Items.Add(SelectItem("4:3", () => _viewModel?.SetAspectRatio(1.3333), Math.Abs(_aspectRatio - 1.3333) < 0.01));
        var moreAspects = SubMenu("More…", Icon("MenuRight"));
        moreAspects.Items.Add(SelectItem("16:10", () => _viewModel?.SetAspectRatio(1.6), Math.Abs(_aspectRatio - 1.6) < 0.01));
        moreAspects.Items.Add(SelectItem("2.35:1", () => _viewModel?.SetAspectRatio(2.35), Math.Abs(_aspectRatio - 2.35) < 0.01));
        video.Items.Add(moreAspects);
        video.Items.Add(new Separator());

        // Crop — common ratios shown inline, advanced behind "More…"
        video.Items.Add(Header("CROP"));
        video.Items.Add(SelectItem("Off", () => _viewModel?.ResetCrop(), _cropValue < 0));
        video.Items.Add(SelectItem("16:9", () => _viewModel?.SetCrop(1.7778), Math.Abs(_cropValue - 1.7778) < 0.01));
        video.Items.Add(SelectItem("4:3", () => _viewModel?.SetCrop(1.3333), Math.Abs(_cropValue - 1.3333) < 0.01));
        // Progressive disclosure: less common ratios in a submenu
        var moreCrops = SubMenu("More Crops…", Icon("MenuRight"));
        moreCrops.Items.Add(SelectItem("16:10", () => _viewModel?.SetCrop(1.6), Math.Abs(_cropValue - 1.6) < 0.01));
        moreCrops.Items.Add(SelectItem("2.35:1", () => _viewModel?.SetCrop(2.35), Math.Abs(_cropValue - 2.35) < 0.01));
        video.Items.Add(moreCrops);

        menu.Items.Add(video);

        // ── Subtitle ──
        var sub = SubMenu("Subtitle", Icon("Subtitles"));
        sub.Items.Add(Item("Cycle Subtitles", "C", () => _player?.CycleSubtitleTrack(), Icon("Subtitles")));
        menu.Items.Add(sub);

        // ── Speed ──
        var speed = SubMenu("Speed", Icon("Speedometer"));
        speed.Items.Add(SelectItem("0.5×", () => _viewModel?.SetSpeed(0.5), Math.Abs(_speedValue - 0.5) < 0.01));
        speed.Items.Add(SelectItem("1.0× (Normal)", () => _viewModel?.SetSpeed(1.0), Math.Abs(_speedValue - 1.0) < 0.01));
        speed.Items.Add(SelectItem("1.5×", () => _viewModel?.SetSpeed(1.5), Math.Abs(_speedValue - 1.5) < 0.01));
        speed.Items.Add(SelectItem("2.0×", () => _viewModel?.SetSpeed(2.0), Math.Abs(_speedValue - 2.0) < 0.01));
        menu.Items.Add(speed);

        // ── Bottom actions ──
        menu.Items.Add(new Separator());
        menu.Items.Add(Item("Keyboard Shortcuts", null, () => new KeyboardShortcutsDialog().Show(_window), Icon("Keyboard")));

        return menu;
    }

    // ════════════════════════════════════════════════════════════
    //  Helpers
    // ════════════════════════════════════════════════════════════

    private static MaterialIcon Icon(global::Material.Icons.MaterialIconKind kind, double size = 16, IBrush? brush = null)
        => new() { Kind = kind, Width = size, Height = size, Foreground = brush ?? AppColors.TextOnDarkHint };

    private static global::Material.Icons.MaterialIconKind Icon(string name)
    {
        try { return (global::Material.Icons.MaterialIconKind)Enum.Parse(typeof(global::Material.Icons.MaterialIconKind), name); }
        catch
        {
            System.Diagnostics.Debug.WriteLine($"[VideoContextMenuBuilder] Unknown icon: \"{name}\"");
            return global::Material.Icons.MaterialIconKind.CircleOutline;
        }
    }

    private MenuItem Item(string text, string? shortcut, Action action,
        global::Material.Icons.MaterialIconKind? icon = null, bool selected = false)
    {
        var item = new MenuItem
        {
            Header = text,
            InputGesture = shortcut != null ? KeyGesture.Parse(shortcut) : null
        };
        if (selected)
            item.Icon = Icon(global::Material.Icons.MaterialIconKind.CheckCircle, 16, AppColors.Accent);
        else if (icon.HasValue)
            item.Icon = Icon(icon.Value, 16);
        item.Click += (_, _) =>
        {
            Cine.Core.Log.ForContext<VideoContextMenuBuilder>().Debug("Menu item clicked: {Item}", text);
            try { action(); }
            catch (Exception ex) { Cine.Core.Log.ForContext<VideoContextMenuBuilder>().Error(ex, "Menu item action failed: {Item}", text); }
        };
        return item;
    }

    private MenuItem SelectItem(string text, Action action, bool isSelected)
    {
        var item = new MenuItem { Header = text };
        item.Icon = isSelected
            ? Icon(global::Material.Icons.MaterialIconKind.CheckCircle, 16, AppColors.Accent)
            : Icon(global::Material.Icons.MaterialIconKind.CircleOutline, 16);
        item.Click += (_, _) =>
        {
            Cine.Core.Log.ForContext<VideoContextMenuBuilder>().Debug("Menu select clicked: {Item}", text);
            try { action(); }
            catch (Exception ex) { Cine.Core.Log.ForContext<VideoContextMenuBuilder>().Error(ex, "Menu select action failed: {Item}", text); }
        };
        return item;
    }

    private static MenuItem Header(string text)
    {
        var item = new MenuItem { Header = text };
        item.Classes.Add("menu-section-header");
        return item;
    }

    private static MenuItem SubMenu(string text, global::Material.Icons.MaterialIconKind icon)
        => new() { Header = text, Icon = Icon(icon, 16) };
}