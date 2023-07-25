using System.Threading;
using Highbyte.DotNet6502.Monitor.SystemSpecific;
using Highbyte.DotNet6502.Systems;
using McMaster.Extensions.CommandLineUtils;

namespace Highbyte.DotNet6502.Monitor;

public abstract class MonitorBase
{
    public MonitorConfig Options { get; private set; }
    private MonitorVariables _variables;

    private readonly SystemRunner _systemRunner;
    public SystemRunner SystemRunner => _systemRunner;
    public CPU Cpu => _systemRunner.System.CPU;
    public Memory Mem => _systemRunner.System.Mem;

    public ISystem System => _systemRunner.System;


    private readonly Dictionary<ushort, BreakPoint> _breakPoints = new();
    public Dictionary<ushort, BreakPoint> BreakPoints => _breakPoints;

    private CommandLineApplication _commandLineApp;

    public MonitorBase(SystemRunner systemRunner, MonitorConfig options)
    {
        _systemRunner = systemRunner;
        _systemRunner.SetCustomExecEvaluator(new BreakPointExecEvaluator(_breakPoints));
        Options = options;
        _variables = new MonitorVariables();
        _commandLineApp = CommandLineApp.Build(this, _variables, options);

    }

    public CommandResult SendCommand(string command)
    {
        if (string.IsNullOrEmpty(command))
            return CommandResult.Ok;

        if (string.Equals(command, "?", StringComparison.InvariantCultureIgnoreCase)
            || string.Equals(command, "-?", StringComparison.InvariantCultureIgnoreCase)
            || string.Equals(command, "help", StringComparison.InvariantCultureIgnoreCase)
            || string.Equals(command, "--help", StringComparison.InvariantCultureIgnoreCase))
        {
            ShowHelp();
            return CommandResult.Ok;
        }

        // Workaround for CommandLineUtils after showing help once, it will always show it for every command, even if syntax is correct.
        // Create new instance for every time we parse input
        _commandLineApp = CommandLineApp.Build(this, _variables, Options);
        var result = (CommandResult)_commandLineApp.Execute(command.Split(' '));
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

    public void ShowInfoAfterBreakTriggerEnabled(ExecEvaluatorTriggerResult execEvaluatorTriggerResult)
    {
        if (!execEvaluatorTriggerResult.Triggered)
            return;

        switch (execEvaluatorTriggerResult.TriggerType)
        {
            case ExecEvaluatorTriggerReasonType.DebugBreakPoint:
                WriteOutput($"Breakpoint triggered at {Cpu.PC.ToHex()}");
                break;
            case ExecEvaluatorTriggerReasonType.UnknownInstruction:
                WriteOutput($"Unknown instruction detected");
                WriteOutput(execEvaluatorTriggerResult.TriggerDescription ?? "");
                break;
            case ExecEvaluatorTriggerReasonType.Other:
                WriteOutput($"Other reason execution stopped");
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
        if (_commandLineApp.Description != null)
            WriteOutput(_commandLineApp.Description);
    }

    public void ShowHelp()
    {
        var helpText = _commandLineApp.GetHelpText();
        var helpTextLines = helpText.Split(Environment.NewLine);
        foreach (var line in helpTextLines)
            WriteOutput(line);
    }

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
