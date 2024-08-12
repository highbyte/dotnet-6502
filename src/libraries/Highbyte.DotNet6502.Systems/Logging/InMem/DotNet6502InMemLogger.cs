using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ObjectPool;

namespace Highbyte.DotNet6502.Systems.Logging.InMem;

public class DotNet6502InMemLogger : DotNet6502LoggerBase
{
    private readonly Func<DotNet6502InMemLoggerConfiguration> _getCurrentConfig;

    public DotNet6502InMemLogger(
        ObjectPool<StringBuilder> stringBuilderPool,
        string categoryName,
        Func<DotNet6502InMemLoggerConfiguration> getCurrentConfig) : base(stringBuilderPool, categoryName)
    {
        _getCurrentConfig = getCurrentConfig;
    }

    public override bool IsEnabled(LogLevel logLevel) => logLevel >= _getCurrentConfig().LogLevel;
    public override void WriteLog(string message) => _getCurrentConfig().WriteLog(message);
}
