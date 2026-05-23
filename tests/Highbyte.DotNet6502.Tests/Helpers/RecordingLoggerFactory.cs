using Microsoft.Extensions.Logging;

namespace Highbyte.DotNet6502.Tests.Helpers;

/// <summary>
/// Minimal in-memory ILoggerFactory/ILogger pair used by tests that need to verify
/// IsEnabled-gated log calls actually execute. Reports IsEnabled=true for every level
/// and captures emitted log messages in a flat list for assertion.
/// </summary>
public class RecordingLoggerFactory : ILoggerFactory
{
    public List<string> Messages { get; } = new();

    public ILogger CreateLogger(string categoryName) => new RecordingLogger(Messages);
    public void AddProvider(ILoggerProvider provider) { }
    public void Dispose() { }

    private sealed class RecordingLogger : ILogger
    {
        private readonly List<string> _messages;
        public RecordingLogger(List<string> messages) { _messages = messages; }

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            _messages.Add(formatter(state, exception));
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();
            public void Dispose() { }
        }
    }
}
