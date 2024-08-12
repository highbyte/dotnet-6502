using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ObjectPool;

namespace Highbyte.DotNet6502.Systems.Logging.Console;

public class DotNet6502ConsoleLoggerProvider : ILoggerProvider
{
    private readonly DotNet6502ConsoleLoggerConfiguration _config;
    private readonly ObjectPool<StringBuilder> _stringBuilderPool;

    public DotNet6502ConsoleLoggerProvider(DotNet6502ConsoleLoggerConfiguration config)
    {
        var poolProvider = new DefaultObjectPoolProvider();
        _stringBuilderPool = poolProvider.CreateStringBuilderPool();
        _config = config;
    }

    public ILogger CreateLogger(string categoryName) => new DotNet6502ConsoleLogger(_stringBuilderPool, categoryName, GetCurrentConfig);

    private DotNet6502ConsoleLoggerConfiguration GetCurrentConfig() => _config;

    public void Dispose()
    {
        //_loggers.Clear();
    }
}
