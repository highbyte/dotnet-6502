using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using System.Text.Json;
using Avalonia;
using Avalonia.Browser;
using Avalonia.Threading;
using Highbyte.DotNet6502.App.Avalonia.Core;
using Highbyte.DotNet6502.Impl.Avalonia.Logging;
using Highbyte.DotNet6502.Impl.Browser.Input;
using Highbyte.DotNet6502.Impl.NAudio.WavePlayers.WebAudioAPI;
using Highbyte.DotNet6502.Scripting.MoonSharp;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Logging.Console;
using Highbyte.DotNet6502.Systems.Logging.InMem;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

internal sealed partial class Program
{
    private const string LOCAL_STORAGE_MAIN_CONFIG_KEY = "dotnet6502.emulator.avalonia.config";
    private const string LOCAL_STORAGE_SCRIPT_PREFIX = "dotnet6502.lua.";
    private const string LOCAL_STORAGE_STORE_PREFIX = "dotnet6502.store.";

    private static readonly string[] _exampleScriptNames =
    [
        "example_monitor.lua",
        "example_store.lua",
        "example_http.lua",
        "example_frameadvance.lua",
        "example_emulator_control.lua",
        "example_c64_border_cycle.lua",
        "example_c64_download_and_run_prg.lua",
    ];

