using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Highbyte.DotNet6502.Systems.Logging.InMem;

public record LogEntry(LogLevel LogLevel, string Message);

public class DotNet6502InMemLogStore
{
    // Event raised when a new log message is added
    public event EventHandler<LogEntry>? LogMessageAdded;

    private readonly List<LogEntry> _logEntries = new();

    private int _maxLogMessages = 100;
    public int MaxLogMessages
    {
        get => _maxLogMessages;
        set
        {
            if (value < 1)
                throw new ArgumentException("MaxLogMessages must be greater than 0.");
            _maxLogMessages = value;
            TrimLogMessages();
        }
    }

    private bool _writeDebugMessage = false;
    public bool WriteDebugMessage
    {
        get => _writeDebugMessage;
        set
        {
            _writeDebugMessage = value;
        }
    }

    private readonly bool _insertAtStart;
    public bool InsertAtStart => _insertAtStart;

    public DotNet6502InMemLogStore(bool insertAtStart = true)
    {
        _insertAtStart = insertAtStart;
    }

    public void WriteLog(string logMessage) => WriteLog(LogLevel.Information, logMessage);

    public void WriteLog(LogLevel logLevel, string logMessage)
    {
        var logEntry = new LogEntry(logLevel, logMessage);
        
        if (_insertAtStart)
            _logEntries.Insert(0, logEntry);
        else
            _logEntries.Add(logEntry);

        TrimLogMessages();

        // Raise event for new log message
        LogMessageAdded?.Invoke(this, logEntry);

        // Check if log also should be written to Debug output
        if (WriteDebugMessage)
            Debug.WriteLine(logMessage);
    }

    private void TrimLogMessages()
    {
        if (_logEntries.Count > _maxLogMessages)
        {
            if (_insertAtStart)
                _logEntries.RemoveRange(_maxLogMessages, _logEntries.Count - _maxLogMessages);
            else
                _logEntries.RemoveRange(0, _logEntries.Count - _maxLogMessages);
        }
    }

    public List<string> GetLogMessages() => _logEntries.Select(entry => entry.Message).ToList();

    public List<LogEntry> GetFullLogMessages() => _logEntries.ToList();

    public void Clear() => _logEntries.Clear();
}
