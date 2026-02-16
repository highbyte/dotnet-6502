using System.Text.Json.Nodes;
using Highbyte.DotNet6502;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Utils;

namespace Highbyte.DotNet6502.DebugAdapter;

/// <summary>
/// Debug Adapter Protocol implementation for the 6502 CPU emulator.
/// 
/// Key design decisions (based on DAP spec and mock-debug reference):
/// 1. Instructions are addressed by their memory location (0x0000 - 0xFFFF)
/// 2. The disassemble request uses instructionOffset (in INSTRUCTIONS, not bytes)
/// 3. Must return EXACTLY instructionCount instructions, padding with invalid if needed
/// 4. Address format uses "0x" prefix for hex (required by VS Code BigInt parsing)
/// </summary>
public class DebugAdapterLogic
{
    private readonly DapProtocol _protocol;
    private readonly StreamWriter _log;
    private ISystem? _system;
    // Track source and instruction breakpoints separately, because VSCode
    // sends setBreakpoints (source) and setInstructionBreakpoints (disassembly)
    // as independent requests, each containing the full set for that category.
    // Source breakpoints are tracked per file because VSCode sends setBreakpoints
    // per source file (the full set for THAT file only).
    private readonly Dictionary<string, HashSet<ushort>> _sourceBreakpointsByFile = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<ushort> _instructionBreakpoints = new();
    // Map from address to breakpoint ID, used to populate hitBreakpointIds in stopped events
    private readonly Dictionary<ushort, int> _breakpointIdsByAddress = new();
    private int _nextBreakpointId = 1;
    private ushort? _temporaryBreakpoint = null; // Temporary breakpoint for step over JSR
    private bool _stepOutMode = false; // Flag to indicate we're stepping out (waiting for RTS)
    private readonly bool _builtInExecution; // When true, the adapter runs the CPU itself (no external execution engine)
    private CancellationTokenSource? _executionCts;
    private const int THREAD_ID = 1;
    private const int FRAME_ID = 0;

    private bool _stopOnBRK = true;
    private bool _stopOnOutOfBounds = false; // Disabled by default - don't stop when PC goes outside source range
    private Ca65DbgParser? _dbgParser;
    private string? _programPath;
    private ushort _programStartAddress;
    private ushort _programEndAddress;

    /// <summary>
    /// Indicates whether the debugger is stopped (at a breakpoint or after a step).
    /// When true, the emulator should pause execution to prevent IRQs from firing.
    /// </summary>
    public bool IsStopped { get; private set; } = false;

    /// <summary>
    /// Fired when a DAP disconnect request is received.
    /// The bool parameter is the value of terminateDebuggee from the disconnect args
    /// (true for launch sessions = debuggee should exit, false for attach sessions = keep running).
    /// </summary>
    public event Action<bool>? OnExit;
    public event Action? OnInitialized;

    /// <param name="builtInExecution">
    /// When true, the adapter runs the CPU itself in a background task on continue/step-over-JSR/step-out.
    /// Set to true for standalone hosts (e.g. ConsoleApp) that have no external execution engine.
    /// Set to false (default) for hosts with their own execution loop (e.g. Avalonia emulator).
    /// </param>
    public DebugAdapterLogic(DapProtocol protocol, StreamWriter log, ISystem? system, bool initiallyPaused = false, bool builtInExecution = false)
    {
        _protocol = protocol;
        _log = log;
        _system = system;
        IsStopped = initiallyPaused;
        _builtInExecution = builtInExecution;
    }

    /// <summary>
    /// Binds a system to the adapter after construction.
    /// Used when the debugger attaches before a system is running.
    /// The emulator continues running; use breakpoints or Pause to stop.
    /// </summary>
    public void SetSystem(ISystem system)
    {
        _system = system;
        LogSafe($"[SetSystem] System bound, PC=${system.CPU?.PC:X4}");
    }

    // Set to true when SetExternalDebugAdapter is called in the host, which sets
    // IsExternalDebuggerAttached=true in the run loop. Until that happens, IsStopped
    // has no effect on the host's execution loop.
    private volatile bool _isInstalledInHost = false;

    /// <summary>
    /// Called by the host after SetExternalDebugAdapter completes, signaling that
    /// IsStopped is now effective (IsExternalDebuggerAttached=true in the run loop).
    /// </summary>
    public void NotifyInstalledInHost()
    {
        _isInstalledInHost = true;
        LogSafe("[NotifyInstalledInHost] Adapter installed in host, IsStopped is now effective");
    }

    // Set to true when AutomatedStartupHandler has finished all setup:
    // KERNAL booted, PRG loaded, PC set. Until then, stopOnEntry must NOT pause
    // the emulator (the KERNAL needs to run freely to initialize hardware).
    private volatile bool _programReady = false;

    // Set to true at the start of the stopOnEntry block in HandleLaunchAsync.
    // Signals NotifyProgramReady() to pause the CPU immediately so that no
    // program instructions execute before HandleLaunchAsync sets PC.
    private volatile bool _stopOnEntryPending = false;

    // Set to true by resume handlers (Continue, Next/JSR, StepOut) before IsStopped=false.
    // Causes ShouldBreakAtCurrentPCAsync to skip the very next breakpoint check so that
    // resuming from a breakpoint doesn't immediately re-trigger on the same address.
    // The core evaluator is now called PRE-execution, so this skip is needed on resume.
    private volatile bool _skipNextBreakpointCheck = false;

    /// <summary>
    /// Called by the host when automated startup is complete (KERNAL booted, PRG loaded, PC set).
    /// If stopOnEntry is pending, pauses the CPU immediately so the host can safely set PC
    /// without the emulator executing any program instructions first.
    /// </summary>
    public void NotifyProgramReady()
    {
        if (_stopOnEntryPending)
        {
            IsStopped = true;
            LogSafe("[NotifyProgramReady] Pausing CPU immediately for pending stopOnEntry");
        }
        _programReady = true;
        LogSafe("[NotifyProgramReady] Program setup complete, stopOnEntry will now proceed");
    }

    /// <summary>
    /// Marks the adapter as stopped (IsStopped=true).
    /// Used by the host when it knows the emulator is already paused
    /// (e.g., WaitForExternalDebugger was set before system start).
    /// </summary>
    public void MarkAsStopped()
    {
        IsStopped = true;
        LogSafe("[MarkAsStopped] Adapter marked as stopped");
    }

    /// <summary>
    /// Safe logging that checks if the writer is still open
    /// </summary>
    private void LogSafe(string message)
    {
        try
        {
            if (_log.BaseStream != null && _log.BaseStream.CanWrite)
            {
                _log.WriteLine(message);
                _log.Flush();
            }
        }
        catch (ObjectDisposedException)
        {
            // Writer is disposed, silently ignore
        }
    }

    /// <summary>
    /// Get an IExecEvaluator that checks breakpoints.
    /// The host app should install this using SystemRunner.SetCustomExecEvaluator()
    /// </summary>
    public IExecEvaluator GetBreakpointEvaluator()
    {
        return new BreakpointEvaluator(this);
    }

    /// <summary>
    /// Reset debugger state when client disconnects.
    /// This ensures the emulator resumes normal execution.
    /// </summary>
    public void Reset()
    {
        StopExecutionLoop();
        IsStopped = false;
        _sourceBreakpointsByFile.Clear();
        _instructionBreakpoints.Clear();
        _breakpointIdsByAddress.Clear();
        _nextBreakpointId = 1;
        _temporaryBreakpoint = null;
        _stepOutMode = false;
        _programReady = false;
        _stopOnEntryPending = false;
        _skipNextBreakpointCheck = false;
        LogSafe("[Reset] Debug adapter reset, emulator will resume");
    }

