using Material.Icons;

namespace Simba.Avalonia.Services.UI;

public interface IOsdService
{
    void Show(string message, double durationMs = 2000);
    void ShowWithIcon(MaterialIconKind icon, string message, double durationMs = 2000);
    void ShowProgress(MaterialIconKind icon, string message, double value, double durationMs = 1500);
}
