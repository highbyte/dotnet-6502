using System.ComponentModel;
using Highbyte.DotNet6502.Systems;

namespace Highbyte.DotNet6502.DebugAdapter;

/// <summary>
/// Manages the TCP debug adapter server and handles incoming debug client connections.
/// Platform-specific concerns (UI thread dispatch, application shutdown, host app access)
/// are supplied by <see cref="ITcpDebugServerEnvironment"/>.
/// </summary>
public sealed class TcpDebugServerManager : IDisposable
{
    private readonly TcpDebugAdapterServer _debugServer;
    private readonly StreamWriter _debugLogWriter;
    private readonly ITcpDebugServerEnvironment _environment;
    private bool _debugClientConnected;
    private int _activeConnectionCount = 0;
    private readonly object _connectionLock = new object();

    // Tracks whether AutomatedStartupHandler has finished all setup (KERNAL boot + PRG load + PC set).
    // Set to true by SignalProgramReady(). An adapter created after that point is also marked ready.
    private volatile bool _startupCompleted = false;
    private DebugAdapterLogic? _activeAdapter;

    public TcpDebugServerManager(StreamWriter debugLogWriter, ITcpDebugServerEnvironment environment)
    {
        _debugLogWriter = debugLogWriter ?? throw new ArgumentNullException(nameof(debugLogWriter));
        _environment = environment ?? throw new ArgumentNullException(nameof(environment));
        _debugServer = new TcpDebugAdapterServer(debugLogWriter);

        // Subscribe to client connection events
        _debugServer.ClientConnected += OnClientConnected;
    }

    /// <summary>
    /// Gets whether a debug client is currently connected.
    /// </summary>
    public bool IsClientConnected => _debugClientConnected;

    /// <summary>
    /// Gets whether the TCP server is currently listening for connections.
    /// </summary>
    public bool IsListening => _debugServer.IsListening;

    /// <summary>
    /// Gets the port the TCP server is listening on (0 if not listening).
    /// </summary>
    public int Port => _debugServer.Port;

    /// <summary>
    /// Raised when <see cref="IsClientConnected"/> changes (client connected or disconnected).
    /// May be fired from a background thread; subscribers should dispatch to the UI thread if needed.
    /// </summary>
    public event EventHandler? StateChanged;

    /// <summary>
    /// Starts the TCP debug adapter server on the specified port.
    /// Begins accepting connections immediately — no waiting for automated startup.
    /// </summary>
    public async Task StartAsync(int port)
    {
        await _debugServer.StartAsync(port);
    }

