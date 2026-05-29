namespace Highbyte.DotNet6502.Systems.Vic20.TimerAndPeripheral;

/// <summary>
/// VIA #1 io chip addresses: $9110-$911F
/// VIA #2 io chip addresses: $9120-$912F
/// Ref: VIC-20 Programmer's Reference Guide, Chapter 3
/// </summary>
public static class ViaAddr
{
    // --------------------
    // VIA #1
    // --------------------

    // VIA #1 Port B (keyboard cols via VIA2) / Port A (keyboard rows)
    public const ushort VIA1_PORTB      = 0x9110;
    public const ushort VIA1_PORTA      = 0x9111;
    public const ushort VIA1_DDRB       = 0x9112;
    public const ushort VIA1_DDRA       = 0x9113;

    // VIA #1 Timer 1 (used by KERNAL for 60 Hz keyboard scan + cursor blink)
    public const ushort VIA1_T1CL       = 0x9114; // Timer 1 counter low  (read)
    public const ushort VIA1_T1CH       = 0x9115; // Timer 1 counter high (read clears IFR T1 flag)
    public const ushort VIA1_T1LL       = 0x9116; // Timer 1 latch low
    public const ushort VIA1_T1LH       = 0x9117; // Timer 1 latch high (write starts timer)

    // VIA #1 Timer 2
    public const ushort VIA1_T2CL       = 0x9118;
    public const ushort VIA1_T2CH       = 0x9119;

    // VIA #1 Auxiliary / Peripheral Control + Interrupt registers
    public const ushort VIA1_SR         = 0x911A;
    public const ushort VIA1_ACR        = 0x911B; // Auxiliary Control Register (bit 6 = T1 free-run)
    public const ushort VIA1_PCR        = 0x911C;
    public const ushort VIA1_IFR        = 0x911D; // Interrupt Flag Register
    public const ushort VIA1_IER        = 0x911E; // Interrupt Enable Register
    public const ushort VIA1_PORTA_NHS  = 0x911F; // Port A, no handshake

    // --------------------
    // VIA #2
    // --------------------

    // VIA #2 Port B (keyboard column strobe output) / Port A (user port / serial)
    public const ushort VIA2_PORTB      = 0x9120;
    public const ushort VIA2_PORTA      = 0x9121;
    public const ushort VIA2_DDRB       = 0x9122;
    public const ushort VIA2_DDRA       = 0x9123;

    // VIA #2 Timer 1
    public const ushort VIA2_T1CL       = 0x9124;
    public const ushort VIA2_T1CH       = 0x9125;
    public const ushort VIA2_T1LL       = 0x9126;
    public const ushort VIA2_T1LH       = 0x9127;

    // VIA #2 Timer 2
    public const ushort VIA2_T2CL       = 0x9128;
    public const ushort VIA2_T2CH       = 0x9129;

    // VIA #2 Auxiliary / Peripheral Control + Interrupt registers
    public const ushort VIA2_SR         = 0x912A;
    public const ushort VIA2_ACR        = 0x912B;
    public const ushort VIA2_PCR        = 0x912C;
    public const ushort VIA2_IFR        = 0x912D;
    public const ushort VIA2_IER        = 0x912E;
    public const ushort VIA2_PORTA_NHS  = 0x912F;
}