    /// <summary>
    /// Starts the built-in execution loop on a background task.
    /// Only used when <see cref="_builtInExecution"/> is true (standalone hosts with no external execution engine).
    /// The loop runs CPU instructions and checks breakpoints until IsStopped becomes true.
    /// </summary>
    private void StartExecutionLoop()
    {
        if (!_builtInExecution)
            return;

        StopExecutionLoop();
        _executionCts = new CancellationTokenSource();
        var ct = _executionCts.Token;
        _ = Task.Run(async () =>
        {
            var cpu = _system?.CPU;
            var memory = _system?.Mem;
            if (cpu == null || memory == null)
                return;

            LogSafe("[ExecutionLoop] Starting");
            try
            {
                // The skip flag is set by the resume handlers (Continue, Next/JSR, StepOut)
                // before IsStopped=false so the first pre-execution check in
                // ShouldBreakAtCurrentPCAsync is skipped, preventing an immediate
                // re-trigger at the breakpoint address we're resuming from.
                int count = 0;
                while (!IsStopped && !ct.IsCancellationRequested)
                {
                    // Check breakpoints before executing the next instruction (pre-execution)
                    if (await ShouldBreakAtCurrentPCAsync())
                        break;

                    cpu.ExecuteOneInstruction(memory);
                    count++;

                    // Yield periodically to keep the DAP message loop responsive
                    if (count % 1000 == 0)
                        await Task.Yield();
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                LogSafe($"[ExecutionLoop] Error: {ex.Message}");
            }
            LogSafe("[ExecutionLoop] Stopped");
        });
    }

    private void StopExecutionLoop()
    {
        _executionCts?.Cancel();
        _executionCts = null;
    }

    /// <summary>
    /// Check if execution should stop at the current PC (called by BreakpointEvaluator).
    /// The core evaluator is called pre-execution, so when resuming from a breakpoint the
    /// caller must set <see cref="_skipNextBreakpointCheck"/> to avoid re-triggering at
    /// the same address.
    /// </summary>
    internal async Task<bool> ShouldBreakAtCurrentPCAsync()
    {
        // Skip one check when resuming from a breakpoint/step so we don't immediately
        // re-trigger at the address we just stopped at.
        if (_skipNextBreakpointCheck)
        {
            _skipNextBreakpointCheck = false;
            return false;
        }

        var cpu = _system?.CPU;
        var memory = _system?.Mem;
        if (cpu == null || memory == null)
            return false;

        var pc = cpu.PC;
        byte currentOpcode = memory[pc];

        // Check if we hit a temporary breakpoint (for step over JSR)
        if (_temporaryBreakpoint.HasValue && _temporaryBreakpoint.Value == pc)
        {
            LogSafe($"[BreakpointHit] Temporary breakpoint hit at ${pc:X4}");
            _temporaryBreakpoint = null; // Clear temporary breakpoint
            IsStopped = true; // Pause emulator
            await SendStoppedEventAsync("step");
            return true;
        }

        // Check if we're in step out mode and about to execute RTS
        if (_stepOutMode && currentOpcode == (byte)OpCodeId.RTS)
        {
            LogSafe($"[StepOut] RTS detected at ${pc:X4}, executing and stopping");
            // Execute the RTS instruction
            cpu.ExecuteOneInstruction(memory);
            LogSafe($"[StepOut] After RTS, PC=${cpu.PC:X4}");
            _stepOutMode = false; // Clear step out mode
            IsStopped = true; // Pause emulator
            await SendStoppedEventAsync("step");
            return true;
        }

        // Check if we hit a breakpoint (from either source or instruction breakpoints)
        if (_sourceBreakpointsByFile.Values.Any(bps => bps.Contains(pc)) || _instructionBreakpoints.Contains(pc))
        {
            LogSafe($"[BreakpointHit] Breakpoint hit at ${pc:X4}");
            IsStopped = true; // Pause emulator
            int[]? hitIds = _breakpointIdsByAddress.TryGetValue(pc, out var bpId) ? new[] { bpId } : null;
            await SendStoppedEventAsync("breakpoint", hitBreakpointIds: hitIds);
            return true;
        }

        if (_stopOnBRK && currentOpcode == (byte)OpCodeId.BRK)
        {
            LogSafe($"[BreakpointHit] BRK instruction hit at ${pc:X4}");
            IsStopped = true; // Pause emulator
            await SendStoppedEventAsync("breakpoint");
            return true;
        }

        return false;
    }

    public async Task HandleMessageAsync(JsonObject message)
    {
        var type = message["type"]?.ToString();

        if (type == "request")
        {
            var command = message["command"]?.ToString();
            var seq = (int)message["seq"]!;
            var arguments = message["arguments"] as JsonObject;

            LogSafe($"[Handler] Request: {command}");

            try
            {
                switch (command)
                {
                    case "initialize":
                        await HandleInitializeAsync(seq, arguments);
                        break;
                    case "launch":
                        await HandleLaunchAsync(seq, arguments);
                        break;
                    case "attach":
                        await HandleAttachAsync(seq, arguments);
                        break;
                    case "configurationDone":
                        await HandleConfigurationDoneAsync(seq);
                        break;
                    case "setBreakpoints":
                        await HandleSetBreakpointsAsync(seq, arguments);
                        break;
                    case "setInstructionBreakpoints":
                        await HandleSetInstructionBreakpointsAsync(seq, arguments);
                        break;
                    case "disassemble":
                        await HandleDisassembleAsync(seq, arguments);
                        break;
                    case "threads":
                        await HandleThreadsAsync(seq);
                        break;
                    case "stackTrace":
                        await HandleStackTraceAsync(seq, arguments);
                        break;
                    case "scopes":
                        await HandleScopesAsync(seq, arguments);
                        break;
                    case "variables":
                        await HandleVariablesAsync(seq, arguments);
                        break;
                    case "readMemory":
                        await HandleReadMemoryAsync(seq, arguments);
                        break;
                    case "evaluate":
                        await HandleEvaluateAsync(seq, arguments);
                        break;
                    case "continue":
                        await HandleContinueAsync(seq, arguments);
                        break;
                    case "next":
                        await HandleNextAsync(seq, arguments);
                        break;
                    case "stepIn":
                        await HandleStepInAsync(seq, arguments);
                        break;
                    case "stepOut":
                        await HandleStepOutAsync(seq, arguments);
                        break;
                    case "pause":
                        await HandlePauseAsync(seq, arguments);
                        break;
                    case "terminate":
                        await HandleTerminateAsync(seq, arguments);
                        break;
                    case "disconnect":
                        await HandleDisconnectAsync(seq, arguments);
                        break;
                    case "getMemoryDump":
                        await HandleGetMemoryDumpAsync(seq, arguments);
                        break;
                    default:
                        LogSafe($"[Handler] Unknown command: {command}");
                        await _protocol.SendResponseAsync(seq, command ?? "unknown");
                        break;
                }
                LogSafe($"[Handler] Completed: {command}");
            }
            catch (Exception ex)
            {
                LogSafe($"[Handler] Exception in {command}: {ex}");
                _log.Flush();
            }
        }
    }

    private async Task HandleInitializeAsync(int seq, JsonObject? args)
    {
        var body = new JsonObject
        {
            ["supportsConfigurationDoneRequest"] = true,
            ["supportTerminateDebuggee"] = true,
            ["supportsTerminateRequest"] = true,
            ["supportsInstructionBreakpoints"] = true,
            ["supportsDisassembleRequest"] = true,
            ["supportsSteppingGranularity"] = true,
            ["supportsReadMemoryRequest"] = true,
            ["supportsEvaluateForHovers"] = true
        };

        await _protocol.SendResponseAsync(seq, "initialize", body);
        OnInitialized?.Invoke();
    }

    private async Task HandleLaunchAsync(int seq, JsonObject? args)
    {
        var stopOnEntry = args?["stopOnEntry"]?.GetValue<bool>() ?? true;
        var loadAddress = args?["loadAddress"]?.GetValue<int?>();
        _stopOnBRK = args?["stopOnBRK"]?.GetValue<bool>() ?? true;
        var programAlreadyLoaded = args?["__programAlreadyLoaded"]?.GetValue<bool>() ?? false;

        // Get program and dbgFile from config, or auto-derive if preLaunchTask was used
        string? program = args?["program"]?.ToString();
        string? dbgFile = args?["dbgFile"]?.ToString();
        var preLaunchTask = args?["preLaunchTask"]?.ToString();

        // If program not specified but preLaunchTask was used, look for recently built .prg files
        if (string.IsNullOrEmpty(program) && !string.IsNullOrEmpty(preLaunchTask))
        {
            var workingDir = args?["__workspaceFolder"]?.ToString() ?? Directory.GetCurrentDirectory();
            LogSafe($"[Launch] program not specified with preLaunchTask, searching in: {workingDir}");

            // Find the most recently modified .prg file (likely just built by the task)
            var prgFiles = Directory.GetFiles(workingDir, "*.prg", SearchOption.AllDirectories)
                .Select(f => new FileInfo(f))
                .OrderByDescending(f => f.LastWriteTime)
                .ToList();

            if (prgFiles.Any())
            {
                var recentPrg = prgFiles.First();
                program = recentPrg.FullName;
                LogSafe($"[Launch] Auto-detected recently built program: {program} (modified: {recentPrg.LastWriteTime})");
                await SendOutputAsync($"Auto-detected program: {Path.GetFileName(program)}\n");

                // Also look for corresponding .dbg file
                var dbgPath = Path.ChangeExtension(program, ".dbg");
                if (File.Exists(dbgPath))
                {
                    dbgFile = dbgPath;
                    LogSafe($"[Launch] Auto-detected debug file: {dbgFile}");
                    await SendOutputAsync($"Auto-detected debug file: {Path.GetFileName(dbgFile)}\n");
                }
            }
            else
            {
                LogSafe($"[Launch] No .prg files found in workspace");
            }
        }

        LogSafe($"[Launch] program={program}, dbgFile={dbgFile}, stopOnEntry={stopOnEntry}, stopOnBRK={_stopOnBRK}, programAlreadyLoaded={programAlreadyLoaded}");

        var memory = _system?.Mem;
        var cpu = _system?.CPU;

        // When program is already loaded by the emulator host, we still use the program
        // path for debug symbols and program bounds, but skip loading into memory.
        if (programAlreadyLoaded)
        {
            LogSafe("[Launch] Program already loaded by emulator host, skipping memory load");

            if (!string.IsNullOrEmpty(program) && File.Exists(program))
            {
                _programPath = program;
                var isBinFile = program.EndsWith(".bin", StringComparison.OrdinalIgnoreCase);

                // Read the file to determine program bounds (without loading into memory)
                var fileBytes = File.ReadAllBytes(program);
                ushort loadAddr;
                ushort fileLength;
                if (!isBinFile && fileBytes.Length >= 2)
                {
                    // .prg file: first two bytes are load address (little-endian)
                    loadAddr = (ushort)(fileBytes[0] | (fileBytes[1] << 8));
                    fileLength = (ushort)(fileBytes.Length - 2);
                }
                else
                {
                    // .bin file: use load address from config or debug symbols
                    loadAddr = loadAddress.HasValue ? (ushort)loadAddress.Value : (ushort)0;
                    fileLength = (ushort)fileBytes.Length;
                }

                _programStartAddress = loadAddr;
                _programEndAddress = (ushort)(loadAddr + fileLength - 1);
                await SendOutputAsync($"Program already loaded by emulator host: {Path.GetFileName(program)}\n");
                await SendOutputAsync($"  Program range: ${_programStartAddress:X4} - ${_programEndAddress:X4}\n");
            }
            else
            {
                await SendOutputAsync($"Attached to emulator host (no program file specified for debug symbols)\n");
            }
        }
        else
        {
            // Standard launch: load program into memory
            if (string.IsNullOrEmpty(program) || !File.Exists(program))
            {
                await SendOutputAsync($"Error: Program file not found: {program}\n");
                await _protocol.SendResponseAsync(seq, "launch");
                return;
            }

            _programPath = program;
            var isBinFile = program.EndsWith(".bin", StringComparison.OrdinalIgnoreCase);

            // Determine load address for .bin files
            ushort? effectiveLoadAddress = null;
            if (isBinFile)
            {
                ushort? dbgLoadAddr = null;
                if (_dbgParser != null)
                    dbgLoadAddr = _dbgParser.GetLoadAddress();

                if (loadAddress.HasValue)
                {
                    effectiveLoadAddress = (ushort)loadAddress.Value;
                    await SendOutputAsync($".bin file: Using load address from config: ${effectiveLoadAddress:X4}\n");
                }
                else if (dbgLoadAddr.HasValue)
                {
                    effectiveLoadAddress = dbgLoadAddr.Value;
                    await SendOutputAsync($".bin file: Using load address from .dbg: ${effectiveLoadAddress:X4}\n");
                }
                else
                {
                    await SendOutputAsync($"Error: .bin file requires either 'loadAddress' in config or a .dbg file\n");
                    await _protocol.SendResponseAsync(seq, "launch");
                    return;
                }
            }
            else if (loadAddress.HasValue)
            {
                effectiveLoadAddress = (ushort)loadAddress.Value;
            }

            if (memory == null)
            {
                await SendOutputAsync($"Error: No system available to load program into\n");
                await _protocol.SendResponseAsync(seq, "launch");
                return;
            }

            memory.Load(program, out ushort loadAddr, out ushort fileLength, forceLoadAddress: effectiveLoadAddress, fileContainsLoadAddress: !isBinFile);

            _programStartAddress = loadAddr;
            _programEndAddress = (ushort)(loadAddr + fileLength - 1);

            await SendOutputAsync($"Loaded {program} at ${loadAddr:X4}, length: {fileLength} bytes\n");
        }

        // Load debug symbols if provided
        if (!string.IsNullOrEmpty(dbgFile))
        {
            if (File.Exists(dbgFile))
            {
                try
                {
                    _dbgParser = new Ca65DbgParser();
                    _dbgParser.ParseFile(dbgFile);
                    var dbgLoadAddress = _dbgParser.GetLoadAddress();
                    await SendOutputAsync($"Loaded debug symbols from {dbgFile}\n");
                    await SendOutputAsync($"  Files: {_dbgParser.SourceLineToAddress.Count}\n");
                    await SendOutputAsync($"  Load address from .dbg: ${dbgLoadAddress:X4}\n");
                }
                catch (Exception ex)
                {
                    await SendOutputAsync($"Warning: Failed to load debug symbols: {ex.Message}\n");
                    _dbgParser = null;
                }
            }
            else
            {
                await SendOutputAsync($"Warning: Debug file not found: {dbgFile}\n");
            }
        }

        if (cpu != null)
            await SendOutputAsync($"PC at ${cpu.PC:X4}\n");
        if (!_stopOnBRK)
        {
            await SendOutputAsync("Note: stopOnBRK is disabled - use Pause button to stop execution\n");
        }

        await _protocol.SendResponseAsync(seq, "launch");

        // Send initialized event
        await _protocol.SendEventAsync("initialized");

        // If stopOnEntry, send stopped event
        if (stopOnEntry)
        {
            // Signal that a stopOnEntry pause is imminent. This causes NotifyProgramReady()
            // to set IsStopped=true synchronously when called, BEFORE AutomatedStartupHandler
            // sets CPU.PC — preventing any program instructions from executing first.
            _stopOnEntryPending = true;

            if (_system == null)
            {
                // Debugger connected before the system started.
                // Wait for SetSystem to bind the system (called by emulatorStateHandler).
                int waitCount = 0;
                while (_system == null && waitCount < 100) // up to 10 seconds
                {
                    await Task.Delay(100);
                    waitCount++;
                }
                if (_system == null)
                {
                    LogSafe("[Launch] Timeout waiting for system to start for stopOnEntry");
                    IsStopped = true;
                    await SendStoppedEventAsync("entry");
                    return;
                }
            }

            // For emulator mode (programAlreadyLoaded), wait for AutomatedStartupHandler to
            // finish all setup: KERNAL boot, PRG load, PC set. Only then pause the emulator.
            // This prevents freezing the C64 before hardware/BASIC is fully initialized.
            if (programAlreadyLoaded && !_programReady)
            {
                LogSafe("[Launch] Waiting for automated startup to complete (KERNAL boot + PRG load)...");
                int readyWait = 0;
                while (!_programReady && readyWait < 300) // up to 30 seconds
                {
                    await Task.Delay(100);
                    readyWait++;
                }
                if (!_programReady)
                    LogSafe("[Launch] Warning: program-ready signal not received — stopOnEntry may be early");
            }

            // Wait for SetExternalDebugAdapter to complete in the host (sets IsExternalDebuggerAttached=true).
            // Until that happens, IsStopped=true has no effect on the run loop.
            int installWait = 0;
            while (!_isInstalledInHost && installWait < 200) // up to 2 seconds
            {
                await Task.Delay(10);
                installWait++;
            }
            if (!_isInstalledInHost)
                LogSafe("[Launch] Warning: adapter not installed in host after timeout — stopOnEntry may not be reliable");

            // NOW pause the emulator. All setup is done; IsExternalDebuggerAttached=true so
            // IsStopped=true will take effect on the run loop immediately.
            IsStopped = true;

            // Brief delay for the run loop to detect IsStopped=true and pause.
            await Task.Delay(50);

            // Re-read CPU in case SetSystem was called while waiting.
            cpu = _system?.CPU;

            // Set PC to program start address so debugger stops at the right place.
            // In emulator mode (programAlreadyLoaded), the emulator host loaded the
            // program but the CPU may still be executing code at a different address.
            // Redirect execution to the program entry point.
            if (_programStartAddress != 0 && cpu != null)
            {
                cpu.PC = _programStartAddress;
            }

            await SendStoppedEventAsync("entry");
            _stopOnEntryPending = false;
        }
    }

    private async Task HandleAttachAsync(int seq, JsonObject? args)
    {
        LogSafe("[Attach] Starting attach sequence");

        var stopOnEntry = args?["stopOnEntry"]?.GetValue<bool>() ?? false;
        _stopOnBRK = args?["stopOnBRK"]?.GetValue<bool>() ?? true;

        // Get program and dbgFile from config, or auto-derive if preLaunchTask was used
        string? program = args?["program"]?.ToString();
        string? dbgFile = args?["dbgFile"]?.ToString();
        var preLaunchTask = args?["preLaunchTask"]?.ToString();

        // If program not specified but preLaunchTask was used, look for recently built .prg files
        if (string.IsNullOrEmpty(program) && !string.IsNullOrEmpty(preLaunchTask))
        {
            var workingDir = args?["__workspaceFolder"]?.ToString() ?? Directory.GetCurrentDirectory();
            LogSafe($"[Attach] program not specified with preLaunchTask, searching in: {workingDir}");

            var prgFiles = Directory.GetFiles(workingDir, "*.prg", SearchOption.AllDirectories)
                .Select(f => new FileInfo(f))
                .OrderByDescending(f => f.LastWriteTime)
                .ToList();

            if (prgFiles.Any())
            {
                var recentPrg = prgFiles.First();
                program = recentPrg.FullName;
                LogSafe($"[Attach] Auto-detected recently built program: {program} (modified: {recentPrg.LastWriteTime})");
                await SendOutputAsync($"Auto-detected program: {Path.GetFileName(program)}\n");

                // Also look for corresponding .dbg file
                if (string.IsNullOrEmpty(dbgFile))
                {
                    var dbgPath = Path.ChangeExtension(program, ".dbg");
                    if (File.Exists(dbgPath))
                    {
                        dbgFile = dbgPath;
                        LogSafe($"[Attach] Auto-detected debug file: {dbgFile}");
                        await SendOutputAsync($"Auto-detected debug file: {Path.GetFileName(dbgFile)}\n");
                    }
                }
            }
            else
            {
                LogSafe($"[Attach] No .prg files found in workspace");
            }
        }

        LogSafe($"[Attach] program={program}, dbgFile={dbgFile}, stopOnEntry={stopOnEntry}, stopOnBRK={_stopOnBRK}");

        // In attach mode, we don't create the emulator - it's already running in the desktop app
        // We just load the debug symbols to enable source-level debugging

        // Store program path for reference
        if (!string.IsNullOrEmpty(program))
        {
            _programPath = program;
            await SendOutputAsync($"Attached to running emulator\n");
            await SendOutputAsync($"Program: {program}\n");
        }

        // Load debug symbols if provided
        if (!string.IsNullOrEmpty(dbgFile))
        {
            if (File.Exists(dbgFile))
            {
                try
                {
                    _dbgParser = new Ca65DbgParser();
                    _dbgParser.ParseFile(dbgFile);
                    var dbgLoadAddress = _dbgParser.GetLoadAddress();

                    // In attach mode, get program bounds from debug symbols
                    if (_dbgParser.SourceLineToAddress.Count > 0)
                    {
                        // Find min and max addresses from all source line mappings
                        var allAddresses = _dbgParser.SourceLineToAddress.Values
                            .SelectMany(lineDict => lineDict.Values)
                            .ToList();

                        if (allAddresses.Any())
                        {
                            _programStartAddress = allAddresses.Min();
                            _programEndAddress = allAddresses.Max();
                            LogSafe($"[Attach] Program bounds from debug symbols: ${_programStartAddress:X4} - ${_programEndAddress:X4}");
                        }
                    }

                    await SendOutputAsync($"Loaded debug symbols from {dbgFile}\n");
                    await SendOutputAsync($"  Source files: {_dbgParser.SourceLineToAddress.Keys.Count}\n");
                    await SendOutputAsync($"  Load address from .dbg: ${dbgLoadAddress:X4}\n");
                    if (_programStartAddress != 0 || _programEndAddress != 0)
                    {
                        await SendOutputAsync($"  Program range: ${_programStartAddress:X4} - ${_programEndAddress:X4}\n");
                    }
                    LogSafe($"[Attach] Loaded {_dbgParser.SourceLineToAddress.Sum(kvp => kvp.Value.Count)} source line mappings");
                }
                catch (Exception ex)
                {
                    await SendOutputAsync($"Warning: Failed to load debug symbols: {ex.Message}\n");
                    LogSafe($"[Attach] Failed to load debug symbols: {ex}");
                    _dbgParser = null;
                }
            }
            else
            {
                await SendOutputAsync($"Warning: Debug file not found: {dbgFile}\n");
                LogSafe($"[Attach] Debug file not found: {dbgFile}");
            }
        }
        else
        {
            await SendOutputAsync($"Note: No debug symbols loaded - only instruction-level debugging available\n");
            LogSafe($"[Attach] No debug file specified");
        }

        await _protocol.SendResponseAsync(seq, "attach");

        // Send initialized event
        await _protocol.SendEventAsync("initialized");

        if (stopOnEntry && _system != null)
        {
            IsStopped = true;
            await SendStoppedEventAsync("entry");
        }
        else if (_system == null)
        {
            await SendOutputAsync("Waiting for emulator to start...\n");
            LogSafe("[Attach] No system available yet, will bind when emulator starts");
        }

        LogSafe("[Attach] Attach sequence complete");
    }

    private async Task HandleConfigurationDoneAsync(int seq)
    {
        await _protocol.SendResponseAsync(seq, "configurationDone");
    }

    private async Task HandleSetBreakpointsAsync(int seq, JsonObject? args)
    {
        // This handles source-level breakpoints.
        // VSCode sends the FULL set of source breakpoints on each call,
        // so we clear and rebuild _sourceBreakpoints.
        LogSafe("[SetBreakpoints] Called");
        LogSafe($"[SetBreakpoints] args: {args?.ToJsonString()}");

        var breakpoints = new JsonArray();

        var source = args?["source"] as JsonObject;
        var sourcePath = source?["path"]?.ToString();
        var fileKey = sourcePath != null ? Path.GetFileName(sourcePath).ToLowerInvariant() : "";

        // Clear only this file's breakpoints and their IDs (other files' breakpoints are preserved)
        if (_sourceBreakpointsByFile.TryGetValue(fileKey, out var existingBps))
        {
            foreach (var addr in existingBps)
                _breakpointIdsByAddress.Remove(addr);
            existingBps.Clear();
        }

        LogSafe($"[SetBreakpoints] sourcePath={sourcePath}, fileKey={fileKey}");

        if (args?["breakpoints"] is JsonArray requestedBps)
        {
            foreach (var bp in requestedBps)
            {
                LogSafe($"[SetBreakpoints] Processing breakpoint: {bp?.ToJsonString()}");

                var line = bp?["line"]?.GetValue<int>() ?? 0;
                ushort address;
                bool verified = false;

                // Try to resolve line to address using debug symbols
                if (_dbgParser != null && !string.IsNullOrEmpty(sourcePath))
                {
                    var fileName = Path.GetFileName(sourcePath);

                    // Search by comparing filenames, because the .dbg file may store
                    // just the filename ("test.asm") or a full absolute path
                    // ("C:\...\test.asm") depending on how ca65 was invoked.
                    Dictionary<int, ushort>? lineMap = null;
                    foreach (var entry in _dbgParser.SourceLineToAddress)
                    {
                        if (Path.GetFileName(entry.Key).Equals(fileName, StringComparison.OrdinalIgnoreCase))
                        {
                            lineMap = entry.Value;
                            break;
                        }
                    }

                    if (lineMap != null && lineMap.TryGetValue(line, out address))
                    {
                        verified = true;
                        LogSafe($"[SetBreakpoints] Resolved {fileName}:{line} to address ${address:X4}");
                    }
                    else
                    {
                        // Can't resolve - set unverified breakpoint
                        address = 0;
                        LogSafe($"[SetBreakpoints] Could not resolve {fileName}:{line} to address (keys: {string.Join(", ", _dbgParser.SourceLineToAddress.Keys)})");
                    }
                }
                else
                {
                    // No debug symbols - treat line as address (legacy mode)
                    address = (ushort)line;
                    verified = true;
                    LogSafe($"[SetBreakpoints] No debug symbols, treating line {line} as address ${address:X4}");
                }

                int bpId = 0;
                if (verified && address > 0)
                {
                    if (!_sourceBreakpointsByFile.TryGetValue(fileKey, out var bpSet))
                    {
                        bpSet = new HashSet<ushort>();
                        _sourceBreakpointsByFile[fileKey] = bpSet;
                    }
                    bpSet.Add(address);
                    bpId = _nextBreakpointId++;
                    _breakpointIdsByAddress[address] = bpId;
                    LogSafe($"[SetBreakpoints] Added source breakpoint id={bpId} at ${address:X4} for {fileKey}");
                }

                // Create a new source object instead of reusing the one from the request
                // to avoid "node already has a parent" error
                var bpSource = source != null ? new JsonObject
                {
                    ["name"] = source["name"]?.DeepClone(),
                    ["path"] = source["path"]?.DeepClone()
                } : null;

                var bpObject = new JsonObject
                {
                    ["id"] = bpId,
                    ["verified"] = verified,
                    ["line"] = line
                };

                if (bpSource != null)
                {
                    bpObject["source"] = bpSource;
                }

                breakpoints.Add(bpObject);
            }
        }

        var body = new JsonObject
        {
            ["breakpoints"] = breakpoints
        };

        await _protocol.SendResponseAsync(seq, "setBreakpoints", body);
    }

    private async Task HandleSetInstructionBreakpointsAsync(int seq, JsonObject? args)
    {
        // VSCode sends the FULL set of instruction breakpoints on each call,
        // so we clear and rebuild _instructionBreakpoints.
        LogSafe("[SetInstructionBreakpoints] Called");
        LogSafe($"[SetInstructionBreakpoints] args: {args?.ToJsonString()}");

        // Build new set of addresses from the request
        var newAddresses = new HashSet<ushort>();
        var breakpoints = new JsonArray();

        if (args?["breakpoints"] is JsonArray requestedBps)
        {
            foreach (var bp in requestedBps)
            {
                LogSafe($"[SetInstructionBreakpoints] Processing breakpoint: {bp?.ToJsonString()}");

                var instructionReference = bp?["instructionReference"]?.ToString();
                var offset = bp?["offset"]?.GetValue<int>() ?? 0;  // offset is in bytes

                if (instructionReference != null)
                {
                    var baseAddress = ParseAddress(instructionReference);
                    var actualAddress = (ushort)(baseAddress + offset);
                    newAddresses.Add(actualAddress);

                    // Reuse existing ID if this address already has one, otherwise assign new ID
                    // This keeps IDs stable so VSCode can properly track breakpoints for toggling
                    if (!_breakpointIdsByAddress.TryGetValue(actualAddress, out var bpId))
                    {
                        bpId = _nextBreakpointId++;
                        _breakpointIdsByAddress[actualAddress] = bpId;
                        LogSafe($"[SetInstructionBreakpoints] Assigned new id={bpId} for instruction breakpoint at ${actualAddress:X4}");
                    }
                    else
                    {
                        LogSafe($"[SetInstructionBreakpoints] Reusing existing id={bpId} for instruction breakpoint at ${actualAddress:X4}");
                    }

                    // Return the same instructionReference and offset that VSCode sent
                    // This ensures VSCode can properly match breakpoints when toggling them in the UI
                    breakpoints.Add(new JsonObject
                    {
                        ["id"] = bpId,
                        ["verified"] = true,
                        ["instructionReference"] = instructionReference,
                        ["offset"] = offset
                    });
                }
            }
        }

        // Remove IDs for addresses that are no longer in the new set
        var addressesToRemove = _instructionBreakpoints.Except(newAddresses).ToList();
        foreach (var addr in addressesToRemove)
        {
            _breakpointIdsByAddress.Remove(addr);
            LogSafe($"[SetInstructionBreakpoints] Removed id for instruction breakpoint at ${addr:X4}");
        }

        // Update the active instruction breakpoints set
        _instructionBreakpoints.Clear();
        foreach (var addr in newAddresses)
            _instructionBreakpoints.Add(addr);

        var body = new JsonObject
        {
            ["breakpoints"] = breakpoints
        };

        await _protocol.SendResponseAsync(seq, "setInstructionBreakpoints", body);
    }

    private async Task HandleThreadsAsync(int seq)
    {
        var threads = new JsonArray
        {
            new JsonObject
            {
                ["id"] = THREAD_ID,
                ["name"] = "6502 CPU"
            }
        };

        var body = new JsonObject
        {
            ["threads"] = threads
        };

        await _protocol.SendResponseAsync(seq, "threads", body);
    }

    private async Task HandleStackTraceAsync(int seq, JsonObject? args)
    {
        LogSafe("[HandleStackTrace] Called");

        var cpu = _system?.CPU;
        var memory = _system?.Mem;

        if (cpu == null || memory == null)
        {
            var body = new JsonObject
            {
                ["stackFrames"] = new JsonArray(),
                ["totalFrames"] = 0
            };
            await _protocol.SendResponseAsync(seq, "stackTrace", body);
            return;
        }

        var pc = cpu.PC;
        var disasm = OutputGen.GetInstructionDisassembly(cpu, memory, pc);

        LogSafe($"[HandleStackTrace] PC=${pc:X4}, instructionPointerReference={FormatAddress(pc)}");

        var frame = new JsonObject
        {
            ["id"] = FRAME_ID,
            ["name"] = $"${pc:X4}: {disasm}",
            ["instructionPointerReference"] = FormatAddress(pc),
            ["presentationHint"] = "normal"
        };

        // Add source information if debug symbols are available
        bool sourceFound = false;
        if (_dbgParser != null)
        {
            foreach (var fileEntry in _dbgParser.SourceLineToAddress)
            {
                foreach (var lineEntry in fileEntry.Value)
                {
                    if (lineEntry.Value == pc)
                    {
                        // Found source mapping for this address.
                        // The .dbg file may store just a filename ("test.asm") or a full
                        // absolute path ("C:\...\test.asm"). Use the path as-is if absolute,
                        // otherwise combine with the program directory.
                        var sourceFileName = Path.GetFileName(fileEntry.Key);
                        string sourcePath;
                        if (Path.IsPathRooted(fileEntry.Key))
                            sourcePath = fileEntry.Key;
                        else
                            sourcePath = Path.Combine(Path.GetDirectoryName(_programPath) ?? "", fileEntry.Key);

                        frame["source"] = new JsonObject
                        {
                            ["name"] = sourceFileName,
                            ["path"] = sourcePath
                        };
                        frame["line"] = lineEntry.Key;
                        frame["column"] = 0;
                        sourceFound = true;
                        LogSafe($"[HandleStackTrace] Resolved PC to {sourceFileName}:{lineEntry.Key} (path={sourcePath})");
                        break;
                    }
                }
                if (sourceFound)
                    break;
            }
        }

        // If no source mapping found, omit line/column entirely
        // Even though DAP spec mentions "line is 0 and should be ignored", VSCode appears to need
        // the complete absence of these fields to properly recognize it should use Disassembly view
        if (!sourceFound)
        {
            LogSafe($"[HandleStackTrace] No source mapping found for PC=${pc:X4}, line/column/source omitted for Disassembly view");
        }

        var stackFrames = new JsonArray { frame };

        var responseBody = new JsonObject
        {
            ["stackFrames"] = stackFrames,
            ["totalFrames"] = 1
        };

        // Log the complete stack frame for debugging
        LogSafe($"[HandleStackTrace] Sending stackFrame: {frame.ToJsonString()}");
        LogSafe($"[HandleStackTrace] sourceFound={sourceFound}, line={frame["line"]}, hasSource={frame.ContainsKey("source")}");

        await _protocol.SendResponseAsync(seq, "stackTrace", responseBody);
    }

    private async Task HandleScopesAsync(int seq, JsonObject? args)
    {
        LogSafe($"[HandleScopes] Called");

        var scopes = new JsonArray
        {
            new JsonObject
            {
                ["name"] = "Registers",
                ["variablesReference"] = 1,
                ["expensive"] = false
            },
            new JsonObject
            {
                ["name"] = "Flags",
                ["variablesReference"] = 2,
                ["expensive"] = false
            }
        };

        var body = new JsonObject
        {
            ["scopes"] = scopes
        };

        await _protocol.SendResponseAsync(seq, "scopes", body);
    }

    private async Task HandleVariablesAsync(int seq, JsonObject? args)
    {
        LogSafe($"[HandleVariables] Called");

        var cpu = _system?.CPU;
        var memory = _system?.Mem;

        var variablesReference = args?["variablesReference"]?.GetValue<int>() ?? 0;
        var variables = new JsonArray();

        LogSafe($"[HandleVariables] variablesReference={variablesReference}, PC=${cpu?.PC:X4}");

        if (cpu != null)
        {
            if (variablesReference == 1) // Registers
            {
                variables.Add(new JsonObject { ["name"] = "PC", ["value"] = $"${cpu.PC:X4}", ["type"] = "ushort", ["variablesReference"] = 0 });
                variables.Add(new JsonObject { ["name"] = "A", ["value"] = $"${cpu.A:X2}", ["type"] = "byte", ["variablesReference"] = 0 });
                variables.Add(new JsonObject { ["name"] = "X", ["value"] = $"${cpu.X:X2}", ["type"] = "byte", ["variablesReference"] = 0 });
                variables.Add(new JsonObject { ["name"] = "Y", ["value"] = $"${cpu.Y:X2}", ["type"] = "byte", ["variablesReference"] = 0 });
                variables.Add(new JsonObject { ["name"] = "SP", ["value"] = $"${cpu.SP:X2}", ["type"] = "byte", ["variablesReference"] = 0 });
                LogSafe($"[HandleVariables] Returning {variables.Count} register variables");
            }
            else if (variablesReference == 2) // Flags
            {
                var ps = cpu.ProcessorStatus;
                variables.Add(new JsonObject { ["name"] = "C (Carry)", ["value"] = ps.Carry ? "1" : "0", ["variablesReference"] = 0 });
                variables.Add(new JsonObject { ["name"] = "Z (Zero)", ["value"] = ps.Zero ? "1" : "0", ["variablesReference"] = 0 });
                variables.Add(new JsonObject { ["name"] = "I (IRQ Disable)", ["value"] = ps.InterruptDisable ? "1" : "0", ["variablesReference"] = 0 });
                variables.Add(new JsonObject { ["name"] = "D (Decimal)", ["value"] = ps.Decimal ? "1" : "0", ["variablesReference"] = 0 });
                variables.Add(new JsonObject { ["name"] = "V (Overflow)", ["value"] = ps.Overflow ? "1" : "0", ["variablesReference"] = 0 });
                variables.Add(new JsonObject { ["name"] = "N (Negative)", ["value"] = ps.Negative ? "1" : "0", ["variablesReference"] = 0 });
                LogSafe($"[HandleVariables] Returning {variables.Count} flag variables");
            }
        }
        else
        {
            LogSafe($"[HandleVariables] WARNING: _cpu is null!");
        }

        var body = new JsonObject
        {
            ["variables"] = variables
        };

        await _protocol.SendResponseAsync(seq, "variables", body);
    }

    private bool IsOutOfBounds(ushort address)
    {
        // Don't check bounds if the feature is disabled
        if (!_stopOnOutOfBounds)
            return false;

        // Don't check bounds if we don't have valid program range
        if (_programStartAddress == 0 && _programEndAddress == 0)
            return false;

        // Check if address is outside loaded program range
        if (address < _programStartAddress || address > _programEndAddress)
        {
            return true;
        }

        // If debug symbols loaded, also check if address has source mapping
        if (_dbgParser != null && _dbgParser.SourceLineToAddress.Count > 0)
        {
            // Check if any source file has a mapping to this address
            foreach (var fileDict in _dbgParser.SourceLineToAddress.Values)
            {
                if (fileDict.Values.Contains(address))
                {
                    return false; // Found a mapping, address is valid
                }
            }
            // No source mapping found for this address
            return true;
        }

        return false;
    }

    private string HandleMemoryDumpCommand(string command)
    {
        // Parse command: "dump <start> [<length>]" or "dump <start> [<end>]"
        // Supports formats: dump 0xc000, dump 0xc000 256, dump $c000 $c0ff, md c000 100
        var parts = command.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length < 2)
        {
            return "Usage: dump <start_address> [length_or_end_address]\nExamples:\n  dump 0xc000\n  dump 0xc000 256\n  dump $c000 $c0ff\n  md c000 100";
        }

        try
        {
            ushort startAddress = ParseAddress(parts[1]);
            int length = 256; // Default to 256 bytes
            int requestedLength = length;

            // If second parameter provided, parse it
            if (parts.Length >= 3)
            {
                string secondParam = parts[2];
                ushort lengthOrEnd = ParseAddress(secondParam);

                // Determine if second parameter is length or end address based on format:
                // - Hex format (0x or $) = end address
                // - Decimal format = length
                bool isHexFormat = secondParam.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ||
                                   secondParam.StartsWith("$");

                if (isHexFormat && lengthOrEnd > startAddress)
                {
                    // Treat as end address
                    length = lengthOrEnd - startAddress + 1;
                }
                else
                {
                    // Treat as length
                    length = lengthOrEnd;
                }
                requestedLength = length;
            }

            // Limit to 64KB address space (don't wrap around past 0xFFFF)
            int maxLength = 0x10000 - startAddress;
            if (length > maxLength)
            {
                length = maxLength;
            }

            if (length < 1)
            {
                return "Error: Invalid length";
            }

            return FormatMemoryDump(startAddress, length);
        }
        catch (Exception ex)
        {
            return $"Error parsing command: {ex.Message}";
        }
    }

