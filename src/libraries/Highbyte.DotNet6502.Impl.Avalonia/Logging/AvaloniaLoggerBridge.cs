using System;
using System.Linq;
using Avalonia.Logging;
using Microsoft.Extensions.Logging;

namespace Highbyte.DotNet6502.Impl.Avalonia.Logging;

/// <summary>
/// Bridges Avalonia.Logging.Logger output to Microsoft.Extensions.Logging.ILogger.
/// This allows both logging systems to write to the same destination (e.g., file or memory).
/// </summary>
public class AvaloniaLoggerBridge : ILogSink
{
    private readonly ILogger _logger;
    private readonly LogLevel _minimumLevel;

    public AvaloniaLoggerBridge(ILogger logger, LogLevel minimumLevel = LogLevel.Warning)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _minimumLevel = minimumLevel;
    }

    public bool IsEnabled(LogEventLevel level, string area)
    {
        var logLevel = ConvertLogLevel(level);
            // Only enable if both the bridge's minimum level and the logger's level allow it
        return logLevel >= _minimumLevel && _logger.IsEnabled(logLevel);
    }

    public void Log(
        LogEventLevel level,
        string area,
        object? source,
        string messageTemplate)
    {
        var logLevel = DowngradeKnownNoisyMessage(level, area, messageTemplate);
        if (logLevel < _minimumLevel || !_logger.IsEnabled(logLevel))
            return;

        var message = FormatMessage(area, source, messageTemplate);
        _logger.Log(logLevel, message);
    }

    public void Log(
        LogEventLevel level,
        string area,
        object? source,
        string messageTemplate,
        params object?[] propertyValues)
    {
        var logLevel = DowngradeKnownNoisyMessage(level, area, messageTemplate);
        if (logLevel < _minimumLevel || !_logger.IsEnabled(logLevel))
            return;

        try
        {
            var message = FormatMessage(area, source, messageTemplate, propertyValues);
            _logger.Log(logLevel, message);
        }
        catch
        {
            // Fallback if formatting fails
            var fallbackMessage = FormatMessage(area, source, messageTemplate);
            _logger.Log(logLevel, fallbackMessage);
        }
    }

    /// <summary>
    /// Downgrades known benign Avalonia internal messages that are logged at Error level
    /// but represent handled fallback paths, not real errors.
    /// </summary>
    private static LogLevel DowngradeKnownNoisyMessage(LogEventLevel level, string area, string messageTemplate)
    {
        // Windows-only: Avalonia's composition render loop times out waiting for
        // ICompositor5.RequestCommitAsync when the app is idle (nothing to render).
        // Avalonia handles this gracefully by force-triggering the next tick.
        // It is not a real error — downgrade to Debug to suppress it from normal log output.
        if (area == "Visual" && messageTemplate.Contains("RequestCommitAsync timed out"))
            return LogLevel.Debug;

        return ConvertLogLevel(level);
    }

    private static LogLevel ConvertLogLevel(LogEventLevel level) =>
        level switch
        {
            LogEventLevel.Verbose => LogLevel.Trace,
            LogEventLevel.Debug => LogLevel.Debug,
            LogEventLevel.Information => LogLevel.Information,
            LogEventLevel.Warning => LogLevel.Warning,
            LogEventLevel.Error => LogLevel.Error,
            LogEventLevel.Fatal => LogLevel.Critical,
            _ => LogLevel.Information
        };

    private static string FormatMessage(
        string area,
        object? source,
        string messageTemplate,
        object?[]? propertyValues = null)
    {
        var sourceInfo = source?.GetType().Name ?? "Unknown";
        var baseMessage = $"[{area}] [{sourceInfo}] {messageTemplate}";

        if (propertyValues != null && propertyValues.Length > 0)
        {
            try
            {
                return string.Format(baseMessage, propertyValues);
            }
            catch
            {
                // If formatting fails, just append values
                return $"{baseMessage} | Values: {string.Join(", ", propertyValues.Select(v => v?.ToString() ?? "null"))}";
            }
        }

        return baseMessage;
    }
}
