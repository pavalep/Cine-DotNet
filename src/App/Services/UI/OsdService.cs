using Material.Icons;
using Simba.Avalonia.Views.Components;

namespace Simba.Avalonia.Services.UI;

public sealed class OsdService : IOsdService
{
    public OsdNotification? NotificationControl { get; set; }

    public void Show(string message, double durationMs = 2000)
        => NotificationControl?.Show(message, durationMs);

    public void ShowWithIcon(MaterialIconKind icon, string message, double durationMs = 2000)
        => NotificationControl?.ShowWithIcon(icon, message, durationMs);

    public void ShowProgress(MaterialIconKind icon, string message, double value, double durationMs = 1500)
        => NotificationControl?.ShowWithProgress(icon, message, value, durationMs);
}
