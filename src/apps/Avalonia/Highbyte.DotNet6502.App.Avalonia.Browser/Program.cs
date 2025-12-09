using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Browser;
using Highbyte.DotNet6502.App.Avalonia.Browser;
using Highbyte.DotNet6502.App.Avalonia.Core;
using Highbyte.DotNet6502.Impl.Avalonia.Logging;
using Highbyte.DotNet6502.Systems.Logging.Console;
using Highbyte.DotNet6502.Systems.Logging.InMem;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NAudio.Wave;

internal sealed partial class Program
{
    private const string LOCAL_STORAGE_MAIN_CONFIG_KEY = "dotnet6502.emulator.avalonia.config";

    [RequiresUnreferencedCode("Calls JsonSerializer.Deserialize(String) and JsonSerializer.Serialize(object)")]
    private static async Task<int> Main(string[] args)
    {
        // Load configuration from Browser Local Storage using source-generated JSON serialization
        string configJson = await GetConfigStringFromLocalStorage(LOCAL_STORAGE_MAIN_CONFIG_KEY);
        Console.WriteLine("Configuration loaded from browser local storage to JSON string.");

        var configDict = GetConfigDictionary(configJson);
        Console.WriteLine("Configuration dictionary created from JSON string.");

        var configurationBuilder = new ConfigurationBuilder();
        configurationBuilder.AddInMemoryCollection(configDict);
        Console.WriteLine("Configuration dictionary string added to IConfiguration.");

        var configuration = configurationBuilder.Build();
        Console.WriteLine("Configuration build succeeded.");

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
        configuration.GetSection($"{EmulatorConfig.ConfigSectionName}").Bind(emulatorConfig);

        emulatorConfig.EnableLoadResourceOverHttp(GetAppUrlHttpClient);

        // Create audio wave player for browser (using WebAudio)
        var wavePlayer = new WebAudioWavePlayer()
        {
            DesiredLatency = 100 // Higher latency for browser stability
        };
        // Load custom JS module for interacting with WebAudio API from WebAudioWavePlayer.
        await JSHost.ImportAsync("WebAudioWavePlayer", "/js/WebAudioWavePlayer.js");

        // Start Avalonia app
        try
        {
            Console.WriteLine("Starting Avalonia Browser app...");
            await BuildAvaloniaApp(configuration, emulatorConfig, logStore, logConfig, loggerFactory, avaloniaLoggerBridge, wavePlayer)
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

    private static Dictionary<string, string?> GetConfigDictionary(string configJson)
    {
        Dictionary<string, string?> configDict = [];

        // If we have a config JSON string from Local Storage, deserialize and add it to IConfiguration
        if (!string.IsNullOrEmpty(configJson))
        {
            try
            {
                var jsonDict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(configJson, HostConfigJsonContext.Default.DictionaryStringJsonElement);

                if (jsonDict != null)
                {
                    // Flatten the dictionary to IConfiguration format
                    foreach (var kvp in jsonDict)
                    {
                        JsonHelper.FlattenJsonElementToDictionary(kvp.Value, kvp.Key, configDict);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Could not parse configuration JSON from Local Storage: {ex.Message}");
            }
        }
        else
        {
            Console.WriteLine("No configuration JSON found in browser local storage.");
        }
        return configDict;
    }

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

    private static async Task<string> GetConfigStringFromLocalStorage(string configKey)
    {
        var jsonString = await Task.Run(() => JSInterop.GetLocalStorage(configKey) ?? string.Empty);
        return jsonString;
    }

    private static async Task PersistStringToLocalStorage(string configKey, string configKeyJsonValue, string? localStorageKey = LOCAL_STORAGE_MAIN_CONFIG_KEY)
    {
        if (string.IsNullOrEmpty(localStorageKey))
            localStorageKey = LOCAL_STORAGE_MAIN_CONFIG_KEY;

        // Load existing config from Local Storage
        string configJson = await GetConfigStringFromLocalStorage(localStorageKey);
        Console.WriteLine("Configuration loaded from browser local storage to JSON string.");

        // Parse existing config or start with empty dictionary
        Dictionary<string, JsonElement> existingConfig;
        if (!string.IsNullOrEmpty(configJson))
        {
            try
            {
                existingConfig = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(configJson, HostConfigJsonContext.Default.DictionaryStringJsonElement)
                    ?? new Dictionary<string, JsonElement>();
                Console.WriteLine("Existing configuration parsed from JSON string.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Could not parse existing configuration JSON: {ex.Message}. Starting with empty config.");
                existingConfig = new Dictionary<string, JsonElement>();
            }
        }
        else
        {
            existingConfig = new Dictionary<string, JsonElement>();
            Console.WriteLine("No existing configuration found. Starting with empty config.");
        }

        // Parse the new section value
        try
        {
            JsonElement newSectionValue = JsonSerializer.Deserialize<JsonElement>(configKeyJsonValue, HostConfigJsonContext.Default.JsonElement);

            // Update or add the section
            existingConfig[configKey] = newSectionValue;
            Console.WriteLine($"Configuration section '{configKey}' updated.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: Could not parse configuration value for key '{configKey}': {ex.Message}");
            throw;
        }

        // Serialize back to JSON string
        string updatedConfigJson = JsonSerializer.Serialize(existingConfig, HostConfigJsonContext.Default.DictionaryStringJsonElement);
        Console.WriteLine("Updated configuration serialized to JSON string.");

        // Persist updated config back to Local Storage
        await Task.Run(() => JSInterop.SetLocalStorage(localStorageKey, updatedConfigJson));
        Console.WriteLine("Updated configuration JSON string saved to browser local storage.");
    }

    private static async Task PersistConfigSectionToLocalStorage(string configSectionName, IConfigurationSection configKeySection, string? localStorageKey = LOCAL_STORAGE_MAIN_CONFIG_KEY)
    {
        // Serialize the IConfigurationSection to JSON string using source-generated serialization
        var configKeyJsonValue = JsonSerializer.Serialize(configKeySection.GetChildren().ToDictionary(x => x.Key, x => x.Value), HostConfigJsonContext.Default.DictionaryStringString);

        // Persist the JSON string to Local Storage
        await PersistStringToLocalStorage(configSectionName, configKeyJsonValue);
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
        AvaloniaLoggerBridge avaloniaLoggerBridge,
        IWavePlayer wavePlayer)
    {
        return AppBuilder.Configure(() =>
        {
            return new App(
                                configuration,
                                emulatorConfig,
                                logStore,
                                logConfig,
                                loggerFactory,
                                wavePlayer,
                                saveCustomConfigString: PersistStringToLocalStorage, // Save configuration to custom provided JSON in Browser Local Storage
                                saveCustomConfigSection: PersistConfigSectionToLocalStorage // Save configuration to custom provided IConfigurationSection in Browser Local Storage
                            );
        })
        .AfterSetup(_ =>
        {
            // Set up the Avalonia logger bridge to route logs via Avalonia Logger through ILogger
            global::Avalonia.Logging.Logger.Sink = avaloniaLoggerBridge;
        });
    }
}
