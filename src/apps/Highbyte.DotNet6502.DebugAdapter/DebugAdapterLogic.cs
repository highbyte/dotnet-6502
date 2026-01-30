using System.Text.Json.Nodes;
using Highbyte.DotNet6502;
using Highbyte.DotNet6502.Monitor;
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
    private CPU? _cpu;
    private Memory? _memory;
    private readonly HashSet<ushort> _breakpoints = new();
    private const int THREAD_ID = 1;
    private const int FRAME_ID = 0;
    private CancellationTokenSource? _continueTokenSource;
    private bool _stopOnBRK = true;
    private Ca65DbgParser? _dbgParser;
    private string? _programPath;

    public event Action? OnExit;

    public DebugAdapterLogic(DapProtocol protocol, StreamWriter log)
    {
        _protocol = protocol;
        _log = log;
    }

    public async Task HandleMessageAsync(JsonObject message)
    {
        var type = message["type"]?.ToString();
        
        if (type == "request")
        {
            var command = message["command"]?.ToString();
            var seq = (int)message["seq"]!;
            var arguments = message["arguments"] as JsonObject;

            _log.WriteLine($"[Handler] Request: {command}");

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
                    default:
                        _log.WriteLine($"[Handler] Unknown command: {command}");
                        await _protocol.SendResponseAsync(seq, command ?? "unknown");
                        break;
                }
                _log.WriteLine($"[Handler] Completed: {command}");
            }
            catch (Exception ex)
            {
                _log.WriteLine($"[Handler] Exception in {command}: {ex}");
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
    }

    private async Task HandleLaunchAsync(int seq, JsonObject? args)
    {
        var program = args?["program"]?.ToString();
        var stopOnEntry = args?["stopOnEntry"]?.GetValue<bool>() ?? true;
        var loadAddress = args?["loadAddress"]?.GetValue<int?>();
        var dbgFile = args?["dbgFile"]?.ToString();
        _stopOnBRK = args?["stopOnBRK"]?.GetValue<bool>() ?? true;

        _log.WriteLine($"[Launch] program={program}, dbgFile={dbgFile}, stopOnEntry={stopOnEntry}, stopOnBRK={_stopOnBRK}");

        if (string.IsNullOrEmpty(program) || !File.Exists(program))
        {
            await SendOutputAsync($"Error: Program file not found: {program}\n");
            await _protocol.SendResponseAsync(seq, "launch");
            return;
        }

        _programPath = program;
        var isBinFile = program.EndsWith(".bin", StringComparison.OrdinalIgnoreCase);

        // Load debug symbols if provided
        ushort? dbgLoadAddress = null;
        if (!string.IsNullOrEmpty(dbgFile))
        {
            if (File.Exists(dbgFile))
            {
                try
                {
                    _dbgParser = new Ca65DbgParser();
                    _dbgParser.ParseFile(dbgFile);
                    dbgLoadAddress = _dbgParser.GetLoadAddress();
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

        // Determine load address for .bin files
        ushort? effectiveLoadAddress = null;
        if (isBinFile)
        {
            if (loadAddress.HasValue)
            {
                effectiveLoadAddress = (ushort)loadAddress.Value;
                await SendOutputAsync($".bin file: Using load address from config: ${effectiveLoadAddress:X4}\n");
            }
            else if (dbgLoadAddress.HasValue)
            {
                effectiveLoadAddress = dbgLoadAddress.Value;
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

        // Load binary
        _memory = BinaryLoader.Load(
            program,
            out ushort loadAddr,
            out ushort fileLength,
            forceLoadAddress: effectiveLoadAddress,
            fileContainsLoadAddress: !isBinFile  // .prg has load address, .bin doesn't
        );

        // Create CPU
        _cpu = new CPU();
        _cpu.PC = loadAddr;

        await SendOutputAsync($"Loaded {program} at ${loadAddr:X4}, length: {fileLength} bytes\n");
        await SendOutputAsync($"PC set to ${_cpu.PC:X4}\n");
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
            await SendStoppedEventAsync("entry");
        }
    }

    private async Task HandleConfigurationDoneAsync(int seq)
    {
        await _protocol.SendResponseAsync(seq, "configurationDone");
    }

    private async Task HandleSetBreakpointsAsync(int seq, JsonObject? args)
    {
        // This handles source-level breakpoints
        _log.WriteLine("[SetBreakpoints] Called");
        _log.WriteLine($"[SetBreakpoints] args: {args?.ToJsonString()}");
        
        _breakpoints.Clear();
        var breakpoints = new JsonArray();

        var source = args?["source"] as JsonObject;
        var sourcePath = source?["path"]?.ToString();
        var requestedBps = args?["breakpoints"] as JsonArray;
        
        _log.WriteLine($"[SetBreakpoints] sourcePath={sourcePath}");

        if (requestedBps != null)
        {
            foreach (var bp in requestedBps)
            {
                _log.WriteLine($"[SetBreakpoints] Processing breakpoint: {bp?.ToJsonString()}");
                
                var line = bp?["line"]?.GetValue<int>() ?? 0;
                ushort address;
                bool verified = false;

                // Try to resolve line to address using debug symbols
                if (_dbgParser != null && !string.IsNullOrEmpty(sourcePath))
                {
                    var fileName = Path.GetFileName(sourcePath);
                    if (_dbgParser.SourceLineToAddress.TryGetValue(fileName, out var lineMap) &&
                        lineMap.TryGetValue(line, out address))
                    {
                        verified = true;
                        _log.WriteLine($"[SetBreakpoints] Resolved {fileName}:{line} to address ${address:X4}");
                    }
                    else
                    {
                        // Can't resolve - set unverified breakpoint
                        address = 0;
                        _log.WriteLine($"[SetBreakpoints] Could not resolve {fileName}:{line} to address");
                    }
                }
                else
                {
                    // No debug symbols - treat line as address (legacy mode)
                    address = (ushort)line;
                    verified = true;
                    _log.WriteLine($"[SetBreakpoints] No debug symbols, treating line {line} as address ${address:X4}");
                }

                if (verified && address > 0)
                {
                    _breakpoints.Add(address);
                    _log.WriteLine($"[SetBreakpoints] Added breakpoint at ${address:X4}");
                }

                breakpoints.Add(new JsonObject
                {
                    ["verified"] = verified,
                    ["line"] = line,
                    ["source"] = source
                });
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
        _log.WriteLine("[SetInstructionBreakpoints] Called");
        _log.WriteLine($"[SetInstructionBreakpoints] args: {args?.ToJsonString()}");
        
        _breakpoints.Clear();
        var breakpoints = new JsonArray();

        var requestedBps = args?["breakpoints"] as JsonArray;
        
        if (requestedBps != null)
        {
            foreach (var bp in requestedBps)
            {
                _log.WriteLine($"[SetInstructionBreakpoints] Processing breakpoint: {bp?.ToJsonString()}");
                
                var instructionReference = bp?["instructionReference"]?.ToString();
                var offset = bp?["offset"]?.GetValue<int>() ?? 0;  // offset is in bytes
                
                if (instructionReference != null)
                {
                    var baseAddress = ParseAddress(instructionReference);
                    var actualAddress = (ushort)(baseAddress + offset);
                    
                    _breakpoints.Add(actualAddress);
                    _log.WriteLine($"[SetInstructionBreakpoints] Added breakpoint at ${actualAddress:X4}");

                    breakpoints.Add(new JsonObject
                    {
                        ["verified"] = true,
                        ["instructionReference"] = FormatAddress(actualAddress),
                        ["offset"] = 0
                    });
                }
            }
        }

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
        _log.WriteLine("[HandleStackTrace] Called");
        
        if (_cpu == null || _memory == null)
        {
            var body = new JsonObject
            {
                ["stackFrames"] = new JsonArray(),
                ["totalFrames"] = 0
            };
            await _protocol.SendResponseAsync(seq, "stackTrace", body);
            return;
        }

        var pc = _cpu.PC;
        var disasm = OutputGen.GetInstructionDisassembly(_cpu, _memory, pc);
        
        _log.WriteLine($"[HandleStackTrace] PC=${pc:X4}, instructionPointerReference={FormatAddress(pc)}");

        var frame = new JsonObject
        {
            ["id"] = FRAME_ID,
            ["name"] = $"${pc:X4}: {disasm}",
            ["line"] = 0,
            ["column"] = 0,
            ["instructionPointerReference"] = FormatAddress(pc)
        };

        // Add source information if debug symbols are available
        if (_dbgParser != null)
        {
            foreach (var fileEntry in _dbgParser.SourceLineToAddress)
            {
                foreach (var lineEntry in fileEntry.Value)
                {
                    if (lineEntry.Value == pc)
                    {
                        // Found source mapping for this address
                        var sourcePath = Path.Combine(Path.GetDirectoryName(_programPath) ?? "", fileEntry.Key);
                        frame["source"] = new JsonObject
                        {
                            ["name"] = fileEntry.Key,
                            ["path"] = sourcePath
                        };
                        frame["line"] = lineEntry.Key;
                        _log.WriteLine($"[HandleStackTrace] Resolved PC to {fileEntry.Key}:{lineEntry.Key}");
                        break;
                    }
                }
            }
        }

        var stackFrames = new JsonArray { frame };

        var responseBody = new JsonObject
        {
            ["stackFrames"] = stackFrames,
            ["totalFrames"] = 1
        };

        await _protocol.SendResponseAsync(seq, "stackTrace", responseBody);
    }

    private async Task HandleScopesAsync(int seq, JsonObject? args)
    {
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
        var variablesReference = args?["variablesReference"]?.GetValue<int>() ?? 0;
        var variables = new JsonArray();

        if (_cpu != null)
        {
            if (variablesReference == 1) // Registers
            {
                variables.Add(new JsonObject { ["name"] = "PC", ["value"] = $"${_cpu.PC:X4}", ["type"] = "ushort", ["variablesReference"] = 0 });
                variables.Add(new JsonObject { ["name"] = "A", ["value"] = $"${_cpu.A:X2}", ["type"] = "byte", ["variablesReference"] = 0 });
                variables.Add(new JsonObject { ["name"] = "X", ["value"] = $"${_cpu.X:X2}", ["type"] = "byte", ["variablesReference"] = 0 });
                variables.Add(new JsonObject { ["name"] = "Y", ["value"] = $"${_cpu.Y:X2}", ["type"] = "byte", ["variablesReference"] = 0 });
                variables.Add(new JsonObject { ["name"] = "SP", ["value"] = $"${_cpu.SP:X2}", ["type"] = "byte", ["variablesReference"] = 0 });
            }
            else if (variablesReference == 2) // Flags
            {
                var ps = _cpu.ProcessorStatus;
                variables.Add(new JsonObject { ["name"] = "C (Carry)", ["value"] = ps.Carry ? "1" : "0", ["variablesReference"] = 0 });
                variables.Add(new JsonObject { ["name"] = "Z (Zero)", ["value"] = ps.Zero ? "1" : "0", ["variablesReference"] = 0 });
                variables.Add(new JsonObject { ["name"] = "I (IRQ Disable)", ["value"] = ps.InterruptDisable ? "1" : "0", ["variablesReference"] = 0 });
                variables.Add(new JsonObject { ["name"] = "D (Decimal)", ["value"] = ps.Decimal ? "1" : "0", ["variablesReference"] = 0 });
                variables.Add(new JsonObject { ["name"] = "V (Overflow)", ["value"] = ps.Overflow ? "1" : "0", ["variablesReference"] = 0 });
                variables.Add(new JsonObject { ["name"] = "N (Negative)", ["value"] = ps.Negative ? "1" : "0", ["variablesReference"] = 0 });
            }
        }

        var body = new JsonObject
        {
            ["variables"] = variables
        };

        await _protocol.SendResponseAsync(seq, "variables", body);
    }

    private async Task HandleEvaluateAsync(int seq, JsonObject? args)
    {
        var expression = args?["expression"]?.ToString() ?? "";
        var context = args?["context"]?.ToString();

        try
        {
            if (_cpu == null || _memory == null)
            {
                await _protocol.SendResponseAsync(seq, "evaluate");
                return;
            }

            // Parse different expression formats
            string result;
            string? memoryReference = null;

            // Check for memory address expressions: $0600, 0x0600, or decimal
            if (expression.StartsWith("$") || expression.StartsWith("0x", StringComparison.OrdinalIgnoreCase) || int.TryParse(expression, out _))
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

                var value = _memory[address];
                result = $"${value:X2} ({value})";
                memoryReference = $"0x{address:X4}";
            }
            // Check for register expressions
            else if (expression.Equals("PC", StringComparison.OrdinalIgnoreCase))
            {
                result = $"${_cpu.PC:X4}";
                memoryReference = $"0x{_cpu.PC:X4}";
            }
            else if (expression.Equals("A", StringComparison.OrdinalIgnoreCase))
            {
                result = $"${_cpu.A:X2}";
            }
            else if (expression.Equals("X", StringComparison.OrdinalIgnoreCase))
            {
                result = $"${_cpu.X:X2}";
            }
            else if (expression.Equals("Y", StringComparison.OrdinalIgnoreCase))
            {
                result = $"${_cpu.Y:X2}";
            }
            else if (expression.Equals("SP", StringComparison.OrdinalIgnoreCase))
            {
                result = $"${_cpu.SP:X2}";
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

            // Add memory reference if we have an address
            if (memoryReference != null)
            {
                body["memoryReference"] = memoryReference;
            }

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
        try
        {
            var memoryReference = args?["memoryReference"]?.ToString();
            var count = args?["count"]?.GetValue<int>() ?? 256;
            var offset = args?["offset"]?.GetValue<int>() ?? 0;

            if (string.IsNullOrEmpty(memoryReference))
            {
                await _protocol.SendResponseAsync(seq, "readMemory");
                return;
            }

            // Parse memory reference - could be hex (0x0600, $0600) or decimal
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
            if (_memory == null)
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
                data[i] = _memory[(ushort)((address + i) & 0xFFFF)];
            }

            // Encode as base64
            var base64Data = Convert.ToBase64String(data);

            var body = new JsonObject
            {
                ["address"] = $"0x{address:X4}",
                ["data"] = base64Data,
                ["unreadableBytes"] = 0
            };

            await _protocol.SendResponseAsync(seq, "readMemory", body);
        }
        catch (Exception ex)
        {
            _log.WriteLine($"[ReadMemory] Error: {ex.Message}");
            await _protocol.SendResponseAsync(seq, "readMemory");
        }
    }

    private async Task HandleContinueAsync(int seq, JsonObject? args)
    {
        if (_cpu != null && _memory != null)
        {
            _continueTokenSource?.Cancel();
            _continueTokenSource = new CancellationTokenSource();
            var token = _continueTokenSource.Token;

            _ = Task.Run(async () =>
            {
                try
                {
                    _log.WriteLine("[Continue] Starting execution loop...");
                    while (!token.IsCancellationRequested)
                    {
                        if (_breakpoints.Contains(_cpu.PC))
                        {
                            _log.WriteLine($"[Continue] Hit breakpoint at ${_cpu.PC:X4}");
                            await SendStoppedEventAsync("breakpoint");
                            return;
                        }

                        if (_stopOnBRK && _memory[_cpu.PC] == 0x00)
                        {
                            _log.WriteLine($"[Continue] Hit BRK at ${_cpu.PC:X4}");
                            await SendStoppedEventAsync("pause", "BRK instruction");
                            return;
                        }

                        _cpu.ExecuteOneInstruction(_memory);
                    }

                    _log.WriteLine("[Continue] Execution paused by user");
                    await SendStoppedEventAsync("pause", "Paused by user");
                }
                catch (Exception ex)
                {
                    _log.WriteLine($"[Continue] Exception: {ex}");
                    await SendStoppedEventAsync("exception", ex.Message);
                }
            }, token);
        }

        var body = new JsonObject
        {
            ["allThreadsContinued"] = true
        };

        await _protocol.SendResponseAsync(seq, "continue", body);
    }

    private async Task HandleNextAsync(int seq, JsonObject? args)
    {
        _log.WriteLine("[HandleNext] Starting...");
        try
        {
            if (_cpu != null && _memory != null)
            {
                _log.WriteLine($"[HandleNext] Executing instruction at ${_cpu.PC:X4}");
                _cpu.ExecuteOneInstruction(_memory);
                _log.WriteLine($"[HandleNext] New PC: ${_cpu.PC:X4}");
                
                await SendStoppedEventAsync("step");
            }

            await _protocol.SendResponseAsync(seq, "next");
        }
        catch (Exception ex)
        {
            _log.WriteLine($"[HandleNext] EXCEPTION: {ex}");
            _log.Flush();
            throw;
        }
    }

    private async Task HandleStepInAsync(int seq, JsonObject? args)
    {
        if (_cpu != null && _memory != null)
        {
            _cpu.ExecuteOneInstruction(_memory);
            await SendStoppedEventAsync("step");
        }

        await _protocol.SendResponseAsync(seq, "stepIn");
    }

    private async Task HandleStepOutAsync(int seq, JsonObject? args)
    {
        if (_cpu != null && _memory != null)
        {
            _cpu.ExecuteOneInstruction(_memory);
            await SendStoppedEventAsync("step");
        }

        await _protocol.SendResponseAsync(seq, "stepOut");
    }

    private async Task HandlePauseAsync(int seq, JsonObject? args)
    {
        _log.WriteLine("[HandlePause] Pause requested");
        
        if (_continueTokenSource != null && !_continueTokenSource.IsCancellationRequested)
        {
            _log.WriteLine("[HandlePause] Cancelling continue operation");
            _continueTokenSource.Cancel();
        }
        else
        {
            _log.WriteLine("[HandlePause] Not currently running, sending stopped event");
            await SendStoppedEventAsync("pause");
        }

        await _protocol.SendResponseAsync(seq, "pause");
    }

    private async Task HandleTerminateAsync(int seq, JsonObject? args)
    {
        await _protocol.SendResponseAsync(seq, "terminate");
        await Task.Delay(100);
        OnExit?.Invoke();
    }

    private async Task HandleDisconnectAsync(int seq, JsonObject? args)
    {
        await _protocol.SendResponseAsync(seq, "disconnect");
        await Task.Delay(100);
        OnExit?.Invoke();
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

    private async Task SendStoppedEventAsync(string reason, string? text = null)
    {
        var body = new JsonObject
        {
            ["reason"] = reason,
            ["threadId"] = THREAD_ID
        };

        if (text != null)
        {
            body["text"] = text;
        }

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
        _log.WriteLine("[HandleDisassemble] Called");
        
        var memoryReference = args?["memoryReference"]?.ToString();
        var offset = args?["offset"]?.GetValue<int>() ?? 0;
        var instructionOffset = args?["instructionOffset"]?.GetValue<int>() ?? 0;
        var requestedCount = args?["instructionCount"]?.GetValue<int>() ?? 100;
        
        _log.WriteLine($"[HandleDisassemble] memoryReference={memoryReference}, offset={offset}, instructionOffset={instructionOffset}, requestedCount={requestedCount}");
        
        var instructions = new JsonArray();
        
        if (_cpu != null && _memory != null && memoryReference != null)
        {
            try
            {
                // Parse base address and apply byte offset
                var baseAddress = ParseAddress(memoryReference);
                int targetAddr = baseAddress + offset;
                
                // Clamp to valid address range
                if (targetAddr < 0) targetAddr = 0;
                if (targetAddr > 0xFFFF) targetAddr = 0xFFFF;
                
                _log.WriteLine($"[HandleDisassemble] baseAddress=${baseAddress:X4}, targetAddr=${targetAddr:X4}");
                
                // Build complete instruction list from 0x0000 - this ensures consistent boundaries
                var instrList = new List<(ushort addr, int len)>();
                int addr = 0;
                while (addr <= 0xFFFF)
                {
                    var opcode = _memory[(ushort)addr];
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
                
                _log.WriteLine($"[HandleDisassemble] targetIndex={targetIndex}, rawStartIndex={rawStartIndex}, totalInstr={instrList.Count}");
                
                // Determine how many instructions to return
                // If the request would start near 0, return a large range to fill VS Code's cache gaps
                int adjustedRequestedCount = requestedCount;
                if (rawStartIndex < 1000)
                {
                    // Return at least 2000 instructions to fill any gaps from previous requests
                    adjustedRequestedCount = Math.Max(requestedCount, Math.Max(2000, targetIndex + 500));
                    _log.WriteLine($"[HandleDisassemble] Adjusted count to {adjustedRequestedCount} to fill cache gaps");
                }
                
                // If startIndex is negative, we need to pad with synthetic instructions
                // so VS Code gets exactly the number of "before" instructions it expects.
                // This is necessary for correct highlighting of the target instruction.
                int paddingNeededBefore = 0;
                if (rawStartIndex < 0)
                {
                    paddingNeededBefore = -rawStartIndex;
                    _log.WriteLine($"[HandleDisassemble] Need {paddingNeededBefore} padding for negative offset");
                    
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
                
                _log.WriteLine($"[HandleDisassemble] startIndex={startIndex}, paddingNeededBefore={paddingNeededBefore}");
                
                // Generate real instructions - use adjustedRequestedCount to fill gaps
                for (int i = startIndex; i < instrList.Count && instructions.Count < adjustedRequestedCount; i++)
                {
                    var (instrAddr, instrLen) = instrList[i];
                    
                    // Get instruction bytes
                    var bytes = new List<byte>();
                    for (int j = 0; j < instrLen && (instrAddr + j) <= 0xFFFF; j++)
                    {
                        bytes.Add(_memory[(ushort)(instrAddr + j)]);
                    }
                    
                    // Get disassembly text
                    var disasm = OutputGen.GetInstructionDisassembly(_cpu, _memory, instrAddr);
                    var instructionText = StripAddressPrefix(disasm);
                    
                    instructions.Add(new JsonObject
                    {
                        ["address"] = FormatAddress(instrAddr),
                        ["instructionBytes"] = BitConverter.ToString(bytes.ToArray()).Replace("-", " "),
                        ["instruction"] = instructionText
                    });
                }
                
                _log.WriteLine($"[HandleDisassemble] Generated {instructions.Count} instructions");
                if (instructions.Count > 0)
                {
                    _log.WriteLine($"[HandleDisassemble] First: {instructions[0]?["address"]}, Last: {instructions[instructions.Count-1]?["address"]}");
                }
            }
            catch (Exception ex)
            {
                _log.WriteLine($"[HandleDisassemble] Error: {ex.Message}");
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
        if (_cpu != null && _cpu.InstructionList.OpCodeDictionary.ContainsKey(opCode))
        {
            return _cpu.InstructionList.GetOpCode(opCode).Size;
        }
        return 1; // Unknown opcodes are treated as 1 byte
    }
    
    /// <summary>
    /// Strip the address prefix from disassembly output.
    /// OutputGen returns something like "0600  LDA #$00" and we want just "LDA #$00"
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
    /// Handles both "0x1234" and "$1234" formats
    /// </summary>
    private static ushort ParseAddress(string addressStr)
    {
        if (addressStr.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            addressStr = addressStr.Substring(2);
        else if (addressStr.StartsWith("$"))
            addressStr = addressStr.Substring(1);
            
        return Convert.ToUInt16(addressStr, 16);
    }
}
