namespace Highbyte.DotNet6502.Systems.Input;

/// <summary>
/// Host-agnostic <em>physical</em> keyboard key.
///
/// This is the keyboard counterpart to <see cref="GamepadButton"/>: an abstraction that every
/// host technology (Silk.NET/GLFW, Avalonia, SadConsole/XNA, browser WASM) translates its native
/// key representation into, so that system-specific input handlers never depend on a host's key
/// type.
///
/// The members are <b>physical</b> keys (the key in a given position on a US-QWERTY keyboard),
/// not characters — exactly like the W3C UI Events <c>KeyboardEvent.code</c> values, whose names
/// this enum mirrors (<c>KeyA</c>, <c>Digit0</c>, <c>Backquote</c>, <c>BracketLeft</c>,
/// <c>IntlBackslash</c>, <c>ArrowUp</c>, ...). The physical key set is standardized and bounded
/// (USB HID usage tables / W3C <c>code</c>) and does not vary between macOS and Windows — only
/// modifier naming differs (e.g. Cmd vs. Win), which is covered by the explicit left/right
/// <c>Meta</c>/<c>Alt</c> members.
///
/// The set is intentionally scoped to keys that emulated systems actually map from; extend it as
/// needed. The string form (<see cref="System.Enum.ToString()"/>) is the same vocabulary used by
/// the string-based input injection API (remote control / scripting).
/// </summary>
public enum HostKey
{
    None = 0,

    // Letters
    KeyA, KeyB, KeyC, KeyD, KeyE, KeyF, KeyG, KeyH, KeyI, KeyJ, KeyK, KeyL, KeyM,
    KeyN, KeyO, KeyP, KeyQ, KeyR, KeyS, KeyT, KeyU, KeyV, KeyW, KeyX, KeyY, KeyZ,

    // Digit row
    Digit0, Digit1, Digit2, Digit3, Digit4, Digit5, Digit6, Digit7, Digit8, Digit9,

    // Whitespace / editing
    Space, Enter, Tab, Backspace, Escape,

    // Function keys
    F1, F2, F3, F4, F5, F6, F7, F8, F9, F10, F11, F12,

    // Navigation / editing cluster
    Insert, Delete, Home, End, PageUp, PageDown,
    ArrowUp, ArrowDown, ArrowLeft, ArrowRight,

    // Modifiers
    ShiftLeft, ShiftRight,
    ControlLeft, ControlRight,
    AltLeft, AltRight,
    MetaLeft, MetaRight,
    CapsLock,

    // Punctuation (named by physical US-QWERTY position, per W3C code)
    Backquote,      // ` ~  (left of Digit1)
    Minus,          // - _
    Equal,          // = +
    BracketLeft,    // [ {
    BracketRight,   // ] }
    Backslash,      // \ |
    Semicolon,      // ; :
    Quote,          // ' "
    Comma,          // , <
    Period,         // . >
    Slash,          // / ?
    IntlBackslash,  // extra key left of Z on ISO/intl keyboards (< >)

    // Numpad
    Numpad0, Numpad1, Numpad2, Numpad3, Numpad4,
    Numpad5, Numpad6, Numpad7, Numpad8, Numpad9,
    NumpadAdd, NumpadSubtract, NumpadMultiply, NumpadDivide,
    NumpadDecimal, NumpadEnter,
}
