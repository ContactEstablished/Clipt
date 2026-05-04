using System.IO;
using System.Windows;
using Clipt.App.ViewModels;
using Clipt.App.Views;
using Clipt.Core.Services;
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

        _host = CreateHost();
        await _host.StartAsync();

        var mainWindow = _host.Services.GetRequiredService<MainWindow>();
        mainWindow.Show();
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

        builder.Services.AddSingleton<IHistoryService, DemoHistoryService>();
        builder.Services.AddSingleton<ISearchService, SearchService>();
        builder.Services.AddSingleton<IContentTypeDetector, ContentTypeDetector>();
        builder.Services.AddSingleton<IPrivacyFilter, PrivacyFilter>();
        builder.Services.AddHostedService<AppLifecycleService>();

        builder.Services.AddSingleton<MainViewModel>();
        builder.Services.AddTransient<PreviewViewModel>();
        builder.Services.AddTransient<SettingsViewModel>();
        builder.Services.AddSingleton<MainWindow>();

        return builder.Build();
    }
}
