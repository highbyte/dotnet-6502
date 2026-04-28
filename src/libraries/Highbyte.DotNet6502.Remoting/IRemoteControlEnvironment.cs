namespace Highbyte.DotNet6502.Remoting;

/// <summary>
/// Platform abstraction for the remote control server.
/// Provides host-environment operations without introducing UI framework dependencies
/// into the platform-agnostic <see cref="RemoteControlController"/>.
/// </summary>
public interface IRemoteControlEnvironment
{
    /// <summary>
    /// Returns the current remotable host app, or null if not yet initialized.
    /// </summary>
    IRemotableHostApp? GetHostApp();

    /// <summary>
    /// Schedules <paramref name="action"/> on the UI/main thread.
    /// Required for control operations (start/stop/reset) that must run on the
    /// thread that owns the emulator run loop.
    /// In headless mode, implementors may call <paramref name="action"/> directly.
    /// </summary>
    void RunOnUiThread(Action action);

    /// <summary>
    /// Whether the <c>emu.quit</c> command is permitted in this environment.
    /// Typically false in Avalonia desktop, true in headless.
    /// </summary>
    bool SupportsQuit { get; }

    /// <summary>
    /// Routes a <c>ui.message</c> command to the platform UI.
    /// Avalonia routes it to the Log tab; headless writes to stdout.
    /// </summary>
    void DisplayRemoteMessage(string text, string level);
}
