using Cine.Avalonia.ViewModels;

namespace Cine.Avalonia;

public partial class MainWindow
{
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
                ShowOsdNotification("PIP failed to start");
        }
    }
}
