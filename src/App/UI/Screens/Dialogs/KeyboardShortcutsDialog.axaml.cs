using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Cine.Avalonia.Services;
using Layout = Avalonia.Layout;

namespace Cine.Avalonia.Views.Dialogs;

public partial class KeyboardShortcutsDialog : Window
{
    public KeyboardShortcutsDialog()
    {
        InitializeComponent();
        BuildShortcuts();
    }

    private void BuildShortcuts()
    {
        AddSection("Playback");
        AddShortcut("Space / K", "Play / Pause");
        AddShortcut("F / F11", "Toggle Fullscreen");
        AddShortcut("Escape", "Exit Fullscreen / Close Flyout");
        AddShortcut("M", "Toggle Mute");
        AddShortcut("↑ / ↓", "Volume Up / Down");
        AddShortcut("← / →", "Seek Backward / Forward (5s)");
        AddShortcut("Shift+← / →", "Seek Large (30s)");
        AddShortcut("J / L", "Seek 10s Backward / Forward");
        AddShortcut("[ / ]", "Speed Down / Up");
        AddShortcut("Backspace", "Reset Speed");
        AddShortcut("S", "Screenshot");
        AddShortcut("Shift+S", "Screenshot (no subs)");
        AddShortcut("T", "Toggle Time Elapsed / Remaining");

        AddSection("Navigation");
        AddShortcut("Ctrl+← / →", "Previous / Next Chapter");
        AddShortcut("Ctrl+[ / ]", "Previous / Next Frame");
        AddShortcut("Page Up / Down", "Subtitle Position ±1");

        AddSection("Files & Playlist");
        AddShortcut("Ctrl+O", "Open Files");
        AddShortcut("Ctrl+Shift+O", "Open Folder");
        AddShortcut("Ctrl+Shift+A", "Add Files to Playlist");
        AddShortcut("Ctrl+P", "Toggle Playlist");
        AddShortcut("Ctrl+S", "Stop");
        AddShortcut("Ctrl+L", "Toggle Loop File");
        AddShortcut("Shift+L", "Toggle Loop File");
        AddShortcut("Ctrl+I", "Toggle Loop Playlist");
        AddShortcut("H", "Toggle Shuffle");
        AddShortcut("N", "Next Playlist Item");
        AddShortcut("B", "Previous Playlist Item");

        AddSection("Subtitles & Audio");
        AddShortcut("C", "Cycle Subtitle Track");
        AddShortcut(", / .", "Subtitle Delay – / +");
        AddShortcut("Ctrl++ / –", "Audio Delay + / –");

        AddSection("Video");
        AddShortcut("+ / –", "Zoom In / Out");
        AddShortcut("1 / 2", "Contrast – / +");
        AddShortcut("3 / 4", "Brightness – / +");
        AddShortcut("5 / 6", "Gamma – / +");
        AddShortcut("7 / 8", "Saturation – / +");

        AddSection("System");
        AddShortcut("Ctrl+G", "Go to Time");
        AddShortcut("Ctrl+,", "Open Preferences");
        AddShortcut("Ctrl+Shift+E", "Equalizer");
        AddShortcut("Ctrl+/", "Show Keyboard Shortcuts");
    }

    private void AddSection(string title)
    {
        if (ShortcutsStack.Children.Count > 0)
        {
            ShortcutsStack.Children.Add(new global::Avalonia.Controls.Shapes.Rectangle
            {
                Height = 1,
                Fill = (IBrush?)global::Avalonia.Application.Current?.FindResource("PopoverBorder"),
                Margin = new Thickness(0, 6, 0, 6)
            });
        }

        ShortcutsStack.Children.Add(new TextBlock
        {
            Text = title,
            FontWeight = FontWeight.Bold,
            FontSize = Token.Size("font-size-subtitle1"),
            Foreground = (IBrush?)global::Avalonia.Application.Current?.FindResource("OsdForeground"),
            Margin = new Thickness(0, 12, 0, 8)
        });
    }

    private void AddShortcut(string key, string action)
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions
            {
                new ColumnDefinition(new GridLength(1, GridUnitType.Star)),
                new ColumnDefinition(new GridLength(1, GridUnitType.Star))
            },
            Margin = new Thickness(0, 2),
            Height = 26
        };

        var keyText = new TextBlock
        {
            Text = key,
            FontSize = Token.Size("font-size-body2"),
            FontWeight = FontWeight.Medium,
            Foreground = (IBrush?)global::Avalonia.Application.Current?.FindResource("OsdForeground"),
            VerticalAlignment = Layout.VerticalAlignment.Center
        };
        Grid.SetColumn(keyText, 0);
        grid.Children.Add(keyText);

        var actionText = new TextBlock
        {
            Text = action,
            FontSize = Token.Size("font-size-body2"),
            Foreground = AppColors.TextOnDarkHint,
            VerticalAlignment = Layout.VerticalAlignment.Center,
            HorizontalAlignment = Layout.HorizontalAlignment.Right
        };
        Grid.SetColumn(actionText, 1);
        grid.Children.Add(actionText);

        ShortcutsStack.Children.Add(grid);
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();
}
