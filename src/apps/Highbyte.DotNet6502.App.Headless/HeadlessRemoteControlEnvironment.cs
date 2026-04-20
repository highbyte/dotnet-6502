using Highbyte.DotNet6502.Remoting;
using Microsoft.Extensions.Logging;

namespace Highbyte.DotNet6502.App.Headless;

/// <summary>
/// Headless implementation of <see cref="IRemoteControlEnvironment"/>.
/// Actions run synchronously on the calling thread (no UI event loop).
/// </summary>
public class HeadlessRemoteControlEnvironment : IRemoteControlEnvironment
{
    private readonly ILogger<HeadlessRemoteControlEnvironment> _logger;
    private readonly bool _allowQuit;

    public HeadlessHostApp? HostApp { get; set; }

    public HeadlessRemoteControlEnvironment(ILoggerFactory loggerFactory, bool allowQuit = true)
    {
        _logger = loggerFactory.CreateLogger<HeadlessRemoteControlEnvironment>();
        _allowQuit = allowQuit;
    }

    public IRemotableHostApp? GetHostApp() => HostApp;

    public void RunOnUiThread(Action action) => action();

    public bool SupportsQuit => _allowQuit;

    public void DisplayRemoteMessage(string text, string level)
    {
        var logLevel = level?.ToLowerInvariant() switch
        {
            "warning" or "warn" => LogLevel.Warning,
            "error"             => LogLevel.Error,
            _                   => LogLevel.Information,
        };
        _logger.Log(logLevel, "[Remote] {Text}", text);
    }
}
