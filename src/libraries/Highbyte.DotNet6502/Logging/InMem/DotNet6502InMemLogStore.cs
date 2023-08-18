namespace Highbyte.DotNet6502.Logging
{
    public class DotNet6502InMemLogStore
    {
        private readonly List<string> _logMessages = new();
        private const int MAX_LOG_MESSAGES = 100;

        public void WriteLog(string logMessage)
        {
            _logMessages.Insert(0, logMessage);

            if (_logMessages.Count > MAX_LOG_MESSAGES)
                _logMessages.RemoveAt(MAX_LOG_MESSAGES);

        }
        public List<string> GetLogMessages() => _logMessages;

        public void Clear() => _logMessages.Clear();
    }
}
