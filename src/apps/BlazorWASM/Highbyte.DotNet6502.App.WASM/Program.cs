using Blazored.Modal;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.AspNetCore.Components;
using Highbyte.DotNet6502.App.WASM;
using Blazored.LocalStorage;
using Toolbelt.Blazor.Extensions.DependencyInjection;
using Highbyte.DotNet6502.Systems.Logging.Console;
using Highbyte.DotNet6502.App.WASM.Emulator;
using Highbyte.DotNet6502.Impl.AspNet;
using Highbyte.DotNet6502.Impl.AspNet.Audio.WebAudioAPISynth;
using Highbyte.DotNet6502.Systems.Plugins;
using Microsoft.Extensions.Logging;
using TextCopy;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.AddBlazoredModal();
builder.Services.AddBlazoredLocalStorage();
builder.Services.AddGamepadList();
builder.Services.InjectClipboard();
builder.Services.AddScoped(sp => new BrowserContext
{
    Uri = sp.GetRequiredService<NavigationManager>().ToAbsoluteUri(sp.GetRequiredService<NavigationManager>().Uri),
    HttpClient = sp.GetRequiredService<HttpClient>(),
    LocalStorage = sp.GetRequiredService<Blazored.LocalStorage.ILocalStorageService>()
});

builder.Logging.ClearProviders();
builder.Logging.AddDotNet6502Console();
builder.Logging.SetMinimumLevel(LogLevel.Debug);
builder.Logging.AddFilter("Microsoft.AspNetCore.Components.RenderTree.*", LogLevel.None);

// Logger for plug-in discovery. Discovery catches and logs per-plugin failures; without a logger
// passed here those failures — e.g. a constructor trimmed away in a published (AOT) build —
// vanish silently and the affected system's menu/UI simply never appears. The log goes to the
// browser console (F12).
using var pluginLoggerFactory = LoggerFactory.Create(lb =>
{
    lb.AddDotNet6502Console();
    lb.SetMinimumLevel(LogLevel.Debug);
});
var pluginLogger = pluginLoggerFactory.CreateLogger("PluginDiscovery");

// Plug-in discovery + registration. A failure here (a plug-in that throws while registering, ...)
// is recorded in BootstrapError; the app still mounts and the Index component shows the error
// instead of the emulator UI — Blazor WASM cannot show a custom UI before the app has mounted.
try
{
    var enabledSystems = builder.Configuration.GetSection("EnabledSystems").Get<string[]>();

    var shellPlugins = SystemPluginDiscovery.Discover<ISystemShellPlugin>(enabledSystems, pluginLogger).ToList();
    pluginLogger.LogInformation("Discovered {Count} shell plug-in(s): {Names}",
        shellPlugins.Count, string.Join(",", shellPlugins.Select(p => p.SystemName)));
    foreach (var plugin in shellPlugins)
    {
        builder.Services.AddSingleton(typeof(ISystemShellPlugin), plugin);
        plugin.RegisterShellServices(builder.Services);
    }

    // Engine plug-ins (in the Impl.AspNet.<System> libraries) register the per-system
    // ISystemConfigurer and optionally contribute render targets (ISkiaWasmRenderTargetPlugin).
    var enginePlugins = SystemPluginDiscovery
        .Discover<ISystemEnginePlugin>(enabledSystems, pluginLogger)
        .ToList();
    pluginLogger.LogInformation("Discovered {Count} engine plug-in(s): {Names}",
        enginePlugins.Count, string.Join(",", enginePlugins.Select(p => p.SystemName)));

    // Diagnose enabled-but-missing systems and engine/shell plug-in mismatches. On a browser host
    // a missing system is most often a missing TrimmerRootAssembly entry in this app's .csproj.
    SystemPluginDiscovery.LogPluginDiagnostics(enabledSystems, enginePlugins, shellPlugins, pluginLogger);

    foreach (var plugin in enginePlugins)
    {
        plugin.Register(builder.Services, builder.Configuration);
        if (plugin is ISkiaWasmRenderTargetPlugin renderTargetPlugin)
            builder.Services.AddSingleton(renderTargetPlugin);
    }
}
catch (Exception ex)
{
    var rootEx = ex is AggregateException agg ? agg.InnerException ?? agg : ex;
    pluginLogger.LogCritical(rootEx, "Fatal error during startup.");
    BootstrapError.Message = rootEx.Message;
}

await builder.Build().RunAsync();