    /// <summary>
    /// Application entry point.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>HTTP Query Parameters (URL-driven automation):</b>
    /// </para>
    /// <list type="table">
    ///   <listheader>
    ///     <term>Parameter</term>
    ///     <description>Description</description>
    ///   </listheader>
    ///   <item>
    ///     <term><c>system</c></term>
    ///     <description>
    ///       Pre-select a system (e.g. <c>C64</c>, <c>Generic</c>). Mirrors desktop <c>--system</c>.
    ///     </description>
    ///   </item>
    ///   <item>
    ///     <term><c>systemVariant</c></term>
    ///     <description>
    ///       Pre-select a system variant. Requires <c>system</c>. Mirrors desktop <c>--systemVariant</c>.
    ///     </description>
    ///   </item>
    ///   <item>
    ///     <term><c>start</c></term>
    ///     <description>
    ///       Auto-start the emulator after selection (boolean flag, e.g. <c>start=1</c>).
    ///       Mirrors desktop <c>--start</c>.
    ///     </description>
    ///   </item>
    ///   <item>
    ///     <term><c>waitForSystemReady</c></term>
    ///     <description>
    ///       Wait until the system reports ready (e.g. C64 BASIC prompt). Requires <c>start</c>.
    ///       Mirrors desktop <c>--waitForSystemReady</c>.
    ///     </description>
    ///   </item>
    /// </list>
    /// <para>
    /// Boolean flags accept <c>1</c>, <c>true</c>, <c>yes</c>, or an empty value as truthy
    /// (case-insensitive). All keys are case-insensitive.
    /// </para>
    /// <para>
    /// <b>Example:</b> <c>?system=C64&amp;systemVariant=PAL&amp;start=1&amp;waitForSystemReady=1</c>
    /// </para>
    /// </remarks>
    private static async Task<int> Main(string[] args)
    {
        AppLogger.ConsoleLoggingEnabled = true;
        WriteBootstrapLog("Avalonia program starting.");

        // Parse URL query parameters for automated startup. The browser host passes
        // globalThis.location.href as args[0] (see wwwroot/main.js).
        var automation = ParseAutomationQuery(args.Length > 0 ? args[0] : null);
        if (automation.SystemName != null)
        {
            WriteBootstrapLog($"Automation requested via query: system={automation.SystemName}, " +
                $"systemVariant={automation.SystemVariant ?? "(none)"}, start={automation.AutoStart}, " +
                $"waitForSystemReady={automation.WaitForSystemReady}");
        }

        // Load configuration from Browser Local Storage using source-generated JSON serialization
        string configJson = await GetConfigStringFromLocalStorageAsync(LOCAL_STORAGE_MAIN_CONFIG_KEY);
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

        // Set the Lua store prefix for display in the settings UI (browser only)
        emulatorConfig.LuaStorePrefix = LOCAL_STORAGE_STORE_PREFIX;

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

        // Load custom JS module for localStorage-based Lua script loading
        WriteBootstrapLog("Importing BrowserScripting JS module.");
        await JSHost.ImportAsync("BrowserScripting", BrowserScriptingResources.GetJavaScriptModuleDataUri());

        // Create scripting engine (loads scripts from localStorage if enabled in config)
        // Binding is done here (not inside the library) so the AOT ConfigurationBindingGenerator
        // can see the ScriptingConfig call site and generate trim-safe code.
        WriteBootstrapLog("Reading scripting config.");
        var scriptingConfig = GetScriptingConfig(configuration);
        scriptingConfig.ScriptLoader = LoadScriptsFromLocalStorage;

        // Wire localStorage-backed store backend (browser-only)
        if (scriptingConfig.AllowStore)
        {
            scriptingConfig.StoreBackend = new DelegateScriptStore(
                get: key => JSInterop.GetLocalStorage($"{LOCAL_STORAGE_STORE_PREFIX}{key}"),
                set: (key, val) => JSInterop.SetLocalStorage($"{LOCAL_STORAGE_STORE_PREFIX}{key}", val),
                delete: key => JSInterop.RemoveLocalStorage($"{LOCAL_STORAGE_STORE_PREFIX}{key}"),
                list: () =>
                {
                    var json = JSInterop.GetLocalStorageKeys(LOCAL_STORAGE_STORE_PREFIX);
                    if (string.IsNullOrEmpty(json)) return [];
                    return JsonSerializer.Deserialize(json, HostConfigJsonContext.Default.ListString) ?? [];
                });
        }

        WriteBootstrapLog("Creating scripting engine.");
        var scriptingEngine = MoonSharpScriptingConfigurator.CreateForBrowser(
            scriptingConfig, loggerFactory);

        // Script persistence callbacks (localStorage-backed, browser-only)
        string? LoadScript(string name) => JSInterop.GetLocalStorage($"{LOCAL_STORAGE_SCRIPT_PREFIX}{name}");
        void SaveScript(string name, string content) => JSInterop.SetLocalStorage($"{LOCAL_STORAGE_SCRIPT_PREFIX}{name}", content);
        void DeleteScript(string name) => JSInterop.RemoveLocalStorage($"{LOCAL_STORAGE_SCRIPT_PREFIX}{name}");

        // Load-examples callback: fetches bundled scripts and saves any that are not yet in localStorage
        Task LoadExamplesAsync() => SeedExampleScriptsAsync(SaveScript);

        // Build an automated-startup runner that MainViewModel.InitializeAsync will invoke once
        // the Avalonia view tree is fully loaded. The runner selects the system synchronously,
        // then defers the actual emulator Start (and any post-Start work) to the UI dispatcher
        // at Background priority via lifecycleInvoker — that lets the framework finish its
        // initial Loaded/Render passes before the C64 frame loop starts hammering the UI thread.
        // The handler itself stays UI-framework-agnostic; only this delegate knows about Avalonia.
        Func<IHostApp, Task>? automatedStartupRunner = null;
        if (automation.SystemName != null)
        {
            automatedStartupRunner = async hostApp =>
            {
                var startupLogger = loggerFactory.CreateLogger(nameof(Program));
                try
                {
                    await AutomatedStartupHandler.ExecuteAsync(
                        hostApp,
                        automation.SystemName,
                        automation.SystemVariant,
                        automation.AutoStart,
                        automation.WaitForSystemReady,
                        loadPrgPath: null,
                        runLoadedProgram: false,
                        enableExternalDebug: false,
                        onStartupComplete: null,
                        loggerFactory: loggerFactory,
                        prepareForExternalDebuggerStart: null,
                        onFatalError: () => startupLogger.LogError("Automated startup aborted; falling back to default UI."),
                        lifecycleInvoker: lifecycle =>
                        {
                            startupLogger.LogInformation("Deferring auto-start lifecycle to UI dispatcher Background priority");
                            Dispatcher.UIThread.Post(
                                () =>
                                {
                                    var task = lifecycle();
                                    // Observe exceptions from the deferred fire-and-forget task
                                    // (handler swallows expected errors via onFatalError; this
                                    // catches any unexpected ones so the WASM runtime stays alive).
                                    _ = task.ContinueWith(
                                        t =>
                                        {
                                            if (t.IsFaulted && t.Exception != null)
                                                startupLogger.LogError(t.Exception, "Deferred auto-start lifecycle threw");
                                        },
                                        TaskScheduler.Default);
                                },
                                DispatcherPriority.Background);
                            return Task.CompletedTask;
                        });
                }
                catch (Exception ex)
                {
                    WriteBootstrapLog($"Automated startup runner failed: {ex.Message}", LogLevel.Error);
                }
            };
        }


        // Start Avalonia app
        try
        {
            WriteBootstrapLog("Starting Avalonia Browser app...");
            await BuildAvaloniaApp(configuration, emulatorConfig, logStore, logConfig, loggerFactory, avaloniaLoggerBridge, browserGamepad, scriptingEngine, LoadScript, SaveScript, DeleteScript, LoadExamplesAsync, automatedStartupRunner: automatedStartupRunner)
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

    /// <summary>
    /// Parsed automation parameters from the URL query string. <see cref="SystemName"/> is null
    /// when no automation was requested.
    /// </summary>
    private sealed record BrowserAutomationParams(
        string? SystemName,
        string? SystemVariant,
        bool AutoStart,
        bool WaitForSystemReady);

    /// <summary>
    /// Parses URL query parameters into an automation request. Logs (and clears) any
    /// invalid combinations rather than aborting startup, so the regular UI still loads.
    /// </summary>
    private static BrowserAutomationParams ParseAutomationQuery(string? url)
    {
        var empty = new BrowserAutomationParams(null, null, false, false);
        if (string.IsNullOrWhiteSpace(url))
            return empty;

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || string.IsNullOrEmpty(uri.Query))
            return empty;

        // Build a case-insensitive map. Last value wins on duplicate keys.
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var raw = uri.Query.StartsWith('?') ? uri.Query[1..] : uri.Query;
        foreach (var pair in raw.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var eq = pair.IndexOf('=');
            string key, value;
            if (eq < 0)
            {
                key = Uri.UnescapeDataString(pair);
                value = string.Empty;
            }
            else
            {
                key = Uri.UnescapeDataString(pair[..eq]);
                value = Uri.UnescapeDataString(pair[(eq + 1)..]);
            }
            if (!string.IsNullOrEmpty(key))
                map[key] = value;
        }

        static bool IsTruthy(string v) =>
            v.Length == 0 ||
            v.Equals("1", StringComparison.Ordinal) ||
            v.Equals("true", StringComparison.OrdinalIgnoreCase) ||
            v.Equals("yes", StringComparison.OrdinalIgnoreCase);

        string? systemName = map.TryGetValue("system", out var s) && !string.IsNullOrWhiteSpace(s) ? s : null;
        string? systemVariant = map.TryGetValue("systemVariant", out var v) && !string.IsNullOrWhiteSpace(v) ? v : null;
        bool autoStart = map.TryGetValue("start", out var st) && IsTruthy(st);
        bool waitForReady = map.TryGetValue("waitForSystemReady", out var w) && IsTruthy(w);

        // systemVariant requires system
        if (systemVariant != null && systemName == null)
        {
            WriteBootstrapLog("Query parameter 'systemVariant' requires 'system'; ignoring 'systemVariant'.", LogLevel.Warning);
            systemVariant = null;
        }

        // start / waitForSystemReady require system
        if (systemName == null && (autoStart || waitForReady))
        {
            WriteBootstrapLog("Query parameters 'start' and 'waitForSystemReady' require 'system'; ignoring.", LogLevel.Warning);
            autoStart = false;
            waitForReady = false;
        }

        // waitForSystemReady requires start
        if (waitForReady && !autoStart)
        {
            WriteBootstrapLog("Query parameter 'waitForSystemReady' requires 'start'; ignoring 'waitForSystemReady'.", LogLevel.Warning);
            waitForReady = false;
        }

        return new BrowserAutomationParams(systemName, systemVariant, autoStart, waitForReady);
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

    private static ScriptingConfig GetScriptingConfig(IConfiguration configuration)
    {
        var section = configuration.GetSection(ScriptingConfig.ConfigSectionName);

        return new ScriptingConfig
        {
            Enabled = section.GetValue(nameof(ScriptingConfig.Enabled), true),
            ScriptDirectory = section.GetValue(nameof(ScriptingConfig.ScriptDirectory), string.Empty) ?? string.Empty,
            MaxExecutionWarningMs = section.GetValue(nameof(ScriptingConfig.MaxExecutionWarningMs), 5),
            MaxInstructionsPerResume = section.GetValue(nameof(ScriptingConfig.MaxInstructionsPerResume), 1_000_000),
            EnableScriptsAtStart = section.GetValue(nameof(ScriptingConfig.EnableScriptsAtStart), false),
            AllowFileIO = section.GetValue(nameof(ScriptingConfig.AllowFileIO), true),
            AllowFileWrite = section.GetValue(nameof(ScriptingConfig.AllowFileWrite), false),
            FileBaseDirectory = section.GetValue<string?>(nameof(ScriptingConfig.FileBaseDirectory)),
            AllowHttpRequests = section.GetValue(nameof(ScriptingConfig.AllowHttpRequests), true),
            AllowTcpClient = section.GetValue(nameof(ScriptingConfig.AllowTcpClient), false),
            AllowStore = section.GetValue(nameof(ScriptingConfig.AllowStore), true),
            StoreSubDirectory = section.GetValue(nameof(ScriptingConfig.StoreSubDirectory), ".store") ?? ".store",
        };
    }

    [SupportedOSPlatform("browser")]
    internal static partial class JSInterop
    {
        [JSImport("globalThis.localStorage.getItem")]
        public static partial string? GetLocalStorage(string key);

        [JSImport("globalThis.localStorage.setItem")]
        public static partial void SetLocalStorage(string key, string? value);

        [JSImport("globalThis.localStorage.removeItem")]
        public static partial void RemoveLocalStorage(string key);

        [JSImport("getScriptsFromLocalStorage", "BrowserScripting")]
        public static partial string GetScriptsFromLocalStorage(string prefix);

        [JSImport("getLocalStorageKeys", "BrowserScripting")]
        public static partial string GetLocalStorageKeys(string prefix);
    }

    private static async Task SeedExampleScriptsAsync(Action<string, string> saveScript)
    {
        using var http = GetAppUrlHttpClient();
        foreach (var scriptName in _exampleScriptNames)
        {
            var key = $"{LOCAL_STORAGE_SCRIPT_PREFIX}{scriptName}";
            if (JSInterop.GetLocalStorage(key) != null)
                continue;   // already exists (user content) — skip

            try
            {
                var content = await http.GetStringAsync($"scripts/{scriptName}");
                saveScript(scriptName, content);   // writes localStorage + hot-adds to engine
                WriteBootstrapLog($"Seeded example script: {scriptName}");
            }
            catch (Exception ex)
            {
                WriteBootstrapLog($"Could not seed example script '{scriptName}': {ex.Message}", LogLevel.Warning);
            }
        }
    }

    private static IEnumerable<(string fileName, string content)> LoadScriptsFromLocalStorage()
    {
        try
        {
            var json = JSInterop.GetScriptsFromLocalStorage(LOCAL_STORAGE_SCRIPT_PREFIX);
            if (string.IsNullOrEmpty(json)) return [];
            var scripts = JsonSerializer.Deserialize(json, HostConfigJsonContext.Default.ListLocalStorageScript);
            return scripts?.OrderBy(s => s.name).Select(s => (s.name, s.content)) ?? [];
        }
        catch (Exception ex)
        {
            WriteBootstrapLog($"Could not load scripts from localStorage: {ex.Message}", LogLevel.Warning);
            return [];
        }
    }

    private static async Task<string> GetConfigStringFromLocalStorageAsync(string configKey)
    {
        var jsonString = await Task.Run(() => JSInterop.GetLocalStorage(configKey) ?? string.Empty);
        return jsonString;
    }

    private static async Task PersistStringToLocalStorageAsync(string configKey, string configKeyJsonValue, string? localStorageKey = LOCAL_STORAGE_MAIN_CONFIG_KEY)
    {
        if (string.IsNullOrEmpty(localStorageKey))
            localStorageKey = LOCAL_STORAGE_MAIN_CONFIG_KEY;

        // Load existing config from Local Storage
        string configJson = await GetConfigStringFromLocalStorageAsync(localStorageKey);
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

    private static async Task PersistConfigSectionToLocalStorageAsync(string configSectionName, IConfigurationSection configKeySection, string? localStorageKey = LOCAL_STORAGE_MAIN_CONFIG_KEY)
    {
        // Serialize the IConfigurationSection to JSON string using source-generated serialization
        var configKeyJsonValue = JsonSerializer.Serialize(configKeySection.GetChildren().ToDictionary(x => x.Key, x => x.Value), HostConfigJsonContext.Default.DictionaryStringString);

        // Persist the JSON string to Local Storage
        await PersistStringToLocalStorageAsync(configSectionName, configKeyJsonValue);
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
        BrowserGamepad? browserGamepad = null,
        IScriptingEngine? scriptingEngine = null,
        Func<string, string?>? loadScript = null,
        Action<string, string>? saveScript = null,
        Action<string>? deleteScript = null,
        Func<Task>? loadExamples = null,
        Func<IHostApp, Task>? automatedStartupRunner = null)
    {
        return AppBuilder.Configure(() =>
        {
            return new App(
                                configuration,
                                emulatorConfig,
                                logStore,
                                logConfig,
                                loggerFactory,
                                saveCustomConfigString: PersistStringToLocalStorageAsync,
                                saveCustomConfigSection: PersistConfigSectionToLocalStorageAsync,
                                gamepad: browserGamepad,
                                scriptingEngine: scriptingEngine,
                                loadScript: loadScript,
                                saveScript: saveScript,
                                deleteScript: deleteScript,
                                loadExamples: loadExamples,
                                automatedStartupRunner: automatedStartupRunner
                            );
        })
        .AfterSetup(_ =>
        {
            // Set up the Avalonia logger bridge to route logs via Avalonia Logger through ILogger
            global::Avalonia.Logging.Logger.Sink = avaloniaLoggerBridge;
        });
    }
}
