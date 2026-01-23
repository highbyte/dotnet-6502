using Highbyte.DotNet6502.DebugAdapter.Protocol;
using StreamJsonRpc;
using Highbyte.DotNet6502;
using Highbyte.DotNet6502.Monitor;
using Highbyte.DotNet6502.Utils;
using StackFrame = Highbyte.DotNet6502.DebugAdapter.Protocol.StackFrame;

namespace Highbyte.DotNet6502.DebugAdapter;

public class DebugAdapter6502
{
    private JsonRpc? _rpc;
    private CPU? _cpu;
    private Memory? _memory;
    private readonly Dictionary<int, ushort> _breakpoints = new();
    private int _nextBreakpointId = 1;
    private bool _isRunning = false;
    private bool _stopRequested = false;
    private bool _stepRequested = false;
    private const int THREAD_ID = 1;

    public void SetRpc(JsonRpc rpc)
    {
        _rpc = rpc;
    }

    // DAP Request Handlers

    [JsonRpcMethod("initialize")]
    public InitializeResponse Initialize(InitializeRequest args)
    {
        return new InitializeResponse(
            supportsConfigurationDoneRequest: true,
            supportsFunctionBreakpoints: false,
            supportsConditionalBreakpoints: false,
            supportsHitConditionalBreakpoints: false,
            supportsEvaluateForHovers: false,
            supportsStepBack: false,
            supportsSetVariable: false,
            supportsRestartFrame: false,
            supportsGotoTargetsRequest: false,
            supportsStepInTargetsRequest: false,
            supportsCompletionsRequest: false,
            supportsModulesRequest: false,
            supportsRestartRequest: false,
            supportsExceptionOptions: false,
            supportsValueFormattingOptions: false,
            supportsExceptionInfoRequest: false,
            supportTerminateDebuggee: true,
            supportSuspendDebuggee: false,
            supportsDelayedStackTraceLoading: false,
            supportsLoadedSourcesRequest: false,
            supportsLogPoints: false,
            supportsTerminateThreadsRequest: false,
            supportsSetExpression: false,
            supportsTerminateRequest: true,
            supportsDataBreakpoints: false,
            supportsReadMemoryRequest: false,
            supportsWriteMemoryRequest: false,
            supportsDisassembleRequest: false
        );
    }

    [JsonRpcMethod("launch")]
    public async Task LaunchAsync(LaunchRequestArguments args)
    {
        try
        {
            // Load the program
            var programPath = args.program;
            if (!File.Exists(programPath))
            {
                SendOutput("console", $"Error: Program file not found: {programPath}");
                return;
            }

            // Load binary with BinaryLoader
            _memory = BinaryLoader.Load(
                programPath,
                out ushort loadAddress,
                out ushort fileLength,
                forceLoadAddress: args.loadAddress.HasValue ? (ushort)args.loadAddress.Value : null
            );

            // Create CPU
            _cpu = new CPU();
            _cpu.PC = loadAddress;

            SendOutput("console", $"Loaded {programPath} at ${loadAddress:X4}, length: {fileLength} bytes");
            SendOutput("console", $"PC set to ${_cpu.PC:X4}");

            // Send initialized event
            await SendEventAsync("initialized", new InitializedEvent());

            // If stopOnEntry is true, send stopped event immediately
            if (args.stopOnEntry == true)
            {
                await SendEventAsync("stopped", new StoppedEvent("entry", THREAD_ID));
            }
        }
        catch (Exception ex)
        {
            SendOutput("console", $"Error launching: {ex.Message}");
        }
    }

    [JsonRpcMethod("setBreakpoints")]
    public SetBreakpointsResponse SetBreakpoints(SetBreakpointsArguments args)
    {
        var results = new List<BreakpointResult>();

        // Clear existing breakpoints
        _breakpoints.Clear();

        if (args.breakpoints != null)
        {
            foreach (var bp in args.breakpoints)
            {
                if (bp.line.HasValue)
                {
                    // In MVP, we treat line number as hex address
                    // e.g., line 2048 = address 0x0800
                    var address = (ushort)bp.line.Value;
                    var id = _nextBreakpointId++;
                    _breakpoints[id] = address;

                    results.Add(new BreakpointResult(
                        id: id,
                        verified: true,
                        line: bp.line.Value
                    ));

                    SendOutput("console", $"Breakpoint set at ${address:X4}");
                }
            }
        }

        return new SetBreakpointsResponse(results.ToArray());
    }

    [JsonRpcMethod("configurationDone")]
    public void ConfigurationDone()
    {
        // Nothing to do
    }

    [JsonRpcMethod("threads")]
    public ThreadsResponse Threads()
    {
        return new ThreadsResponse(new[]
        {
            new Protocol.Thread(THREAD_ID, "6502 CPU")
        });
    }

    [JsonRpcMethod("stackTrace")]
    public StackTraceResponse StackTrace(StackTraceArguments args)
    {
        if (_cpu == null || _memory == null)
        {
            return new StackTraceResponse(Array.Empty<StackFrame>(), 0);
        }

        var frames = new List<StackFrame>();

        // Create disassembly for current instruction
        var disassembly = OutputGen.GetNextInstructionDisassembly(_cpu, _memory);
        
        // Use a consistent frame ID - frame 0 is typically the top frame
        frames.Add(new StackFrame(
            id: 0,
            name: $"${_cpu.PC:X4}: {disassembly}",
            source: null,
            line: _cpu.PC, // Use PC as line number
            column: 0,
            presentationHint: "normal"
        ));

        return new StackTraceResponse(frames.ToArray(), frames.Count);
    }

