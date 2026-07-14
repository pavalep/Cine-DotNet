using Avalonia.Controls;
using Simba.Avalonia.ViewModels.Dialogs;

namespace Simba.Avalonia.Views.Dialogs;

public partial class FirstLaunchDialog : Window
{
    public FirstLaunchDialog()
    {
        InitializeComponent();
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        if (DataContext is FirstLaunchViewModel vm)
            vm.StartDownloadAsync().ContinueWith(_ => { }, TaskContinuationOptions.OnlyOnFaulted);
    }
}
