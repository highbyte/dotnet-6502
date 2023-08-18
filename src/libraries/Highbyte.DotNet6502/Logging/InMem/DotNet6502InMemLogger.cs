using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ObjectPool;

namespace Highbyte.DotNet6502.Logging.InMem
{
    public class DotNet6502InMemLogger : ILogger
    {
        private readonly ObjectPool<StringBuilder> _stringBuilderPool;
        private readonly string _categoryName;
        private readonly Func<DotNet6502InMemLoggerConfiguration> _getCurrentConfig;

        public DotNet6502InMemLogger(
            ObjectPool<StringBuilder> stringBuilderPool,
            string categoryName,
            Func<DotNet6502InMemLoggerConfiguration> getCurrentConfig)
        {
            _stringBuilderPool = stringBuilderPool;
            _categoryName = categoryName;
            _getCurrentConfig = getCurrentConfig;
        }

        public IDisposable BeginScope<TState>(TState state) => default!;
        public bool IsEnabled(LogLevel logLevel) => logLevel >= _getCurrentConfig().LogLevel;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            if (!IsEnabled(logLevel))
                return;

            if (formatter is null)
                throw new ArgumentNullException(nameof(formatter));

            var message = formatter(state, exception);

            if (string.IsNullOrEmpty(message) && exception is null)
                return;

            WriteMessage(logLevel, eventId.Id, message, exception);
        }

        private void WriteMessage(LogLevel logLevel, int eventId, string message, Exception ex)
        {
            var builder = _stringBuilderPool.Get();
            try
            {
                var time = DateTime.Now.ToString("HH:mm:ss.fff");
                //var threadId = Environment.CurrentManagedThreadId;

                builder
                    .Append(time)
                    .Append(" [")
                    .Append(GetLogLevelString(logLevel))
                    .Append("] ")
                    .Append(_categoryName)
                    .Append(' ')
                    //.Append("(")
                    //.Append(eventId)
                    //.Append(") ")
                    //.Append(" (")
                    //.Append(threadId)
                    //.Append(") ")
                    .AppendLine(message);

                if (ex is { })
                {
                    builder.Append("    ");
                    builder.AppendLine(ex.ToString());
                }

                var logMsg = builder.ToString();

                _getCurrentConfig().WriteLog(logMsg);
            }
            finally
            {
                _stringBuilderPool.Return(builder);
            }

            static string GetLogLevelString(LogLevel logLevel) => logLevel switch
            {
                LogLevel.Trace => "trce",
                LogLevel.Debug => "dbug",
                LogLevel.Information => "info",
                LogLevel.Warning => "warn",
                LogLevel.Error => "fail",
                LogLevel.Critical => "crit",
                _ => throw new ArgumentOutOfRangeException(nameof(logLevel)),
            };
        }
    }
}
