using System;
using System.IO;
using Cine.Avalonia.Controls;
using Cine.Avalonia.Views.Dialogs;
using Cine.Media.Interfaces;
using RoutedEventArgs = Avalonia.Interactivity.RoutedEventArgs;

namespace Cine.Avalonia;

public partial class MainWindow
{
    private void InitPipHandlers()
    {
        _headerBar.PipToggled += OnPipToggled;
    }

    private void OnPipToggled(object? sender, EventArgs e)
    {
        OnTogglePip(sender, e);
    }

    private void OnTogglePip(object? sender, EventArgs e)
    {
        if (_isPipMode)
        {
            _pipWindow?.Close();
            _pipWindow = null;
            _pipPlayer = null;
            _isPipMode = false;
            _headerBar.SetPipChecked(false);
            ShowOsdNotification("PIP closed");
        }
        else
        {
            if (_viewModel == null || string.IsNullOrEmpty(_viewModel.FilePath))
            {
                ShowOsdNotification("No media loaded");
                return;
            }

            try
            {
                _pipPlayer = _playerService!.CreateSecondaryPlayer();
                _pipWindow = new PipWindow(_pipPlayer, _playerService.Player!, _viewModel!.FilePath!)
                {
                    DataContext = _viewModel
                };

                _playerService.Player?.Pause();
                if (_videoHost != null) _videoHost.IsVideoSurfaceVisible = false;

                if (_viewModel != null)
                {
                    _viewModel.NotifyPipSync = () =>
                    {
                        if (_pipWindow is PipWindow pw)
                            pw.SyncFromMain();
                    };
                }

                _pipWindow.Closed += (s, args) =>
                {
                    _pipWindow = null;
                    _pipPlayer = null;
                    _isPipMode = false;
                    _headerBar.SetPipChecked(false);
                    if (_viewModel != null) _viewModel.NotifyPipSync = null;
                    if (_videoHost != null) _videoHost.IsVideoSurfaceVisible = true;
                    _playerService?.Player?.Play();
                };

                _pipWindow.Show(this);
                _isPipMode = true;
                _headerBar.SetPipChecked(true);
                ShowOsdNotification("PIP mode active");
            }
            catch (Exception ex)
            {
                _pipWindow = null;
                _pipPlayer = null;
                _isPipMode = false;
                ShowOsdNotification($"PIP failed: {ex.Message}");
            }
        }
    }
}
