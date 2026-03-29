using System.IO;
using System.Text.Json;
using System.Threading.Channels;
using System.Windows;
using FenixFpm.Contracts.Interop;
using FenixFpm.Core.Abstractions;
using FenixFpm.Core.Models;
using FenixFpm.Core.Modules;
using FenixFpm.Core.Services;
using FenixFpm.Desktop.ViewModels;
using FenixFpm.Infrastructure.Debrief;
using FenixFpm.Infrastructure.Ingestion;
using FenixFpm.Infrastructure.Persistence;
using FenixFpm.Infrastructure.SharedMemory;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace FenixFpm.Desktop;

public partial class App : Application
{
    private IHost? _host;

    public static IServiceProvider Services { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        var builder = Host.CreateApplicationBuilder();
        ConfigureServices(builder.Services);

        _host = builder.Build();
        Services = _host.Services;

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            LogException(args.ExceptionObject as Exception);
        };

        DispatcherUnhandledException += (_, args) =>
        {
            LogException(args.Exception);
            args.Handled = true;
        };

        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            LogException(args.Exception);
            args.SetObserved();
        };

        try
        {
            _host.StartAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            LogException(ex);
        }

        var mainWindow = Services.GetRequiredService<MainWindow>();
        mainWindow.Show();

        base.OnStartup(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_host is not null)
        {
            try
            {
                _host.StopAsync().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                LogException(ex);
            }

            _host.Dispose();
        }

        base.OnExit(e);
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddDbContextFactory<FenixTelemetryDbContext>(options =>
            options.UseSqlite("Data Source=fenix_telemetry.db"));

        services.AddSingleton<IFlightSessionService, FlightSessionService>();
        services.AddSingleton<AnalyticsService>();
        services.AddSingleton<IAnalyticsService>(sp => sp.GetRequiredService<AnalyticsService>());
        services.AddSingleton<IDebriefService, DebriefService>();
        services.AddSingleton<IPerformanceDataService, PerformanceDataService>();
        services.AddSingleton(LoadSopConfiguration());

        services.AddSingleton<IAirbusModule>(sp =>
            new StabilizedApproachModule(sp.GetRequiredService<SopConfiguration>()));
        services.AddSingleton<IAirbusModule, AutobrakeMonitorModule>();
        services.AddSingleton(sp =>
            new PerformanceEngine(sp.GetServices<IAirbusModule>()));

        services.AddSingleton<Channel<FenixFpmSharedBuffer>>(_ =>
            Channel.CreateUnbounded<FenixFpmSharedBuffer>(
                new UnboundedChannelOptions { SingleReader = true, SingleWriter = false }));

        services.AddSingleton<FenixSharedMemoryReader>();
        services.AddHostedService<FenixSharedMemoryReader>(provider => provider.GetRequiredService<FenixSharedMemoryReader>());
        services.AddSingleton<ISimConnectService>(provider => provider.GetRequiredService<FenixSharedMemoryReader>());

        services.AddSingleton<TelemetryIngestionWorker>();
        services.AddHostedService(provider => provider.GetRequiredService<TelemetryIngestionWorker>());

        services.AddSingleton<ActiveFlightViewModel>();
        services.AddTransient<MainViewModel>();
        services.AddTransient<PreflightViewModel>();
        services.AddTransient<TakeoffViewModel>();
        services.AddTransient<ClimbCruiseViewModel>();
        services.AddTransient<ApproachViewModel>();
        services.AddTransient<LandingViewModel>();
        services.AddTransient<DebriefViewModel>();

        services.AddSingleton<MainWindow>();
    }

    private static SopConfiguration LoadSopConfiguration()
    {
        var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "A320.json");
        if (!File.Exists(configPath))
        {
            return SopConfiguration.Default;
        }

        try
        {
            var json = File.ReadAllText(configPath);
            return JsonSerializer.Deserialize<SopConfiguration>(json) ?? SopConfiguration.Default;
        }
        catch (Exception ex)
        {
            LogException(ex);
            return SopConfiguration.Default;
        }
    }

    private static void LogException(Exception? ex)
    {
        if (ex is null)
        {
            return;
        }

        var logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "error.log");
        var message = $"[{DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm:ss}] {ex.GetType().Name}: {ex.Message}{Environment.NewLine}{ex.StackTrace}{Environment.NewLine}{Environment.NewLine}";
        File.AppendAllText(logPath, message);
    }
}
