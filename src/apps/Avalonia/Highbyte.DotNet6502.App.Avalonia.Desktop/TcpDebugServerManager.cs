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

    // Tracks whether AutomatedStartupHandler has finished all setup (KERNAL boot + PRG load + PC set).
    // Set to true by SignalProgramReady(). An adapter created after that point is also marked ready.
    private volatile bool _startupCompleted = false;
    private DebugAdapterLogic? _activeAdapter;

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
    /// Starts the TCP debug adapter server on the specified port.
    /// Begins accepting connections immediately — no waiting for automated startup.
    /// </summary>
    public async Task StartAsync(int port)
    {
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

    /// <summary>
    /// Called by AutomatedStartupHandler when all setup is complete (KERNAL booted, PRG loaded, PC set).
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

        // Get the system from the running host app (may be null if emulator hasn't started yet)
        var hostApp = Core.App.Current?.HostApp;
        var system = hostApp?.CurrentRunningSystem;

        // Start with IsStopped=true if host is waiting for debugger (boot sequence debugging).
        // This ensures no gap between SetExternalDebugAdapter clearing WaitForExternalDebugger
        // and HandleLaunchAsync setting IsStopped — the adapter is already paused.
        bool initiallyPaused = hostApp?.WaitForExternalDebugger == true;
        var adapter = new DebugAdapterLogic(protocol, _debugLogWriter, system, initiallyPaused);

        // If automated startup already completed before this client connected, mark program ready now.
        // Otherwise the adapter will wait for SignalProgramReady() to be called later.
        if (_startupCompleted)
            adapter.NotifyProgramReady();

        _debugLogWriter.WriteLine($"Debug adapter created (system available: {system != null}, initiallyPaused: {initiallyPaused}, startupCompleted: {_startupCompleted})");

        // Only set up the external debug adapter when a real DAP session starts (initialize message received).
        // This avoids setting it up for probe connections (e.g., VSCode's TCP readiness check)
        // that connect and immediately disconnect without sending any DAP messages.
        // Always call SetExternalDebugAdapter here — it is null-safe when CurrentSystemRunner
        // is not yet available, and sets IsExternalDebuggerAttached=true so the UI reflects
        // the attached state immediately even before an emulator system has been started.
        adapter.OnInitialized += () =>
        {
            // Real DAP session started (not a probe connection).
            // Now safe to set these flags — probe connections never send initialize.
            _debugClientConnected = true;
            _activeAdapter = adapter;
            _debugLogWriter.WriteLine("DAP initialize received — marked as active debug client");

            Dispatcher.UIThread.Post(() =>
            {
                var currentHostApp = Core.App.Current?.HostApp;
                if (currentHostApp != null)
                {
                    currentHostApp.SetExternalDebugAdapter(adapter);
                    adapter.NotifyInstalledInHost();
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
        //
        // IMPORTANT: The debug client may connect before the Avalonia HostApp is initialized
        // (the TCP server starts before app.StartWithClassicDesktopLifetime, so the extension's
        // TCP readiness check can succeed before HostApp is available). We handle this by always
        // defining the handler and using a background task to subscribe when HostApp is ready.
        var sessionCts = new CancellationTokenSource();

        PropertyChangedEventHandler? emulatorStateHandler = (s, args) =>
        {
            if (args.PropertyName == nameof(IHostApp.EmulatorState))
            {
                var currentHostApp = Core.App.Current?.HostApp;
                if (currentHostApp?.EmulatorState == EmulatorState.Running
                    && currentHostApp.CurrentRunningSystem != null)
                {
                    _debugLogWriter.WriteLine("EmulatorState became Running, binding/re-binding debug adapter");

                    // Capture WaitForExternalDebugger BEFORE SetExternalDebugAdapter clears it.
                    // When the debugger connected before the system started, initiallyPaused was false
                    // (the flag wasn't set yet). Mirror it into adapter.IsStopped now so the
                    // stopOnEntry wait loop in HandleLaunchAsync unblocks correctly.
                    bool wasWaitingForDebugger = currentHostApp.WaitForExternalDebugger;

                    adapter.SetSystem(currentHostApp.CurrentRunningSystem);

                    if (wasWaitingForDebugger)
                    {
                        adapter.MarkAsStopped();
                        _debugLogWriter.WriteLine("WaitForExternalDebugger was set — adapter marked as stopped");
                    }

                    // Must be on UI thread — SetExternalDebugAdapter installs the
                    // breakpoint evaluator on the (possibly new) CurrentSystemRunner.
                    Dispatcher.UIThread.Post(() =>
                    {
                        if (Core.App.Current?.HostApp != null)
                        {
                            Core.App.Current.HostApp.SetExternalDebugAdapter(adapter);
                            adapter.NotifyInstalledInHost();
                            _debugLogWriter.WriteLine("Debug adapter installed in host app (emulator started/restarted)");
                        }
                    });
                }
                else if (currentHostApp?.EmulatorState == EmulatorState.Uninitialized)
                {
                    _debugLogWriter.WriteLine("EmulatorState became Uninitialized (system stopped from UI), sending terminated event");
                    _ = adapter.SendTerminatedEventAsync();
                }
            }
        };

        if (hostApp is INotifyPropertyChanged notifier)
        {
            // HostApp is already available — subscribe immediately.
            notifier.PropertyChanged += emulatorStateHandler;
            _debugLogWriter.WriteLine("Subscribed to EmulatorState changes (HostApp available at connect time)");
        }
        else
        {
            // HostApp is not yet initialized (Avalonia still starting up when client connected).
            // Start background task to subscribe once HostApp becomes available.
            _ = Task.Run(async () =>
            {
                IHostApp? currentHostApp = null;
                while (!sessionCts.IsCancellationRequested
                       && (currentHostApp = Core.App.Current?.HostApp) == null)
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
                    lateNotifier.PropertyChanged += emulatorStateHandler;
                    _debugLogWriter.WriteLine("Subscribed to EmulatorState changes (deferred — HostApp was not ready at connect time)");
                }

                // If the system is already running by the time we subscribe, fire the handler immediately.
                if (currentHostApp?.EmulatorState == EmulatorState.Running
                    && currentHostApp.CurrentRunningSystem != null)
                {
                    _debugLogWriter.WriteLine("System already running when deferred subscription completed — triggering handler immediately");
                    emulatorStateHandler(currentHostApp, new PropertyChangedEventArgs(nameof(IHostApp.EmulatorState)));
                }
            });
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
        _ = Task.Run(async () => await ProcessMessagesAsync(protocol, adapter, emulatorStateHandler, sessionCts));
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
            _debugLogWriter.WriteLine($"Connection closed at {DateTime.Now} (remaining active connections: {_activeConnectionCount}, wasRealSession: {wasRealSession})");

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
            _debugLogWriter.WriteLine("Probe connection cleaned up (no DAP session was established)");
            sessionCts.Dispose();
            return;
        }

        // Real DAP session — full cleanup.

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

        // Dispose session CTS (already cancelled above)
        sessionCts.Dispose();

        _activeAdapter = null;

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
