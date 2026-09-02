using Highbyte.DotNet6502.Utils;

namespace Highbyte.DotNet6502.CycleEnginePrototype;

/// <summary>
/// A loop built only from the slice, used by the benchmarks and the whole-program equivalence
/// tests. One iteration is 14 instructions (the subroutine's RTS included) and exercises every conditional cycle in the slice:
/// indexed read without and with page crossing, indexed store, RMW, an implied instruction, a
/// push, a subroutine call and return, a branch not taken, and a branch taken across a page.
/// The IRQ vector points at an RTI so the system stub's timer interrupt is survivable.
/// </summary>
public static class SliceProgram
{
    public const ushort LoopStart = 0x10F0;      // body crosses into $1100 so the back-branch crosses a page
    public const ushort DataBase = 0x2000;
    public const ushort Subroutine = 0x1F00;
    public const ushort IrqHandler = 0x1F80;
    public const int InstructionsPerIteration = 14;

    public static void Assemble(Memory mem, CPU cpu)
    {
        byte[] body =
        [
            SliceOpcodes.LdaImm, 0x01,
            SliceOpcodes.LdaAbs, 0x00, 0x20,          // LDA $2000
            SliceOpcodes.LdaAbsX, 0x00, 0x20,         // LDA $2000,X   X=1: no page cross
            SliceOpcodes.LdaAbsX, 0xFF, 0x20,         // LDA $20FF,X   X=1: page cross
            SliceOpcodes.StaAbsX, 0x00, 0x20,         // STA $2000,X
            SliceOpcodes.IncAbs, 0x01, 0x20,          // INC $2001
            SliceOpcodes.Nop,
            SliceOpcodes.Pha,
            SliceOpcodes.Jsr, 0x00, 0x1F,             // JSR $1F00
            SliceOpcodes.LdaImm, 0x00,
            SliceOpcodes.Bne, 0x02,                   // not taken (Z set)
            SliceOpcodes.LdaImm, 0x01,
            SliceOpcodes.Bne, 0x00,                   // back to LoopStart, patched below
        ];
        var branchEnd = LoopStart + body.Length;
        body[^1] = (byte)(LoopStart - branchEnd);

        for (var i = 0; i < body.Length; i++)
            mem[(ushort)(LoopStart + i)] = body[i];

        mem[Subroutine] = SliceOpcodes.Rts;
        mem[IrqHandler] = SliceOpcodes.Rti;
        mem.WriteWord(CPU.BrkIRQHandlerVector, IrqHandler);
        mem.WriteWord(CPU.NonMaskableIRQHandlerVector, IrqHandler);

        cpu.PC = LoopStart;
        cpu.X = 1;
        cpu.SP = 0xFF;
        cpu.ProcessorStatus.InterruptDisable = false;
    }
}
