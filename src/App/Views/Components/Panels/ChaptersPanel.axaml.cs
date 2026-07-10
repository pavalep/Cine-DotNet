using Avalonia;
using Avalonia.Controls;

namespace Cine.Avalonia.Views.Components.Panels;

public partial class ChaptersPanel : UserControl
{
    public ItemsControl TrackListControl => TrackList;

    public ChaptersPanel()
    {
        InitializeComponent();
    }
}
