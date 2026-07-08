using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Cine.Avalonia.Components;

public partial class TrialBanner : UserControl
{
    public TrialBanner()
    {
        InitializeComponent();
    }

    private void OnUpgradeClick(object? sender, RoutedEventArgs e)
    {
        // TODO: Open upgrade/license activation dialog (Phase 5.6.3)
    }
}
