using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ObjectPool;

namespace Highbyte.DotNet6502.Systems.Logging.InMem;

public class DotNet6502InMemLoggerProvider : ILoggerProvider
{
    private readonly DotNet6502InMemLoggerConfiguration _config;
    private readonly ObjectPool<StringBuilder> _stringBuilderPool;

    public DotNet6502InMemLoggerProvider(DotNet6502InMemLoggerConfiguration config)
    {
        var poolProvider = new DefaultObjectPoolProvider();
        _stringBuilderPool = poolProvider.CreateStringBuilderPool();
        _config = config;
    }

    public ILogger CreateLogger(string categoryName) => new DotNet6502InMemLogger(_stringBuilderPool, categoryName, GetCurrentConfig);

    private DotNet6502InMemLoggerConfiguration GetCurrentConfig() => _config;

    public void Dispose()
    {
        //_loggers.Clear();
    }
}