    private string FormatMemoryDump(ushort startAddress, int length)
    {
        var cpu = _system?.CPU;
        var memory = _system?.Mem;

        if (memory == null)
        {
            return "Memory not available";
        }

        var sb = new System.Text.StringBuilder();
        int bytesPerRow = 16;

        for (int offset = 0; offset < length; offset += bytesPerRow)
        {
            ushort address = (ushort)((startAddress + offset) & 0xFFFF);
            sb.Append($"0x{address:X4}: ");

            // Hex bytes
            int rowBytes = Math.Min(bytesPerRow, length - offset);
            for (int i = 0; i < bytesPerRow; i++)
            {
                if (i < rowBytes)
                {
                    byte value = memory[(ushort)((address + i) & 0xFFFF)];
                    sb.Append($"{value:X2} ");
                }
                else
                {
                    sb.Append("   "); // Padding for incomplete rows
                }
            }

            sb.Append(" ");

            // ASCII/PETSCII representation
            for (int i = 0; i < rowBytes; i++)
            {
                byte value = memory[(ushort)((address + i) & 0xFFFF)];
                char c;

                // Convert PETSCII to displayable character
                if (value >= 0x20 && value <= 0x7E)
                {
                    c = (char)value; // Standard ASCII printable
                }
                else if (value >= 0xA0 && value <= 0xFE)
                {
                    // PETSCII graphics characters - map to similar ASCII
                    c = (char)(value - 0x80);
                }
                else
                {
                    c = '.'; // Non-printable
                }

                sb.Append(c);
            }

            sb.AppendLine();
        }

        // Remove trailing newline to avoid empty row at end
        if (sb.Length > 0 && sb[sb.Length - 1] == '\n')
        {
            sb.Length--;
            if (sb.Length > 0 && sb[sb.Length - 1] == '\r')
            {
                sb.Length--;
            }
        }

        return sb.ToString();
    }

