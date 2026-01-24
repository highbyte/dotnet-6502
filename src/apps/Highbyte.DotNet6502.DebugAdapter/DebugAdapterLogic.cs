using System.Text.Json.Nodes;
using Highbyte.DotNet6502;
using Highbyte.DotNet6502.Monitor;
using Highbyte.DotNet6502.Utils;

namespace Highbyte.DotNet6502.DebugAdapter;

public class DebugAdapterLogic
{
    private readonly DapProtocol _protocol;
    private readonly StreamWriter _log;
    private CPU? _cpu;
    private Memory? _memory;
    private readonly Dictionary<int, ushort> _breakpoints = new();
    private int _nextBreakpointId = 1;
    private const int THREAD_ID = 1;
    private const int FRAME_ID = 0;
    private CancellationTokenSource? _continueTokenSource;
    private bool _stopOnBRK = true;
    
    // Cached instruction address list - built once from 0x0000 forward
    private List<ushort>? _instructionAddresses;
    private Dictionary<ushort, int>? _addressToIndex;

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
            ["supportsDisassembleRequest"] = true
        };

        await _protocol.SendResponseAsync(seq, "initialize", body);
    }

    private async Task HandleLaunchAsync(int seq, JsonObject? args)
    {
        var program = args?["program"]?.ToString();
        var stopOnEntry = args?["stopOnEntry"]?.GetValue<bool>() ?? true;
        var loadAddress = args?["loadAddress"]?.GetValue<int?>();
        _stopOnBRK = args?["stopOnBRK"]?.GetValue<bool>() ?? true;

        _log.WriteLine($"[Launch] program={program}, stopOnEntry={stopOnEntry}, stopOnBRK={_stopOnBRK}");

        if (string.IsNullOrEmpty(program) || !File.Exists(program))
        {
            await SendOutputAsync($"Error: Program file not found: {program}\n");
            await _protocol.SendResponseAsync(seq, "launch");
            return;
        }

        // Load binary
        _memory = BinaryLoader.Load(
            program,
            out ushort loadAddr,
            out ushort fileLength,
            forceLoadAddress: loadAddress.HasValue ? (ushort)loadAddress.Value : null
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
        _breakpoints.Clear();
        var breakpoints = new JsonArray();

        var requestedBps = args?["breakpoints"] as JsonArray;
        if (requestedBps != null)
        {
            foreach (var bp in requestedBps)
            {
                var line = bp?["line"]?.GetValue<int>() ?? 0;
                var address = (ushort)line;
                var id = _nextBreakpointId++;
                _breakpoints[id] = address;

                breakpoints.Add(new JsonObject
                {
                    ["id"] = id,
                    ["verified"] = true,
                    ["line"] = line
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
        _log.WriteLine($"[SetInstructionBreakpoints] Full args: {args?.ToJsonString()}");
        
        // Clear existing breakpoints when setting instruction breakpoints
        _breakpoints.Clear();
        var breakpoints = new JsonArray();

        var requestedBps = args?["breakpoints"] as JsonArray;
        _log.WriteLine($"[SetInstructionBreakpoints] Requested breakpoints count: {requestedBps?.Count ?? 0}");
        
        if (requestedBps != null && _memory != null)
        {
            foreach (var bp in requestedBps)
            {
                _log.WriteLine($"[SetInstructionBreakpoints] Processing breakpoint: {bp?.ToJsonString()}");
                var instructionReference = bp?["instructionReference"]?.ToString();
                var offset = bp?["offset"]?.GetValue<int>() ?? 0;
                
                if (instructionReference != null)
                {
                    try
                    {
                        // Parse hex address (strip 0x or $ prefix if present)
                        var addressStr = instructionReference;
                        if (addressStr.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                            addressStr = addressStr.Substring(2);
                        else if (addressStr.StartsWith("$"))
                            addressStr = addressStr.Substring(1);
                        var baseAddress = Convert.ToUInt16(addressStr, 16);
                        
                        // offset is in bytes
                        var actualAddress = (ushort)(baseAddress + offset);
                        
                        var id = _nextBreakpointId++;
                        _breakpoints[id] = actualAddress;

                        _log.WriteLine($"[SetInstructionBreakpoints] Added breakpoint at ${actualAddress:X4} (base=${baseAddress:X4}, offset={offset})");

                        breakpoints.Add(new JsonObject
                        {
                            ["id"] = id,
                            ["verified"] = true,
                            ["instructionReference"] = $"0x{actualAddress:X4}",  // Must use 0x prefix for BigInt parsing
                            ["offset"] = 0
                        });
                    }
                    catch (Exception ex)
                    {
                        _log.WriteLine($"[SetInstructionBreakpoints] Error parsing address '{instructionReference}': {ex.Message}");
                        breakpoints.Add(new JsonObject
                        {
                            ["verified"] = false,
                            ["message"] = $"Invalid hex address: {instructionReference}"
                        });
                    }
                }
            }
        }

        _log.WriteLine($"[SetInstructionBreakpoints] Total breakpoints set: {_breakpoints.Count}");
        foreach (var bp in _breakpoints)
        {
            _log.WriteLine($"[SetInstructionBreakpoints]   ID {bp.Key} -> ${bp.Value:X4}");
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
            _log.WriteLine("[HandleStackTrace] CPU or Memory is null, returning empty stack");
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
        
        _log.WriteLine($"[HandleStackTrace] PC=${pc:X4}, instructionPointerReference=0x{pc:X4}");

        var frame = new JsonObject
        {
            ["id"] = FRAME_ID,
            ["name"] = $"${pc:X4}: {disasm}",
            ["line"] = 0,
            ["column"] = 0,
            ["instructionPointerReference"] = $"0x{pc:X4}"  // Must use 0x prefix for BigInt parsing
        };

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

    private async Task HandleContinueAsync(int seq, JsonObject? args)
    {
        if (_cpu != null && _memory != null)
        {
            // Cancel any existing continue operation
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
                        // Check for breakpoint BEFORE executing
                        if (_breakpoints.Values.Contains(_cpu.PC))
                        {
                            _log.WriteLine($"[Continue] Hit breakpoint at ${_cpu.PC:X4}");
                            await SendStoppedEventAsync("breakpoint");
                            return;
                        }

                        // Check for BRK BEFORE executing (only if stopOnBRK is enabled)
                        if (_stopOnBRK && _memory[_cpu.PC] == 0x00)
                        {
                            _log.WriteLine($"[Continue] Hit BRK at ${_cpu.PC:X4}");
                            // Invalidate instruction cache since we're stopping
                            InvalidateInstructionCache();
                            // Tell VS Code to re-fetch disassembly
                            await SendStoppedEventAsync("pause", "BRK instruction");
                            return;
                        }

                        _cpu.ExecuteOneInstruction(_memory);
                    }

                    // Invalidate instruction cache since memory may have changed during execution
                    InvalidateInstructionCache();
                    // Tell VS Code to re-fetch disassembly
                    
                    // If we got here, we were cancelled by Pause
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
                
                // Invalidate instruction cache since memory may have changed
                InvalidateInstructionCache();
                
                // Tell VS Code to re-fetch disassembly (it caches the old data)
                
                await SendStoppedEventAsync("step");
            }
            else
            {
                _log.WriteLine("[HandleNext] CPU or Memory is null!");
            }

            await _protocol.SendResponseAsync(seq, "next");
            _log.WriteLine("[HandleNext] Response sent");
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
            
            // Invalidate instruction cache since memory may have changed
            InvalidateInstructionCache();
            
            // Tell VS Code to re-fetch disassembly (it caches the old data)
            
            await SendStoppedEventAsync("step");
        }

        await _protocol.SendResponseAsync(seq, "stepIn");
    }

    private async Task HandleStepOutAsync(int seq, JsonObject? args)
    {
        if (_cpu != null && _memory != null)
        {
            _cpu.ExecuteOneInstruction(_memory);
            
            // Invalidate instruction cache since memory may have changed
            InvalidateInstructionCache();
            
            // Tell VS Code to re-fetch disassembly (it caches the old data)
            
            await SendStoppedEventAsync("step");
        }

        await _protocol.SendResponseAsync(seq, "stepOut");
    }

    private async Task HandlePauseAsync(int seq, JsonObject? args)
    {
        _log.WriteLine("[HandlePause] Pause requested");
        
        // Cancel the continue operation if it's running
        if (_continueTokenSource != null && !_continueTokenSource.IsCancellationRequested)
        {
            _log.WriteLine("[HandlePause] Cancelling continue operation");
            _continueTokenSource.Cancel();
        }
        else
        {
            // If not running, just send a stopped event
            _log.WriteLine("[HandlePause] Not currently running, sending stopped event");
            await SendStoppedEventAsync("pause");
        }

        await _protocol.SendResponseAsync(seq, "pause");
    }

    private async Task HandleTerminateAsync(int seq, JsonObject? args)
    {
        _log.WriteLine("[HandleTerminate] Sending response...");
        await _protocol.SendResponseAsync(seq, "terminate");
        _log.WriteLine("[HandleTerminate] Response sent, waiting for flush...");
        
        // Give a moment for the response to be sent before exiting
        await Task.Delay(100);
        
        _log.WriteLine("[HandleTerminate] Triggering exit");
        OnExit?.Invoke();
    }

    private async Task HandleDisconnectAsync(int seq, JsonObject? args)
    {
        _log.WriteLine("[HandleDisconnect] Sending response...");
        await _protocol.SendResponseAsync(seq, "disconnect");
        _log.WriteLine("[HandleDisconnect] Response sent, waiting for flush...");
        
        // Give a moment for the response to be sent before exiting
        await Task.Delay(100);
        
        _log.WriteLine("[HandleDisconnect] Triggering exit");
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
    /// Build/rebuild the instruction address cache by disassembling from 0x0000 forward.
    /// This ensures consistent instruction boundaries across all requests.
    /// </summary>
    private void EnsureInstructionCache()
    {
        if (_instructionAddresses != null && _addressToIndex != null)
            return; // Already built
            
        if (_memory == null || _cpu == null)
            return;
            
        _instructionAddresses = new List<ushort>();
        _addressToIndex = new Dictionary<ushort, int>();
        
        ushort addr = 0;
        int index = 0;
        
        while (addr <= 0xFFFF)
        {
            _instructionAddresses.Add(addr);
            _addressToIndex[addr] = index;
            
            var len = GetInstructionLength(_memory[addr]);
            int nextAddr = addr + len;
            
            if (nextAddr > 0xFFFF)
                break;
                
            addr = (ushort)nextAddr;
            index++;
        }
        
        _log.WriteLine($"[EnsureInstructionCache] Built cache with {_instructionAddresses.Count} instructions");
    }
    
    /// <summary>
    /// Invalidate the instruction cache (call when memory changes significantly)
    /// </summary>
    private void InvalidateInstructionCache()
    {
        _instructionAddresses = null;
        _addressToIndex = null;
    }

    private async Task HandleDisassembleAsync(int seq, JsonObject? args)
    {
        _log.WriteLine("[HandleDisassemble] Called");
        
        var memoryReference = args?["memoryReference"]?.ToString();
        var offset = args?["offset"]?.GetValue<int>() ?? 0;
        var instructionOffset = args?["instructionOffset"]?.GetValue<int>() ?? 0;
        var instructionCount = args?["instructionCount"]?.GetValue<int>() ?? 100;
        
        _log.WriteLine($"[HandleDisassemble] memoryReference={memoryReference}, offset={offset}, instructionOffset={instructionOffset}, instructionCount={instructionCount}");
        
        var instructions = new JsonArray();
        
        if (_cpu != null && _memory != null && memoryReference != null)
        {
            try
            {
                // Ensure we have a consistent instruction cache built from 0x0000
                EnsureInstructionCache();
                
                if (_instructionAddresses == null || _addressToIndex == null)
                {
                    _log.WriteLine("[HandleDisassemble] Failed to build instruction cache");
                    await _protocol.SendResponseAsync(seq, "disassemble", new JsonObject { ["instructions"] = instructions });
                    return;
                }
                
                // Parse the memory reference as hex address
                var startAddressStr = memoryReference;
                if (startAddressStr.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                    startAddressStr = startAddressStr.Substring(2);
                else if (startAddressStr.StartsWith("$"))
                    startAddressStr = startAddressStr.Substring(1);
                var baseAddress = Convert.ToUInt16(startAddressStr, 16);
                
                // Apply byte offset to get reference address
                int refAddr = baseAddress + offset;
                if (refAddr < 0) refAddr = 0;
                if (refAddr > 0xFFFF) refAddr = 0xFFFF;
                ushort referenceAddress = (ushort)refAddr;
                
                // Find the index of the reference address in our cache
                // If exact match not found, find the instruction that contains this address
                int refIndex;
                if (_addressToIndex.TryGetValue(referenceAddress, out refIndex))
                {
                    // Exact match
                }
                else
                {
                    // Find the instruction that starts at or before this address
                    refIndex = 0;
                    for (int i = 0; i < _instructionAddresses.Count; i++)
                    {
                        if (_instructionAddresses[i] <= referenceAddress)
                            refIndex = i;
                        else
                            break;
                    }
                }
                
                _log.WriteLine($"[HandleDisassemble] Reference ${referenceAddress:X4} -> index {refIndex} (address ${_instructionAddresses[refIndex]:X4})");
                
                // Calculate the requested start index (may be negative)
                int requestedStartIndex = refIndex + instructionOffset;
                
                // How many "padding" instructions do we need before the real data?
                int paddingCount = 0;
                if (requestedStartIndex < 0)
                {
                    paddingCount = -requestedStartIndex;
                    requestedStartIndex = 0;
                }
                
                // Clamp to valid range
                int startIndex = requestedStartIndex;
                if (startIndex >= _instructionAddresses.Count) startIndex = _instructionAddresses.Count - 1;
                
                // Calculate how many real instructions we can provide
                int realInstructionsNeeded = instructionCount - paddingCount;
                int endIndex = startIndex + realInstructionsNeeded;
                if (endIndex > _instructionAddresses.Count) endIndex = _instructionAddresses.Count;
                
                _log.WriteLine($"[HandleDisassemble] paddingCount={paddingCount}, startIndex={startIndex}, endIndex={endIndex}");
                
                // First, add padding instructions with "before memory" addresses
                // Use negative addresses formatted to look like they're before 0x0000
                for (int i = 0; i < paddingCount; i++)
                {
                    // Create synthetic addresses counting down from where we'd start
                    // Use 0xFFFF, 0xFFFE, etc. but mark as invalid
                    // Actually, better to use addresses that won't confuse VS Code
                    // We'll use the same address format but mark instruction as invalid
                    int syntheticIndex = paddingCount - 1 - i;
                    ushort syntheticAddress = (ushort)(0x10000 - paddingCount + i); // wraps around to 0xFFxx
                    
                    instructions.Add(new JsonObject
                    {
                        ["address"] = $"0x{syntheticAddress:X4}",
                        ["instructionBytes"] = "??",
                        ["instruction"] = "(before memory)",
                        ["presentationHint"] = "invalid"
                    });
                }
                
                // Generate instruction objects for the requested range
                for (int idx = startIndex; idx < endIndex; idx++)
                {
                    ushort currentAddress = _instructionAddresses[idx];
                    
                    var opCodeByte = _memory[currentAddress];
                    var instructionLength = GetInstructionLength(opCodeByte);
                    
                    // Build instruction bytes
                    var bytes = new List<byte>();
                    for (int j = 0; j < instructionLength && currentAddress + j <= 0xFFFF; j++)
                    {
                        bytes.Add(_memory[(ushort)(currentAddress + j)]);
                    }
                    
                    // Get disassembly
                    var tempPC = _cpu.PC;
                    _cpu.PC = currentAddress;
                    var disasm = OutputGen.GetInstructionDisassembly(_cpu, _memory, currentAddress);
                    _cpu.PC = tempPC;
                    
                    // Strip address prefix from disassembly
                    var instructionOnly = disasm;
                    if (disasm.Length >= 6 && disasm.Substring(4, 2) == "  ")
                    {
                        instructionOnly = disasm.Substring(6);
                    }
                    
                    instructions.Add(new JsonObject
                    {
                        ["address"] = $"0x{currentAddress:X4}",
                        ["instructionBytes"] = bytes.Count > 0 ? BitConverter.ToString(bytes.ToArray()).Replace("-", " ") : "00",
                        ["instruction"] = instructionOnly,
                        ["location"] = new JsonObject
                        {
                            ["path"] = "",
                            ["name"] = $"0x{currentAddress:X4}"
                        }
                    });
                }
                
                // Pad at the end if we still need more instructions
                while (instructions.Count < instructionCount)
                {
                    // Get the last address and add 1 (or use a placeholder beyond memory)
                    ushort lastAddr = instructions.Count > 0 && _instructionAddresses.Count > 0 
                        ? (ushort)(_instructionAddresses[_instructionAddresses.Count - 1] + 1)
                        : (ushort)0xFFFF;
                    ushort syntheticAddress = (ushort)(lastAddr + (instructions.Count - paddingCount - (endIndex - startIndex)));
                    
                    instructions.Add(new JsonObject
                    {
                        ["address"] = $"0x{syntheticAddress:X4}",
                        ["instructionBytes"] = "??",
                        ["instruction"] = "(beyond memory)",
                        ["presentationHint"] = "invalid"
                    });
                }
                
                _log.WriteLine($"[HandleDisassemble] Generated {instructions.Count} instructions (padded to match instructionCount={instructionCount})");
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
    
    private int GetInstructionLength(byte opCode)
    {
        // Use the CPU's instruction list to get accurate size
        if (_cpu != null && _cpu.InstructionList.OpCodeDictionary.ContainsKey(opCode))
        {
            return _cpu.InstructionList.GetOpCode(opCode).Size;
        }
        // Default to 1 byte for unknown opcodes
        return 1;
    }
}
