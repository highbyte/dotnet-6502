using System.Diagnostics;

namespace Highbyte.DotNet6502.Logging;

public class DotNet6502InMemLogStore
{
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

            if (_logMessages.Count > _maxLogMessages)
                _logMessages.RemoveRange(_maxLogMessages, _logMessages.Count - _maxLogMessages);
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

    public void WriteLog(string logMessage)
    {
        _logMessages.Insert(0, logMessage);

        if (_logMessages.Count > MaxLogMessages)
            _logMessages.RemoveAt(MaxLogMessages);

        // Check if log also should be written to Debug output
        if (WriteDebugMessage)
            Debug.WriteLine(logMessage);

    }
    public List<string> GetLogMessages() => _logMessages;

    public void Clear() => _logMessages.Clear();
}
