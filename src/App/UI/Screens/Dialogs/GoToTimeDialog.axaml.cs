using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Cine.Avalonia.ViewModels;

namespace Cine.Avalonia.Views.Dialogs;

public partial class GoToTimeDialog : Window
{
    public GoToTimeDialog()
    {
        InitializeComponent();
        TimeTextBox.AttachedToVisualTree += (_, _) => TimeTextBox.Focus();
    }

    private void OnGoClick(object? sender, RoutedEventArgs e) => SeekToTime();

    private void OnTimeTextBoxKeyDown(object? sender, global::Avalonia.Input.KeyEventArgs e)
    {
        if (e.Key == global::Avalonia.Input.Key.Enter)
        {
            e.Handled = true;
            SeekToTime();
        }
        else if (e.Key == global::Avalonia.Input.Key.Escape)
        {
            e.Handled = true;
            Close();
        }
    }

    private void SeekToTime()
    {
        var text = TimeTextBox.Text?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            Close();
            return;
        }

        var ts = ParseTime(text);
        if (ts == null)
        {
            // Flash the text box red to indicate invalid input
            TimeTextBox.BorderBrush = global::Avalonia.Media.Brush.Parse("#FFE81123");
            return;
        }

        if (DataContext is MainViewModel vm)
        {
            vm.Position = ts.Value;
        }

        Close();
    }

    private static TimeSpan? ParseTime(string input)
    {
        // Try HH:MM:SS or MM:SS
        var parts = input.Split(':');
        if (parts.Length == 3)
        {
            if (int.TryParse(parts[0], out var h) &&
                int.TryParse(parts[1], out var m) &&
                int.TryParse(parts[2], out var s))
                return new TimeSpan(h, m, s);
        }
        else if (parts.Length == 2)
        {
            if (int.TryParse(parts[0], out var m) &&
                int.TryParse(parts[1], out var s))
                return new TimeSpan(0, m, s);
        }
        else if (parts.Length == 1 && int.TryParse(parts[0], out var sec))
        {
            return TimeSpan.FromSeconds(sec);
        }

        return null;
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();
}
