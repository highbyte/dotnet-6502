namespace Highbyte.DotNet6502.Systems.Vic20.TimerAndPeripheral;

/// <summary>
/// VIA #2 (at $9120-$912F) — keyboard / joystick VIA.
///
/// Handles:
///   - Port B ($9120): keyboard column strobe — KERNAL writes active-low column mask here.
///   - Port A ($9121): keyboard row sense — KERNAL reads active-low row bitmask here.
///     Both ports forward to <see cref="Via1.Keyboard"/> which holds the key state.
///
/// All other registers (Timer 1/2, SR, ACR, PCR, IFR/IER) are coherent stubs —
/// the KERNAL writes to them (e.g. VIA2 Timer 1 for IEC serial timing) but the
/// emulator does not need to service them for basic keyboard/cursor operation.
/// </summary>
public class Via2 : ViaBase
{
    private readonly Via1 _via1; // column select is forwarded to VIA1's keyboard

    public Via2(Vic20 vic20, Via1 via1)
        : base(vic20, new ViaIRQ(useNMI: false))
    {
        _via1 = via1;
    }

    // VIA2 has no timer that needs processing for basic keyboard operation.
    public override void ProcessTimers(ulong cyclesExecuted) { }

    public override void MapIOLocations(Memory mem)
    {
        // Port B — keyboard column strobe (KERNAL writes here to select columns).
        mem.MapReader(ViaAddr.VIA2_PORTB, PortBLoad);
        mem.MapWriter(ViaAddr.VIA2_PORTB, PortBStore);

        // Port A — keyboard row sense (KERNAL reads rows here after writing column to Port B).
        mem.MapReader(ViaAddr.VIA2_PORTA,     PortALoad);
        mem.MapWriter(ViaAddr.VIA2_PORTA,     StubStore);
        mem.MapReader(ViaAddr.VIA2_PORTA_NHS, PortALoad);
        mem.MapWriter(ViaAddr.VIA2_PORTA_NHS, StubStore);

        // DDR
        mem.MapReader(ViaAddr.VIA2_DDRA, DDRALoad);
        mem.MapWriter(ViaAddr.VIA2_DDRA, DDRAStore);
        mem.MapReader(ViaAddr.VIA2_DDRB, DDRBLoad);
        mem.MapWriter(ViaAddr.VIA2_DDRB, DDRBStore);

        // IFR / IER — stub (VIA2 IRQs not used for keyboard).
        mem.MapReader(ViaAddr.VIA2_IFR, StubLoad);
        mem.MapWriter(ViaAddr.VIA2_IFR, StubStore);
        mem.MapRW(ViaAddr.VIA2_IER, new Memory.MemValue());

        // All remaining registers — write-verify coherent stubs (KERNAL reads back what it wrote).
        mem.MapRW(ViaAddr.VIA2_T1CL, new Memory.MemValue());
        mem.MapRW(ViaAddr.VIA2_T1CH, new Memory.MemValue());
        mem.MapRW(ViaAddr.VIA2_T1LL, new Memory.MemValue());
        mem.MapRW(ViaAddr.VIA2_T1LH, new Memory.MemValue());
        mem.MapRW(ViaAddr.VIA2_T2CL, new Memory.MemValue());
        mem.MapRW(ViaAddr.VIA2_T2CH, new Memory.MemValue());
        mem.MapRW(ViaAddr.VIA2_SR,   new Memory.MemValue());
        mem.MapRW(ViaAddr.VIA2_ACR,  new Memory.MemValue());
        mem.MapRW(ViaAddr.VIA2_PCR,  new Memory.MemValue());
    }

    private byte _portBValue = 0xFF;
    private byte PortALoad(ushort _) => _via1.Keyboard.GetPressedRowsForSelectedColumns();
    private byte PortBLoad(ushort _) => _portBValue;
    private void PortBStore(ushort _, byte value)
    {
        _portBValue = value;
        _via1.Keyboard.SetSelectedColumns(value);
    }
}
