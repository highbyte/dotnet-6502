using Highbyte.DotNet6502.Systems.Commodore64.Transport;
using Microsoft.Extensions.Logging;

namespace Highbyte.DotNet6502.Impl.Avalonia.Commodore64.Transport;

public static class BrowserWebSocketTransportFactory
{
    public static Func<string, string?, ILogger, ISwiftLinkTransport>? Create { get; set; }
}