    /// <summary>
    /// Stops the TCP server. Any in-progress debug session continues until the client
    /// disconnects; no new connections will be accepted. The server can be restarted
    /// by calling <see cref="StartAsync"/> again.
    /// </summary>
    public Task StopAsync()
    {
        _debugLogWriter.WriteLine("StopAsync: stopping TCP server (no new connections will be accepted)");
        _debugServer.Stop();
        return Task.CompletedTask;
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

    /// <summary>
    /// Called by the host when all setup is complete (KERNAL booted, PRG loaded, PC set).
    /// Notifies the active debug adapter that the emulator is ready to be paused for stopOnEntry.
    /// </summary>
    public void SignalProgramReady()
    {
        _startupCompleted = true;
        _activeAdapter?.NotifyProgramReady();
        _debugLogWriter.WriteLine("SignalProgramReady: automated startup complete, notified active adapter");
    }

    private void OnClientConnected(object? sender, ClientConnectedEventArgs e)
    {
        // NOTE: Do NOT set _debugClientConnected or _activeAdapter here.
        // VSCode's TCP readiness probe connects and immediately disconnects without sending
        // any DAP messages. If we set these flags now, the probe's cleanup would clear the
        // state needed by the real connection that follows.
        // Instead, defer these until OnInitialized fires (real DAP initialize received).

        lock (_connectionLock)
        {
            _activeConnectionCount++;
            _debugLogWriter.WriteLine($"TCP connection accepted at {DateTime.Now} (total active connections: {_activeConnectionCount})");
        }

        var protocol = new DapProtocol(e.Transport, _debugLogWriter);
        var adapter = CreateAdapter(protocol);
        var sessionCts = new CancellationTokenSource();
        var emulatorStateHandler = CreateEmulatorStateHandler(adapter);

        SetupDapInitializationHandler(adapter);
        SubscribeToEmulatorState(adapter, emulatorStateHandler, sessionCts);
        SetupDisconnectHandler(adapter);

        // Start message loop for this client
        _ = Task.Run(async () => await ProcessMessagesAsync(protocol, adapter, emulatorStateHandler, sessionCts));
    }

    /// <summary>
    /// Creates a new DebugAdapterLogic instance for the connection, pre-configured
    /// with the current system state and startup completion status.
    /// </summary>
    private DebugAdapterLogic CreateAdapter(DapProtocol protocol)
    {
        var hostApp = _environment.GetHostApp();
        var system = hostApp?.CurrentRunningSystem;

        // Start with IsStopped=true if host is waiting for debugger (boot sequence debugging).
        // This ensures no gap between SetExternalDebugAdapter clearing WaitForExternalDebugger
        // and HandleLaunchAsync setting IsStopped — the adapter is already paused.
        bool initiallyPaused = hostApp?.WaitForExternalDebugger == true;
        var adapter = new DebugAdapterLogic(protocol, _debugLogWriter, system, initiallyPaused);

        // If automated startup already completed before this client connected, mark program ready now.
        if (_startupCompleted)
            adapter.NotifyProgramReady();

        _debugLogWriter.WriteLine($"Debug adapter created (system available: {system != null}, initiallyPaused: {initiallyPaused}, startupCompleted: {_startupCompleted})");
        return adapter;
    }

    /// <summary>
    /// Sets up the OnInitialized handler that fires when a real DAP session starts
    /// (initialize message received), as opposed to VSCode's TCP readiness probe.
    /// Installs the debug adapter in the host app on the UI thread.
    /// </summary>
    private void SetupDapInitializationHandler(DebugAdapterLogic adapter)
    {
        adapter.OnInitialized += () =>
        {
            _debugClientConnected = true;
            _activeAdapter = adapter;
            _debugLogWriter.WriteLine("DAP initialize received — marked as active debug client");
            StateChanged?.Invoke(this, EventArgs.Empty);

            _environment.RunOnUiThread(() =>
            {
                var currentHostApp = _environment.GetHostApp();
                if (currentHostApp != null)
                {
                    currentHostApp.SetExternalDebugAdapter(adapter);
                    adapter.NotifyInstalledInHost();
                    _debugLogWriter.WriteLine("Debug adapter set in host app (DAP initialize received)");
                }
            });
        };
    }

    /// <summary>
    /// Creates a PropertyChanged handler that binds/re-binds the debug adapter when
    /// the emulator starts or restarts, and sends a terminated event when stopped from UI.
    /// </summary>
    private PropertyChangedEventHandler CreateEmulatorStateHandler(DebugAdapterLogic adapter)
    {
        // Set to true when the emulated system is stopped from the UI (Uninitialized state).
        // Prevents a race where the user quickly restarts the system: if Running fires before
        // CleanupDebugSession has unsubscribed this handler, we must not re-install the
        // stale adapter. Both Uninitialized and Running fire on the UI thread, so this flag
        // is written and read on the same thread — no locking needed.
        bool sessionEnded = false;

        return (s, args) =>
        {
            if (args.PropertyName != nameof(IHostApp.EmulatorState))
                return;

            var currentHostApp = _environment.GetHostApp();
            if (currentHostApp?.EmulatorState == EmulatorState.Running
                && currentHostApp.CurrentRunningSystem != null)
            {
                // If the session already ended (system was stopped), do nothing.
                // CleanupDebugSession will unsubscribe us shortly, but the user may
                // have restarted the system before that cleanup runs.
                if (sessionEnded)
                {
                    _debugLogWriter.WriteLine("EmulatorState became Running but session already ended — ignoring");
                    return;
                }

                _debugLogWriter.WriteLine("EmulatorState became Running, binding/re-binding debug adapter");

                // Capture WaitForExternalDebugger BEFORE SetExternalDebugAdapter clears it.
                bool wasWaitingForDebugger = currentHostApp.WaitForExternalDebugger;

                adapter.SetSystem(currentHostApp.CurrentRunningSystem);

                if (wasWaitingForDebugger)
                {
                    adapter.MarkAsStopped();
                    _debugLogWriter.WriteLine("WaitForExternalDebugger was set — adapter marked as stopped");
                }

                // Must be on UI thread — SetExternalDebugAdapter installs the
                // breakpoint evaluator on the (possibly new) CurrentSystemRunner.
                _environment.RunOnUiThread(() =>
                {
                    // Guard again inside the UI-thread lambda: session may have ended
                    // between when this post was queued and when it actually runs.
                    if (sessionEnded)
                    {
                        _debugLogWriter.WriteLine("RunOnUiThread: session ended before SetExternalDebugAdapter could run — skipping");
                        return;
                    }

                    var hostApp = _environment.GetHostApp();
                    if (hostApp != null)
                    {
                        hostApp.SetExternalDebugAdapter(adapter);
                        adapter.NotifyInstalledInHost();
                        _debugLogWriter.WriteLine("Debug adapter installed in host app (emulator started/restarted)");
                    }
                });
            }
            else if (currentHostApp?.EmulatorState == EmulatorState.Uninitialized)
            {
                // Mark session as ended before sending the terminated event so that any
                // subsequent Running event (user restarting the system) is ignored.
                sessionEnded = true;
                _debugLogWriter.WriteLine("EmulatorState became Uninitialized (system stopped from UI), sending terminated event");
                _ = adapter.SendTerminatedEventAsync();
            }
        };
    }

    /// <summary>
    /// Subscribes to EmulatorState changes for the lifetime of this debug session.
    /// Handles two timing scenarios:
    ///   (a) HostApp is already available — subscribes immediately.
    ///   (b) HostApp is not yet initialized (TCP server started before the host) — defers
    ///       subscription via a background task that waits for HostApp to become available.
    /// </summary>
    private void SubscribeToEmulatorState(DebugAdapterLogic adapter, PropertyChangedEventHandler handler, CancellationTokenSource sessionCts)
    {
        var hostApp = _environment.GetHostApp();

        if (hostApp is INotifyPropertyChanged notifier)
        {
            notifier.PropertyChanged += handler;
            _debugLogWriter.WriteLine("Subscribed to EmulatorState changes (HostApp available at connect time)");

            // PropertyChanged only fires on *changes*, so if it's already Running we'd miss it.
            if (hostApp.EmulatorState == EmulatorState.Running
                && hostApp.CurrentRunningSystem != null)
            {
                _debugLogWriter.WriteLine("System already running at connect time — triggering handler immediately");
                handler(hostApp, new PropertyChangedEventArgs(nameof(IHostApp.EmulatorState)));
            }
        }
        else
        {
            // HostApp is not yet initialized — wait for it in the background.
            _ = Task.Run(async () => await DeferredEmulatorStateSubscription(handler, sessionCts));
        }
    }

    /// <summary>
    /// Background task that waits for HostApp to become available, then subscribes
    /// to EmulatorState changes. Used when the TCP server starts before the host is ready.
    /// </summary>
    private async Task DeferredEmulatorStateSubscription(PropertyChangedEventHandler handler, CancellationTokenSource sessionCts)
    {
        IDebuggableHostApp? currentHostApp = null;
        while (!sessionCts.IsCancellationRequested
               && (currentHostApp = _environment.GetHostApp()) == null)
        {
            await Task.Delay(100);
        }

        if (sessionCts.IsCancellationRequested)
        {
            _debugLogWriter.WriteLine("Session ended before HostApp became available — skipping EmulatorState subscription");
            return;
        }

        if (currentHostApp is INotifyPropertyChanged lateNotifier)
        {
            lateNotifier.PropertyChanged += handler;
            _debugLogWriter.WriteLine("Subscribed to EmulatorState changes (deferred — HostApp was not ready at connect time)");
        }

        // If the system is already running by the time we subscribe, fire the handler immediately.
        if (currentHostApp?.EmulatorState == EmulatorState.Running
            && currentHostApp.CurrentRunningSystem != null)
        {
            _debugLogWriter.WriteLine("System already running when deferred subscription completed — triggering handler immediately");
            handler(currentHostApp, new PropertyChangedEventArgs(nameof(IHostApp.EmulatorState)));
        }
    }

    /// <summary>
    /// Sets up the OnExit handler: if terminateDebuggee is true (launch mode),
    /// terminates the host application.
    /// </summary>
    private void SetupDisconnectHandler(DebugAdapterLogic adapter)
    {
        adapter.OnExit += (terminateDebuggee) =>
        {
            if (terminateDebuggee)
            {
                _debugLogWriter.WriteLine("terminateDebuggee=true (launch mode), terminating application");
                _environment.TerminateApplication();
            }
        };
    }

    private async Task ProcessMessagesAsync(DapProtocol protocol, DebugAdapterLogic adapter, PropertyChangedEventHandler? emulatorStateHandler, CancellationTokenSource sessionCts)
    {
        try
        {
            while (true)
            {
                var message = await protocol.ReadMessageAsync();
                if (message == null)
                {
                    SafeLog("Received null message, debug client disconnected");
                    break;
                }
                await adapter.HandleMessageAsync(message);
            }
        }
        catch (Exception ex)
        {
            SafeLog($"Debug adapter error: {ex}");
        }
        finally
        {
            CleanupDebugSession(adapter, emulatorStateHandler, sessionCts);
        }
    }

    private void CleanupDebugSession(DebugAdapterLogic adapter, PropertyChangedEventHandler? emulatorStateHandler, CancellationTokenSource sessionCts)
    {
        // Cancel the background subscription task (if it's still waiting for HostApp to initialize).
        sessionCts.Cancel();

        // Check if this was a real DAP session (OnInitialized fired) or a probe connection.
        bool wasRealSession = (_activeAdapter == adapter);

        bool clientDisconnected = false;

        lock (_connectionLock)
        {
            _activeConnectionCount--;
            SafeLog($"Connection closed at {DateTime.Now} (remaining active connections: {_activeConnectionCount}, wasRealSession: {wasRealSession})");

            // Only set flag to false if this is the last connection
            if (_activeConnectionCount <= 0)
            {
                clientDisconnected = true;
                _activeConnectionCount = 0; // Ensure it doesn't go negative
            }
        }

        if (!wasRealSession)
        {
            // Probe connection — no DAP initialize was received. Minimal cleanup only.
            SafeLog("Probe connection cleaned up (no DAP session was established)");
            sessionCts.Dispose();
            return;
        }

        // Real DAP session — full cleanup.

        // Unsubscribe from PropertyChanged if still subscribed (e.g., debugger disconnected before system started)
        if (emulatorStateHandler != null)
        {
            var hostApp = _environment.GetHostApp();
            if (hostApp is INotifyPropertyChanged notifier)
            {
                notifier.PropertyChanged -= emulatorStateHandler;
                SafeLog("Unsubscribed from EmulatorState changes (debug session ended)");
            }
        }

        // Dispose session CTS (already cancelled above)
        sessionCts.Dispose();

        _activeAdapter = null;

        // Reset debugger state to unfreeze emulator
        adapter.Reset();

        if (clientDisconnected)
        {
            _environment.RunOnUiThread(() =>
            {
                var hostApp = _environment.GetHostApp();
                if (hostApp != null)
                {
                    hostApp.ClearExternalDebugAdapter();
                    SafeLog("External debug adapter cleared on UI thread (all connections closed)");
                }
            });
            _debugClientConnected = false;
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Writes to the log, silently swallowing ObjectDisposedException.
    /// Used in fire-and-forget background tasks (ProcessMessagesAsync, CleanupDebugSession)
    /// where the StreamWriter may be closed if StopAsync is called while a connection is active.
    /// </summary>
    private void SafeLog(string message)
    {
        try
        {
            _debugLogWriter.WriteLine(message);
        }
        catch (ObjectDisposedException) { }
    }

    public void Dispose()
    {
        _debugServer.ClientConnected -= OnClientConnected;
        _debugLogWriter?.Close();
    }
}
