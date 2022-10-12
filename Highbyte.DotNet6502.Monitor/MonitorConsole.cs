using System.Text;
using McMaster.Extensions.CommandLineUtils;

namespace Highbyte.DotNet6502.Monitor
{
    /// <summary>
    /// McMaster CommandLine console implementation that does nothing except prints to our MonitorBase.
    /// This is for output that cannot be controlled by our application, like help texts details per command.
    /// By default McMaster CommandLine writes to a system console, which doesn't exist unless hosted in a .NET Console app.
    /// </summary>
    public class MonitorConsole : IConsole
    {
        private readonly MonitorBase _monitor;

        private MonitorConsole(MonitorBase monitor)
        {
            Error = Out = new MonitorTextWriter(monitor);
            _monitor = monitor;
        }

        /// <summary>
        /// A shared instance of <see cref="MonitorConsole"/>.
        /// </summary>
        public static MonitorConsole BuildSingleton(MonitorBase monitor)
        {
            return new MonitorConsole(monitor);
        }

        /// <summary>
        /// A writer that does nothing. 
        /// </summary>
        public TextWriter Out { get; }

        /// <summary>
        /// A writer that does nothing. 
        /// </summary>
        public TextWriter Error { get; }

        /// <summary>
        /// An empty reader.
        /// </summary>
        public TextReader In { get; } = new StringReader(string.Empty);

        /// <summary>
        /// Always <c>false</c>.
        /// </summary>
        public bool IsInputRedirected => false;

        /// <summary>
        /// Always <c>false</c>.
        /// </summary>
        public bool IsOutputRedirected => false;

        /// <summary>
        /// Always <c>false</c>.
        /// </summary>
        public bool IsErrorRedirected => false;

        public ConsoleColor ForegroundColor { get; set; }

        public ConsoleColor BackgroundColor { get; set; }

        /// <summary>
        /// This event never fires.
        /// </summary>
        public event ConsoleCancelEventHandler? CancelKeyPress
        {
            add { }
            remove { }
        }

        public override bool Equals(object? obj)
        {
            return base.Equals(obj);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        public void ResetColor()
        {
        }

        public override string? ToString()
        {
            return base.ToString();
        }

        private sealed class MonitorTextWriter : TextWriter
        {
            List<char> _printedChars = new();

            private readonly MonitorBase _monitor;

            public override Encoding Encoding => Encoding.Unicode;

            public MonitorTextWriter(MonitorBase monitor)
            {
                _monitor = monitor;
            }
            public override void Write(char value)
            {
                _printedChars.Add(value);
                if (_printedChars.Count >= Environment.NewLine.Length)
                {
                    var printedString = new string(_printedChars.ToArray());
                    var lastPart = printedString.Substring(printedString.Length - Environment.NewLine.Length, Environment.NewLine.Length);
                    if (lastPart == Environment.NewLine)
                    {
                        var partWithoutNewLine = printedString.Substring(0, printedString.Length - Environment.NewLine.Length);
                        _monitor.WriteOutput(partWithoutNewLine.ToString());
                        _printedChars.Clear();
                    }
                }
            }
        }
    }
}