using System.Runtime.InteropServices;

namespace Highbyte.DotNet6502.Systems.Input;

/// <summary>
/// Host-agnostic per-frame input state.
///
/// Each host technology's input context implements this in addition to
/// <see cref="IInputHandlerContext"/>, translating its native keyboard/gamepad representation
/// into the neutral <see cref="HostKey"/> / <see cref="GamepadButton"/> abstractions.
///
/// A reusable, system-specific input handler (e.g. the C64 input handler) reads input solely
/// through this interface and therefore no longer needs to know the host technology — the
/// counterpart, on the rendering side, is how systems emit frame data through neutral interfaces
/// instead of holding a host render object.
/// </summary>
public interface IHostInputState
{
    /// <summary>
    /// The physical host keyboard keys currently held down.
    /// </summary>
    IReadOnlySet<HostKey> KeysDown { get; }

    /// <summary>
    /// The gamepad/joystick buttons currently held down (XInput-style abstraction).
    /// Empty when the host has no gamepad support or none is connected.
    /// </summary>
    IReadOnlySet<GamepadButton> GamepadButtonsDown { get; }

    /// <summary>
    /// Whether the host caps-lock is currently on.
    /// </summary>
    bool CapsLockOn { get; }

    /// <summary>
    /// Called once per emulator frame before input is read, so a host that polls (rather than
    /// receives events) can refresh its keyboard/gamepad snapshot. Hosts that update via events
    /// implement this as a no-op.
    /// </summary>
    void UpdatePerFrame();

    /// <summary>
    /// True when the host is running on macOS — including a browser running on macOS. Used to
    /// correct the macOS ISO-keyboard quirk where the § and &lt; keys are reported with swapped
    /// physical key codes (see the C64 input handler).
    /// <para>
    /// The default is correct for every <em>native</em> host. A host that can run inside a
    /// browser (where the .NET runtime reports <see cref="OSPlatform"/> Browser, not the
    /// underlying OS) must override this with a value detected from the browser.
    /// </para>
    /// </summary>
    bool IsRunningOnMacOS => RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

    /// <summary>
    /// The host's currently active native keyboard layout, as a raw platform-specific identifier
    /// (a Windows KLID, a macOS input-source id, …), or <c>null</c> when it cannot be determined.
    /// Used to auto-select a system keyboard mapping when the user has not pinned one explicitly.
    /// <para>
    /// The default queries the OS via <see cref="KeyboardLayoutDetector"/>, correct for native
    /// hosts. A host that runs inside a browser must override this — the .NET runtime reports
    /// <see cref="OSPlatform"/> Browser there, so the OS query yields nothing — with a value
    /// detected from the browser.
    /// </para>
    /// </summary>
    string? DetectNativeKeyboardLayoutId() => KeyboardLayoutDetector.DetectNativeLayoutId();
}
