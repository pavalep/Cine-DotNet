using System;
using Avalonia.Threading;
using Cine.Avalonia.Helpers;
using Cine.Avalonia.ViewModels;

namespace Cine.Avalonia;

public partial class MainWindow
{
    private void InitPipHandlers()
    {
        if (_pipService == null) return;
        _headerBar.PipToggled += OnPipToggled;

        _pipService.PipOpened += (_, _) =>
        {
            Dispatcher.UIThread.OnUiThread(() =>
            {
                _playerService?.Player?.Pause();
                if (_videoHost != null) _videoHost.IsVideoSurfaceVisible = false;
                _headerBar.SetPipChecked(true);
                ShowOsdNotification("PIP mode active");
            });
        };

        _pipService.PipClosed += (_, _) =>
        {
            Dispatcher.UIThread.OnUiThread(() =>
            {
                _headerBar.SetPipChecked(false);
                if (_videoHost != null) _videoHost.IsVideoSurfaceVisible = true;
                _playerService?.Player?.Play();
                ShowOsdNotification("PIP closed");
            });
        };

        _pipService.PipError += (_, error) =>
        {
            Dispatcher.UIThread.OnUiThread(() =>
            {
                ShowOsdNotification(error, 4000);
            });
        };

        // Sync file path when media changes
        _viewModel!.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.FilePath) && _viewModel != null)
            {
                _pipService.SetCurrentFilePath(_viewModel.FilePath);
            }
        };
    }

    private void OnPipToggled(object? sender, EventArgs e)
    {
        if (_pipService == null) return;

        if (_pipService.IsActive)
        {
            _pipService.ExitPip();
        }
        else
        {
            if (_viewModel == null || string.IsNullOrEmpty(_viewModel.FilePath))
            {
                ShowOsdNotification("No media loaded");
                return;
            }

            _pipService.Initialize(_playerService!.Player!);
            _pipService.SetCurrentFilePath(_viewModel.FilePath);
            var pipWindow = _pipService.EnterPip();

            if (pipWindow == null)
            {
                ShowOsdNotification("PIP failed to start");
            }
        }
    }
}
