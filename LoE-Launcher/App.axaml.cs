using System;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using LoE_Launcher.Core;
using LoE_Launcher.Services;
using LoE_Launcher.ViewModels;
using LoE_Launcher.Views;
using Microsoft.Extensions.DependencyInjection;

namespace LoE_Launcher;

public partial class App : Application
{
    public IServiceProvider? Services { get; private set; }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void ConfigureServices(IServiceCollection services)
    {
        // Services
        services.AddSingleton<Downloader>();
        services.AddSingleton<CacheManager>(provider => 
            new CacheManager(
                Path.Combine(
                    provider.GetRequiredService<Downloader>().LauncherFolder.Path, 
                    Constants.CacheDirectoryName)
                )
        );
        services.AddSingleton<ChangelogParser>();
        services.AddSingleton<IWindowProvider, WindowProvider>();
        services.AddSingleton<IDialogService, DialogService>();

        // ViewModels
        services.AddTransient<MainWindowViewModel>();
        services.AddTransient<SettingsViewModel>();
    }

    public override void OnFrameworkInitializationCompleted()
    {
        var services = new ServiceCollection();
        ConfigureServices(services);
        Services = services.BuildServiceProvider();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainWindow = new MainWindow();
            
            var windowProvider = Services.GetRequiredService<IWindowProvider>() as WindowProvider;
            if (windowProvider != null)
            {
                windowProvider.MainWindow = mainWindow;
            }

            mainWindow.DataContext = Services.GetRequiredService<MainWindowViewModel>();
            
            desktop.MainWindow = mainWindow;
        }
        
        base.OnFrameworkInitializationCompleted();
    }
}
