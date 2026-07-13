using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;

namespace Cine.Avalonia.ViewModels;

public class PlaylistItemViewModel : INotifyPropertyChanged
{
    private readonly MainViewModel _parent;
    private readonly int _index;
    private readonly string _path;
    private bool _isVisible = true;

    public PlaylistItemViewModel(MainViewModel parent, int index, string path)
    {
        _parent = parent;
        _index = index;
        _path = path;
    }

    public string Title => Path.GetFileNameWithoutExtension(_path);
    public string Directory => Path.GetFileName(Path.GetDirectoryName(_path) ?? string.Empty);
    public string FilePath => _path;
    public int Index => _index;

    // Compiled binding aliases
    public bool IsCurrent => IsPlaying;
    public string DisplayTitle => Title;
    public TimeSpan Duration => TimeSpan.Zero;

    public bool IsPlaying => _parent.PlaylistPosition == _index;

    public bool IsVisible
    {
        get => _isVisible;
        set
        {
            if (_isVisible != value)
            {
                _isVisible = value;
                OnPropertyChanged();
            }
        }
    }

    public void NotifyPlayingChanged()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsPlaying)));
    }

    public void Play()
    {
        _parent.PlayPlaylistItem(_index);
    }

    public void Remove()
    {
        _parent.RemovePlaylistItem(_index);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
