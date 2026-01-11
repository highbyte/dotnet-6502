using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using Highbyte.DotNet6502.Systems.Input;
using Microsoft.Extensions.Logging;

namespace Highbyte.DotNet6502.Impl.Browser.Input;

/// <summary>
/// Browser implementation of IGamepad using the browser Gamepad API via JavaScript interop.
/// Uses the standard gamepad mapping which aligns with Xbox controller layout.
/// https://developer.mozilla.org/en-US/docs/Web/API/Gamepad_API/Using_the_Gamepad_API
/// </summary>
[SupportedOSPlatform("browser")]
public partial class BrowserGamepad : IGamepad
{
    private readonly ILogger _logger;
    private int _gamepadIndex = -1;
    private static bool s_jsModuleLoaded = false;

    public bool IsInitialized { get; private set; }
    public bool IsConnected => _gamepadIndex >= 0 && JSInterop.IsGamepadConnected(_gamepadIndex);
    public string? GamepadName { get; private set; }
    public HashSet<GamepadButton> ButtonsDown { get; } = [];

    // Standard Gamepad button indices (based on W3C Standard Gamepad mapping)
    // https://w3c.github.io/gamepad/#remapping
    private static class StandardButtons
    {
        public const int A = 0;              // Bottom face button
        public const int B = 1;              // Right face button
        public const int X = 2;              // Left face button
        public const int Y = 3;              // Top face button
        public const int LeftBumper = 4;     // Left shoulder
        public const int RightBumper = 5;    // Right shoulder
        public const int LeftTrigger = 6;    // Left trigger
        public const int RightTrigger = 7;   // Right trigger
        public const int Back = 8;           // Back/Select/Share
        public const int Start = 9;          // Start/Options
        public const int LeftStick = 10;     // Left stick pressed
        public const int RightStick = 11;    // Right stick pressed
        public const int DPadUp = 12;        // D-Pad up
        public const int DPadDown = 13;      // D-Pad down
        public const int DPadLeft = 14;      // D-Pad left
        public const int DPadRight = 15;     // D-Pad right
        public const int Guide = 16;         // Guide/Home/PS button
    }

    // Standard Gamepad axis indices
    private static class StandardAxes
    {
        public const int LeftStickX = 0;
        public const int LeftStickY = 1;
        public const int RightStickX = 2;
        public const int RightStickY = 3;
    }

    // Threshold for considering stick movement as a "button press"
    private const float StickThreshold = 0.5f;

    // Only check for gamepad connection changes every N frames to reduce JSInterop overhead
    // Gamepad connection/disconnection is a rare event, so checking every few seconds is sufficient
    private const int DetectGamepadIntervalFrames = 180; // Check every ~3 seconds at 60fps
    private int _framesSinceLastDetect = DetectGamepadIntervalFrames; // Start with detection on first frame

    public BrowserGamepad(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger(nameof(BrowserGamepad));
    }

    public void Init()
    {
        if (IsInitialized)
            return;

        try
        {
            // JS module loading is handled externally before Init is called
            if (!s_jsModuleLoaded)
            {
                _logger.LogWarning("JavaScript gamepad module not yet loaded. Call LoadJsModuleAsync first.");
                return;
            }

            // Try to find an initial gamepad
            DetectGamepad();

            IsInitialized = true;
            _logger.LogInformation("BrowserGamepad initialized");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize BrowserGamepad");
        }
    }

    /// <summary>
    /// Loads the JavaScript gamepad module. Must be called before Init().
    /// </summary>
    public static async Task LoadJsModuleAsync()
    {
        if (s_jsModuleLoaded)
            return;

        var jsModuleUri = BrowserGamepadResources.GetJavaScriptModuleDataUri();
        await JSHost.ImportAsync("BrowserGamepad", jsModuleUri);
        s_jsModuleLoaded = true;
    }

    private void DetectGamepad()
    {
        int index = FindBestGamepadIndex();
        if (index >= 0 && index != _gamepadIndex)
        {
            _gamepadIndex = index;
            GamepadName = JSInterop.GetGamepadName(index);
            _logger.LogInformation("Gamepad detected: {Name} at index {Index}", GamepadName, _gamepadIndex);
        }
        else if (index < 0 && _gamepadIndex >= 0)
        {
            _logger.LogInformation("Gamepad disconnected: {Name}", GamepadName);
            _gamepadIndex = -1;
            GamepadName = null;
            ButtonsDown.Clear();
        }
    }

