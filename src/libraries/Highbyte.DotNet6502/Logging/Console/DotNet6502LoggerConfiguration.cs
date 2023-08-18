using Microsoft.Extensions.Logging;

namespace Highbyte.DotNet6502.Logging.Console;

public class DotNet6502ConsoleLoggerConfiguration
{
    public int EventId { get; set; }

    public LogLevel LogLevel { get; set; } = LogLevel.Information;

    public Dictionary<LogLevel, LogFormat> LogLevelFormatMap { get; set; } =
        new()
        {
            [LogLevel.Debug] = LogFormat.Short,
            [LogLevel.Information] = LogFormat.Short,
            [LogLevel.Warning] = LogFormat.Short,
            [LogLevel.Error] = LogFormat.Long
        };

    public enum LogFormat
    {
        Short,
        Long
    }
}
