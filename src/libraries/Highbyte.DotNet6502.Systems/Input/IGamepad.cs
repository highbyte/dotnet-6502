namespace Highbyte.DotNet6502.Systems.Input;

/// <summary>
/// Abstraction for gamepad input.
/// This interface allows different implementations for browser (JavaScript interop) 
/// and desktop (SDL2, Silk.NET, etc.) platforms.
/// </summary>
public interface IGamepad : IDisposable
{
    /// <summary>
    /// Gets whether the gamepad provider has been initialized.
    /// </summary>
    public bool IsInitialized { get; }

    /// <summary>
    /// Gets whether a gamepad is currently connected.
    /// </summary>
    public bool IsConnected { get; }

    /// <summary>
    /// Gets the name/identifier of the currently connected gamepad, or null if none connected.
    /// </summary>
    public string? GamepadName { get; }

    /// <summary>
    /// Initialize the gamepad provider.
    /// </summary>
    public void Init();

    /// <summary>
    /// Update the gamepad state. Should be called once per frame before checking button states.
    /// </summary>
    public void Update();

    /// <summary>
    /// Cleanup the gamepad provider.
    /// </summary>
    public void Cleanup();

    /// <summary>
    /// Gets the set of currently pressed gamepad buttons.
    /// </summary>
    public HashSet<GamepadButton> ButtonsDown { get; }

    /// <summary>
    /// Checks if a specific button is currently pressed.
    /// </summary>
    /// <param name="button">The button to check.</param>
    /// <returns>True if the button is pressed, false otherwise.</returns>
    public bool IsButtonDown(GamepadButton button);
}
