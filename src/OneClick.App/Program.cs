using System.IO;
using System.Globalization;
using System.Windows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OneClick.App.Services;
using Serilog;

namespace OneClick.App;

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // Configure Serilog early for startup logging
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(
                path: Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OneClick", "logs", "app-.txt"),
                formatProvider: CultureInfo.InvariantCulture,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                fileSizeLimitBytes: 10_000_000,
                rollOnFileSizeLimit: true)
            .CreateLogger();

        try
        {
            Log.Information("Starting One Click application");
            
            var host = CreateHost(args);
            var app = new App();
            
            // Set the service provider for dependency injection
            App.ServiceProvider = host.Services;
            
            app.InitializeComponent();
            app.Run();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Application terminated unexpectedly");
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    private static IHost CreateHost(string[] args)
    {
        return Host.CreateDefaultBuilder(args)
            .UseSerilog()
            .ConfigureAppConfiguration((context, config) =>
            {
                config.SetBasePath(AppContext.BaseDirectory);
                config.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
                config.AddEnvironmentVariables("ONECLICK_");
            })
            .ConfigureServices((context, services) =>
            {
                services.AddSingleton<AutomationService>();
                services.AddSingleton<BatteryService>();
                services.AddSingleton<JournalService>();
                services.AddSingleton<OrganiseService>();
                services.AddSingleton<PerformanceService>();
                services.AddSingleton<RenameService>();
                services.AddSingleton<SearchService>();
                services.AddSingleton<SettingsService>();
                services.AddSingleton<StorageService>();
                services.AddSingleton<MainWindow>();
            })
            .Build();
    }
}
