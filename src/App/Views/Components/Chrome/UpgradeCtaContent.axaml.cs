using Avalonia.Controls;
using Avalonia.Interactivity;
using Cine.Avalonia.Services;
using Cine.Avalonia.Views.Shell;
using Control = Avalonia.Controls.Control;

namespace Cine.Avalonia.Views.Components;

public partial class UpgradeCtaContent : UserControl
{
    /// <summary>Optional action invoked when the user clicks the upgrade button.</summary>
    public Action? UpgradeAction { get; set; }

    public UpgradeCtaContent()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Creates an <see cref="UpgradeCtaContent"/> and shows it via the <see cref="FlyoutManager"/>
    /// and <see cref="FlyoutOverlay"/>, anchored to the given element.
    /// </summary>
    public static FlyoutOverlay? Show(
        IFlyoutService? manager,
        string featureKey,
        Control anchor,
        Control visualParent,
        string? featureDisplayName = null)
    {
        var overlay = MainWindow.GetOverlay(visualParent);
        if (overlay == null) return null;

        var cta = new UpgradeCtaContent();
        cta.UpgradeAction = () => manager?.HideFlyout(featureKey, () => overlay.HideContent());

        manager?.ShowFlyout(featureKey, anchor, cta, true,
            (a, c, p) =>
            {
                overlay.OnBackgroundDismissed -= OnDismissed;
                overlay.OnBackgroundDismissed += OnDismissed;
                overlay.ShowContent(a, c, p);
            });

        return overlay;

        void OnDismissed()
        {
            overlay.OnBackgroundDismissed -= OnDismissed;
            manager?.MarkClosed(featureKey);
        }
    }

    private void OnUpgradeClick(object? sender, RoutedEventArgs e)
    {
        UpgradeAction?.Invoke();
        // TODO: Open license activation dialog (Phase 5.6.3 follow-up)
    }
}
