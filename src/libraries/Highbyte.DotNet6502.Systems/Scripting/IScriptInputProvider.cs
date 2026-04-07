namespace Highbyte.DotNet6502.Systems;

/// <summary>
/// Abstract interface for system-specific input providers.
///
/// Implement this in each system's project (e.g., Systems.Commodore64)
/// to bridge the engine-agnostic Lua input API to the system's internal
/// keyboard and joystick state.
///
/// No Lua dependencies should appear in implementations.
/// </summary>
public interface IScriptInputProvider
{
    /// <summary>
    /// Returns the string key names recognized by this system.
    /// Scripts use these names in <c>input.key_press()</c> and <c>input.is_key_down()</c>.
    /// </summary>
    IReadOnlyList<string> GetAvailableKeys();

    /// <summary>
    /// Returns the script joystick action names supported by this system.
    /// </summary>
    IReadOnlyList<string> GetAvailableJoystickActions();

    /// <summary>
    /// Returns the number of joystick ports this system supports.
    /// </summary>
    int JoystickPortCount { get; }

    /// <summary>
    /// Injects a key press for the current frame. The key name must be
    /// one returned by <see cref="GetAvailableKeys"/>.
    /// </summary>
    void KeyPress(string keyName);

    /// <summary>
    /// Releases an injected key. The key name must be one returned by
    /// <see cref="GetAvailableKeys"/>.
    /// </summary>
    void KeyRelease(string keyName);

    /// <summary>
    /// Releases all injected keys for this frame.
    /// Called automatically by the scripting engine before each frame's
    /// input is collected, so scripts do not normally need to call this.
    /// </summary>
    void KeyReleaseAll();

    /// <summary>
    /// Returns true if the given key is currently pressed, whether by the
    /// user or by a script injection. The key name must be one returned
    /// by <see cref="GetAvailableKeys"/>.
    /// </summary>
    bool IsKeyDown(string keyName);

    /// <summary>
    /// Sets the state of a joystick action on the specified port (1-based).
    /// The action name should be one returned by <see cref="GetAvailableJoystickActions"/>.
    /// </summary>
    void SetJoystickAction(int port, string actionName, bool pressed);

    /// <summary>
    /// Returns true if the given joystick action is active on the specified
    /// port (1-based), whether by the user or by a script injection.
    /// The action name should be one returned by <see cref="GetAvailableJoystickActions"/>.
    /// </summary>
    bool IsJoystickActionDown(int port, string actionName);

    /// <summary>
    /// Resets all injected input state. Called by the scripting engine
    /// before each frame so injected inputs do not persist across frames.
    /// </summary>
    void Clear();
}