    /// <summary>
    /// Finds the best gamepad index by prioritizing devices with standard mapping.
    /// The W3C Gamepad API's "standard" mapping indicates a recognized game controller.
    /// Note: Browser Gamepad API requires user interaction (button press) before gamepads are visible.
    /// </summary>
    private int FindBestGamepadIndex()
    {
        int gamepadCount = JSInterop.GetGamepadCount();
        if (gamepadCount == 0)
            return -1;

        int standardMappingIndex = -1;
        int fallbackIndex = -1;

        // Check all connected gamepads and find the best one
        // Browser typically supports up to 4 gamepads (indices 0-3)
        for (int i = 0; i < 4; i++)
        {
            if (!JSInterop.IsGamepadConnected(i))
                continue;

            string name = JSInterop.GetGamepadName(i);
            if (string.IsNullOrEmpty(name))
                continue;

            bool hasStandardMapping = JSInterop.HasStandardMapping(i);

            if (hasStandardMapping)
            {
                // Found a device with standard mapping - this is definitely a gamepad
                if (standardMappingIndex < 0)
                {
                    standardMappingIndex = i;
                    _logger.LogDebug("Found gamepad with standard mapping: {Name} at index {Index}", name, i);
                }
            }
            else if (fallbackIndex < 0 && GetGamepadPriority(name) >= 0)
            {
                // Found a non-standard device that passes the name filter - use as fallback
                fallbackIndex = i;
                _logger.LogDebug("Found potential gamepad without standard mapping: {Name} at index {Index}", name, i);
            }
            else if (GetGamepadPriority(name) < 0)
            {
                _logger.LogDebug("Skipping non-gamepad device: {Name} at index {Index}", name, i);
            }
        }

        // Prefer standard mapping devices, fall back to name-filtered devices
        return standardMappingIndex >= 0 ? standardMappingIndex : fallbackIndex;
    }

    // Exclude known non-gamepad devices (audio adapters, etc.)
    // This list is only used for non-standard mapping devices as a fallback
    private static readonly string[] s_excludePatterns = [
            "audeze",
            "jabra",
            "headphone",
            "headset",
            "audio",
            "microphone",
            "speaker",
            "speakerphone",
            "sound",
            "realtek",
            "corsair void",
            "steelseries arctis",
            "hyperx cloud",
            "logitech g pro headset",
            "razer kraken",
            "astro a",
            "conference",
            "webcam",
            "camera",
        ];

    /// <summary>
    /// Gets the priority of a gamepad based on its name.
    /// This is a fallback filter for devices that don't have standard mapping.
    /// Returns -1 for devices that should be excluded (not gamepads).
    /// </summary>
    private static int GetGamepadPriority(string name)
    {
        string nameLower = name.ToLowerInvariant();

        foreach (var pattern in s_excludePatterns)
        {
            if (nameLower.Contains(pattern))
                return -1; // Exclude this device
        }

        // Accept any device that passed the exclusion filter
        return 1;
    }

    public void Update()
    {
        if (!IsInitialized || !s_jsModuleLoaded)
            return;

        // Check for gamepad connection changes periodically, not every frame
        _framesSinceLastDetect++;
        if (_framesSinceLastDetect >= DetectGamepadIntervalFrames)
        {
            _framesSinceLastDetect = 0;
            DetectGamepad();
        }

        if (_gamepadIndex < 0)
            return;

        // Clear previous state
        ButtonsDown.Clear();

        // Get pressed buttons from JavaScript
        string pressedButtonsStr = JSInterop.GetPressedButtons(_gamepadIndex);
        if (!string.IsNullOrEmpty(pressedButtonsStr))
        {
            foreach (string buttonIndexStr in pressedButtonsStr.Split(','))
            {
                if (int.TryParse(buttonIndexStr, out int buttonIndex))
                {
                    var mappedButton = MapBrowserButtonToGamepadButton(buttonIndex);
                    if (mappedButton.HasValue)
                    {
                        ButtonsDown.Add(mappedButton.Value);
                    }
                }
            }
        }

        // Check left stick for D-Pad emulation
        CheckStickAsButtons();
    }

