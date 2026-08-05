namespace Highbyte.DotNet6502.Systems.Apple2.Peripherals;

/// <summary>
/// The Apple II keyboard latch.
///
/// There is no keyboard matrix to scan and no interrupt: the encoder latches the ASCII code of
/// the last key pressed and raises a strobe bit. Software polls $C000 until bit 7 is set, reads
/// the code from bits 6-0, and touches $C010 to clear the strobe.
/// </summary>
public class Apple2Keyboard
{
    private byte _latch;

    /// <summary>Raw latch contents: ASCII in bits 6-0, strobe in bit 7.</summary>
    public byte Latch => _latch;

    /// <summary>Whether a key press is waiting to be read.</summary>
    public bool StrobeSet => (_latch & 0x80) != 0;

    /// <summary>Latches an ASCII code and raises the strobe.</summary>
    public void KeyPressed(byte ascii) => _latch = (byte)((ascii & 0x7F) | 0x80);

    /// <summary>Reads $C000: the latch, strobe included. No side effect.</summary>
    public byte ReadData() => _latch;

    /// <summary>Accesses $C010: clears the strobe and returns the pre-clear latch value.</summary>
    public byte ReadAndClearStrobe()
    {
        var value = _latch;
        ClearStrobe();
        return value;
    }

    /// <summary>Clears the strobe, keeping the last ASCII code readable.</summary>
    public void ClearStrobe() => _latch &= 0x7F;

    public void Reset() => _latch = 0;
}
