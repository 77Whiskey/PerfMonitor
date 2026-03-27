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

        builder.Services.AddDbContextFactory<FenixTelemetryDbContext>(options =>
            options.UseSqlite("Data Source=fenix_telemetry.db"));

        builder.Services.AddSingleton<IFlightSessionService, FlightSessionService>();
        builder.Services.AddSingleton<AnalyticsService>();
        builder.Services.AddSingleton<IAnalyticsService>(sp => sp.GetRequiredService<AnalyticsService>());
        builder.Services.AddSingleton<IDebriefService, DebriefService>();
        builder.Services.AddSingleton<IPerformanceDataService, PerformanceDataService>();
        builder.Services.AddSingleton(LoadSopConfiguration());

        builder.Services.AddSingleton<IAirbusModule>(sp =>
            new StabilizedApproachModule(sp.GetRequiredService<SopConfiguration>()));
        builder.Services.AddSingleton<IAirbusModule, AutobrakeMonitorModule>();
        builder.Services.AddSingleton(sp =>
            new PerformanceEngine(sp.GetServices<IAirbusModule>()));

        builder.Services.AddSingleton<Channel<FenixFpmSharedBuffer>>(_ =>
            Channel.CreateUnbounded<FenixFpmSharedBuffer>(
                new UnboundedChannelOptions { SingleReader = true, SingleWriter = false }));

        builder.Services.AddSingleton(sp =>
            new FenixSharedMemoryReader(snapshotChannel: sp.GetRequiredService<Channel<FenixFpmSharedBuffer>>()));

        builder.Services.AddSingleton<TelemetryIngestionWorker>();
        builder.Services.AddHostedService(sp => sp.GetRequiredService<TelemetryIngestionWorker>());

        builder.Services.AddSingleton<ActiveFlightViewModel>();
        builder.Services.AddTransient<MainViewModel>();
        builder.Services.AddTransient<PreflightViewModel>();
        builder.Services.AddTransient<TakeoffViewModel>();
        builder.Services.AddTransient<ClimbCruiseViewModel>();
        builder.Services.AddTransient<ApproachViewModel>();
        builder.Services.AddTransient<LandingViewModel>();
        builder.Services.AddTransient<DebriefViewModel>();

        builder.Services.AddSingleton<MainWindow>();

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

        try
        {
            Services.GetRequiredService<FenixSharedMemoryReader>()
                .InitializeAsync()
                .AsTask()
                .GetAwaiter()
                .GetResult();
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