    private void CheckStickAsButtons()
    {
        if (_gamepadIndex < 0)
            return;

        float leftX = JSInterop.GetAxisValue(_gamepadIndex, StandardAxes.LeftStickX);
        float leftY = JSInterop.GetAxisValue(_gamepadIndex, StandardAxes.LeftStickY);

        // Only add stick directions if D-Pad is not already pressed (D-Pad takes priority)
        if (!ButtonsDown.Contains(GamepadButton.DPadLeft) && !ButtonsDown.Contains(GamepadButton.DPadRight))
        {
            if (leftX < -StickThreshold)
                ButtonsDown.Add(GamepadButton.DPadLeft);
            else if (leftX > StickThreshold)
                ButtonsDown.Add(GamepadButton.DPadRight);
        }

        if (!ButtonsDown.Contains(GamepadButton.DPadUp) && !ButtonsDown.Contains(GamepadButton.DPadDown))
        {
            if (leftY < -StickThreshold)
                ButtonsDown.Add(GamepadButton.DPadUp);
            else if (leftY > StickThreshold)
                ButtonsDown.Add(GamepadButton.DPadDown);
        }
    }

    private static GamepadButton? MapBrowserButtonToGamepadButton(int browserButtonIndex)
    {
        // Map W3C Standard Gamepad button indices to our GamepadButton enum
        return browserButtonIndex switch
        {
            StandardButtons.A => GamepadButton.A,
            StandardButtons.B => GamepadButton.B,
            StandardButtons.X => GamepadButton.X,
            StandardButtons.Y => GamepadButton.Y,
            StandardButtons.LeftBumper => GamepadButton.LeftBumper,
            StandardButtons.RightBumper => GamepadButton.RightBumper,
            StandardButtons.LeftTrigger => GamepadButton.LeftTrigger,
            StandardButtons.RightTrigger => GamepadButton.RightTrigger,
            StandardButtons.Back => GamepadButton.Back,
            StandardButtons.Start => GamepadButton.Start,
            StandardButtons.LeftStick => GamepadButton.LeftStick,
            StandardButtons.RightStick => GamepadButton.RightStick,
            StandardButtons.DPadUp => GamepadButton.DPadUp,
            StandardButtons.DPadDown => GamepadButton.DPadDown,
            StandardButtons.DPadLeft => GamepadButton.DPadLeft,
            StandardButtons.DPadRight => GamepadButton.DPadRight,
            StandardButtons.Guide => GamepadButton.Guide,
            _ => null // Unknown button
        };
    }

    public bool IsButtonDown(GamepadButton button) => ButtonsDown.Contains(button);

    public void Cleanup()
    {
        ButtonsDown.Clear();
        _gamepadIndex = -1;
        GamepadName = null;
        IsInitialized = false;
    }

    public void Dispose()
    {
        Cleanup();
    }

    // ================================================================================
    // JavaScript Interop Methods
    // ================================================================================

    [SupportedOSPlatform("browser")]
    private static partial class JSInterop
    {
        [JSImport("getGamepadCount", "BrowserGamepad")]
        internal static partial int GetGamepadCount();

        [JSImport("getFirstGamepadIndex", "BrowserGamepad")]
        internal static partial int GetFirstGamepadIndex();

        [JSImport("isGamepadConnected", "BrowserGamepad")]
        internal static partial bool IsGamepadConnected(int index);

        [JSImport("getGamepadName", "BrowserGamepad")]
        internal static partial string GetGamepadName(int index);

        [JSImport("getPressedButtons", "BrowserGamepad")]
        internal static partial string GetPressedButtons(int index);

        [JSImport("getAxes", "BrowserGamepad")]
        internal static partial string GetAxes(int index);

        [JSImport("getAxisValue", "BrowserGamepad")]
        internal static partial float GetAxisValue(int gamepadIndex, int axisIndex);

        [JSImport("isButtonPressed", "BrowserGamepad")]
        internal static partial bool IsButtonPressed(int gamepadIndex, int buttonIndex);

        [JSImport("getButtonCount", "BrowserGamepad")]
        internal static partial int GetButtonCount(int index);

        [JSImport("getAxesCount", "BrowserGamepad")]
        internal static partial int GetAxesCount(int index);

        [JSImport("hasStandardMapping", "BrowserGamepad")]
        internal static partial bool HasStandardMapping(int index);
    }
}
