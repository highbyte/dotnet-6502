using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using Highbyte.DotNet6502.DebugAdapter;

namespace Highbyte.DotNet6502.App.Avalonia.Desktop;

/// <summary>
/// Avalonia-specific implementation of <see cref="ITcpDebugServerEnvironment"/>.
/// Bridges the platform-agnostic <see cref="TcpDebugServerManager"/> to the Avalonia host.
/// </summary>
internal sealed class AvaloniaDebugServerEnvironment : ITcpDebugServerEnvironment
{
    public IDebuggableHostApp? GetHostApp() => Core.App.Current?.HostApp;

    public void RunOnUiThread(Action action) => Dispatcher.UIThread.Post(action);

    public void TerminateApplication()
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime lifetime)
                lifetime.Shutdown();
        });
    }
}
