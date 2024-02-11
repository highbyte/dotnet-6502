using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ObjectPool;

namespace Highbyte.DotNet6502.Logging
{
    public abstract class DotNet6502LoggerBase : ILogger
    {
        private readonly ObjectPool<StringBuilder> _stringBuilderPool;
        private readonly string _categoryName;

        public DotNet6502LoggerBase(
            ObjectPool<StringBuilder> stringBuilderPool,
            string categoryName)
        {
            _stringBuilderPool = stringBuilderPool;
            _categoryName = categoryName;
        }

        IDisposable ILogger.BeginScope<TState>(TState state) => default!;

        public abstract bool IsEnabled(LogLevel logLevel);

        public virtual void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
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

        protected virtual void WriteMessage(LogLevel logLevel, int eventId, string message, Exception? ex)
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

                WriteLog(logMsg);
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

        public abstract void WriteLog(string message);
    }
}
