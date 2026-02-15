using System;
using System.ComponentModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using Highbyte.DotNet6502.DebugAdapter;
using Highbyte.DotNet6502.Systems;

namespace Highbyte.DotNet6502.App.Avalonia.Desktop;

/// <summary>
/// Manages the TCP debug adapter server and handles incoming debug client connections.
/// </summary>
internal sealed class TcpDebugServerManager : IDisposable
{
    private readonly TcpDebugAdapterServer _debugServer;
    private readonly StreamWriter _debugLogWriter;
    private bool _debugClientConnected;
    private int _activeConnectionCount = 0;
    private readonly object _connectionLock = new object();
    private readonly object _startupLock = new object();
    private TaskCompletionSource<bool>? _startupCompletionSource;

    public TcpDebugServerManager(StreamWriter debugLogWriter)
    {
        _debugLogWriter = debugLogWriter ?? throw new ArgumentNullException(nameof(debugLogWriter));
        _debugServer = new TcpDebugAdapterServer(debugLogWriter);

        // Subscribe to client connection events
        _debugServer.ClientConnected += OnClientConnected;
    }

    /// <summary>
    /// Gets whether a debug client is currently connected.
    /// </summary>
    public bool IsClientConnected => _debugClientConnected;

    /// <summary>
    /// Signals that automated startup is complete, unblocking StartAsync if it was waiting.
    /// </summary>
    public void SignalAutomatedStartupComplete(IHostApp hostApp)
    {
        if (hostApp == null)
            throw new ArgumentNullException(nameof(hostApp));

        lock (_startupLock)
        {
            _debugLogWriter.WriteLine("Automated startup complete signal received");
            // Signal that the server can now start listening for connections
            _startupCompletionSource?.TrySetResult(true);
        }
    }

    /// <summary>
    /// Starts the TCP debug adapter server on the specified port.
    /// If automated startup is in progress, waits for it to complete before listening for connections.
    /// </summary>
    /// <param name="port">The port number to listen on.</param>
    /// <param name="waitForAutomatedStartup">If true, waits for automated startup to complete before listening.</param>
    public async Task StartAsync(int port, bool waitForAutomatedStartup = false, ISystem? system = null)
    {
        if (waitForAutomatedStartup && system != null)
        {
            throw new ArgumentException("Cannot provide a system instance when waiting for automated startup - the system will be obtained from the HostApp when startup completes");
        }

        if (waitForAutomatedStartup)
        {
            _debugLogWriter.WriteLine("TCP server will wait for automated startup to complete before listening...");
            _startupCompletionSource = new TaskCompletionSource<bool>();

            // Wait for automated startup to complete (with timeout)
            var timeoutTask = Task.Delay(60000); // 60 second timeout
            var completedTask = await Task.WhenAny(_startupCompletionSource.Task, timeoutTask);

            if (completedTask == timeoutTask)
            {
                _debugLogWriter.WriteLine("WARNING: Timeout waiting for automated startup - starting server anyway");
            }
            else
            {
                _debugLogWriter.WriteLine("Automated startup complete, now starting TCP server...");
            }
        }

        await _debugServer.StartAsync(port);
    }

    /// <summary>
    /// Waits for a debug client to connect within the specified timeout period.
    /// </summary>
    /// <param name="timeoutSeconds">The maximum time to wait in seconds.</param>
    /// <returns>True if a client connected within the timeout period; otherwise, false.</returns>
    public bool WaitForClientConnection(int timeoutSeconds = 30)
    {
        var waitStart = DateTime.Now;
        while (!_debugClientConnected && (DateTime.Now - waitStart).TotalSeconds < timeoutSeconds)
        {
            Thread.Sleep(100);
        }
        return _debugClientConnected;
    }

