using System.Diagnostics;
using Highbyte.DotNet6502.Instructions;
using Microsoft.Extensions.Logging;
using static Highbyte.DotNet6502.Systems.Commodore64.TimerAndPeripheral.C64Joystick;

namespace Highbyte.DotNet6502.Systems.Commodore64.TimerAndPeripheral;

/// <summary>
/// C64 keyboard matrix with keys assigned to their position.
/// </summary>
public class C64Keyboard
{
    private readonly C64Key[,] _matrix;
    private readonly List<C64Key> _pressedKeys = new List<C64Key>();
    private readonly C64 _c64;
    private List<int> _selectedMatrixRowBitPositions = new();
    private bool _capsLockOn;
    private readonly ILogger<C64Keyboard> _logger;

    public C64Keyboard(C64 c64, ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<C64Keyboard>();

        _matrix = new C64Key[,]
        {
            //Rows: Bits 0-7, write 0 one of the bits in $DC00
            //Cols: Bits 0-7, read from $DC01, every bit with value 0 (of the selected row in $DC00) means that the corresponding key is pressed

            //Col 0             1               2               3               4               5               6               7
            //Row 0
            { C64Key.Delete,    C64Key.Return,  C64Key.CrsrRt,  C64Key.F7,      C64Key.F1,      C64Key.F3,      C64Key.F5,      C64Key.CrsrDn },
            //Row 1
            { C64Key.Three,     C64Key.W,       C64Key.A,       C64Key.Four,    C64Key.Z,       C64Key.S,       C64Key.E,       C64Key.LShift },
            //Row 2
            { C64Key.Five,      C64Key.R,       C64Key.D,       C64Key.Six,     C64Key.C,       C64Key.F,       C64Key.T,       C64Key.X },
            //Row 3
            { C64Key.Seven,     C64Key.Y,       C64Key.G,       C64Key.Eight,   C64Key.B,       C64Key.H,       C64Key.U,       C64Key.V },
            //Row 4
            { C64Key.Nine,      C64Key.I,       C64Key.J,       C64Key.Zero,    C64Key.M,       C64Key.K,       C64Key.O,       C64Key.N },
            //Row 5
            { C64Key.Plus,      C64Key.P,       C64Key.L,       C64Key.Minus,   C64Key.Period,  C64Key.Colon,   C64Key.At,      C64Key.Comma },
            //Row 6
            { C64Key.Lira,      C64Key.Astrix,  C64Key.Semicol, C64Key.Home,    C64Key.RShift,  C64Key.Equal,   C64Key.UArrow,  C64Key.Slash},
            //Row 7
            { C64Key.One,       C64Key.LArrow,  C64Key.Ctrl,    C64Key.Two,     C64Key.Space,   C64Key.CBM,     C64Key.Q,       C64Key.Stop},
        };
        _c64 = c64;
    }

    /// <summary>
    /// Set currently pressed keys from the host system
    /// </summary>
    /// <param name="key"></param>
    public void SetKeysPressed(List<C64Key> keys, bool restorePressed, bool capsLockOn)
    {
        // Check for special key: Restore
        if (restorePressed)
        {
            SetRestoreKeyPressed();
            _logger.LogDebug($"C64 restore key pressed, NMI is invokded.");
        }

        // Set pressed keys
        _pressedKeys.Clear();
        foreach (var key in keys)
            _pressedKeys.Add(key);
        if (keys.Count > 0)
            _logger.LogDebug($"C64 keys pressed: {string.Join(",", keys)}");

        // Check for special key: Caps lock
        // Not connected to the C64 keyboard matrix, it's connected to the left shift key (keeping it pressed)
        if (capsLockOn != _capsLockOn)
            _logger.LogDebug($"C64 caps lock changed to: {capsLockOn}");
        _capsLockOn = capsLockOn;
        if (capsLockOn)
            _pressedKeys.Add(C64Key.LShift);

        // Handle joystick actions via keyboard presses
        HandleJoystickKeyboard();
    }

