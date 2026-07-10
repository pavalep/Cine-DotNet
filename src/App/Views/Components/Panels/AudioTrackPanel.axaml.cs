using Avalonia;
using Avalonia.Controls;

namespace Cine.Avalonia.Views.Components.Panels;

public partial class AudioTrackPanel : UserControl
{
    public ItemsControl TrackListControl => TrackList;

    public AudioTrackPanel()
    {
        InitializeComponent();
    }
}
