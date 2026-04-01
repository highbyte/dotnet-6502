using Highbyte.DotNet6502.DebugAdapter;

namespace Highbyte.DotNet6502.App.Headless;

/// <summary>
/// Headless implementation of <see cref="ITcpDebugServerEnvironment"/>.
/// Runs without any UI framework — actions are invoked directly on the calling thread.
/// </summary>
internal sealed class HeadlessDebugServerEnvironment : ITcpDebugServerEnvironment
{
    private readonly CancellationTokenSource _appCts;

    /// <summary>
    /// Set after the host app is created so <see cref="GetHostApp"/> can return it.
    /// </summary>
    public HeadlessHostApp? HostApp { get; set; }

    public HeadlessDebugServerEnvironment(CancellationTokenSource appCts)
    {
        _appCts = appCts;
    }

    public IDebuggableHostApp? GetHostApp() => HostApp;

    public void RunOnUiThread(Action action) => action();

    public void TerminateApplication() => _appCts.Cancel();
}
