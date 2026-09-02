namespace Highbyte.DotNet6502.CycleEnginePrototype;

/// <summary>
/// The representative instruction slice every candidate engine implements. One shape of each
/// kind that matters for cycle timing: immediate read, absolute read, indexed read with and
/// without page crossing, indexed store with its unconditional dummy read, read-modify-write in
/// both the NMOS and CMOS sequence, branch taken/not taken/across a page, implied with its
/// dummy read, stack push, subroutine call and both returns, plus hardware interrupt entry.
/// </summary>
public static class SliceOpcodes
{
    public const byte LdaImm = 0xA9;
    public const byte LdaAbs = 0xAD;
    public const byte LdaAbsX = 0xBD;
    public const byte StaAbsX = 0x9D;
    public const byte IncAbs = 0xEE;
    public const byte Bne = 0xD0;
    public const byte Nop = 0xEA;
    public const byte Pha = 0x48;
    public const byte Jsr = 0x20;
    public const byte Rts = 0x60;
    public const byte Rti = 0x40;

    public static readonly byte[] All = [LdaImm, LdaAbs, LdaAbsX, StaAbsX, IncAbs, Bne, Nop, Pha, Jsr, Rts, Rti];
}
