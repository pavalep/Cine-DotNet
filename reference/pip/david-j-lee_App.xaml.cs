// SOURCE: https://github.com/david-j-lee/picture-in-picture/blob/main/App.xaml.cs
// PROJECT: picture-in-picture by david-j-lee (WPF/C#, MVVM + DI)
//
// KEY INSIGHTS:
// 1. DI registration of PipModeViewModel, PipModeWindow, ProcessesService
// 2. All ViewModels are Transient (new instance each time), Views are Singleton
// 3. PipModeViewModel has own dedicated ViewModel (not inline code-behind)
// 4. Services + Models + ViewModels + Views separation via DI

using System;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PictureInPicture.Services;
using PictureInPicture.ViewModels;
using PictureInPicture.Views;

namespace PictureInPicture
{
  public partial class App : Application
  {
    public IServiceProvider Services { get; }

    public App()
    {
      Services = ConfigureServices();
      InitializeComponent();
    }

    public new static App Current => (App)Application.Current;

    private void OnStartup(object sender, StartupEventArgs e)
    {
      var mainWindow = Services.GetService<MainWindow>();
      mainWindow.Show();
    }

    private static IServiceProvider ConfigureServices()
    {
      var services = new ServiceCollection();
      services.AddLogging(configure => configure.AddConsole());

      // Services
      services.AddSingleton<ProcessesService>();

      // View Models
      services.AddTransient<CropperViewModel>();
      services.AddTransient<MainViewModel>();
      services.AddTransient<PipModeViewModel>();

      // Views
      services.AddSingleton<CropperWindow>();
      services.AddSingleton<MainWindow>();
      services.AddSingleton<PipModeWindow>();

      return services.BuildServiceProvider();
    }
  }
}
