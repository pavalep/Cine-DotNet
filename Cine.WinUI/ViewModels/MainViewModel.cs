using System;
using System.ComponentModel;
using System.Windows.Input;
using Cine.Media.Interfaces;
using Cine.WinUI.Services;

namespace Cine.WinUI.ViewModels;
public class MainViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public ICommand PlayCommand { get; }
    public ICommand PauseCommand { get; }
    public ICommand StopCommand { get; }

    private string _filePath = string.Empty;
    public string FilePath { get => _filePath; set { _filePath = value; OnPropertyChanged(nameof(FilePath)); } }

    private double _volume = 0.8;    public double Volume { get => _volume; set { _volume = value; OnPropertyChanged(nameof(Volume)); if(ServiceLocator.MediaPlayer!=null) ServiceLocator.MediaPlayer.Volume = value; } }

    private TimeSpan _position = TimeSpan.Zero;    public TimeSpan Position { get => _position; set { _position = value; OnPropertyChanged(nameof(Position)); OnPropertyChanged(nameof(PositionText)); } }

    private TimeSpan _duration = TimeSpan.Zero;    public TimeSpan Duration { get => _duration; set { _duration = value; OnPropertyChanged(nameof(Duration)); OnPropertyChanged(nameof(DurationText)); } }

    public string PositionText => Position.ToString(@"hh\:mm\:ss");
    public string DurationText => Duration.ToString(@"hh\:mm\:ss");

    public double SeekPercent
    {
        get => (Duration.TotalSeconds > 0) ? (Position.TotalSeconds / Duration.TotalSeconds) : 0.0;
        set
        {
            if (Duration.TotalSeconds > 0)
            {
                Position = TimeSpan.FromSeconds(Duration.TotalSeconds * value);
                // Seek on media player if available (stub)
            }
            OnPropertyChanged(nameof(SeekPercent));
        }
    }

    public MainViewModel()
    {
        PlayCommand = new RelayCommand(_ => Play());
        PauseCommand = new RelayCommand(_ => Pause());
        StopCommand = new RelayCommand(_ => Stop());
    }

    public void OpenFile(string path)
    {
        FilePath = path;
        if (ServiceLocator.MediaPlayer != null)
        {
            ServiceLocator.MediaPlayer.Open(path);
            Duration = ServiceLocator.MediaPlayer.Duration;
        }
    }

    public void Play() { ServiceLocator.MediaPlayer?.Play(); }
    public void Pause() { ServiceLocator.MediaPlayer?.Pause(); }
    public void Stop() { ServiceLocator.MediaPlayer?.Stop(); }

    protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    class RelayCommand : ICommand
    {
        private readonly Action<object?> _execute;
        public RelayCommand(Action<object?> execute) => _execute = execute;
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => _execute(parameter);
        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }
    }
}