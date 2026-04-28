using System;
using Avalonia.Threading;
using Highbyte.DotNet6502.Remoting;
using Microsoft.Extensions.Logging;

namespace Highbyte.DotNet6502.App.Avalonia.Desktop;

/// <summary>
/// Avalonia desktop implementation of <see cref="IRemoteControlEnvironment"/>.
/// Routes UI-thread operations to the Avalonia Dispatcher and routes display
/// messages through ILogger (which feeds the in-memory log store / Log tab).
/// </summary>
internal sealed class AvaloniaRemoteControlEnvironment : IRemoteControlEnvironment
{
    private readonly ILogger<AvaloniaRemoteControlEnvironment> _logger;

    public AvaloniaRemoteControlEnvironment(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<AvaloniaRemoteControlEnvironment>();
    }

    public IRemotableHostApp? GetHostApp() => Core.App.Current?.HostApp as IRemotableHostApp;

    public void RunOnUiThread(Action action) => Dispatcher.UIThread.Post(action);

    public bool SupportsQuit => false;

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
