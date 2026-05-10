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
using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Systems.Logging.Console;
using Highbyte.DotNet6502.Systems.Logging.InMem;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

internal sealed partial class Program
{
    private const string LOCAL_STORAGE_MAIN_CONFIG_KEY = "dotnet6502.emulator.avalonia.config";
    private const string LOCAL_STORAGE_SCRIPT_PREFIX = "dotnet6502.lua.";
    private const string LOCAL_STORAGE_STORE_PREFIX = "dotnet6502.store.";

    // Synthetic file name used for a URL-injected Lua script. Kept transient (not persisted to
    // localStorage); LoadScript() falls back to the in-memory content for this name so the
    // script editor can show / edit it during the current session.
    private const string URL_INJECTED_SCRIPT_NAME = "__url_script.lua";

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
    ///   <item>
    ///     <term><c>loadPrgUrl</c></term>
    ///     <description>
    ///       URL (relative to the app origin or absolute) to fetch a <c>.prg</c> file from after
    ///       the system has started. The fetched bytes must include the standard 2-byte
    ///       little-endian load-address header. Requires <c>system</c> and <c>start</c>.
    ///       Browser-equivalent of desktop <c>--loadPrg &lt;path&gt;</c>.
    ///     </description>
    ///   </item>
    ///   <item>
    ///     <term><c>runLoadedProgram</c></term>
    ///     <description>
    ///       After PRG load, redirect <c>CPU.PC</c> to the load address so execution starts at
    ///       the program. Requires <c>loadPrgUrl</c>. Mirrors desktop <c>--runLoadedProgram</c>.
    ///     </description>
    ///   </item>
    ///   <item>
    ///     <term><c>basicText</c></term>
    ///     <description>
    ///       Base64url-encoded inline C64 BASIC source text (UTF-8) to paste into the running
    ///       C64 after BASIC is ready. Requires <c>system=C64</c>, <c>start</c>, and
    ///       <c>waitForSystemReady</c>. Mutually exclusive with <c>loadPrgUrl</c> /
    ///       <c>runLoadedProgram</c>.
    ///     </description>
    ///   </item>
    ///   <item>
    ///     <term><c>basicUrl</c></term>
    ///     <description>
    ///       URL (relative or absolute) to fetch C64 BASIC source text from. Same semantics as
    ///       <c>basicText</c> but unconstrained by URL length. Requires <c>system=C64</c>,
    ///       <c>start</c>, and <c>waitForSystemReady</c>. Mutually exclusive with
    ///       <c>loadPrgUrl</c> / <c>runLoadedProgram</c>.
    ///     </description>
    ///   </item>
    ///   <item>
    ///     <term><c>runBasic</c></term>
    ///     <description>
    ///       After <c>basicText</c> / <c>basicUrl</c> has been pasted, queue <c>run</c> followed
    ///       by Return. (Lower-case — the C64 keyboard buffer expects unshifted characters; an
    ///       upper-case <c>RUN</c> would arrive as the graphic / shifted glyphs and not be
    ///       recognised by BASIC.) Requires <c>basicText</c> or <c>basicUrl</c>.
    ///     </description>
    ///   </item>
    ///   <item>
    ///     <term><c>script</c></term>
    ///     <description>
    ///       Base64url-encoded inline Lua script to load and auto-enable at startup. The script
    ///       owns the emulator lifecycle, so this parameter is mutually exclusive with
    ///       <c>system</c>, <c>start</c>, <c>waitForSystemReady</c>, <c>loadPrgUrl</c>,
    ///       <c>runLoadedProgram</c>, <c>basicText</c>, <c>basicUrl</c>, and <c>runBasic</c>.
    ///       Gated by <c>Scripting.AllowUrlScripts</c> (default false).
    ///     </description>
    ///   </item>
    ///   <item>
    ///     <term><c>scriptUrl</c></term>
    ///     <description>
    ///       URL (relative or absolute) to fetch a Lua script from. Same semantics as
    ///       <c>script</c> but unconstrained by URL length. Mutually exclusive with <c>script</c>,
    ///       and with the system-driven parameters above. Gated by
    ///       <c>Scripting.AllowUrlScripts</c> (default false).
    ///     </description>
    ///   </item>
    /// </list>
    /// <para>
    /// Boolean flags accept <c>1</c>, <c>true</c>, <c>yes</c>, or an empty value as truthy
    /// (case-insensitive). All keys are case-insensitive.
    /// </para>
    /// <para>
    /// <b>Examples:</b>
    /// </para>
    /// <code>
    /// # System-driven: select C64 (PAL), start, wait for BASIC ready
    /// ?system=C64&amp;systemVariant=PAL&amp;start=1&amp;waitForSystemReady=1
    ///
    /// # Load and run a bundled PRG (same-origin URL)
    /// ?system=C64&amp;start=1&amp;waitForSystemReady=1&amp;loadPrgUrl=prg/c64/smooth_scroller_and_raster.prg&amp;runLoadedProgram=1
    ///
    /// # Paste BASIC source from a browser-served text file and run it
    /// ?system=C64&amp;start=1&amp;waitForSystemReady=1&amp;basicUrl=basic/c64/hello-world.bas&amp;runBasic=1
    ///
    /// # Paste inline BASIC source and run it
    /// ?system=C64&amp;start=1&amp;waitForSystemReady=1&amp;basicText=MTAgYzE9NzpjMj0xNAoyMCBjPWMxCjMwIGlmIGM9YzEgdGhlbiBjPWMyIDogZ290byA1MAo0MCBpZiBjPWMyIHRoZW4gYz1jMQo1MCBwb2tlIDUzMjgwLGMKNjAgcHJpbnQgImhlbGxvIHdvcmxkISIKNzAgZm9yIGk9MSB0byAxNTA6bmV4dAo4MCBnb3RvIDMwCg&amp;runBasic=1
    ///
    /// # Run an inline Lua script (requires Scripting.AllowUrlScripts=true in browser localStorage config)
    /// ?script=bG9nLmluZm8oJ2hlbGxvJyk        # base64url of: log.info('hello')
    /// # (Lua scripts in this emulator use `log.info`/`log.debug`/`log.warn`/`log.error` — `print` is not defined.)
    ///
    /// # Run a Lua script fetched from a URL
    /// ?scriptUrl=scripts/example_emulator_control.lua
    /// </code>
    /// <para>
    /// <b>Security note:</b> URL-supplied Lua executes against the user's emulator and
    /// localStorage. The <c>script</c> / <c>scriptUrl</c> parameters are therefore disabled by
    /// default and must be opted in via the <c>Scripting.AllowUrlScripts</c> config flag stored
    /// in browser localStorage. <c>loadPrgUrl</c> bytes are inert until the user (or the same
    /// query via <c>runLoadedProgram</c>) directs the CPU to execute them, so it is not gated.
    /// <c>basicText</c> / <c>basicUrl</c> use the normal C64 keyboard paste path and are limited
    /// to the C64 system after BASIC reports ready.
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
                $"waitForSystemReady={automation.WaitForSystemReady}, " +
                $"loadPrgUrl={automation.LoadPrgUrl ?? "(none)"}, runLoadedProgram={automation.RunLoadedProgram}, " +
                $"basicText={(automation.BasicText != null ? $"{automation.BasicText.Length} chars" : "(none)")}, " +
                $"basicUrl={automation.BasicUrl ?? "(none)"}, runBasic={automation.RunBasic}");
        }
        else if (automation.ScriptContent != null || automation.ScriptUrl != null)
        {
            WriteBootstrapLog(automation.ScriptUrl != null
                ? $"Automation requested via query: scriptUrl={automation.ScriptUrl}"
                : $"Automation requested via query: inline script ({automation.ScriptContent!.Length} chars)");
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

        // Resolve URL-supplied script (inline base64url or fetched from URL) before the engine
        // is built. Gated by ScriptingConfig.AllowUrlScripts (default false) so a crafted link
        // can't execute Lua against the user's emulator/localStorage without explicit opt-in.
        string? urlScriptContent = null;
        bool scriptDrivenAutomation = false;
        if (automation.ScriptContent != null || automation.ScriptUrl != null)
        {
            if (!scriptingConfig.AllowUrlScripts)
            {
                WriteBootstrapLog(
                    "URL-driven script requested via 'script'/'scriptUrl' but Scripting.AllowUrlScripts=false; ignoring. " +
                    "Set Scripting.AllowUrlScripts=true in browser localStorage config to enable.",
                    LogLevel.Error);
            }
            else if (automation.ScriptContent != null)
            {
                urlScriptContent = automation.ScriptContent;
                WriteBootstrapLog($"Using inline 'script' query parameter ({urlScriptContent.Length} chars decoded).");
            }
            else if (automation.ScriptUrl != null)
            {
                try
                {
                    using var http = GetAppUrlHttpClient();
                    urlScriptContent = await http.GetStringAsync(automation.ScriptUrl);
                    WriteBootstrapLog($"Fetched 'scriptUrl' from {automation.ScriptUrl} ({urlScriptContent.Length} chars).");
                }
                catch (Exception ex)
                {
                    WriteBootstrapLog($"Failed to fetch 'scriptUrl' '{automation.ScriptUrl}': {ex.Message}", LogLevel.Error);
                }
            }
        }
        if (urlScriptContent != null)
        {
            var localStorageLoader = scriptingConfig.ScriptLoader!;
            var injected = urlScriptContent;
            scriptingConfig.ScriptLoader = () => localStorageLoader().Append((URL_INJECTED_SCRIPT_NAME, injected));
            // Note: we deliberately leave EnableScriptsAtStart=false so the bundled localStorage
            // example scripts don't auto-enable (some of them call emu.start() at top level which
            // would race the URL script + the view tree's first paint). The URL script is enabled
            // selectively from the runner below, after MainView has loaded.
            scriptDrivenAutomation = true;
            WriteBootstrapLog("Injected URL-supplied script; will be enabled after view tree is loaded.");
        }

        WriteBootstrapLog("Creating scripting engine.");
        var scriptingEngine = MoonSharpScriptingConfigurator.CreateForBrowser(
            scriptingConfig, loggerFactory);

        // Script persistence callbacks (localStorage-backed, browser-only).
        // LoadScript falls back to the in-memory URL-injected script when localStorage misses,
        // so the script editor can display the URL script's content during the current session.
        // (Saving from the editor will persist it to localStorage under the synthetic name —
        // intentional, so users can keep an edited URL script across reloads if they choose.)
        string? LoadScript(string name)
        {
            var fromLocalStorage = JSInterop.GetLocalStorage($"{LOCAL_STORAGE_SCRIPT_PREFIX}{name}");
            if (fromLocalStorage != null)
                return fromLocalStorage;
            if (name == URL_INJECTED_SCRIPT_NAME && urlScriptContent != null)
                return urlScriptContent;
            return null;
        }
        void SaveScript(string name, string content) => JSInterop.SetLocalStorage($"{LOCAL_STORAGE_SCRIPT_PREFIX}{name}", content);
        void DeleteScript(string name) => JSInterop.RemoveLocalStorage($"{LOCAL_STORAGE_SCRIPT_PREFIX}{name}");

        // Load-examples callback: fetches bundled scripts and saves any that are not yet in localStorage
        Task LoadExamplesAsync() => SeedExampleScriptsAsync(SaveScript);

        // Build an automated-startup runner that MainViewModel.InitializeAsync will invoke once
        // the Avalonia view tree is fully loaded. The runner is one of three things:
        //  - URL script-driven  → no-op (script owns the lifecycle; we just suppress default selection).
        //  - URL system-driven  → AutomatedStartupHandler with optional loadPrgUrl fetch and/or
        //                          C64 BASIC text paste after the system reports ready.
        //  - neither            → null (default system selection runs).
        // For system-driven, the post-selection lifecycle is deferred to the UI dispatcher at
        // Background priority via lifecycleInvoker so the framework finishes its initial
        // Loaded/Render passes before the emulator starts hammering the UI thread.
        Func<IHostApp, Task>? automatedStartupRunner = null;
        if (scriptDrivenAutomation)
        {
            // Browser URL scripts run alongside the normal UI: do the regular default system
            // selection first (so the System combobox isn't blank), then enable the URL script.
            // The script can still override via `emu.select(...)` / `emu.start()` if it wants.
            // This differs intentionally from desktop's `--script` which is full-automation.
            //
            // Enabling the URL script runs its top-level Lua code inline (via InitialResume).
            // If the script calls emu.start() at top level, the action queue drains shortly
            // after and the C64 frame timer starts hammering the UI thread. We defer
            // SetScriptEnabled to Background priority so the framework completes its initial
            // Loaded/Render passes first — same reasoning as the lifecycleInvoker pattern used
            // for system-driven URL automation.
            var engine = scriptingEngine;
            var defaultSystem = emulatorConfig.DefaultEmulator;
            automatedStartupRunner = async hostApp =>
            {
                if (!string.IsNullOrEmpty(defaultSystem))
                {
                    WriteBootstrapLog($"Selecting default system '{defaultSystem}' before enabling URL script.");
                    await hostApp.SelectSystem(defaultSystem);
                }

                WriteBootstrapLog($"Deferring URL-injected script '{URL_INJECTED_SCRIPT_NAME}' to UI dispatcher Background priority.");
                _ = Dispatcher.UIThread.InvokeAsync(
                    async () =>
                    {
                        try
                        {
                            WriteBootstrapLog($"Enabling URL-injected script '{URL_INJECTED_SCRIPT_NAME}'.");
                            engine.SetScriptEnabled(URL_INJECTED_SCRIPT_NAME, true);

                            // Drain any actions the script's top-level code enqueued (e.g.
                            // emu.start) immediately, on the same Background-priority dispatch.
                            // Otherwise the script-tick timer would drain them at Render
                            // priority on its next tick — racing the framework's first paint
                            // with a tight FrameTimer-driven Render-priority loop and hanging
                            // the UI. Same reasoning as the system-driven lifecycleInvoker.
                            await engine.DrainPendingActionsAsync();
                        }
                        catch (Exception ex)
                        {
                            WriteBootstrapLog($"Enabling URL script threw: {ex.GetType().Name}: {ex.Message}", LogLevel.Error);
                        }
                    },
                    DispatcherPriority.Background);
            };
        }
        else if (automation.SystemName != null)
        {
            automatedStartupRunner = async hostApp =>
            {
                var startupLogger = loggerFactory.CreateLogger(nameof(Program));

                // If a PRG URL is set, build a bytes provider that fetches over HTTP using the
                // same HttpClient (with current app origin) that emulatorConfig already uses.
                Func<Task<byte[]>>? prgBytesProvider = null;
                if (automation.LoadPrgUrl != null)
                {
                    var prgUrl = automation.LoadPrgUrl;
                    prgBytesProvider = async () =>
                    {
                        using var http = GetAppUrlHttpClient();
                        startupLogger.LogInformation($"Fetching PRG from '{prgUrl}'...");
                        var bytes = await http.GetByteArrayAsync(prgUrl);
                        startupLogger.LogInformation($"Fetched {bytes.Length} bytes from '{prgUrl}'.");
                        return bytes;
                    };
                }

                string? basicSourceText = automation.BasicText;
                if (automation.BasicUrl != null)
                {
                    try
                    {
                        using var http = GetAppUrlHttpClient();
                        basicSourceText = await http.GetStringAsync(automation.BasicUrl);
                        if (string.IsNullOrWhiteSpace(basicSourceText))
                        {
                            startupLogger.LogError($"Fetched 'basicUrl' '{automation.BasicUrl}' but it contained no BASIC source.");
                            basicSourceText = null;
                        }
                        else
                        {
                            startupLogger.LogInformation($"Fetched BASIC source from '{automation.BasicUrl}' ({basicSourceText.Length} chars).");
                        }
                    }
                    catch (Exception ex)
                    {
                        startupLogger.LogError($"Failed to fetch 'basicUrl' '{automation.BasicUrl}': {ex.Message}");
                    }
                }

                Action? onStartupComplete = null;
                if (!string.IsNullOrWhiteSpace(basicSourceText))
                {
                    var basicSourceToPaste = BuildC64BasicPasteText(basicSourceText, automation.RunBasic);
                    var basicSourceDescription = automation.BasicUrl != null
                        ? $"basicUrl={automation.BasicUrl}"
                        : $"basicText ({basicSourceText.Length} chars)";
                    var basicLineCount = CountNonEmptyLines(basicSourceText);
                    onStartupComplete = () =>
                    {
                        if (hostApp.CurrentRunningSystem is not C64 c64)
                        {
                            startupLogger.LogError("C64 BASIC source automation requires the running system to be C64.");
                            return;
                        }

                        startupLogger.LogInformation(
                            $"Queueing C64 BASIC source from {basicSourceDescription} " +
                            $"({basicLineCount} line(s), runBasic={automation.RunBasic}).");
                        c64.TextPaste.Paste(basicSourceToPaste);
                    };
                }

                try
                {
                    await AutomatedStartupHandler.ExecuteAsync(
                        hostApp,
                        automation.SystemName,
                        automation.SystemVariant,
                        automation.AutoStart,
                        automation.WaitForSystemReady,
                        loadPrgPath: null,
                        runLoadedProgram: automation.RunLoadedProgram,
                        enableExternalDebug: false,
                        onStartupComplete: onStartupComplete,
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
                        },
                        loadPrgBytesProvider: prgBytesProvider);
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
    /// when no system-driven automation was requested. <see cref="ScriptContent"/> /
    /// <see cref="ScriptUrl"/> are mutually exclusive with the system fields and own the
    /// emulator lifecycle when present. <see cref="BasicText"/> / <see cref="BasicUrl"/> are
    /// C64-only system-driven automation helpers that queue BASIC source via the normal keyboard
    /// paste path once BASIC is ready.
    /// </summary>
    private sealed record BrowserAutomationParams(
        string? SystemName,
        string? SystemVariant,
        bool AutoStart,
        bool WaitForSystemReady,
        string? LoadPrgUrl,
        bool RunLoadedProgram,
        string? BasicText,
        string? BasicUrl,
        bool RunBasic,
        string? ScriptContent,
        string? ScriptUrl);

    /// <summary>
    /// Parses URL query parameters into an automation request. Logs (and clears) any
    /// invalid combinations rather than aborting startup, so the regular UI still loads.
    /// </summary>
    private static BrowserAutomationParams ParseAutomationQuery(string? url)
    {
        var empty = new BrowserAutomationParams(null, null, false, false, null, false, null, null, false, null, null);
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
        string? loadPrgUrl = map.TryGetValue("loadPrgUrl", out var lp) && !string.IsNullOrWhiteSpace(lp) ? lp : null;
        bool runLoaded = map.TryGetValue("runLoadedProgram", out var rl) && IsTruthy(rl);
        string? basicB64 = map.TryGetValue("basicText", out var bt) && !string.IsNullOrWhiteSpace(bt) ? bt : null;
        string? basicUrl = map.TryGetValue("basicUrl", out var bu) && !string.IsNullOrWhiteSpace(bu) ? bu : null;
        bool runBasic = map.TryGetValue("runBasic", out var rb) && IsTruthy(rb);
        string? scriptB64 = map.TryGetValue("script", out var sc) && !string.IsNullOrWhiteSpace(sc) ? sc : null;
        string? scriptUrl = map.TryGetValue("scriptUrl", out var su) && !string.IsNullOrWhiteSpace(su) ? su : null;

        // Decode base64url-encoded inline script (RFC 4648 §5: '-' and '_' substitute '+' '/'; padding optional).
        string? basicText = DecodeBase64UrlUtf8QueryValue("basicText", basicB64);
        string? scriptContent = DecodeBase64UrlUtf8QueryValue("script", scriptB64, logRawValue: true);

        // ── system-driven automation validation ──────────────────────────────────────────
        if (systemVariant != null && systemName == null)
        {
            WriteBootstrapLog("Query parameter 'systemVariant' requires 'system'; ignoring 'systemVariant'.", LogLevel.Warning);
            systemVariant = null;
        }
        if (systemName == null && (autoStart || waitForReady))
        {
            WriteBootstrapLog("Query parameters 'start' and 'waitForSystemReady' require 'system'; ignoring.", LogLevel.Warning);
            autoStart = false;
            waitForReady = false;
        }
        if (waitForReady && !autoStart)
        {
            WriteBootstrapLog("Query parameter 'waitForSystemReady' requires 'start'; ignoring 'waitForSystemReady'.", LogLevel.Warning);
            waitForReady = false;
        }
        if (loadPrgUrl != null && (systemName == null || !autoStart))
        {
            WriteBootstrapLog("Query parameter 'loadPrgUrl' requires 'system' and 'start'; ignoring 'loadPrgUrl'.", LogLevel.Warning);
            loadPrgUrl = null;
        }
        if (runLoaded && loadPrgUrl == null)
        {
            WriteBootstrapLog("Query parameter 'runLoadedProgram' requires 'loadPrgUrl'; ignoring 'runLoadedProgram'.", LogLevel.Warning);
            runLoaded = false;
        }
        if (basicText != null && basicUrl != null)
        {
            WriteBootstrapLog("Query parameters 'basicText' and 'basicUrl' are mutually exclusive; ignoring both.", LogLevel.Warning);
            basicText = null;
            basicUrl = null;
            runBasic = false;
        }
        if ((basicText != null || basicUrl != null) &&
            (!string.Equals(systemName, C64.SystemName, StringComparison.OrdinalIgnoreCase) || !autoStart || !waitForReady))
        {
            WriteBootstrapLog(
                "Query parameters 'basicText'/'basicUrl' require 'system=C64', 'start', and 'waitForSystemReady'; ignoring BASIC source parameters.",
                LogLevel.Warning);
            basicText = null;
            basicUrl = null;
            runBasic = false;
        }
        if ((basicText != null || basicUrl != null) && (loadPrgUrl != null || runLoaded))
        {
            WriteBootstrapLog(
                "Query parameters 'basicText'/'basicUrl' are mutually exclusive with 'loadPrgUrl' and 'runLoadedProgram'; ignoring BASIC source parameters.",
                LogLevel.Warning);
            basicText = null;
            basicUrl = null;
            runBasic = false;
        }
        if (runBasic && basicText == null && basicUrl == null)
        {
            WriteBootstrapLog("Query parameter 'runBasic' requires 'basicText' or 'basicUrl'; ignoring 'runBasic'.", LogLevel.Warning);
            runBasic = false;
        }

        // ── script-driven automation validation ──────────────────────────────────────────
        // 'script' and 'scriptUrl' are mutually exclusive.
        if (scriptContent != null && scriptUrl != null)
        {
            WriteBootstrapLog("Query parameters 'script' and 'scriptUrl' are mutually exclusive; ignoring both.", LogLevel.Warning);
            scriptContent = null;
            scriptUrl = null;
        }
        // Scripts own the lifecycle: incompatible with system-driven automation.
        if ((scriptContent != null || scriptUrl != null) &&
            (systemName != null || autoStart || waitForReady || loadPrgUrl != null || runLoaded ||
             basicText != null || basicUrl != null || runBasic))
        {
            WriteBootstrapLog(
                "Query parameters 'script'/'scriptUrl' are mutually exclusive with 'system', 'start', " +
                "'waitForSystemReady', 'loadPrgUrl', 'runLoadedProgram', 'basicText', 'basicUrl', and 'runBasic'; ignoring script parameters.",
                LogLevel.Warning);
            scriptContent = null;
            scriptUrl = null;
        }

        return new BrowserAutomationParams(systemName, systemVariant, autoStart, waitForReady,
            loadPrgUrl, runLoaded, basicText, basicUrl, runBasic, scriptContent, scriptUrl);
    }

    private static string? DecodeBase64UrlUtf8QueryValue(string parameterName, string? base64UrlValue, bool logRawValue = false)
    {
        if (base64UrlValue == null)
            return null;

        if (logRawValue)
            WriteBootstrapLog($"Query '{parameterName}' raw value: '{base64UrlValue}' (length {base64UrlValue.Length}).");
        else
            WriteBootstrapLog($"Query '{parameterName}' provided ({base64UrlValue.Length} chars).");

        try
        {
            var standardB64 = base64UrlValue.Replace('-', '+').Replace('_', '/');
            switch (standardB64.Length % 4)
            {
                case 2: standardB64 += "=="; break;
                case 3: standardB64 += "="; break;
                case 1:
                    WriteBootstrapLog($"Query parameter '{parameterName}' has invalid base64url length; ignoring '{parameterName}'.", LogLevel.Warning);
                    return null;
            }

            var bytes = Convert.FromBase64String(standardB64);
            var decoded = System.Text.Encoding.UTF8.GetString(bytes);
            if (string.IsNullOrWhiteSpace(decoded))
            {
                WriteBootstrapLog($"Query parameter '{parameterName}' decoded to empty/whitespace text; ignoring '{parameterName}'.", LogLevel.Warning);
                return null;
            }

            var preview = decoded.Length <= 80 ? decoded : decoded[..80] + "...";
            WriteBootstrapLog($"Decoded '{parameterName}' to {decoded.Length} chars: \"{preview}\"");
            return decoded;
        }
        catch (FormatException ex)
        {
            WriteBootstrapLog($"Query parameter '{parameterName}' is not valid base64url: {ex.Message}; ignoring '{parameterName}'.", LogLevel.Warning);
            return null;
        }
    }

    private static string BuildC64BasicPasteText(string basicSource, bool runBasic)
    {
        var normalized = basicSource.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\r", "\n", StringComparison.Ordinal);
        if (!normalized.EndsWith("\n", StringComparison.Ordinal))
            normalized += "\n";
        if (runBasic)
            normalized += "run\n";
        return normalized;
    }

    private static int CountNonEmptyLines(string text)
    {
        return text.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\r", "\n", StringComparison.Ordinal)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Length;
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
            AllowUrlScripts = section.GetValue(nameof(ScriptingConfig.AllowUrlScripts), false),
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
