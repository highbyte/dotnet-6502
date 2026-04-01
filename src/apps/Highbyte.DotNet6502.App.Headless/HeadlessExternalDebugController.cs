using Highbyte.DotNet6502.DebugAdapter;
using Microsoft.Extensions.Logging;

namespace Highbyte.DotNet6502.App.Headless;

/// <summary>
/// Headless implementation of <see cref="IExternalDebugController"/>.
/// Manages the <see cref="TcpDebugServerManager"/> lifecycle for CLI-driven debug sessions.
/// </summary>
internal sealed class HeadlessExternalDebugController : IExternalDebugController
{
    private readonly ITcpDebugServerEnvironment _environment;
    private readonly ILoggerFactory? _loggerFactory;
    private readonly ILogger<HeadlessExternalDebugController>? _logger;
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

    public HeadlessExternalDebugController(ITcpDebugServerEnvironment environment, ILoggerFactory? loggerFactory = null)
    {
        _environment = environment ?? throw new ArgumentNullException(nameof(environment));
        _loggerFactory = loggerFactory;
        _logger = loggerFactory?.CreateLogger<HeadlessExternalDebugController>();
    }

    public async Task StartAsync(int port)
    {
        if (_isListening)
            return;

        _port = port;

        var debugLogFilePath = Path.Combine(
            Path.GetTempPath(),
            $"dotnet6502-debugadapter-headless-{DateTime.Now:yyyyMMdd-HHmmss}.log");
        var debugLogWriter = new StreamWriter(debugLogFilePath, append: true) { AutoFlush = true };
        var startedAt = DateTime.Now;
        _logger?.LogInformation("Debug adapter server started at {StartedAt}", startedAt);
        debugLogWriter.WriteLine($"Debug adapter server started at {startedAt}");

        _serverManager = new TcpDebugServerManager(debugLogWriter, _environment, _loggerFactory);
        _serverManager.StateChanged += OnServerManagerStateChanged;

        await _serverManager.StartAsync(port);
        IsListening = true;
    }

    public async Task StopAsync()
    {
        if (_serverManager == null || !_isListening)
            return;

        _serverManager.StateChanged -= OnServerManagerStateChanged;
        await _serverManager.StopAsync();
        _serverManager.Dispose();
        _serverManager = null;

        IsListening = false;
    }

    /// <summary>
    /// Blocks until a debug client connects or the timeout elapses.
    /// </summary>
    public bool WaitForClientConnection(int timeoutSeconds = 30)
        => _serverManager?.WaitForClientConnection(timeoutSeconds) ?? false;

    /// <summary>
    /// Signals that automated startup is complete.
    /// </summary>
    public void SignalProgramReady() => _serverManager?.SignalProgramReady();

    private void OnServerManagerStateChanged(object? sender, EventArgs e)
    {
        StateChanged?.Invoke(this, EventArgs.Empty);
    }
}
