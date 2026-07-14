using Avalonia;
using Avalonia.Controls;

namespace Simba.Avalonia.Views.Components.Panels;

public partial class ChaptersPanel : UserControl
{
    public ItemsControl TrackListControl => TrackList;

    public ChaptersPanel()
    {
        InitializeComponent();
    }
}
