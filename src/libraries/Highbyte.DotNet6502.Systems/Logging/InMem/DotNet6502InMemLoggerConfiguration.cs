using Microsoft.Extensions.Logging;

namespace Highbyte.DotNet6502.Systems.Logging.InMem;

public class DotNet6502InMemLoggerConfiguration
{
    private readonly DotNet6502InMemLogStore _logStore;

    public int EventId { get; set; }

    public LogLevel LogLevel { get; set; } = LogLevel.Information;

    public DotNet6502InMemLoggerConfiguration(DotNet6502InMemLogStore logStore)
    {
        _logStore = logStore;
    }

    public void WriteLog(string message) => _logStore.WriteLog(message);
}