    private void OnClientConnected(object? sender, ClientConnectedEventArgs e)
    {
        _debugClientConnected = true;

        lock (_connectionLock)
        {
            _activeConnectionCount++;
            _debugLogWriter.WriteLine($"Debug client connected at {DateTime.Now} (total active connections: {_activeConnectionCount})");
        }

        var protocol = new DapProtocol(e.Transport, _debugLogWriter);

        // Get the system from the running host app (may be null if emulator hasn't started yet)
        var hostApp = Core.App.Current?.HostApp;
        var system = hostApp?.CurrentRunningSystem;

        // Start with IsStopped=true if host is waiting for debugger (boot sequence debugging).
        // This ensures no gap between SetExternalDebugAdapter clearing WaitForExternalDebugger
        // and HandleLaunchAsync setting IsStopped — the adapter is already paused.
        bool initiallyPaused = hostApp?.WaitForExternalDebugger == true;
        var adapter = new DebugAdapterLogic(protocol, _debugLogWriter, system, initiallyPaused);

        _debugLogWriter.WriteLine($"Debug adapter created (system available: {system != null}, initiallyPaused: {initiallyPaused})");

        // Only set up the external debug adapter when a real DAP session starts (initialize message received).
        // This avoids setting it up for probe connections (e.g., VSCode's TCP readiness check)
        // that connect and immediately disconnect without sending any DAP messages.
        // Always call SetExternalDebugAdapter here — it is null-safe when CurrentSystemRunner
        // is not yet available, and sets IsExternalDebuggerAttached=true so the UI reflects
        // the attached state immediately even before an emulator system has been started.
        adapter.OnInitialized += () =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                var currentHostApp = Core.App.Current?.HostApp;
                if (currentHostApp != null)
                {
                    currentHostApp.SetExternalDebugAdapter(adapter);
                    _debugLogWriter.WriteLine("Debug adapter set in host app (DAP initialize received)");
                }
            });
        };

        // Subscribe to EmulatorState changes for the lifetime of this debug session.
        // This handles two cases:
        //   (a) Initial binding when the debugger attached before the system started.
        //   (b) Re-binding after the user stops and restarts the emulator — the new
        //       SystemRunner needs the breakpoint evaluator reinstalled.
        // The handler stays subscribed (does NOT unsubscribe) so restart cycles work.
        PropertyChangedEventHandler? emulatorStateHandler = null;
        if (hostApp is INotifyPropertyChanged notifier)
        {
            emulatorStateHandler = (s, args) =>
            {
                if (args.PropertyName == nameof(IHostApp.EmulatorState))
                {
                    var currentHostApp = Core.App.Current?.HostApp;
                    if (currentHostApp?.EmulatorState == EmulatorState.Running
                        && currentHostApp.CurrentRunningSystem != null)
                    {
                        _debugLogWriter.WriteLine("EmulatorState became Running, binding/re-binding debug adapter");
                        adapter.SetSystem(currentHostApp.CurrentRunningSystem);

                        // Must be on UI thread — SetExternalDebugAdapter installs the
                        // breakpoint evaluator on the (possibly new) CurrentSystemRunner.
                        Dispatcher.UIThread.Post(() =>
                        {
                            if (Core.App.Current?.HostApp != null)
                            {
                                Core.App.Current.HostApp.SetExternalDebugAdapter(adapter);
                                _debugLogWriter.WriteLine("Debug adapter installed in host app (emulator started/restarted)");
                            }
                        });
                    }
                }
            };
            notifier.PropertyChanged += emulatorStateHandler;
        }

        // Handle disconnect: if terminateDebuggee is true (launch mode), shut down the app.
        adapter.OnExit += (terminateDebuggee) =>
        {
            if (terminateDebuggee)
            {
                _debugLogWriter.WriteLine("terminateDebuggee=true (launch mode), shutting down application");
                Dispatcher.UIThread.Post(() =>
                {
                    if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime lifetime)
                        lifetime.Shutdown();
                });
            }
        };

        // Start message loop for this client, passing the handler for cleanup on disconnect
        _ = Task.Run(async () => await ProcessMessagesAsync(protocol, adapter, emulatorStateHandler));
    }

    private async Task ProcessMessagesAsync(DapProtocol protocol, DebugAdapterLogic adapter, PropertyChangedEventHandler? emulatorStateHandler)
    {
        try
        {
            while (true)
            {
                var message = await protocol.ReadMessageAsync();
                if (message == null)
                {
                    _debugLogWriter.WriteLine("Received null message, debug client disconnected");
                    break;
                }
                await adapter.HandleMessageAsync(message);
            }
        }
        catch (Exception ex)
        {
            _debugLogWriter.WriteLine($"Debug adapter error: {ex}");
        }
        finally
        {
            CleanupDebugSession(adapter, emulatorStateHandler);
        }
    }

    private void CleanupDebugSession(DebugAdapterLogic adapter, PropertyChangedEventHandler? emulatorStateHandler)
    {
        bool clientDisconnected = false;

        lock (_connectionLock)
        {
            _activeConnectionCount--;
            _debugLogWriter.WriteLine($"Debug adapter stopped at {DateTime.Now} (remaining active connections: {_activeConnectionCount})");

            // Only set flag to false if this is the last connection
            if (_activeConnectionCount <= 0)
            {
                clientDisconnected = true;
                _activeConnectionCount = 0; // Ensure it doesn't go negative
            }
        }

        // Unsubscribe from PropertyChanged if still subscribed (e.g., debugger disconnected before system started)
        if (emulatorStateHandler != null)
        {
            var hostApp = Core.App.Current?.HostApp;
            if (hostApp is INotifyPropertyChanged notifier)
            {
                notifier.PropertyChanged -= emulatorStateHandler;
                _debugLogWriter.WriteLine("Unsubscribed from EmulatorState changes (debug session ended)");
            }
        }

        // Reset debugger state to unfreeze emulator
        adapter.Reset();

        if (clientDisconnected)
        {
            // All HostApp interactions must be done on UI thread
            Dispatcher.UIThread.Post(() =>
            {
                if (Core.App.Current?.HostApp != null)
                {
                    Core.App.Current.HostApp.ClearExternalDebugAdapter();
                    _debugLogWriter.WriteLine("External debug adapter cleared on UI thread (all connections closed)");
                }
            });
            _debugClientConnected = false;
        }
    }

    public void Dispose()
    {
        _debugServer.ClientConnected -= OnClientConnected;
        _debugLogWriter?.Close();
    }
}
