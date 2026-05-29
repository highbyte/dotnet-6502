namespace Highbyte.DotNet6502.Systems.Vic20.TimerAndPeripheral;

/// <summary>
/// VIA 6522 interrupt flag and enable state, modelled after the C64's CiaIRQ but with
/// VIA-correct behaviour:
/// - IFR is NOT auto-cleared on read (must write 1 to a bit to clear it, or read T1C-H for T1).
/// - IER bit 7 always reads as 1; writing with bit 7 set enables, bit 7 clear disables.
///
/// Implements Timer 1 (bit 6) and CA1 (bit 1).
/// On VIA1, CA1 is connected to the VIC-I raster interrupt output — this is the
/// 50/60 Hz signal that drives the KERNAL keyboard scan and cursor blink (not Timer 1).
/// </summary>
public class ViaIRQ
{
    private readonly bool _useNMI;

    // Timer 1 interrupt state
    private bool _timer1Enabled;
    private bool _timer1Condition;

    // CA1 interrupt state (VIA1: VIC-I raster signal; drives keyboard scan + cursor blink)
    private bool _ca1Enabled;
    private bool _ca1Condition;

    public const int CA1BitPos    = 1;   // IFR/IER bit 1 = CA1
    public const int Timer1BitPos = 6;   // IFR/IER bit 6 = Timer 1
    public const int AnyBitPos    = 7;   // IFR bit 7 = any enabled + triggered source

    public ViaIRQ(bool useNMI) => _useNMI = useNMI;

    public void Trigger(CPU cpu, string source)
    {
        if (_useNMI)
            cpu.CPUInterrupts.SetNMISourceActive(source);
        else
            cpu.CPUInterrupts.SetIRQSourceActive(source, autoAcknowledge: true);
    }

    public bool IsTimer1Enabled   => _timer1Enabled;
    public bool IsTimer1Triggered => _timer1Condition;
    public bool IsCA1Enabled      => _ca1Enabled;
    public bool IsCA1Triggered    => _ca1Condition;

    public void SetTimer1Condition()   => _timer1Condition = true;
    public void ClearTimer1Condition() => _timer1Condition = false;

    /// <summary>
    /// Raise the CA1 interrupt flag. Call when the VIC-I raster pulse fires (once per frame).
    /// Clears automatically when the CPU reads Port A (AcknowledgeCA1).
    /// </summary>
    public void SetCA1Condition()   => _ca1Condition = true;
    public void ClearCA1Condition() => _ca1Condition = false;

    // IFR read — NOT cleared on read (VIA behaviour).
    // Bit 7 is set when any enabled source has triggered.
    public byte ReadIFR()
    {
        byte ifr = 0;
        if (_timer1Condition) ifr |= (1 << Timer1BitPos);
        if (_ca1Condition)    ifr |= (1 << CA1BitPos);
        bool anyActive = (_timer1Condition && _timer1Enabled) || (_ca1Condition && _ca1Enabled);
        if (anyActive) ifr |= (1 << AnyBitPos);
        return ifr;
    }

    // IFR write — writing a 1 to a bit clears that flag.
    public void WriteIFR(byte value)
    {
        if ((value & (1 << Timer1BitPos)) != 0) _timer1Condition = false;
        if ((value & (1 << CA1BitPos))    != 0) _ca1Condition    = false;
    }

    // IER read — bit 7 always reads as 1 (VIA behaviour).
    public byte ReadIER()
    {
        byte ier = (1 << AnyBitPos);
        if (_timer1Enabled) ier |= (1 << Timer1BitPos);
        if (_ca1Enabled)    ier |= (1 << CA1BitPos);
        return ier;
    }

    // IER write — bit 7 = 1 → set enables; bit 7 = 0 → clear enables.
    public void WriteIER(byte value)
    {
        bool set = (value & (1 << AnyBitPos)) != 0;
        if ((value & (1 << Timer1BitPos)) != 0)
            _timer1Enabled = set;
        if ((value & (1 << CA1BitPos)) != 0)
            _ca1Enabled = set;
    }
}
