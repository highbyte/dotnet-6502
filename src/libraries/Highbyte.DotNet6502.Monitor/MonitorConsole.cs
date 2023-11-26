using System.CommandLine;
using System.CommandLine.IO;
using System.Text;

namespace Highbyte.DotNet6502.Monitor;

/// <summary>
/// System.CommandLine.IConsole implementation that does nothing except prints to our MonitorBase.
/// This is for output that cannot be controlled by our application, like help texts details per command.
/// By default System.CommandLine writes to a system console, which doesn't exist unless hosted in a .NET Console app.
/// </summary>
public class MonitorConsole : IConsole
{
    private readonly MonitorBase _monitor;
    private readonly MonitorStandardStreamWriter _monitorStandardStreamWriter;

    private MonitorConsole(MonitorBase monitor)
    {
        _monitor = monitor;
        _monitorStandardStreamWriter = new MonitorStandardStreamWriter(_monitor);
    }

    public IStandardStreamWriter Out => _monitorStandardStreamWriter;

    public IStandardStreamWriter Error => _monitorStandardStreamWriter;

    public bool IsOutputRedirected { get; protected set; }

    public bool IsErrorRedirected { get; protected set; }

    public bool IsInputRedirected { get; protected set; }

    /// <summary>
    /// A shared instance of <see cref="MonitorConsole"/>.
    /// </summary>
    public static MonitorConsole BuildSingleton(MonitorBase monitor)
    {
        return new MonitorConsole(monitor);
    }

    internal class MonitorStandardStreamWriter : TextWriter, IStandardStreamWriter
    {
        readonly List<char> _printedChars = new();

        private readonly MonitorBase _monitor;

        public MonitorStandardStreamWriter(MonitorBase monitor)
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

        public override void Write(string? value)
        {
            if (value is null)
                return;
            foreach (var c in value)
                Write(c);
        }

        public override Encoding Encoding { get; } = Encoding.Unicode;

        public override string ToString()
        {
            var str = _printedChars.ToString();
            return str is null ? "" : str;
        }
    }
}