    /// <summary>
    /// Tells the system that the RESTORE key is pressed, which is isn't in the Keyboard matrix,
    /// but intead raises an NMI interrupt every time it's pressed.
    /// </summary>
    public void SetRestoreKeyPressed()
    {
        _c64.CPU.CPUInterrupts.SetNMISourceActive("KeyboardReset");
    }

    /// <summary>
    /// Set the value of $DC00
    /// </summary>
    /// <param name="selectedMatrixRow"></param>
    public void SetSelectedMatrixRow(byte selectedMatrixRow)
    {
        _c64.WriteIOStorage(CiaAddr.CIA1_DATAA, selectedMatrixRow);

        // Get the position of the first bit in _selectedMatrixRow with value 0.
        // This is the position of the pressed key. 
        _selectedMatrixRowBitPositions.Clear();
        byte mask = 0b00000001;
        for (var col = 0; col < 8; col++)
        {
            if ((selectedMatrixRow & mask) == 0)    // Check if bit is clear
            {
                _selectedMatrixRowBitPositions.Add(col);
            }
            mask <<= 1;
        }
    }
    /// <summary>
    /// Return the value of $DC00
    /// </summary>
    /// <returns></returns>
    public byte GetSelectedMatrixRow()
    {
        return _c64.ReadIOStorage(CiaAddr.CIA1_DATAA);
    }

    /// <summary>
    /// Return the value of $DC01
    /// </summary>
    /// <returns></returns>
    public byte GetPressedKeysForSelectedMatrixRow()
    {
        byte pressedKeys = 0xff;    // All bits set to 1 means no keys pressed.

        foreach (var row in _selectedMatrixRowBitPositions)
        {
            for (var col = 0; col < 8; col++)
            {
                if (_pressedKeys.Contains(_matrix[row, col]))
                    pressedKeys.ClearBit(col);
            }
        }
        return pressedKeys;
    }

    /// <summary>
    /// Move joystick based on keyboard input (if configured).
    /// </summary>
    private void HandleJoystickKeyboard()
    {
        var joystick = _c64.Cia.Joystick;
        if (joystick.KeyboardJoystickEnabled)
        {
            var joystick1KeyboardMap = joystick.KeyboardJoystickMap.KeyToJoystick1Map;
            var joystick1Actions = new HashSet<C64JoystickAction>();
            foreach (var c64Key in joystick1KeyboardMap.Keys)
            {
                if (_pressedKeys.Contains(c64Key))
                    joystick1Actions.Add(joystick1KeyboardMap[c64Key]);
            }
            joystick.SetJoystick1Actions(joystick1Actions);

            var joystick2KeyboardMap = joystick.KeyboardJoystickMap.KeyToJoystick2Map;
            var joystick2Actions = new HashSet<C64JoystickAction>();
            foreach (var c64Key in joystick2KeyboardMap.Keys)
            {
                if (_pressedKeys.Contains(c64Key))
                    joystick2Actions.Add(joystick2KeyboardMap[c64Key]);
            }
            joystick.SetJoystick2Actions(joystick2Actions);
        }
    }
}

public enum C64Key
{
    None,
    Space,
    A,
    B,
    C,
    D,
    E,
    F,
    G,
    H,
    I,
    J,
    K,
    L,
    M,
    N,
    O,
    P,
    Q,
    R,
    S,
    T,
    U,
    V,
    W,
    X,
    Y,
    Z,
    One,
    Two,
    Three,
    Four,
    Five,
    Six,
    Seven,
    Eight,
    Nine,
    Zero,
    Plus,
    Minus,
    Astrix,     // Asterix
    Slash,
    Colon,
    Semicol,    // Semi colon
    Equal,
    Period,
    Comma,
    At,
    Lira,       // TODO: Or is it pound?
    LArrow,     // Left arrow. TODO: what is this used for?
    UArrow,     // Up arrow. Similar use a "caret" (^) on modern keyboards

    // Non printable keys
    Stop,
    CBM,    // Commodore key
    Ctrl,
    RShift,
    Home,
    LShift,
    CrsrDn, // Cursor down
    CrsrRt, // Cursor right
    Return,
    Delete,
    F1,
    F3,
    F5,
    F7,
}
