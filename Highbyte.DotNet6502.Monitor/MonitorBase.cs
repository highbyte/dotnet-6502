using Highbyte.DotNet6502.Monitor.Commands;
using Highbyte.DotNet6502.Systems;
using McMaster.Extensions.CommandLineUtils;

namespace Highbyte.DotNet6502.Monitor
{
    public abstract class MonitorBase
    {
        public MonitorOptions Options { get; private set; }

        private readonly SystemRunner _systemRunner;
        public SystemRunner SystemRunner => _systemRunner;
        public CPU Cpu => _systemRunner.System.CPU;
        public Memory Mem => _systemRunner.System.Mem;

        private readonly Dictionary<ushort, BreakPoint> _breakPoints = new();
        public Dictionary<ushort, BreakPoint> BreakPoints => _breakPoints;

        private CommandLineApplication _commandLineApp;

        public bool Quit { get; set; }

        public MonitorBase(SystemRunner systemRunner, MonitorOptions options)
        {
            Options = options;
            _commandLineApp = CommandLineApp.Build(this);
            _systemRunner = systemRunner;

            _systemRunner.SetCustomExecEvaluator(new BreakPointExecEvaluator(_breakPoints));
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
            _commandLineApp = CommandLineApp.Build(this);
            var result = (CommandResult)_commandLineApp.Execute(command.Split(' '));
            return result;
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

        public abstract void LoadBinary(string fileName, out ushort loadedAtAddress, ushort? forceLoadAddress = null);
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
}
