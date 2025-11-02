using System;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Browser;
using Highbyte.DotNet6502.App.Avalonia.Core;
using Highbyte.DotNet6502.App.Avalonia.Core.Logging;
using Highbyte.DotNet6502.Systems.Logging.Console;
using Highbyte.DotNet6502.Systems.Logging.InMem;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

internal sealed partial class Program
{
    private static async Task<int> Main(string[] args)
    {
        // Load configuration from appsettings.json in wwwroot
        //var configuration = await LoadConfigurationFromAppsettingsAsync();
        // Use empty configuration for now, as we currently load config from Local Storage
        var configuration = await GetEmptyConfigurationAsync();

        // Configure logging
        DotNet6502InMemLogStore logStore = new() { WriteDebugMessage = true };
        var logConfig = new DotNet6502InMemLoggerConfiguration(logStore);
        var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddInMem(logConfig);    // Log to in-memory log store
            builder.AddDotNet6502Console(); // Logs to console (which will be visible in browser F12 DevTools console)
            builder.SetMinimumLevel(LogLevel.Information);
        });

        // Create an ILogger for bridging Avalonia logs
        var avaloniaILogger = loggerFactory.CreateLogger("Avalonia");
        var avaloniaLoggerBridge = new AvaloniaLoggerBridge(avaloniaILogger, LogLevel.Warning);

        // Emulator config
        var emulatorConfig = new EmulatorConfig();
        emulatorConfig.EnableLoadResourceOverHttp(GetAppUrlHttpClient);

        try
        {
            Console.WriteLine("Starting Avalonia Browser app...");
            await BuildAvaloniaApp(configuration, emulatorConfig, logStore, logConfig, loggerFactory, avaloniaLoggerBridge)
                .WithInterFont()
                .StartBrowserAppAsync("out");

            Console.WriteLine("Avalonia Browser app started.");

        }
        catch (Exception ex)
        {
            // Safe error handling for WebAssembly/AOT environments
            try
            {
                var errorMessage = ex?.Message ?? "Unknown error";
                Console.WriteLine($"Fatal error starting application: {errorMessage}");

                // Also log exception type if possible
                var exceptionType = ex?.GetType()?.Name ?? "Unknown";
                Console.WriteLine($"Exception type: {exceptionType}");
            }
            catch
            {
                Console.WriteLine("Fatal error starting application: Unable to access exception details");
            }
            throw;
        }
        //Console.WriteLine("Avalonia Browser app exiting.");
        return 0;
    }

    private static async Task<IConfiguration> GetEmptyConfigurationAsync()
    {
        return await Task.FromResult(new ConfigurationBuilder()
            .AddInMemoryCollection()
            .Build());
    }
    // private static async Task<IConfiguration> LoadConfigurationFromAppsettingsAsync()
    // {
    //     var configBuilder = new ConfigurationBuilder();

    //     try
    //     {
    //         using var httpClient = GetHttpClient();

    //         // Load base appsettings.json
    //         Stream appSettingsStream = await httpClient.GetStreamAsync("appsettings.json");
    //         configBuilder.AddJsonStream(appSettingsStream);

    //         // string jsonContent = await  httpClient.GetStringAsync("appsettings.json");
    //         // Console.WriteLine($"appsettings.json content length: {jsonContent.Length}");
    //         // Console.WriteLine($"appsettings.json content (first 2000 chars): {jsonContent.Substring(0, Math.Min(2000, jsonContent.Length))}");
    //         // configBuilder.AddJsonStream(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(jsonContent)));

    //         // Try to load development settings if available
    //         try
    //         {
    //             appSettingsStream = await httpClient.GetStreamAsync("appsettings.Development.json");
    //             configBuilder.AddJsonStream(appSettingsStream);
    //         }
    //         catch
    //         {
    //             // Development settings are optional, ignore if not found
    //         }
    //         return configBuilder.Build();
    //     }
    //     catch (Exception ex)
    //     {
    //         // Safe error handling for WebAssembly/AOT environments
    //         try
    //         {
    //             var errorMessage = ex?.Message ?? "Unknown error";
    //             Console.WriteLine($"Warning: Could not load configuration from appsettings.json. {errorMessage}");
    //         }
    //         catch
    //         {
    //             Console.WriteLine("Warning: Could not load configuration from appsettings.json. Unable to access exception details");
    //         }

    //         // Fallback to in-memory configuration if appsettings.json cannot be loaded
    //         return new ConfigurationBuilder()
    //             .AddInMemoryCollection()
    //             .Build();
    //     }
    // }

    /// <summary>
    /// Gets a HttpClient with BaseAddress set to the current app origin.
    /// </summary>
    /// <returns></returns>
    private static HttpClient GetAppUrlHttpClient()
    {
        var httpClient = new HttpClient();
        httpClient.BaseAddress = new Uri(GetCurrentAppBaseUrl());
        return httpClient;
    }

    [SupportedOSPlatform("browser")]
    internal static partial class JSInterop
    {
        [JSImport("globalThis.localStorage.getItem")]
        public static partial string? GetLocalStorage(string key);

        [JSImport("globalThis.localStorage.setItem")]
        public static partial void SetLocalStorage(string key, string? value);
    }

    private static async Task<string> GetConfigJsonFromLocalStorage(string configKey)
    {
        var jsonString = await Task.Run(() => JSInterop.GetLocalStorage(configKey) ?? string.Empty);
        return jsonString;
    }

    private static async Task PersistJsonToLocalStorage(string configKey, string jsonString)
    {
        await Task.Run(() => JSInterop.SetLocalStorage(configKey, jsonString));
    }

    // For Avalonia Browser, derive the app base URL (including hosting path)
    private static string GetCurrentAppBaseUrl()
    {
        try
        {
            // Use the URL passed as argument to main, which should contain the full URL
            // This is the simplest and most reliable approach for WASM
            var args = Environment.GetCommandLineArgs();
            if (args.Length > 1)
            {
                var url = args[1]; // The URL is passed as the second argument in main.js
                if (!string.IsNullOrEmpty(url) && Uri.TryCreate(url, UriKind.Absolute, out var uri))
                {
                    var appBase = uri.GetLeftPart(UriPartial.Path);

                    if (!appBase.EndsWith("/", StringComparison.Ordinal))
                    {
                        appBase += "/";
                    }

                    return appBase;
                }
            }
        }
        catch (Exception ex)
        {
            // Safe error handling for WebAssembly/AOT environments
            try
            {
                var errorMessage = ex?.Message ?? "Unknown error";
                Console.WriteLine($"Warning: Could not get origin from command line args: {errorMessage}");
            }
            catch
            {
                Console.WriteLine("Warning: Could not get origin from command line args: Unable to access exception details");
            }
        }

        // Fallback for development
        return "https://localhost:5000/";
    }

    public static AppBuilder BuildAvaloniaApp(
        IConfiguration configuration,
        EmulatorConfig emulatorConfig,
        DotNet6502InMemLogStore logStore,
        DotNet6502InMemLoggerConfiguration logConfig,
        ILoggerFactory loggerFactory,
        AvaloniaLoggerBridge avaloniaLoggerBridge)
    {
        return AppBuilder.Configure(() =>
        {
            return new App(
                                configuration,
                                emulatorConfig,
                                logStore,
                                logConfig,
                                loggerFactory,
                                getCustomConfigJson: GetConfigJsonFromLocalStorage, // Load configuration from custom provided JSON read from Browser Local Storage
                                saveCustomConfigJson: PersistJsonToLocalStorage // Save configuration to custom provided JSON in Browser Local Storage
                            );
        })
        .AfterSetup(_ =>
        {
            // Set up the Avalonia logger bridge to route logs via Avalonia Logger through ILogger
            global::Avalonia.Logging.Logger.Sink = avaloniaLoggerBridge;
        });
    }
}