    private async Task HandleGetMemoryDumpAsync(int seq, JsonObject? args)
    {
        LogSafe("[HandleGetMemoryDump] Called");

        var cpu = _system?.CPU;
        var memory = _system?.Mem;

        try
        {
            if (cpu == null || memory == null)
            {
                await _protocol.SendResponseAsync(seq, "getMemoryDump", new JsonObject
                {
                    ["success"] = false,
                    ["message"] = "Not debugging"
                });
                return;
            }

            // Parse arguments
            var address = args?["address"]?.GetValue<int>() ?? cpu.PC;
            var length = args?["length"]?.GetValue<int>() ?? 256;

            // Validate address
            if (address < 0 || address > 0xFFFF)
            {
                await _protocol.SendResponseAsync(seq, "getMemoryDump", new JsonObject
                {
                    ["success"] = false,
                    ["message"] = $"Invalid address: 0x{address:X}"
                });
                return;
            }

            // Limit to address space boundary
            int maxLength = 0x10000 - address;
            if (length > maxLength)
            {
                length = maxLength;
            }

            // Generate memory dump
            var content = FormatMemoryDump((ushort)address, length);

            await _protocol.SendResponseAsync(seq, "getMemoryDump", new JsonObject
            {
                ["success"] = true,
                ["address"] = address,
                ["length"] = length,
                ["content"] = content
            });
        }
        catch (Exception ex)
        {
            LogSafe($"[GetMemoryDump] Error: {ex.Message}");
            await _protocol.SendResponseAsync(seq, "getMemoryDump", new JsonObject
            {
                ["success"] = false,
                ["message"] = ex.Message
            });
        }
    }

