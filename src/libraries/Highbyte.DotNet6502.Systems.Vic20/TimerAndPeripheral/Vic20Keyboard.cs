using Highbyte.DotNet6502.Utils;
using Microsoft.Extensions.Logging;

namespace Highbyte.DotNet6502.Systems.Vic20.TimerAndPeripheral;

/// <summary>
/// VIC-20 8×8 keyboard matrix.
///
/// Scan convention (matches VIC-20 KERNAL 901486-07):
///   Column drive  — KERNAL writes active-low column mask to VIA2 Port B ($9120).
///   Row sense     — KERNAL reads VIA2 Port A ($9121); a 0-bit means key is pressed in that row.
///
/// Matrix layout derived from the KERNAL decode table at $EC5E
/// (_matrix[sense_row, scan_col], indexed as Y = scan_col*8 + sense_row):
///
///           scan0  scan1  scan2   scan3    scan4   scan5   scan6   scan7
/// sense0:    1      ←      Ctrl   RunStop  Space   CBM     Q       2
/// sense1:    3      W      A      LShift   Z       S       E       4
/// sense2:    5      R      D      X        C       F       T       6
/// sense3:    7      Y      G      V        B       H       U       8
/// sense4:    9      I      J      N        M       K       O       0
/// sense5:    +      P      L      ,        .       :       @       -
/// sense6:    £      *      ;      /        RShift  =       ↑      Home
/// sense7:   Del   Ret    CrLR   CrUD      F1      F3      F5      F7
///
/// Notes:
///   CrLR = Cursor Left/Right physical key (unshifted = right, shift = left)
///   CrUD = Cursor Up/Down physical key (unshifted = down, shift = up)
///   £    = Pound sign character key
///   ←/↑  = Back Arrow / Up Arrow character keys (PETSCII $5F / $5E)
/// </summary>
public class Vic20Keyboard
{
    private readonly Vic20Key[,] _matrix = new Vic20Key[8, 8];
    private readonly List<Vic20Key> _pressedKeys = new();
    private byte _selectedColumnMask = 0xFF; // all columns deselected by default
    private readonly ILogger _logger;

    public Vic20Keyboard(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger(nameof(Vic20Keyboard));
        InitMatrix();
    }

    private void InitMatrix()
    {
        // Matrix positions from KERNAL 901486-07 decode table at $EC5E.
        // _matrix[sense_row, scan_col] where:
        //   scan_col = Port B bit driven low (0=PB0..7=PB7)
        //   sense_row = Port A bit sensed   (0=PA0..7=PA7)

        // scan col 0 (PB0 = 0): odd digits + symbols
        _matrix[0, 0] = Vic20Key.One;
        _matrix[1, 0] = Vic20Key.Three;
        _matrix[2, 0] = Vic20Key.Five;
        _matrix[3, 0] = Vic20Key.Seven;
        _matrix[4, 0] = Vic20Key.Nine;
        _matrix[5, 0] = Vic20Key.Plus;
        _matrix[6, 0] = Vic20Key.Pound;
        _matrix[7, 0] = Vic20Key.Delete;

        // scan col 1 (PB1 = 0)
        _matrix[0, 1] = Vic20Key.BackArrow; // ← character (PETSCII $5F)
        _matrix[1, 1] = Vic20Key.W;
        _matrix[2, 1] = Vic20Key.R;
        _matrix[3, 1] = Vic20Key.Y;
        _matrix[4, 1] = Vic20Key.I;
        _matrix[5, 1] = Vic20Key.P;
        _matrix[6, 1] = Vic20Key.Asterisk;
        _matrix[7, 1] = Vic20Key.Return;

        // scan col 2 (PB2 = 0)
        _matrix[0, 2] = Vic20Key.Ctrl;
        _matrix[1, 2] = Vic20Key.A;
        _matrix[2, 2] = Vic20Key.D;
        _matrix[3, 2] = Vic20Key.G;
        _matrix[4, 2] = Vic20Key.J;
        _matrix[5, 2] = Vic20Key.L;
        _matrix[6, 2] = Vic20Key.Semicolon;
        _matrix[7, 2] = Vic20Key.CrsrRight;

        // scan col 3 (PB3 = 0)
        _matrix[0, 3] = Vic20Key.RunStop;
        _matrix[1, 3] = Vic20Key.LShift;
        _matrix[2, 3] = Vic20Key.X;
        _matrix[3, 3] = Vic20Key.V;
        _matrix[4, 3] = Vic20Key.N;
        _matrix[5, 3] = Vic20Key.Comma;
        _matrix[6, 3] = Vic20Key.Slash;
        _matrix[7, 3] = Vic20Key.CrsrDown;

        // scan col 4 (PB4 = 0)
        _matrix[0, 4] = Vic20Key.Space;
        _matrix[1, 4] = Vic20Key.Z;
        _matrix[2, 4] = Vic20Key.C;
        _matrix[3, 4] = Vic20Key.B;
        _matrix[4, 4] = Vic20Key.M;
        _matrix[5, 4] = Vic20Key.Period;
        _matrix[6, 4] = Vic20Key.RShift;
        _matrix[7, 4] = Vic20Key.F1;

        // scan col 5 (PB5 = 0)
        _matrix[0, 5] = Vic20Key.CBM;
        _matrix[1, 5] = Vic20Key.S;
        _matrix[2, 5] = Vic20Key.F;
        _matrix[3, 5] = Vic20Key.H;
        _matrix[4, 5] = Vic20Key.K;
        _matrix[5, 5] = Vic20Key.Colon;
        _matrix[6, 5] = Vic20Key.Equal;
        _matrix[7, 5] = Vic20Key.F3;

        // scan col 6 (PB6 = 0)
        _matrix[0, 6] = Vic20Key.Q;
        _matrix[1, 6] = Vic20Key.E;
        _matrix[2, 6] = Vic20Key.T;
        _matrix[3, 6] = Vic20Key.U;
        _matrix[4, 6] = Vic20Key.O;
        _matrix[5, 6] = Vic20Key.At;
        _matrix[6, 6] = Vic20Key.UpArrow;
        _matrix[7, 6] = Vic20Key.F5;

        // scan col 7 (PB7 = 0): even digits + symbols
        _matrix[0, 7] = Vic20Key.Two;
        _matrix[1, 7] = Vic20Key.Four;
        _matrix[2, 7] = Vic20Key.Six;
        _matrix[3, 7] = Vic20Key.Eight;
        _matrix[4, 7] = Vic20Key.Zero;
        _matrix[5, 7] = Vic20Key.Minus;
        _matrix[6, 7] = Vic20Key.Home;
        _matrix[7, 7] = Vic20Key.F7;
    }

