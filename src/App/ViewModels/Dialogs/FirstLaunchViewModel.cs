using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Cine.Avalonia.Services;
using Cine.Avalonia.Utilities;
using Cine.Core;

namespace Cine.Avalonia.ViewModels.Dialogs;

public class DownloadItem : INotifyPropertyChanged
{
    private string _status = "Pending";
    public string FileName { get; set; } = "";
    public string Status
    {
        get => _status;
        set { _status = value; OnPropertyChanged(); }
    }
    public event PropertyChangedEventHandler? PropertyChanged;
    public void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public class FirstLaunchViewModel : INotifyPropertyChanged
{
    private double _overallProgress;
    private string _statusText = "Preparing download...";
    private string _buttonText = "Downloading...";
    private bool _isDownloading = true;
    private bool _isComplete;
    private readonly CancellationTokenSource _cts = new();

    public ObservableCollection<DownloadItem> Downloads { get; } = new();
    public ICommand DownloadCommand { get; }
    public event Action? DownloadComplete;
    public event PropertyChangedEventHandler? PropertyChanged;

    public double OverallProgress
    {
        get => _overallProgress;
        set { _overallProgress = value; OnPropertyChanged(); }
    }

    public string StatusText
    {
        get => _statusText;
        set { _statusText = value; OnPropertyChanged(); }
    }

    public string ButtonText
    {
        get => _buttonText;
        set { _buttonText = value; OnPropertyChanged(); }
    }

    public FirstLaunchViewModel()
    {
        DownloadCommand = new RelayCommand(_ => ExecuteDownloadCommand(), _ => !_isDownloading && _isComplete);

        Downloads.Add(new DownloadItem { FileName = "libmpv-2.dll", Status = "Pending" });
        Downloads.Add(new DownloadItem { FileName = "libEGL.dll", Status = "Pending" });
        Downloads.Add(new DownloadItem { FileName = "libGLESv2.dll", Status = "Pending" });
    }

    public async Task StartDownloadAsync()
    {
        try
        {
            var progress = new Progress<string>(msg =>
            {
                StatusText = msg;

                // Update per-file status based on progress messages
                foreach (var dl in Downloads)
                {
                    if (msg.Contains(dl.FileName))
                    {
                        if (msg.Contains("already installed"))
                            dl.Status = "\u2713 Ready";
                        else if (msg.Contains("done"))
                            dl.Status = "\u2713 Ready";
                        else if (msg.Contains("%"))
                        {
                            var pct = msg.Split('%')[0].TrimEnd();
                            if (pct.Split(' ').LastOrDefault() is string num && int.TryParse(num, out var n))
                                dl.Status = $"{n}%";
                        }
                        else if (msg.Contains("Downloading"))
                            dl.Status = "Downloading...";
                    }
                }

                // Parse overall progress from "  libmpv-2.dll: 50% (22 / 45 MB)"
                if (msg.Contains('%') && msg.Contains('/'))
                {
                    try
                    {
                        var parts = msg.Split('(');
                        if (parts.Length > 1)
                        {
                            var nums = parts[1].Replace(" MB)", "").Split('/');
                            if (nums.Length == 2 && double.TryParse(nums[0], out var done) && double.TryParse(nums[1], out var total))
                                OverallProgress = done / total * 100;
                        }
                    }
                    catch { /* best-effort progress parsing */ }
                }

                if (msg.Contains("Runtime ready"))
                {
                    OverallProgress = 100;
                    foreach (var dl in Downloads)
                        dl.Status = "\u2713 Ready";
                }
            });

            var runtimeDir = await RuntimeDownloader.EnsureRuntimeAsync(progress, _cts.Token);

            // Store runtime path for native loader
            Environment.SetEnvironmentVariable("CINE_RUNTIME_DIR", runtimeDir);

            StatusText = "All components ready!";
            ButtonText = "Launch Cine";
            _isDownloading = false;
            _isComplete = true;

            DownloadComplete?.Invoke();
        }
        catch (OperationCanceledException)
        {
            StatusText = "Download cancelled";
            ButtonText = "Cancel";
        }
        catch (Exception ex)
        {
            StatusText = $"Download failed: {ex.Message}";
            ButtonText = "Retry";
            Log.ForContext<FirstLaunchViewModel>().Error(ex, "Runtime download failed");
        }
    }

    private void ExecuteDownloadCommand()
    {
        if (_isComplete)
            DownloadComplete?.Invoke();
    }

    public void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}


