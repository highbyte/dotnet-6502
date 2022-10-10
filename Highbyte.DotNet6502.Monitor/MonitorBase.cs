using Highbyte.DotNet6502.Monitor.Commands;
using McMaster.Extensions.CommandLineUtils;

namespace Highbyte.DotNet6502.Monitor
{
    public abstract class MonitorBase
    {
        public CPU Cpu { get; private set; }
        public Memory Mem { get; private set; }

        private CommandLineApplication _commandLineApp;

        public bool Quit { get; set; }
        public MonitorBase(CPU cpu, Memory mem)
        {
            Cpu = cpu;
            Mem = mem;
            _commandLineApp = FluentCommands.Configure(this);
        }

        public void SendCommand(string command)
        {
            if (string.IsNullOrEmpty(command))
                return;

            if (string.Equals(command, "?", StringComparison.InvariantCultureIgnoreCase) 
                || string.Equals(command, "-?", StringComparison.InvariantCultureIgnoreCase)
                || string.Equals(command, "help", StringComparison.InvariantCultureIgnoreCase)
                || string.Equals(command, "--help", StringComparison.InvariantCultureIgnoreCase))
            {
                ShowHelp();
                return;
            }

            // Workaround for CommandLineUtils after showing help once, it will always show it for every command, even if syntax is correct.
            // Create new instance for every time we parse input
            _commandLineApp = FluentCommands.Configure(this);
            int result = _commandLineApp.Execute(command.Split(' '));
            if (result == 2)
                Quit = true;
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
}
