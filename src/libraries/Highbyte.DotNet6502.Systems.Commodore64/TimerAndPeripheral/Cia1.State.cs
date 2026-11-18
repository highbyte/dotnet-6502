namespace Highbyte.DotNet6502.Systems.Commodore64.TimerAndPeripheral;

public partial class Cia1
{
    // CIA1's data-port and data-direction registers are cached in these private fields (the
    // keyboard scan reads them via DataALoad/DataBLoad), not in the C64 IO register storage. They
    // must be restored or the KERNAL keyboard scan drives no columns (e.g. _ddra defaults to 0
    // instead of the $FF the KERNAL sets), so no key presses are detected.
    internal (byte PortA, byte PortB, byte Ddra, byte Ddrb) GetSnapshotPortState()
        => (_portA, _portB, _ddra, _ddrb);

    internal void RestoreSnapshotPortState(byte portA, byte portB, byte ddra, byte ddrb)
    {
        _portA = portA;
        _portB = portB;
        _ddra = ddra;
        _ddrb = ddrb;
    }
}
