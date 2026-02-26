using Microsoft.AspNetCore.SignalR.Protocol;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace Mooseware.TimeToAir;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : System.Windows.Application
{
    public static IHost AppHost { get; private set; }

    public App() 
    {
        // Establish both a main window singleton and a concurrent queue singleton to be used for the HTTP API
        AppHost = (IHost)Host.CreateDefaultBuilder()
        .ConfigureServices((hostContext, services) =>
        {
            services.Configure<Configuration.AppSettings>(hostContext.Configuration.GetSection("ApplicationSettings"));
            services.AddSingleton<MainWindow>();
            services.AddHttpClient();
            services.AddSerilog(
                new LoggerConfiguration()
                .ReadFrom.Configuration(hostContext.Configuration)
                .CreateLogger()
                );
        })
        .Build();

    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        await AppHost!.StartAsync();

        // Launch the MainWindow
        var startupForm = AppHost.Services.GetRequiredService<MainWindow>();
        startupForm.Show();

        base.OnStartup(e);

    }

    protected override async void OnExit(ExitEventArgs e)
    {
        // Wind down the API server...
        await AppHost!.StopAsync();
        base.OnExit(e);
    }
}
