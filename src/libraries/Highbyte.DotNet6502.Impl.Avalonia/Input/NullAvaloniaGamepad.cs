namespace Highbyte.DotNet6502.Impl.Avalonia.Input;

/// <summary>
/// A null implementation of IAvaloniaGamepad that does nothing.
/// Used when no gamepad implementation is available or for systems that don't need gamepad input.
/// </summary>
public class NullAvaloniaGamepad : IAvaloniaGamepad
{
    public bool IsInitialized { get; private set; }
    public bool IsConnected => false;
    public string? GamepadName => null;
    public HashSet<GamepadButton> ButtonsDown { get; } = new();

    public void Init()
    {
        IsInitialized = true;
    }

    public void Update()
    {
        // No-op
    }

    public void Cleanup()
    {
        IsInitialized = false;
    }

    public bool IsButtonDown(GamepadButton button) => false;

    public void Dispose()
    {
        Cleanup();
    }
}
