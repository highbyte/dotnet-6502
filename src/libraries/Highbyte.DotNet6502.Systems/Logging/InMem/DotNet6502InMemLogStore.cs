using System.Diagnostics;

namespace Highbyte.DotNet6502.Systems.Logging.InMem;

public class DotNet6502InMemLogStore
{
    // Event raised when a new log message is added
    public event EventHandler<string>? LogMessageAdded;

    private readonly List<string> _logMessages = new();

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

    public void WriteLog(string logMessage)
    {
        if (_insertAtStart)
            _logMessages.Insert(0, logMessage);
        else
            _logMessages.Add(logMessage);

        TrimLogMessages();

        // Raise event for new log message
        LogMessageAdded?.Invoke(this, logMessage);

        // Check if log also should be written to Debug output
        if (WriteDebugMessage)
            Debug.WriteLine(logMessage);
    }

    private void TrimLogMessages()
    {
        if (_logMessages.Count > _maxLogMessages)
        {
            if (_insertAtStart)
                _logMessages.RemoveRange(_maxLogMessages, _logMessages.Count - _maxLogMessages);
            else
                _logMessages.RemoveRange(0, _logMessages.Count - _maxLogMessages);
        }
    }

    public List<string> GetLogMessages() => _logMessages;

    public void Clear() => _logMessages.Clear();
}
