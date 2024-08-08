using Highbyte.DotNet6502.Monitor.SystemSpecific;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Utils;
using System.CommandLine;
using System.CommandLine.Parsing;

namespace Highbyte.DotNet6502.Monitor;

public abstract class MonitorBase
{
    public MonitorConfig Options { get; private set; }
    private MonitorVariables _variables;

    private readonly SystemRunner _systemRunner;
    public SystemRunner SystemRunner => _systemRunner;

    private readonly BreakPointExecEvaluator _breakPointExecEvaluator;
    public BreakPointExecEvaluator BreakPointExecEvaluator => _breakPointExecEvaluator;

    public CPU Cpu => _systemRunner.System.CPU;
    public Memory Mem => _systemRunner.System.Mem;

    public ISystem System => _systemRunner.System;


    private readonly Dictionary<ushort, BreakPoint> _breakPoints = new();
    public Dictionary<ushort, BreakPoint> BreakPoints => _breakPoints;

    private readonly Parser _commandLineApp;
    private readonly MonitorConsole _console;

    public MonitorBase(SystemRunner systemRunner, MonitorConfig options)
    {
        _systemRunner = systemRunner;

        // Init systemrunner with a custom exec evaluator that can handle breakpoints
        _breakPointExecEvaluator = new BreakPointExecEvaluator(_breakPoints);
        _systemRunner.SetCustomExecEvaluator(_breakPointExecEvaluator);

        Options = options;
        ApplyOptionsOnBreakPointExecEvaluator();

        _variables = new MonitorVariables();
        _console = MonitorConsole.BuildSingleton(this);
        _commandLineApp = CommandLineApp.Build(this, _variables, options, _console);
    }

    public CommandResult SendCommand(string command)
    {
        if (string.IsNullOrEmpty(command))
            return CommandResult.Ok;

        var cmdLine = $"{_commandLineApp.Configuration.RootCommand.Name} {command}".Split(' ');
        var parseResult = _commandLineApp.Parse(cmdLine);
        if (parseResult.Errors.Count > 0)
        {
            foreach (var error in parseResult.Errors)
                WriteOutput(error.Message, MessageSeverity.Error);
            return CommandResult.Error;
        }
        var invokeResult = _commandLineApp.Invoke(cmdLine, _console);
        var result = (CommandResult)invokeResult;
        return result;
    }

    public void Reset()
    {
        _variables = new MonitorVariables();

        // If there are system-specific monitor commands, issue reset there too
        if (SystemRunner.System is ISystemMonitor systemWithMonitor)
        {
            var monitorCommands = systemWithMonitor.GetSystemMonitorCommands();
            monitorCommands.Reset(this);
        }
    }

    public void ApplyOptionsOnBreakPointExecEvaluator()
    {
        _breakPointExecEvaluator.StopAfterBRKInstruction = Options.StopAfterBRKInstruction;
        _breakPointExecEvaluator.StopAfterUnknownInstruction = Options.StopAfterUnknownInstruction;
    }

    public void ShowInfoAfterBreakTriggerEnabled(ExecEvaluatorTriggerResult execEvaluatorTriggerResult)
    {
        if (!execEvaluatorTriggerResult.Triggered)
            return;

        switch (execEvaluatorTriggerResult.TriggerType)
        {
            case ExecEvaluatorTriggerReasonType.DebugBreakPoint:
                WriteOutput($"Breakpoint triggered at {Cpu.PC.ToHex("", lowerCase: true)}");
                break;
            case ExecEvaluatorTriggerReasonType.UnknownInstruction:
                WriteOutput(execEvaluatorTriggerResult.TriggerDescription ?? "");
                break;
            case ExecEvaluatorTriggerReasonType.BRKInstruction:
                WriteOutput(execEvaluatorTriggerResult.TriggerDescription ?? "");
                break;
            case ExecEvaluatorTriggerReasonType.Other:
                //WriteOutput($"Other reason execution stopped");
                WriteOutput(execEvaluatorTriggerResult.TriggerDescription ?? "");
                break;
            default:
                throw new DotNet6502Exception($"Internal error. Unknown ExecEvaluatorTriggerReasonType: {execEvaluatorTriggerResult.TriggerType}");
        }

        // Show disassembly of next instruction
        WriteOutput($"{OutputGen.GetNextInstructionDisassembly(Cpu, Mem)}");
    }
    public void ShowDescription()
    {
        if (_commandLineApp.Configuration.RootCommand.Description != null)
            WriteOutput(_commandLineApp.Configuration.RootCommand.Description);
    }

    public void ShowHelp()
    {
        _commandLineApp.Invoke("?", _console);
    }

    public virtual void ShowOptions()
    {
        WriteOutput("Current options:");
        WriteOutput("  Stop on BRK instruction:     " + FormatOptionValue(Options.StopAfterBRKInstruction));
        WriteOutput("  Stop on unknown instruction: " + FormatOptionValue(Options.StopAfterUnknownInstruction));
        //if (Options.DefaultDirectory != null)
        //    WriteOutput("  Default directory:           " + Options.DefaultDirectory);
        if (Options.MaxLineLength.HasValue)
            WriteOutput("  Max line length:             " + Options.MaxLineLength);
    }

    private string FormatOptionValue(bool value) => value ? "1 (1=yes,0=no)" : "0 (1=yes,0=no)";

    public abstract bool LoadBinary(string fileName, out ushort loadedAtAddress, out ushort fileLength, ushort? forceLoadAddress = null, Action<MonitorBase, ushort, ushort>? afterLoadCallback = null);
    public abstract bool LoadBinary(out ushort loadedAtAddress, out ushort fileLength, ushort? forceLoadAddress = null, Action<MonitorBase, ushort, ushort>? afterLoadCallback = null);
    public abstract void SaveBinary(string fileName, ushort startAddress, ushort endAddress, bool addFileHeaderWithLoadAddress);
    public abstract void WriteOutput(string message);
    public abstract void WriteOutput(string message, MessageSeverity severity);
}

public enum MessageSeverity
{
    Information,
    Warning,
    Error
}

public enum CommandResult
{
    Ok = 0,
    Error = 1,
    Quit = 2,
    Continue = 3,
}
