using System;
using System.IO;
using System.Threading.Tasks;
using Highbyte.DotNet6502.DebugAdapter;
using Microsoft.Extensions.Logging;

namespace Highbyte.DotNet6502.App.Avalonia.Desktop;

/// <summary>
/// Desktop implementation of <see cref="IExternalDebugController"/>.
/// Manages the <see cref="TcpDebugServerManager"/> lifecycle so the user can
/// start and stop the TCP debug server at runtime from the UI.
/// </summary>
internal sealed class AvaloniaExternalDebugController : IExternalDebugController
{
    private readonly ITcpDebugServerEnvironment _environment;
    private readonly ILoggerFactory? _loggerFactory;
    private readonly ILogger<AvaloniaExternalDebugController>? _logger;
    private TcpDebugServerManager? _serverManager;

    private bool _isListening;
    public bool IsListening
    {
        get => _isListening;
        private set
        {
            if (_isListening != value)
            {
                _isListening = value;
                StateChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public bool IsClientConnected => _serverManager?.IsClientConnected ?? false;

    private int _port = 6502;
    public int Port => _port;

    public event EventHandler? StateChanged;

    public AvaloniaExternalDebugController(ITcpDebugServerEnvironment environment, ILoggerFactory? loggerFactory = null)
    {
        _environment = environment ?? throw new ArgumentNullException(nameof(environment));
        _loggerFactory = loggerFactory;
        _logger = loggerFactory?.CreateLogger<AvaloniaExternalDebugController>();
    }

    /// <inheritdoc/>
    public async Task StartAsync(int port)
    {
        if (_isListening)
            return;

        _port = port;

        var debugLogFilePath = Path.Combine(
            Path.GetTempPath(),
            $"dotnet6502-debugadapter-avalonia-{DateTime.Now:yyyyMMdd-HHmmss}.log");
        var debugLogWriter = new StreamWriter(debugLogFilePath, append: true) { AutoFlush = true };
        var startedAt = DateTime.Now;
        _logger?.LogInformation("Debug adapter server started at {StartedAt}", startedAt);
        debugLogWriter.WriteLine($"Debug adapter server started at {startedAt}");

        _serverManager = new TcpDebugServerManager(debugLogWriter, _environment, _loggerFactory);
        _serverManager.StateChanged += OnServerManagerStateChanged;

        await _serverManager.StartAsync(port);
        IsListening = true; // fires StateChanged
    }

    /// <inheritdoc/>
    public async Task StopAsync()
    {
        if (_serverManager == null || !_isListening)
            return;

        _serverManager.StateChanged -= OnServerManagerStateChanged;
        await _serverManager.StopAsync();
        _serverManager.Dispose();
        _serverManager = null;

        IsListening = false; // fires StateChanged
    }

    /// <summary>
    /// Blocks until a debug client connects or the timeout elapses.
    /// Used by <c>Program.cs</c> for the <c>--debug-wait</c> startup flag.
    /// </summary>
    public bool WaitForClientConnection(int timeoutSeconds = 30)
        => _serverManager?.WaitForClientConnection(timeoutSeconds) ?? false;

    /// <summary>
    /// Signals that automated startup (KERNAL boot + PRG load + PC set) is complete.
    /// Used by <c>Program.cs</c> after the <c>AutomatedStartupHandler</c> finishes.
    /// </summary>
    public void SignalProgramReady() => _serverManager?.SignalProgramReady();

    private void OnServerManagerStateChanged(object? sender, EventArgs e)
    {
        // Propagate client-connect / client-disconnect state changes to listeners.
        StateChanged?.Invoke(this, EventArgs.Empty);
    }
}
