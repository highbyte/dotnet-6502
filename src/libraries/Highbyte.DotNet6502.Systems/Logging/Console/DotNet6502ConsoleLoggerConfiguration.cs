using Microsoft.Extensions.Logging;

namespace Highbyte.DotNet6502.Systems.Logging.Console;

public class DotNet6502ConsoleLoggerConfiguration
{
    public int EventId { get; set; }

    public LogLevel LogLevel { get; set; } = LogLevel.Information;

}
