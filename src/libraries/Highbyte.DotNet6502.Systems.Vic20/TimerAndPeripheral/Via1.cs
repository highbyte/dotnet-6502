using Microsoft.Extensions.Logging;

namespace Highbyte.DotNet6502.Systems.Vic20.TimerAndPeripheral;

/// <summary>
/// VIA #1 (at $9110-$911F) — user port / serial bus VIA.
///
/// Handles:
///   - CA1 interrupt: driven by the VIC-I raster pulse (simulated once per frame).
///     The KERNAL enables CA1 (IER bit 1 = $82) and uses this 50/60 Hz interrupt for
///     keyboard scan and cursor blink — NOT Timer 1, which the KERNAL leaves unused here.
///   - Port A ($9111): user port / serial bus lines — returns $FF (all idle, no device).
///   - IFR/IER: interrupt flag/enable registers (CA1 + Timer 1 implemented).
///   - Timer 1: present but not used by the KERNAL for keyboard timing.
///
/// The keyboard matrix is wired to VIA #2 (Port B = columns, Port A = rows).
/// The <see cref="Vic20Keyboard"/> object lives here because it owns the key state;
/// VIA2 forwards column writes and row reads to it via this reference.
/// </summary>
public class Via1 : ViaBase
{
    public Vic20Keyboard Keyboard { get; }

    public Via1(Vic20 vic20, ILoggerFactory loggerFactory)
        : base(vic20, new ViaIRQ(useNMI: false))
    {
        Keyboard = new Vic20Keyboard(loggerFactory);
    }

    public override void MapIOLocations(Memory mem)
    {
        // Port A — keyboard row sense (read clears CA1 flag — VIA 6522 handshake behaviour)
        mem.MapReader(ViaAddr.VIA1_PORTA,     PortALoad);
        mem.MapWriter(ViaAddr.VIA1_PORTA,     StubStore);  // writes to PA not used for keyboard

        // Port B — not used for keyboard on VIA1 (VIA2 drives columns); stub.
        mem.MapReader(ViaAddr.VIA1_PORTB,     StubLoad);
        mem.MapWriter(ViaAddr.VIA1_PORTB,     StubStore);

        // DDR
        mem.MapReader(ViaAddr.VIA1_DDRA,      DDRALoad);
        mem.MapWriter(ViaAddr.VIA1_DDRA,      DDRAStore);
        mem.MapReader(ViaAddr.VIA1_DDRB,      DDRBLoad);
        mem.MapWriter(ViaAddr.VIA1_DDRB,      DDRBStore);

        // Timer 1
        mem.MapReader(ViaAddr.VIA1_T1CL,      T1CLLoad);
        mem.MapWriter(ViaAddr.VIA1_T1CL,      T1LLStore);  // T1C-L write = latch low only
        mem.MapReader(ViaAddr.VIA1_T1CH,      T1CHLoad);   // side-effect: clears IFR T1
        mem.MapWriter(ViaAddr.VIA1_T1CH,      T1CHStore);  // T1C-H write = load counter + start timer
        mem.MapReader(ViaAddr.VIA1_T1LL,      T1LLLoad);
        mem.MapWriter(ViaAddr.VIA1_T1LL,      T1LLStore);
        mem.MapReader(ViaAddr.VIA1_T1LH,      T1LHLoad);
        mem.MapWriter(ViaAddr.VIA1_T1LH,      T1LHStore);  // T1L-H write = latch only, no start

        // IFR / IER
        mem.MapReader(ViaAddr.VIA1_IFR,       IFRLoad);
        mem.MapWriter(ViaAddr.VIA1_IFR,       IFRStore);
        mem.MapReader(ViaAddr.VIA1_IER,       IERLoad);
        mem.MapWriter(ViaAddr.VIA1_IER,       IERStore);

        // ACR (controls T1 free-run vs one-shot)
        mem.MapReader(ViaAddr.VIA1_ACR,       ACRLoad);
        mem.MapWriter(ViaAddr.VIA1_ACR,       ACRStore);

        // Unimplemented registers — write-verify coherent stubs (KERNAL reads back what it wrote).
        mem.MapRW(ViaAddr.VIA1_T2CL, new Memory.MemValue());
        mem.MapRW(ViaAddr.VIA1_T2CH, new Memory.MemValue());
        mem.MapRW(ViaAddr.VIA1_SR,   new Memory.MemValue());
        mem.MapRW(ViaAddr.VIA1_PCR,  new Memory.MemValue());
        mem.MapReader(ViaAddr.VIA1_PORTA_NHS, PortALoadNoHandshake);  // no CA1 clear
        mem.MapWriter(ViaAddr.VIA1_PORTA_NHS, StubStore);
    }

    // VIA1 Port A is the user port / serial bus — return $FF (all lines idle / no device).
    // Reading with handshake (PORTA) also clears the CA1 IFR flag.
    private byte PortALoad(ushort _)
    {
        AcknowledgeCA1();
        return 0xFF;
    }

    private byte PortALoadNoHandshake(ushort _) => 0xFF;
}
