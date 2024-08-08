using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ObjectPool;

namespace Highbyte.DotNet6502.Systems.Logging.Console;

public class DotNet6502ConsoleLogger : DotNet6502LoggerBase
{
    private readonly Func<DotNet6502ConsoleLoggerConfiguration> _getCurrentConfig;

    public DotNet6502ConsoleLogger(
        ObjectPool<StringBuilder> stringBuilderPool,
        string categoryName,
        Func<DotNet6502ConsoleLoggerConfiguration> getCurrentConfig) : base(stringBuilderPool, categoryName)
    {
        _getCurrentConfig = getCurrentConfig;
    }

    public override bool IsEnabled(LogLevel logLevel) => logLevel >= _getCurrentConfig().LogLevel;
    public override void WriteLog(string message) => System.Console.WriteLine(message);
}
