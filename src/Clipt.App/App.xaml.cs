using System.IO;
using System.Windows;
using System.Windows.Threading;
using Clipt.App.Services;
using Clipt.App.ViewModels;
using Clipt.App.Views;
using Clipt.Core.Services;
using Clipt.Data;
using Clipt.Data.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;

namespace Clipt.App;

public partial class App : Application
{
    private IHost? _host;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;

        try
        {
            _host = CreateHost();

            var migrationRunner = _host.Services.GetRequiredService<MigrationRunner>();
            await migrationRunner.RunAsync(CancellationToken.None);

            await _host.StartAsync();

            var mainWindow = _host.Services.GetRequiredService<MainWindow>();
            mainWindow.Show();
        }
        catch (Exception exception)
        {
            Log.Fatal(exception, "Clipt failed during startup.");
            MessageBox.Show(exception.ToString(), "Clipt startup failed", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(-1);
        }
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_host is not null)
        {
            await _host.StopAsync(TimeSpan.FromSeconds(3));
            _host.Dispose();
        }

        Log.CloseAndFlush();
        base.OnExit(e);
    }

    private static IHost CreateHost()
    {
        var builder = Host.CreateApplicationBuilder();
        var logDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Clipt",
            "logs");

        Directory.CreateDirectory(logDirectory);
        var logPath = Path.Combine(logDirectory, "clipt-.log");

        builder.Services.AddSerilog((_, configuration) =>
        {
            configuration
                .MinimumLevel.Debug()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                .Enrich.FromLogContext()
                .WriteTo.Debug()
                .WriteTo.File(logPath, rollingInterval: RollingInterval.Day, retainedFileCountLimit: 10);
        });

        builder.Services.AddSingleton<DatabasePathProvider>();
        builder.Services.AddSingleton<MigrationRunner>();
        builder.Services.AddSingleton<IHistoryService, ClipboardRepository>();
        builder.Services.AddSingleton<ISettingsService, SettingsRepository>();
        builder.Services.AddSingleton<ISearchService, SearchService>();
        builder.Services.AddSingleton<IContentTypeDetector, ContentTypeDetector>();
        builder.Services.AddSingleton<IPrivacyFilter, PrivacyFilter>();
        builder.Services.AddSingleton<IClipboardMonitor, WpfClipboardMonitor>();
        builder.Services.AddSingleton<ForegroundWindowTracker>();
        builder.Services.AddSingleton<IClipboardWriter, WpfClipboardWriter>();
        builder.Services.AddSingleton<IInputSimulator, SendInputPasteService>();
        builder.Services.AddSingleton<IHotkeyService, GlobalHotkeyService>();
        builder.Services.AddHostedService<AppLifecycleService>();
        builder.Services.AddHostedService<ClipboardMonitorHostedService>();

        builder.Services.AddSingleton<MainViewModel>();
        builder.Services.AddTransient<PreviewViewModel>();
        builder.Services.AddTransient<SettingsViewModel>();
        builder.Services.AddSingleton<MainWindow>();

        return builder.Build();
    }

    private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Log.Fatal(e.Exception, "Unhandled UI exception.");
        e.Handled = false;
    }

    private static void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception exception)
        {
            Log.Fatal(exception, "Unhandled domain exception.");
        }
    }
}