    [JsonRpcMethod("scopes")]
    public ScopesResponse Scopes(ScopesArguments args)
    {
        // Frame ID should be 0 (matching stackTrace)
        if (args.frameId != 0)
        {
            return new ScopesResponse(Array.Empty<Scope>());
        }

        return new ScopesResponse(new[]
        {
            new Scope("Registers", variablesReference: 1, presentationHint: "registers"),
            new Scope("Flags", variablesReference: 2, presentationHint: "registers")
        });
    }

    [JsonRpcMethod("variables")]
    public VariablesResponse Variables(VariablesArguments args)
    {
        if (_cpu == null)
        {
            return new VariablesResponse(Array.Empty<Variable>());
        }

        var variables = new List<Variable>();

        if (args.variablesReference == 1) // Registers
        {
            variables.Add(new Variable("PC", $"${_cpu.PC:X4}", "ushort"));
            variables.Add(new Variable("SP", $"${_cpu.SP:X2}", "byte"));
            variables.Add(new Variable("A", $"${_cpu.A:X2}", "byte"));
            variables.Add(new Variable("X", $"${_cpu.X:X2}", "byte"));
            variables.Add(new Variable("Y", $"${_cpu.Y:X2}", "byte"));
        }
        else if (args.variablesReference == 2) // Flags
        {
            variables.Add(new Variable("N", _cpu.ProcessorStatus.Negative ? "1" : "0", "bool"));
            variables.Add(new Variable("V", _cpu.ProcessorStatus.Overflow ? "1" : "0", "bool"));
            variables.Add(new Variable("B", _cpu.ProcessorStatus.Break ? "1" : "0", "bool"));
            variables.Add(new Variable("D", _cpu.ProcessorStatus.Decimal ? "1" : "0", "bool"));
            variables.Add(new Variable("I", _cpu.ProcessorStatus.InterruptDisable ? "1" : "0", "bool"));
            variables.Add(new Variable("Z", _cpu.ProcessorStatus.Zero ? "1" : "0", "bool"));
            variables.Add(new Variable("C", _cpu.ProcessorStatus.Carry ? "1" : "0", "bool"));
        }

        return new VariablesResponse(variables.ToArray());
    }

    [JsonRpcMethod("continue")]
    public async Task<ContinueResponse> ContinueAsync(ContinueArguments args)
    {
        if (_cpu == null || _memory == null)
        {
            return new ContinueResponse();
        }

        _isRunning = true;
        _stopRequested = false;

        // Run in background
        _ = Task.Run(async () =>
        {
            try
            {
                while (_isRunning && !_stopRequested)
                {
                    // Check breakpoints
                    if (_breakpoints.Values.Contains(_cpu.PC))
                    {
                        _isRunning = false;
                        await SendEventAsync("stopped", new StoppedEvent("breakpoint", THREAD_ID));
                        break;
                    }

                    // Execute one instruction
                    _cpu.ExecuteOneInstruction(_memory);

                    // Check for BRK
                    if (_cpu.ProcessorStatus.Break)
                    {
                        _isRunning = false;
                        await SendEventAsync("stopped", new StoppedEvent("breakpoint", THREAD_ID, "BRK instruction"));
                        break;
                    }
                }

                if (_stopRequested)
                {
                    _isRunning = false;
                    await SendEventAsync("stopped", new StoppedEvent("pause", THREAD_ID));
                }
            }
            catch (Exception ex)
            {
                _isRunning = false;
                SendOutput("console", $"Execution error: {ex.Message}");
                await SendEventAsync("stopped", new StoppedEvent("exception", THREAD_ID, ex.Message));
            }
        });

        return new ContinueResponse();
    }

    [JsonRpcMethod("next")]
    public async Task NextAsync(NextArguments args)
    {
        if (_cpu == null || _memory == null)
        {
            return;
        }

        try
        {
            // Execute single instruction
            _cpu.ExecuteOneInstruction(_memory);

            // Send stopped event
            await SendEventAsync("stopped", new StoppedEvent("step", THREAD_ID));
        }
        catch (Exception ex)
        {
            SendOutput("console", $"Step error: {ex.Message}");
            await SendEventAsync("stopped", new StoppedEvent("exception", THREAD_ID, ex.Message));
        }
    }

    [JsonRpcMethod("stepIn")]
    public Task StepInAsync(StepInArguments args)
    {
        // For 6502, stepIn is same as next
        return NextAsync(new NextArguments(args.threadId));
    }

    [JsonRpcMethod("stepOut")]
    public Task StepOutAsync(StepOutArguments args)
    {
        // For MVP, stepOut is same as next
        return NextAsync(new NextArguments(args.threadId));
    }

    [JsonRpcMethod("pause")]
    public void Pause(PauseArguments args)
    {
        _stopRequested = true;
    }

    [JsonRpcMethod("disconnect")]
    public async Task DisconnectAsync(DisconnectArguments args)
    {
        _isRunning = false;
        _stopRequested = true;
        
        await SendEventAsync("terminated", new TerminatedEvent());
    }

    // Helper methods

    private void SendOutput(string category, string output)
    {
        _ = _rpc?.NotifyAsync("output", new OutputEvent(category, output + "\n"));
    }

    private async Task SendEventAsync(string eventName, object eventBody)
    {
        if (_rpc != null)
        {
            await _rpc.NotifyAsync(eventName, eventBody);
        }
    }
}
