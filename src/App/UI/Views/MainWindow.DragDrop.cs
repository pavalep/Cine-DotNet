using System;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Cine.Avalonia.Controls;

namespace Cine.Avalonia;

public partial class MainWindow
{
    private bool _isDropIndicatorVisible = false;

    private void OnWindowDragEnter(object? sender, global::Avalonia.Input.DragEventArgs e)
    {
        if (e.DataTransfer != null && e.DataTransfer.Contains(global::Avalonia.Input.DataFormat.File))
        {
            e.DragEffects = global::Avalonia.Input.DragDropEffects.Copy;
            
            var sp = this.FindControl<StartPage>("StartPage");
            if (sp != null && sp.IsVisible)
            {
                var dt = sp.FindControl<global::Avalonia.Controls.Border>("DropTarget");
                if (dt != null)
                {
                    dt.BorderBrush = new global::Avalonia.Media.SolidColorBrush(
                        global::Avalonia.Media.Color.FromArgb(0xFF, 0x00, 0x78, 0xD7));
                    dt.Background = new global::Avalonia.Media.SolidColorBrush(
                        global::Avalonia.Media.Color.FromArgb(0x40, 0x00, 0x78, 0xD7));
                }
            }

            UpdateDropIndicator(e, show: true);
        }
        else
        {
            e.DragEffects = global::Avalonia.Input.DragDropEffects.None;
            UpdateDropIndicator(e, show: false);
        }
    }

    private void OnWindowDragLeave(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        ResetStartPageDragVisuals();
        UpdateDropIndicator(null, show: false);
    }

    private void OnWindowDrop(object? sender, global::Avalonia.Input.DragEventArgs e)
    {
        ResetStartPageDragVisuals();
        UpdateDropIndicator(null, show: false);

        if (e.DataTransfer != null && e.DataTransfer.Contains(global::Avalonia.Input.DataFormat.File))
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
        var dt = sp.FindControl<global::Avalonia.Controls.Border>("DropTarget");
        if (dt != null)
        {
            dt.BorderBrush = new global::Avalonia.Media.SolidColorBrush(
                global::Avalonia.Media.Color.FromArgb(0x40, 0xFF, 0xFF, 0xFF));
            dt.Background = new global::Avalonia.Media.SolidColorBrush(
                global::Avalonia.Media.Color.FromArgb(0x20, 0xFF, 0xFF, 0xFF));
        }
    }

    private async void UpdateDropIndicator(global::Avalonia.Input.DragEventArgs? e, bool show)
    {
        if (DropIndicatorOverlay == null || DropIndicatorText == null || DropIndicatorIcon == null)
            return;

        if (show == _isDropIndicatorVisible) return;
        _isDropIndicatorVisible = show;

        if (show)
        {
            bool subtitleDrop = false;
            try
            {
                var files = e?.DataTransfer?.TryGetFiles();
                var first = files?.FirstOrDefault()?.Path.LocalPath;
                if (!string.IsNullOrWhiteSpace(first))
                {
                    var ext = Path.GetExtension(first).ToLowerInvariant();
                    subtitleDrop = ext is ".srt" or ".ass" or ".ssa" or ".vtt" or ".sub" or ".idx";
                }
            }
            catch { }

            if (subtitleDrop && !string.IsNullOrWhiteSpace(_viewModel?.FilePath))
            {
                DropIndicatorText.Text = "Add Subtitle Track";
                TrySetIcon(DropIndicatorIcon, "SubtitlesIcon");
            }
            else
            {
                DropIndicatorText.Text = "Play";
                TrySetIcon(DropIndicatorIcon, "PlayIcon");
            }

            DropIndicatorOverlay.IsVisible = true;
            await FadeVisual(DropIndicatorOverlay, DropIndicatorOverlay.Opacity, 1, 200, true);
        }
        else
        {
            await FadeVisual(DropIndicatorOverlay, DropIndicatorOverlay.Opacity, 0, 200, false);
            if (!_isDropIndicatorVisible)
                DropIndicatorOverlay.IsVisible = false;
        }
    }
}