    private async Task HandleEvaluateAsync(int seq, JsonObject? args)
    {
        LogSafe("[ HandleEvaluate] Called");

        var cpu = _system?.CPU;
        var memory = _system?.Mem;

        var expression = args?["expression"]?.ToString() ?? "";
        var context = args?["context"]?.ToString();

        try
        {
            if (cpu == null || memory == null)
            {
                await _protocol.SendResponseAsync(seq, "evaluate");
                return;
            }

            // Parse different expression formats
            string result;

            // Check for REPL commands (Debug Console commands)
            if (context == "repl" && (expression.StartsWith("dump ", StringComparison.OrdinalIgnoreCase) ||
                expression.StartsWith("md ", StringComparison.OrdinalIgnoreCase) ||
                expression.StartsWith("memdump ", StringComparison.OrdinalIgnoreCase)))
            {
                result = HandleMemoryDumpCommand(expression);
            }
            // Check for memory address expressions: $c000, 0xc000, or decimal
            else if (expression.StartsWith("$") || expression.StartsWith("0x", StringComparison.OrdinalIgnoreCase) || int.TryParse(expression, out _))
            {
                ushort address;
                if (expression.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                {
                    address = Convert.ToUInt16(expression.Substring(2), 16);
                }
                else if (expression.StartsWith("$"))
                {
                    address = Convert.ToUInt16(expression.Substring(1), 16);
                }
                else
                {
                    address = Convert.ToUInt16(expression);
                }

                var value = memory[address];
                result = $"${value:X2} ({value})";
            }
            // Check for register expressions
            else if (expression.Equals("PC", StringComparison.OrdinalIgnoreCase))
            {
                result = $"${cpu.PC:X4}";
            }
            else if (expression.Equals("A", StringComparison.OrdinalIgnoreCase))
            {
                result = $"${cpu.A:X2}";
            }
            else if (expression.Equals("X", StringComparison.OrdinalIgnoreCase))
            {
                result = $"${cpu.X:X2}";
            }
            else if (expression.Equals("Y", StringComparison.OrdinalIgnoreCase))
            {
                result = $"${cpu.Y:X2}";
            }
            else if (expression.Equals("SP", StringComparison.OrdinalIgnoreCase))
            {
                result = $"${cpu.SP:X2}";
            }
            else
            {
                result = "not available";
            }

            var body = new JsonObject
            {
                ["result"] = result,
                ["variablesReference"] = 0
            };

            await _protocol.SendResponseAsync(seq, "evaluate", body);
        }
        catch (Exception ex)
        {
            var body = new JsonObject
            {
                ["result"] = $"Error: {ex.Message}",
                ["variablesReference"] = 0
            };
            await _protocol.SendResponseAsync(seq, "evaluate", body);
        }
    }

    private async Task HandleReadMemoryAsync(int seq, JsonObject? args)
    {
        LogSafe("[HandleReadMemory] Called");
        LogSafe($"[HandleReadMemory] args: {args?.ToJsonString()}");

        var cpu = _system?.CPU;
        var memory = _system?.Mem;

        try
        {
            var memoryReference = args?["memoryReference"]?.ToString();
            var count = args?["count"]?.GetValue<int>() ?? 256;
            var offset = args?["offset"]?.GetValue<int>() ?? 0;

            LogSafe($"[HandleReadMemory] memoryReference={memoryReference}, count={count}, offset={offset}");
            _log.Flush();

            if (string.IsNullOrEmpty(memoryReference))
            {
                LogSafe("[HandleReadMemory] memoryReference is empty!");
                await _protocol.SendResponseAsync(seq, "readMemory");
                return;
            }

            // Parse memory reference - could be hex (0xc000, $c000) or decimal
            ushort address;
            if (memoryReference.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                address = Convert.ToUInt16(memoryReference.Substring(2), 16);
            }
            else if (memoryReference.StartsWith("$"))
            {
                address = Convert.ToUInt16(memoryReference.Substring(1), 16);
            }
            else
            {
                address = Convert.ToUInt16(memoryReference);
            }

            // Apply offset
            address = (ushort)((address + offset) & 0xFFFF);

            // Read memory
            if (memory == null)
            {
                await _protocol.SendResponseAsync(seq, "readMemory");
                return;
            }

            // Limit count to not exceed 64KB address space
            int maxCount = 0x10000 - address;
            if (count > maxCount)
            {
                count = maxCount;
            }

            var data = new byte[count];
            for (int i = 0; i < count; i++)
            {
                data[i] = memory[(ushort)((address + i) & 0xFFFF)];
            }

            // Encode as base64
            var base64Data = Convert.ToBase64String(data);

            // According to DAP spec and vscode-mock-debug example, the address field should be the offset, not the calculated address
            // VS Code uses this offset value for address display in the memory viewer
            var body = new JsonObject
            {
                ["address"] = offset.ToString(),  // Return offset as address (per DAP spec/examples)
                ["data"] = base64Data,
                ["unreadableBytes"] = 0
            };

            LogSafe($"[HandleReadMemory] Returning address={offset} (offset), actualMemoryAddress=0x{address:X4}");
            _log.Flush();

            await _protocol.SendResponseAsync(seq, "readMemory", body);
        }
        catch (Exception ex)
        {
            LogSafe($"[ReadMemory] Error: {ex.Message}");
            await _protocol.SendResponseAsync(seq, "readMemory");
        }
    }

    private async Task HandleContinueAsync(int seq, JsonObject? args)
    {
        LogSafe("[HandleContinue] Called");

        var cpu = _system?.CPU;
        var memory = _system?.Mem;

        if (cpu != null && memory != null)
        {
            LogSafe($"[Continue] Resuming emulator execution at PC=${cpu.PC:X4}");

            // Skip the first pre-execution check to avoid re-triggering on the
            // breakpoint address we're resuming from.
            _skipNextBreakpointCheck = true;
            IsStopped = false; // Resume emulator
            StartExecutionLoop();
        }

        var body = new JsonObject
        {
            ["allThreadsContinued"] = true
        };

        await _protocol.SendResponseAsync(seq, "continue", body);
    }

    private async Task HandleNextAsync(int seq, JsonObject? args)
    {
        LogSafe("[HandleNext] Starting...");
        var cpu = _system?.CPU;
        var memory = _system?.Mem;
        try
        {
            if (cpu != null && memory != null)
            {
                // Check if current instruction is JSR (Jump to Subroutine)
                byte opCode = memory[cpu.PC];
                LogSafe($"[HandleNext] Instruction at ${cpu.PC:X4}: opcode=${opCode:X2}");

                if (opCode == (byte)OpCodeId.JSR)
                {
                    // JSR is 3 bytes: opcode (1) + address (2)
                    // Set temporary breakpoint at return address (PC + 3)
                    ushort returnAddress = (ushort)(cpu.PC + 3);
                    _temporaryBreakpoint = returnAddress;
                    LogSafe($"[HandleNext] JSR detected, setting temporary breakpoint at ${returnAddress:X4}");

                    // Resume execution - the breakpoint evaluator will stop at the return address.
                    // Skip the first pre-execution check so we don't re-trigger on the JSR itself.
                    _skipNextBreakpointCheck = true;
                    IsStopped = false;
                    StartExecutionLoop();
                }
                else
                {
                    // Not a JSR - execute single instruction
                    LogSafe($"[HandleNext] Executing single instruction at ${cpu.PC:X4}");
                    _system?.ExecuteOneInstruction(out _);
                    LogSafe($"[HandleNext] New PC: ${cpu.PC:X4}");

                    // Check if PC moved out of bounds after execution
                    if (IsOutOfBounds(cpu.PC))
                    {
                        LogSafe($"[HandleNext] Stepped outside program bounds to ${cpu.PC:X4}");
                        await SendOutputAsync($"⚠️  Warning: Execution outside program bounds at ${cpu.PC:X4}\n");
                        await SendOutputAsync($"   Program range: ${_programStartAddress:X4} - ${_programEndAddress:X4}\n");
                    }
                    // Always use "step" reason to prevent VSCode from clearing variables view
                    IsStopped = true; // Keep emulator paused after step
                    await SendStoppedEventAsync("step");
                }
            }

            await _protocol.SendResponseAsync(seq, "next");
        }
        catch (Exception ex)
        {
            LogSafe($"[HandleNext] EXCEPTION: {ex}");
            _log.Flush();
            throw;
        }
    }

    private async Task HandleStepInAsync(int seq, JsonObject? args)
    {
        LogSafe("[HandleStepIn] Starting...");

        var cpu = _system?.CPU;
        var memory = _system?.Mem;

        if (cpu != null && memory != null)
        {
            _system?.ExecuteOneInstruction(out _);

            // Check if PC moved out of bounds after execution
            if (IsOutOfBounds(cpu.PC))
            {
                await SendOutputAsync($"⚠️  Warning: Execution outside program bounds at ${cpu.PC:X4}\n");
                await SendOutputAsync($"   Program range: ${_programStartAddress:X4} - ${_programEndAddress:X4}\n");
            }
            // Always use "step" reason to prevent VSCode from clearing variables view
            IsStopped = true; // Keep emulator paused after step
            await SendStoppedEventAsync("step");
        }

        await _protocol.SendResponseAsync(seq, "stepIn");
    }

    private async Task HandleStepOutAsync(int seq, JsonObject? args)
    {
        LogSafe("[HandleStepOut] Starting...");

        var cpu = _system?.CPU;
        var memory = _system?.Mem;

        if (cpu != null && memory != null)
        {
            LogSafe($"[HandleStepOut] Enabling step out mode, will run until RTS at PC=${cpu.PC:X4}");
            // Enable step out mode - execution will continue until RTS is found
            _stepOutMode = true;
            // Resume execution - the breakpoint evaluator will stop at RTS.
            // Skip the first pre-execution check so we don't re-trigger on the current PC.
            _skipNextBreakpointCheck = true;
            IsStopped = false;
            StartExecutionLoop();
        }

        await _protocol.SendResponseAsync(seq, "stepOut");
    }

    private async Task HandlePauseAsync(int seq, JsonObject? args)
    {
        LogSafe("[HandlePause] Pause requested");

        IsStopped = true; // Pause the emulator's run loop
        StopExecutionLoop();

        await _protocol.SendResponseAsync(seq, "pause");
        await SendStoppedEventAsync("pause");
    }

    private async Task HandleTerminateAsync(int seq, JsonObject? args)
    {
        await _protocol.SendResponseAsync(seq, "terminate");
        // Send terminated event so VSCode knows to follow up with disconnect.
        // Without this, VSCode waits and the user must press Stop a second time.
        await _protocol.SendEventAsync("terminated");
    }

    private async Task HandleDisconnectAsync(int seq, JsonObject? args)
    {
        // Per DAP spec, terminateDebuggee indicates whether the debuggee should exit.
        // VSCode sends true for "launch" sessions and false for "attach" sessions.
        // Default to true if not specified (DAP spec default for launch).
        bool terminateDebuggee = args?["terminateDebuggee"]?.GetValue<bool>() ?? true;
        LogSafe($"[Disconnect] terminateDebuggee={terminateDebuggee}");
        StopExecutionLoop();

        await _protocol.SendResponseAsync(seq, "disconnect");
        await Task.Delay(100);
        OnExit?.Invoke(terminateDebuggee);
    }

    private async Task SendOutputAsync(string text)
    {
        var body = new JsonObject
        {
            ["category"] = "console",
            ["output"] = text
        };

        await _protocol.SendEventAsync("output", body);
    }

    /// <summary>
    /// Sends a stopped event to the debug client.
    /// Used internally and can be called externally when attaching to an already-running emulator.
    /// </summary>
    public async Task SendStoppedEventAsync(string reason, string? text = null, bool preserveFocusHint = false, int[]? hitBreakpointIds = null)
    {
        var cpu = _system?.CPU;
        var memory = _system?.Mem;

        var body = new JsonObject
        {
            ["reason"] = reason,
            ["threadId"] = THREAD_ID,
            ["allThreadsStopped"] = true
        };

        if (hitBreakpointIds != null && hitBreakpointIds.Length > 0)
        {
            var ids = new JsonArray();
            foreach (var id in hitBreakpointIds)
                ids.Add(id);
            body["hitBreakpointIds"] = ids;
        }

        if (text != null)
        {
            body["text"] = text;
        }

        if (preserveFocusHint)
        {
            body["preserveFocusHint"] = true;
        }

        LogSafe($"[SendStoppedEvent] reason={reason}, text={text}, preserveFocusHint={preserveFocusHint}, PC=${cpu?.PC:X4}");
        LogSafe($"[SendStoppedEvent] Event body: {body.ToJsonString()}");

        await _protocol.SendEventAsync("stopped", body);
    }

    /// <summary>
    /// Handles the disassemble request according to DAP specification.
    /// 
    /// For 6502 with variable-length instructions, we MUST always disassemble from a fixed
    /// starting point (0x0000) to ensure consistent instruction boundaries. VS Code caches
    /// disassembly by address, so the same address must always map to the same instruction.
    /// 
    /// To avoid gaps in VS Code's cached disassembly when PC jumps around, we always return
    /// a contiguous range starting from 0x0000 when the start index would be low anyway.
    /// This ensures VS Code always has complete coverage.
    /// </summary>
    private async Task HandleDisassembleAsync(int seq, JsonObject? args)
    {
        LogSafe("[HandleDisassemble] Called");
        var cpu = _system?.CPU;
        var memory = _system?.Mem;

        var memoryReference = args?["memoryReference"]?.ToString();
        var offset = args?["offset"]?.GetValue<int>() ?? 0;
        var instructionOffset = args?["instructionOffset"]?.GetValue<int>() ?? 0;
        var requestedCount = args?["instructionCount"]?.GetValue<int>() ?? 100;

        LogSafe($"[HandleDisassemble] memoryReference={memoryReference}, offset={offset}, instructionOffset={instructionOffset}, requestedCount={requestedCount}");

        var instructions = new JsonArray();

        if (cpu != null && memory != null && memoryReference != null)
        {
            try
            {
                // Parse base address and apply byte offset
                var baseAddress = ParseAddress(memoryReference);
                int targetAddr = baseAddress + offset;

                // Clamp to valid address range
                if (targetAddr < 0) targetAddr = 0;
                if (targetAddr > 0xFFFF) targetAddr = 0xFFFF;

                LogSafe($"[HandleDisassemble] baseAddress=${baseAddress:X4}, targetAddr=${targetAddr:X4}");

                // Build complete instruction list from 0x0000 - this ensures consistent boundaries
                var instrList = new List<(ushort addr, int len)>();
                int addr = 0;
                while (addr <= 0xFFFF)
                {
                    var opcode = memory[(ushort)addr];
                    var len = GetInstructionLength(opcode);
                    instrList.Add(((ushort)addr, len));
                    addr += len;
                    if (addr > 0xFFFF) break;
                }

                // Find the index of the target address (or nearest instruction containing it)
                int targetIndex = 0;
                for (int i = 0; i < instrList.Count; i++)
                {
                    var (iAddr, iLen) = instrList[i];
                    if (iAddr == targetAddr)
                    {
                        targetIndex = i;
                        break;
                    }
                    if (iAddr + iLen > targetAddr)
                    {
                        // Target is within this instruction
                        targetIndex = i;
                        break;
                    }
                    if (iAddr > targetAddr)
                    {
                        // Passed it, use previous
                        targetIndex = Math.Max(0, i - 1);
                        break;
                    }
                    targetIndex = i;
                }

                // Calculate start index with instruction offset
                int rawStartIndex = targetIndex + instructionOffset;

                LogSafe($"[HandleDisassemble] targetIndex={targetIndex}, rawStartIndex={rawStartIndex}, totalInstr={instrList.Count}");

                // Determine how many instructions to return
                // If the request would start near 0, return a large range to fill VS Code's cache gaps
                int adjustedRequestedCount = requestedCount;
                if (rawStartIndex < 1000)
                {
                    // Return at least 2000 instructions to fill any gaps from previous requests
                    adjustedRequestedCount = Math.Max(requestedCount, Math.Max(2000, targetIndex + 500));
                    LogSafe($"[HandleDisassemble] Adjusted count to {adjustedRequestedCount} to fill cache gaps");
                }

                // If startIndex is negative, we need to pad with synthetic instructions
                // so VS Code gets exactly the number of "before" instructions it expects.
                // This is necessary for correct highlighting of the target instruction.
                int paddingNeededBefore = 0;
                if (rawStartIndex < 0)
                {
                    paddingNeededBefore = -rawStartIndex;
                    LogSafe($"[HandleDisassemble] Need {paddingNeededBefore} padding for negative offset");

                    // Add synthetic instructions for addresses "before" 0x0000 at the START of array
                    // Use wrapped addresses that VS Code can track for position counting
                    for (int p = rawStartIndex; p < 0 && instructions.Count < adjustedRequestedCount; p++)
                    {
                        instructions.Add(new JsonObject
                        {
                            ["address"] = $"0x{(0x10000 + p):x4}", // e.g., 0xffce for position -50
                            ["instructionBytes"] = "--",
                            ["instruction"] = "; (before memory)",
                            ["presentationHint"] = "invalid"
                        });
                    }
                }

                // Start from index 0 (or wherever is valid)
                int startIndex = Math.Max(0, rawStartIndex);
                if (startIndex >= instrList.Count) startIndex = instrList.Count - 1;

                LogSafe($"[HandleDisassemble] startIndex={startIndex}, paddingNeededBefore={paddingNeededBefore}");

                // Generate real instructions - use adjustedRequestedCount to fill gaps
                for (int i = startIndex; i < instrList.Count && instructions.Count < adjustedRequestedCount; i++)
                {
                    var (instrAddr, instrLen) = instrList[i];

                    // Get instruction bytes
                    var bytes = new List<byte>();
                    for (int j = 0; j < instrLen && (instrAddr + j) <= 0xFFFF; j++)
                    {
                        bytes.Add(memory[(ushort)(instrAddr + j)]);
                    }

                    // Get disassembly text
                    var disasm = OutputGen.GetInstructionDisassembly(cpu, memory, instrAddr);
                    var instructionText = StripAddressPrefix(disasm);

                    instructions.Add(new JsonObject
                    {
                        ["address"] = FormatAddress(instrAddr),
                        ["instructionBytes"] = BitConverter.ToString(bytes.ToArray()).Replace("-", " "),
                        ["instruction"] = instructionText
                    });
                }

                LogSafe($"[HandleDisassemble] Generated {instructions.Count} instructions");
                if (instructions.Count > 0)
                {
                    LogSafe($"[HandleDisassemble] First: {instructions[0]?["address"]}, Last: {instructions[instructions.Count - 1]?["address"]}");
                }
            }
            catch (Exception ex)
            {
                LogSafe($"[HandleDisassemble] Error: {ex.Message}");
            }
        }

        var body = new JsonObject
        {
            ["instructions"] = instructions
        };

        await _protocol.SendResponseAsync(seq, "disassemble", body);
    }

    /// <summary>
    /// Create an invalid/placeholder instruction for addresses outside valid range
    /// </summary>
    private JsonObject CreateInvalidInstruction(int address)
    {
        // Keep address in valid range for display, mark as invalid
        var displayAddr = address < 0 ? 0 : (address > 0xFFFF ? 0xFFFF : address);

        return new JsonObject
        {
            ["address"] = FormatAddress((ushort)displayAddr),
            ["instructionBytes"] = "??",
            ["instruction"] = "???",
            ["presentationHint"] = "invalid"
        };
    }

    /// <summary>
    /// Get the length of a 6502 instruction based on its opcode
    /// </summary>
    private int GetInstructionLength(byte opCode)
    {
        var cpu = _system?.CPU;
        var memory = _system?.Mem;

        if (cpu != null && cpu.InstructionList.OpCodeDictionary.ContainsKey(opCode))
        {
            return cpu.InstructionList.GetOpCode(opCode).Size;
        }
        return 1; // Unknown opcodes are treated as 1 byte
    }

    /// <summary>
    /// Strip the address prefix from disassembly output.
    /// OutputGen returns something like "c000  LDA #$00" and we want just "LDA #$00"
    /// </summary>
    private string StripAddressPrefix(string disasm)
    {
        // OutputGen format is typically "XXXX  instruction"
        if (disasm.Length >= 6 && disasm.Substring(4, 2) == "  ")
        {
            return disasm.Substring(6);
        }
        return disasm;
    }

    /// <summary>
    /// Format an address for DAP protocol (must use "0x" prefix for BigInt parsing)
    /// Use lowercase hex like other DAP implementations (e.g., RetroC64)
    /// </summary>
    private static string FormatAddress(ushort address)
    {
        return $"0x{address:x4}";
    }

    /// <summary>
    /// Parse an address from DAP protocol format
    /// Handles "0x1234", "$1234", and decimal formats
    /// </summary>
    private static ushort ParseAddress(string addressStr)
    {
        if (addressStr.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            addressStr = addressStr.Substring(2);
            return Convert.ToUInt16(addressStr, 16);
        }
        else if (addressStr.StartsWith("$"))
        {
            addressStr = addressStr.Substring(1);
            return Convert.ToUInt16(addressStr, 16);
        }
        else
        {
            // Try hex first if it looks like hex, otherwise decimal
            if (addressStr.All(c => "0123456789ABCDEFabcdef".Contains(c)) && addressStr.Length == 4)
            {
                return Convert.ToUInt16(addressStr, 16);
            }
            return Convert.ToUInt16(addressStr);
        }
    }
}
