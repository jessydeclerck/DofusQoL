using System.Windows;
using DofusManager.Core.Services;
using DofusManager.Core.Win32;
using DofusManager.UI.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace DofusManager.UI;

public partial class App : Application
{
    private ServiceProvider? _serviceProvider;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.Console()
            .WriteTo.File("logs/dofusmanager-.log", rollingInterval: RollingInterval.Day)
            .CreateLogger();

        Log.Information("DofusManager démarré");

        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();

        var mainWindow = _serviceProvider.GetRequiredService<Views.MainWindow>();
        mainWindow.Show();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // Win32
        services.AddSingleton<IWin32WindowHelper, WindowHelper>();

        // Services
        services.AddSingleton<IWindowDetectionService, WindowDetectionService>();
        services.AddSingleton<IHotkeyService, HotkeyService>();
        services.AddSingleton<IFocusService, FocusService>();
        services.AddSingleton<IProfileService, ProfileService>();
        services.AddSingleton<IBroadcastService, BroadcastService>();
        services.AddSingleton<IPushToBroadcastService, PushToBroadcastService>();

        // ViewModels (Singleton pour éviter les doubles abonnements aux événements)
        services.AddSingleton<HotkeyViewModel>();
        services.AddSingleton<ProfileViewModel>();
        services.AddSingleton<BroadcastViewModel>();
        services.AddSingleton<MainViewModel>();

        // Views
        services.AddTransient<Views.MainWindow>();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Log.Information("DofusManager arrêté");
        Log.CloseAndFlush();

        _serviceProvider?.Dispose();
        base.OnExit(e);
    }
}
