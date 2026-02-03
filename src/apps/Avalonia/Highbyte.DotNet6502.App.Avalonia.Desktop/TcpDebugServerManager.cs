using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Highbyte.DotNet6502.DebugAdapter;

namespace Highbyte.DotNet6502.App.Avalonia.Desktop;

/// <summary>
/// Manages the TCP debug adapter server and handles incoming debug client connections.
/// </summary>
internal sealed class TcpDebugServerManager : IDisposable
{
    private readonly TcpDebugAdapterServer _debugServer;
    private readonly StreamWriter _debugLogWriter;
    private bool _debugClientConnected;
    private IExecEvaluator? _originalBreakpointEvaluator;

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
        _debugLogWriter.WriteLine($"Debug client connected at {DateTime.Now}");

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

            // Install breakpoint evaluator
            var breakpointEvaluator = adapter.GetBreakpointEvaluator();
            _originalBreakpointEvaluator = Core.App.Current.HostApp.CurrentSystemRunner!.CustomExecEvaluator;
            Core.App.Current.HostApp.CurrentSystemRunner!.SetCustomExecEvaluator(breakpointEvaluator);

            // Set debug adapter reference so AvaloniaHostApp can check IsStopped property
            Core.App.Current.HostApp.SetDebugAdapter(adapter);

            // Set flag to disable built-in monitor when external debugger is attached
            Core.App.Current.HostApp.IsExternalDebuggerAttached = true;

            _debugLogWriter.WriteLine("Breakpoint evaluator installed and external debugger flag set");
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
        _debugLogWriter.WriteLine($"Debug adapter stopped at {DateTime.Now}");

        // Reset debugger state to unfreeze emulator
        adapter.Reset();
        if (Core.App.Current?.HostApp != null)
        {
            // Remove breakpoint evaluator to prevent exceptions when program runs again
            Core.App.Current.HostApp.CurrentSystemRunner?.SetCustomExecEvaluator(_originalBreakpointEvaluator);
            Core.App.Current.HostApp.IsExternalDebuggerAttached = false;
            Core.App.Current.HostApp.SetDebugAdapter(null!);
            _debugLogWriter.WriteLine("Emulator state reset, breakpoint evaluator removed, resuming normal execution");
        }

        _debugClientConnected = false;
    }

    public void Dispose()
    {
        _debugServer.ClientConnected -= OnClientConnected;
        _debugLogWriter?.Close();
    }
}
