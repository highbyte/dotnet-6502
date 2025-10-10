using System;
using System.IO;
using Avalonia;
using Avalonia.ReactiveUI;
using Highbyte.DotNet6502.App.Avalonia.Core;
using Highbyte.DotNet6502.Systems.Logging.InMem;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Highbyte.DotNet6502.App.Avalonia.Desktop;

sealed class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        // ----------
        // Get config file
        // ----------
        var builder = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json")
            .AddJsonFile("appsettings.Development.json", optional: true);

        var devEnvironmentVariable = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");
        var isDevelopment = string.IsNullOrEmpty(devEnvironmentVariable) || devEnvironmentVariable.ToLower() == "development";
        if (isDevelopment)
        {
            builder.AddUserSecrets<Core.App>();
        }

        IConfiguration configuration = builder.Build();

        // ----------
        // Create logging
        // ----------
        DotNet6502InMemLogStore logStore = new() { WriteDebugMessage = true };
        var logConfig = new DotNet6502InMemLoggerConfiguration(logStore);
        var loggerFactory = LoggerFactory.Create(builder =>
        {
            logConfig.LogLevel = LogLevel.Information;
            builder.AddInMem(logConfig);
            builder.SetMinimumLevel(LogLevel.Trace);
        });

        // ----------
        // Get emulator host config
        // ----------
        var emulatorConfig = new EmulatorConfig();
        configuration.GetSection(EmulatorConfig.ConfigSectionName).Bind(emulatorConfig);

        BuildAvaloniaApp(configuration, emulatorConfig, logStore, logConfig, loggerFactory)
            .StartWithClassicDesktopLifetime(args);
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp(
        IConfiguration configuration,
        EmulatorConfig emulatorConfig,
        DotNet6502InMemLogStore logStore,
        DotNet6502InMemLoggerConfiguration logConfig,
        ILoggerFactory loggerFactory)
        => AppBuilder.Configure(() => new Core.App(configuration, emulatorConfig, logStore, logConfig, loggerFactory))
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace()
            .UseReactiveUI();
}
