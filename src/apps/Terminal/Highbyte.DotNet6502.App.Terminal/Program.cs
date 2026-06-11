using Highbyte.DotNet6502.App.Terminal;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Logging.InMem;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

// ----------
// DotNet 6502 emulator — interactive terminal (TUI) host.
// Renders the emulated (text-mode) screen as colored text cells in the real terminal via Terminal.Gui.
// ----------

// Anchor file/relative resource access to the built app location.
Environment.CurrentDirectory = AppContext.BaseDirectory;

// ----------
// Configuration
// ----------
var appDir = AppContext.BaseDirectory;
var configBuilder = new ConfigurationBuilder()
    .SetBasePath(appDir)
    .AddJsonFile("appsettings.json")
    .AddJsonFile("appsettings.Development.json", optional: true);

var devEnvironmentVariable = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");
var isDevelopment = string.IsNullOrEmpty(devEnvironmentVariable)
    || devEnvironmentVariable.Equals("development", StringComparison.OrdinalIgnoreCase);
if (isDevelopment)
    configBuilder.AddUserSecrets<Program>(optional: true);

IConfiguration configuration = configBuilder.Build();

// ----------
// Logging — in-memory store shown in the TUI "Logs" pane.
// (No console logging: the terminal is owned by the TUI.)
// ----------
var logStore = new DotNet6502InMemLogStore { WriteDebugMessage = false };
var logConfig = new DotNet6502InMemLoggerConfiguration(logStore) { LogLevel = LogLevel.Information };
using var loggerFactory = LoggerFactory.Create(logBuilder =>
{
    logBuilder.AddInMem(logConfig);
    logBuilder.SetMinimumLevel(LogLevel.Trace);
});

var bootstrapLogger = loggerFactory.CreateLogger("Program");

try
{
    // ----------
    // Host config
    // ----------
    var emulatorConfig = new EmulatorConfig();
    configuration.GetSection(EmulatorConfig.ConfigSectionName).Bind(emulatorConfig);

    // ----------
    // System list. The terminal host supports the C64 and VIC-20 (text mode).
    // Configurers are created directly (no plug-in discovery needed for a few systems).
    // ----------
    var systemList = new SystemList();
    systemList.AddSystem(new C64TerminalSetup(loggerFactory, configuration));
    systemList.AddSystem(new Vic20TerminalSetup(loggerFactory, configuration));
    await systemList.RemoveSystemsWithNoConfigurationVariants(bootstrapLogger);

    // ----------
    // Run the TUI host app (or the headless self-test that needs no TTY).
    // ----------
    var hostApp = new TuiHostApp(systemList, loggerFactory, emulatorConfig, logStore);

    if (args.Contains("--selftest"))
    {
        var frames = 200;
        var idx = Array.IndexOf(args, "--frames");
        if (idx >= 0 && idx + 1 < args.Length && int.TryParse(args[idx + 1], out var f))
            frames = f;

        string? systemName = null;
        var sidx = Array.IndexOf(args, "--system");
        if (sidx >= 0 && sidx + 1 < args.Length)
            systemName = args[sidx + 1];

        var rendered = hostApp.RunSelfTest(frames, systemName);
        Console.WriteLine(rendered);
    }
    else
    {
        hostApp.Run();
    }
}
catch (Exception ex)
{
    var root = ex is AggregateException agg ? agg.InnerException ?? agg : ex;
    bootstrapLogger.LogCritical(root, "The terminal host could not start.");
    Console.Error.WriteLine($"FATAL: the terminal host could not start: {root.Message}");
    Environment.ExitCode = 1;
}

// Make Program accessible for AddUserSecrets<Program>().
public partial class Program;
