using System.Windows;
using Microsoft.Extensions.DependencyInjection;

namespace OneClick.App;

public partial class App : System.Windows.Application
{
    public static IServiceProvider? ServiceProvider { get; set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        
        if (ServiceProvider != null)
        {
            OneClick.App.Services.ServiceLocator.Configure(ServiceProvider);
            var mainWindow = ServiceProvider.GetRequiredService<MainWindow>();
            mainWindow.Show();
        }
    }
}
