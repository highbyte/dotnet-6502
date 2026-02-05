using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
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
    private IExecEvaluator? _originalBreakpointEvaluator;
    private ushort? _pendingProgramCounter;
    private bool _automatedStartupComplete = false;
    private bool _stoppedEventSent = false; // Track if we've already sent the stopped event
    private readonly object _startupLock = new object();

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
    /// Sets the pending program counter address that will be applied when a debugger connects.
    /// This is used when a program is loaded but should not run until the debugger attaches.
    /// </summary>
    /// <param name="address">The address to set PC to when debugger connects.</param>
    public void SetPendingProgramCounter(ushort address)
    {
        _pendingProgramCounter = address;
        _debugLogWriter.WriteLine($"Pending PC set to 0x{address:X4} - will be applied when debugger connects");
    }

    public void SignalAutomatedStartupComplete()
    {
        lock (_startupLock)
        {
            _automatedStartupComplete = true;
            _debugLogWriter.WriteLine("Automated startup complete signal received");
        }
    }

    /// <summary>
    /// Starts the TCP debug adapter server on the specified port.
    /// </summary>
    /// <param name="port">The port number to listen on.</param>
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

    private void OnClientConnected(object? sender, ClientConnectedEventArgs e)
    {
        _debugClientConnected = true;
        
        lock (_connectionLock)
        {
            _activeConnectionCount++;
            _debugLogWriter.WriteLine($"Debug client connected at {DateTime.Now} (total active connections: {_activeConnectionCount})");
        }

        // Set flag to disable built-in monitor when external debugger connects
        // Must be dispatched to UI thread for ReactiveUI to pick up the change
        Dispatcher.UIThread.Post(() =>
        {
            if (Core.App.Current?.HostApp != null)
            {
                Core.App.Current.HostApp.IsExternalDebuggerAttached = true;
                _debugLogWriter.WriteLine("IsExternalDebuggerAttached set to true on UI thread");
            }
        });

        var protocol = new DapProtocol(e.Transport, _debugLogWriter);
        var adapter = new DebugAdapterLogic(protocol, _debugLogWriter);

        // Attach to emulator when it's running
        _ = Task.Run(async () => await AttachToEmulatorAsync(adapter));

        // Start message loop for this client
        _ = Task.Run(async () => await ProcessMessagesAsync(protocol, adapter));
    }

    private async Task AttachToEmulatorAsync(DebugAdapterLogic adapter)
    {
        try
        {
            // Wait for app instance and emulator to be in a running state
            _debugLogWriter.WriteLine("Waiting for emulator to be in running state...");
            while (Core.App.Current?.HostApp?.CurrentRunningSystem == null)
            {
                await Task.Delay(100);
            }

            _debugLogWriter.WriteLine("Emulator is running, attaching debug adapter...");
            var system = Core.App.Current.HostApp.CurrentRunningSystem;
            adapter.AttachToEmulator(system.CPU, system.Mem);

            // If there's a pending PC address, wait for automated startup to complete before setting it
            if (_pendingProgramCounter.HasValue)
            {
                var pendingPcValue = _pendingProgramCounter.Value; // Store value before clearing
                _debugLogWriter.WriteLine($"Pending PC 0x{pendingPcValue:X4} - waiting for automated startup to complete...");
                
                // Wait for automated startup to complete (with timeout)
                int waitCount = 0;
                while (waitCount < 100) // 10 second timeout
                {
                    lock (_startupLock)
                    {
                        if (_automatedStartupComplete)
                            break;
                    }
                    await Task.Delay(100);
                    waitCount++;
                }
                
                _pendingProgramCounter = null; // Clear it now
                
                bool shouldSendStoppedEvent = false;
                lock (_startupLock)
                {
                    // Only send stopped event once, even if multiple connections
                    if (!_stoppedEventSent)
                    {
                        _stoppedEventSent = true;
                        shouldSendStoppedEvent = true;
                    }
                }
                
                if (_automatedStartupComplete && shouldSendStoppedEvent)
                {
                    _debugLogWriter.WriteLine($"Setting PC to pending address 0x{pendingPcValue:X4}");
                    
                    // Pause the emulator before setting PC to prevent it from continuing to run
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        Core.App.Current?.HostApp?.Pause();
                    });
                    
                    system.CPU.PC = pendingPcValue;
                    
                    // Send stopped event so VSCode debugger UI updates and breaks at entry point
                    // Delay slightly to ensure everything is settled before sending the event
                    _debugLogWriter.WriteLine("Sending stopped event to debugger");
                    await Task.Delay(100);
                    try
                    {
                        await adapter.SendStoppedEventAsync("entry");
                    }
                    catch (Exception ex)
                    {
                        _debugLogWriter.WriteLine($"Error sending stopped event: {ex.Message}");
                    }
                }
                else if (!_automatedStartupComplete && shouldSendStoppedEvent)
                {
                    _debugLogWriter.WriteLine("Timeout waiting for automated startup - setting PC anyway");
                    
                    // Pause the emulator before setting PC
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        Core.App.Current?.HostApp?.Pause();
                    });
                    
                    system.CPU.PC = pendingPcValue;
                    
                    // Send stopped event so VSCode debugger UI updates and breaks at entry point
                    try
                    {
                        await adapter.SendStoppedEventAsync("entry");
                    }
                    catch (Exception ex)
                    {
                        _debugLogWriter.WriteLine($"Error sending stopped event: {ex.Message}");
                    }
                }
                else
                {
                    _debugLogWriter.WriteLine("Stopped event already sent by another connection - skipping");
                }
            }

            // Install breakpoint evaluator
            var breakpointEvaluator = adapter.GetBreakpointEvaluator();
            _originalBreakpointEvaluator = Core.App.Current.HostApp.CurrentSystemRunner!.CustomExecEvaluator;
            Core.App.Current.HostApp.CurrentSystemRunner!.SetCustomExecEvaluator(breakpointEvaluator);

            // Set debug adapter reference so AvaloniaHostApp can check IsStopped property
            Core.App.Current.HostApp.SetDebugAdapter(adapter);

            _debugLogWriter.WriteLine("Breakpoint evaluator installed and debug adapter set");
        }
        catch (Exception ex)
        {
            _debugLogWriter.WriteLine($"Failed to attach to emulator: {ex}");
        }
    }

    private async Task ProcessMessagesAsync(DapProtocol protocol, DebugAdapterLogic adapter)
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
            CleanupDebugSession(adapter);
        }
    }

    private void CleanupDebugSession(DebugAdapterLogic adapter)
    {
        bool shouldSetFlagToFalse = false;
        
        lock (_connectionLock)
        {
            _activeConnectionCount--;
            _debugLogWriter.WriteLine($"Debug adapter stopped at {DateTime.Now} (remaining active connections: {_activeConnectionCount})");
            
            // Only set flag to false if this is the last connection
            if (_activeConnectionCount <= 0)
            {
                shouldSetFlagToFalse = true;
                _activeConnectionCount = 0; // Ensure it doesn't go negative
            }
        }

        // Reset debugger state to unfreeze emulator
        adapter.Reset();
        if (Core.App.Current?.HostApp != null)
        {
            // Remove breakpoint evaluator to prevent exceptions when program runs again
            Core.App.Current.HostApp.CurrentSystemRunner?.SetCustomExecEvaluator(_originalBreakpointEvaluator);
            Core.App.Current.HostApp.SetDebugAdapter(null!);
            
            // Only set flag to false if all connections are closed
            if (shouldSetFlagToFalse)
            {
                // Resume emulator if it was paused by the debugger - must be on UI thread
                _ = Dispatcher.UIThread.InvokeAsync(async () =>
                {
                    Core.App.Current.HostApp.IsExternalDebuggerAttached = false;
                    _debugLogWriter.WriteLine("IsExternalDebuggerAttached set to false on UI thread (all connections closed)");
                    
                    if (Core.App.Current.HostApp.EmulatorState == EmulatorState.Paused)
                    {
                        _debugLogWriter.WriteLine("Resuming emulator after debugger disconnect");
                        await Core.App.Current.HostApp.Start();
                    }
                });
            }
            
            _debugLogWriter.WriteLine("Emulator state reset, breakpoint evaluator removed, resuming normal execution");
        }

        if (shouldSetFlagToFalse)
        {
            _debugClientConnected = false;
        }
    }

    public void Dispose()
    {
        _debugServer.ClientConnected -= OnClientConnected;
        _debugLogWriter?.Close();
    }
}
