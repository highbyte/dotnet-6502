using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using System.Text.Json;
using Avalonia;
using Avalonia.Browser;
using Highbyte.DotNet6502.App.Avalonia.Core;
using Highbyte.DotNet6502.Impl.Avalonia.Logging;
using Highbyte.DotNet6502.Impl.Browser.Input;
using Highbyte.DotNet6502.Impl.NAudio.WavePlayers.WebAudioAPI;
using Highbyte.DotNet6502.Systems.Logging.Console;
using Highbyte.DotNet6502.Systems.Logging.InMem;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

internal sealed partial class Program
{
    private const string LOCAL_STORAGE_MAIN_CONFIG_KEY = "dotnet6502.emulator.avalonia.config";

    [RequiresUnreferencedCode("Calls JsonSerializer.Deserialize(String) and JsonSerializer.Serialize(object)")]
    private static async Task<int> Main(string[] args)
    {
        AppLogger.ConsoleLoggingEnabled = true;
        WriteBootstrapLog("Avalonia program starting.");

        // Load configuration from Browser Local Storage using source-generated JSON serialization
        string configJson = await GetConfigStringFromLocalStorage(LOCAL_STORAGE_MAIN_CONFIG_KEY);
        WriteBootstrapLog("Configuration loaded from browser local storage to JSON string.");

        var configDict = GetConfigDictionary(configJson);
        WriteBootstrapLog("Configuration dictionary created from JSON string.");

        var configurationBuilder = new ConfigurationBuilder();
        configurationBuilder.AddInMemoryCollection(configDict);
        WriteBootstrapLog("Configuration dictionary string added to IConfiguration.");

        var configuration = configurationBuilder.Build();
        WriteBootstrapLog("Configuration build succeeded.");

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
        WriteBootstrapLog("Initializing logging.");
        var avaloniaILogger = loggerFactory.CreateLogger("Avalonia");
        var avaloniaLoggerBridge = new AvaloniaLoggerBridge(avaloniaILogger, LogLevel.Warning);

        // Emulator config
        WriteBootstrapLog("Reading emulator config.");
        var emulatorConfig = new EmulatorConfig();
        configuration.GetSection($"{EmulatorConfig.ConfigSectionName}").Bind(emulatorConfig);

        emulatorConfig.EnableLoadResourceOverHttp(GetAppUrlHttpClient);

        // Load custom JS module that WebAudioWavePlayer requires for interacting with WebAudio API.
        WriteBootstrapLog("Importing WebAudio WavePlayer JS module.");
        var jsModuleUri = WebAudioWavePlayerResources.GetJavaScriptModuleDataUri();
        await JSHost.ImportAsync("WebAudioWavePlayer", jsModuleUri);

        // Load custom JS module for gamepad input
        WriteBootstrapLog("Importing Gamepad JS module.");
        await BrowserGamepad.LoadJsModuleAsync();

        // Create browser gamepad instance
        WriteBootstrapLog("Creating Gamepad implementation (Browser).");
        var browserGamepad = new BrowserGamepad(loggerFactory);

        // Start Avalonia app
        try
        {
            WriteBootstrapLog("Starting Avalonia Browser app...");
            await BuildAvaloniaApp(configuration, emulatorConfig, logStore, logConfig, loggerFactory, avaloniaLoggerBridge, browserGamepad)
                .WithInterFont()
                .StartBrowserAppAsync("out");

            WriteBootstrapLog("Avalonia Browser app exiting.");
        }
        catch (Exception ex)
        {
            // Safe error handling for WebAssembly/AOT environments
            try
            {
                var errorMessage = ex?.Message ?? "Unknown error";
                WriteBootstrapLog($"Fatal error starting application: {errorMessage}", LogLevel.Critical);

                // Also log exception type if possible
                var exceptionType = ex?.GetType()?.Name ?? "Unknown";
                WriteBootstrapLog($"Exception type: {exceptionType}", LogLevel.Critical);
            }
            catch
            {
                WriteBootstrapLog("Fatal error starting application: Unable to access exception details", LogLevel.Critical);
            }
            throw;
        }
        return 0;
    }

    private static void WriteBootstrapLog(string message, LogLevel logLevel = LogLevel.Information)
    {
        AppLogger.WriteBootstrapLog(message, logLevel, nameof(Program));
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
                WriteBootstrapLog($"Could not parse configuration JSON from Local Storage: {ex.Message}", LogLevel.Warning);
            }
        }
        else
        {
            WriteBootstrapLog("No configuration JSON found in browser local storage.");
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
        WriteBootstrapLog("Configuration loaded from browser local storage to JSON string.");

        // Parse existing config or start with empty dictionary
        Dictionary<string, JsonElement> existingConfig;
        if (!string.IsNullOrEmpty(configJson))
        {
            try
            {
                existingConfig = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(configJson, HostConfigJsonContext.Default.DictionaryStringJsonElement)
                    ?? new Dictionary<string, JsonElement>();
                WriteBootstrapLog("Existing configuration parsed from JSON string.");
            }
            catch (Exception ex)
            {
                WriteBootstrapLog($"Could not parse existing configuration JSON: {ex.Message}. Starting with empty config.", LogLevel.Warning);
                existingConfig = new Dictionary<string, JsonElement>();
            }
        }
        else
        {
            existingConfig = new Dictionary<string, JsonElement>();
            WriteBootstrapLog("No existing configuration found. Starting with empty config.");
        }

        // Parse the new section value
        try
        {
            JsonElement newSectionValue = JsonSerializer.Deserialize<JsonElement>(configKeyJsonValue, HostConfigJsonContext.Default.JsonElement);

            // Update or add the section
            existingConfig[configKey] = newSectionValue;
            WriteBootstrapLog($"Configuration section '{configKey}' updated.");
        }
        catch (Exception ex)
        {
            WriteBootstrapLog($"Could not parse configuration value for key '{configKey}': {ex.Message}", LogLevel.Error);
            throw;
        }

        // Serialize back to JSON string
        string updatedConfigJson = JsonSerializer.Serialize(existingConfig, HostConfigJsonContext.Default.DictionaryStringJsonElement);
        WriteBootstrapLog("Updated configuration serialized to JSON string.");

        // Persist updated config back to Local Storage
        await Task.Run(() => JSInterop.SetLocalStorage(localStorageKey, updatedConfigJson));
        WriteBootstrapLog("Updated configuration JSON string saved to browser local storage.");
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
                WriteBootstrapLog($"Could not get origin from command line args: {errorMessage}", LogLevel.Warning);
            }
            catch
            {
                WriteBootstrapLog("Could not get origin from command line args: Unable to access exception details", LogLevel.Warning);
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
        BrowserGamepad? browserGamepad = null)
    {
        return AppBuilder.Configure(() =>
        {
            return new App(
                                configuration,
                                emulatorConfig,
                                logStore,
                                logConfig,
                                loggerFactory,
                                saveCustomConfigString: PersistStringToLocalStorage,
                                saveCustomConfigSection: PersistConfigSectionToLocalStorage,
                                gamepad: browserGamepad
                            );
        })
        .AfterSetup(_ =>
        {
            // Set up the Avalonia logger bridge to route logs via Avalonia Logger through ILogger
            global::Avalonia.Logging.Logger.Sink = avaloniaLoggerBridge;
        });
    }
}
