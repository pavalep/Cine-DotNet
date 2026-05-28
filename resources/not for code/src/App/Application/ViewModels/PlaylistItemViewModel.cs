using System;
using System.ComponentModel;
using System.IO;

namespace Cine.Avalonia.ViewModels;

public class PlaylistItemViewModel : INotifyPropertyChanged
{
    private readonly MainViewModel _parent;
    private readonly int _index;
    private readonly string _path;

    public PlaylistItemViewModel(MainViewModel parent, int index, string path)
    {
        _parent = parent;
        _index = index;
        _path = path;
    }

    public string Title => Path.GetFileNameWithoutExtension(_path);
    public string Directory => Path.GetFileName(Path.GetDirectoryName(_path) ?? string.Empty);
    public string PathStr => _path;
    public int Index => _index;

    public bool IsPlaying => _parent.PlaylistPosition == _index;

    public void NotifyPlayingChanged()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsPlaying)));
    }

    public void Play()
    {
        _parent.PlayPlaylistItem(_index);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
