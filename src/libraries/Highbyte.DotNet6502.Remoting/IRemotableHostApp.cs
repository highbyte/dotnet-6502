using Highbyte.DotNet6502.Systems;

namespace Highbyte.DotNet6502.Remoting;

/// <summary>
/// Extends <see cref="IHostApp"/> with remote control capabilities.
/// Host apps that support TCP remoting must implement this interface.
/// </summary>
public interface IRemotableHostApp : IHostApp
{
    /// <summary>
    /// Enqueues an action to run at the next frame boundary.
    /// Used for input injection (joystick, keyboard, memory writes) that must
    /// be synchronized with the emulation loop.
    /// </summary>
    void EnqueueRemoteAction(Action action);

    /// <summary>
    /// Captures the current display as a PNG byte array, or null if rendering
    /// is unavailable (e.g. headless mode with no renderer configured).
    /// </summary>
    byte[]? CaptureScreenshotPng();
}