    /// <summary>Called by the host input consumer each frame with the current key state.</summary>
    public void SetKeysPressed(List<Vic20Key> keys, bool capsLockOn)
    {
        _pressedKeys.Clear();
        foreach (var key in keys)
            _pressedKeys.Add(key);
        if (capsLockOn)
            _pressedKeys.Add(Vic20Key.LShift);
        if (keys.Count > 0)
            _logger.LogTrace($"VIC-20 keys pressed: {string.Join(",", keys)}");
    }

    /// <summary>
    /// Called when KERNAL writes to VIA2 Port B ($9120) to select keyboard columns.
    /// Active-low: a 0-bit selects that column.
    /// </summary>
    public void SetSelectedColumns(byte columnMask) => _selectedColumnMask = columnMask;

    /// <summary>
    /// Called when KERNAL reads VIA1 Port A ($9111) to sense keyboard rows.
    /// Returns active-low byte — a 0-bit means that key is pressed in any selected column.
    /// </summary>
    public byte GetPressedRowsForSelectedColumns()
    {
        byte rows = 0xFF; // all bits set = no keys pressed

        for (int col = 0; col < 8; col++)
        {
            if ((_selectedColumnMask & (1 << col)) == 0) // column is selected (bit = 0)
            {
                for (int row = 0; row < 8; row++)
                {
                    if (_pressedKeys.Contains(_matrix[row, col]))
                        rows = (byte)(rows & ~(1 << row)); // clear row bit = key pressed
                }
            }
        }
        return rows;
    }

    public bool IsKeyPressed(Vic20Key key) => _pressedKeys.Contains(key);
}

public enum Vic20Key
{
    None,

    // Letters
    A, B, C, D, E, F, G, H, I, J, K, L, M,
    N, O, P, Q, R, S, T, U, V, W, X, Y, Z,

    // Digits
    Zero, One, Two, Three, Four, Five, Six, Seven, Eight, Nine,

    // Symbols
    At,         // @
    Plus,       // +
    Minus,      // -
    Asterisk,   // *
    Slash,      // /
    Equal,      // =
    Colon,      // :
    Semicolon,  // ;
    Comma,      // ,
    Period,     // .
    Pound,      // £ (PETSCII $5C)
    BackArrow,  // ← character (PETSCII $5F, not cursor left)
    UpArrow,    // ↑ character (PETSCII $5E, not cursor up)

    // Non-printable / special
    Space,
    Return,
    Delete,
    Home,
    RunStop,    // RUN/STOP key (PETSCII $03)
    Ctrl,
    LShift,
    RShift,
    CBM,        // Commodore key (PETSCII $04)
    CrsrRight,  // Cursor right physical key (shift = left)
    CrsrDown,   // Cursor down physical key (shift = up)

    // Function keys (shift = F2/F4/F6/F8)
    F1, F3, F5, F7,
}
