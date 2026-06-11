using System;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Cine.Avalonia.Controls;
using DragEventArgs = Avalonia.Input.DragEventArgs;
using RoutedEventArgs = Avalonia.Interactivity.RoutedEventArgs;
using DragDropEffects = Avalonia.Input.DragDropEffects;

namespace Cine.Avalonia;

public partial class MainWindow
{
    private void OnWindowDragEnter(object? sender, DragEventArgs e)
    {
        if (e.DataTransfer != null && e.DataTransfer.Contains(DataFormat.File))
        {
            e.DragEffects = DragDropEffects.Copy;

            var sp = this.FindControl<StartPage>("StartPage");
            if (sp != null && sp.IsVisible)
            {
                var dt = sp.FindControl<Border>("DropTarget");
                if (dt != null)
                {
                    dt.BorderBrush = AppColors.DragAccent;
                    dt.Background = AppColors.DragAccentDim;
                }
            }

            _ = _dropIndicator.Show();
        }
        else
        {
            e.DragEffects = DragDropEffects.None;
        }
    }

    private void OnWindowDragLeave(object? sender, RoutedEventArgs e)
    {
        ResetStartPageDragVisuals();
        _ = _dropIndicator.Hide();
    }

    private void OnWindowDrop(object? sender, DragEventArgs e)
    {
        ResetStartPageDragVisuals();

        if (_dropIndicator.IsShowing)
            _ = _dropIndicator.Hide();

        if (e.DataTransfer != null && e.DataTransfer.Contains(DataFormat.File))
        {
            var files = e.DataTransfer.TryGetFiles();
            if (files != null)
            {
                var paths = files.Select(f => f.Path.LocalPath).ToArray();
                var videoFiles = StartPage.FilterVideoFiles(paths).ToList();
                var subtitleFiles = paths.Where(f =>
                    f.EndsWith(".srt", StringComparison.OrdinalIgnoreCase) ||
                    f.EndsWith(".ass", StringComparison.OrdinalIgnoreCase) ||
                    f.EndsWith(".vtt", StringComparison.OrdinalIgnoreCase)).ToList();

                if (videoFiles.Any())
                    _viewModel?.OpenFiles(videoFiles.ToArray());

                if (subtitleFiles.Any() && _viewModel != null && !string.IsNullOrEmpty(_viewModel.FilePath))
                {
                    foreach (var subFile in subtitleFiles)
                        _playerService?.Player?.AddSubtitle(subFile);
                }
            }
        }
    }

    private void ResetStartPageDragVisuals()
    {
        var sp = this.FindControl<StartPage>("StartPage");
        if (sp == null) return;
        var dt = sp.FindControl<Border>("DropTarget");
        if (dt != null)
        {
            dt.BorderBrush = AppColors.BorderLight;
            dt.Background = AppColors.BorderDim;
        }
    }
}
