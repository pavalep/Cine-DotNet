using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Cine.Avalonia.Services;
using Cine.Avalonia.ViewModels;
using KeyEventArgs = Avalonia.Input.KeyEventArgs;

namespace Cine.Avalonia.Views.Dialogs;

public partial class GoToTimeDialog : Window
{
    public GoToTimeDialog()
    {
        InitializeComponent();
        TimeTextBox.AttachedToVisualTree += (_, _) => 
        {
            TimeTextBox.Focus();
            EnsureCentered();
        };
    }

    private void EnsureCentered()
    {
        // Ensure explicit centering in case CenterOwner doesn't work properly in fullscreen
        var owner = TopLevel.GetTopLevel(this);
        if (owner is Window w)
        {
            PositionX = w.Position.X + (w.Bounds.Width - Width) / 2;
            PositionY = w.Position.Y + (w.Bounds.Height - Height) / 2;
        }
    }

    private void OnGoClick(object? sender, RoutedEventArgs e) => SeekToTime();

    private void OnTimeTextBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            SeekToTime();
        }
        else if (e.Key == Key.Escape)
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

        var ts = TimeParsingUtility.TryParseTime(text);
        if (ts == null)
        {
            TimeTextBox.BorderBrush = global::Avalonia.Media.Brush.Parse("#FFE81123");
            return;
        }

        if (DataContext is MainViewModel vm)
            vm.Position = ts.Value;

        Close();
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();
}
