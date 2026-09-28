using Highbyte.DotNet6502.Utils;

namespace Highbyte.DotNet6502;

/// <summary>
/// Bespoke execute handlers whose behavior is identical on every CPU model: stack
/// pushes/pulls, subroutine and interrupt flow, and NOP. Static methods — no captured
/// state, no per-call allocation, AOT-safe. (Model-divergent bespoke handlers live in
/// NmosHandlers/CmosHandlers instead.)
///
/// Every cycle is a bus access: the documented dummy reads are performed as real memory
/// accesses so the number of accesses equals the cycle count (the SingleStepTests vectors
/// hold the sequences to the silicon).
/// </summary>
internal static class SharedHandlers
{
    /// <summary>
    /// Cycle 2 of every single-byte instruction reads the next opcode byte and discards it.
    /// </summary>
    internal static void DummyReadNextByte(CPU cpu, Memory mem) => cpu.FetchByte(mem, cpu.PC);

    /// <summary>
    /// Pulls and returns: after the next-byte dummy read, a cycle reads the stack at the
    /// current (not yet incremented) stack pointer.
    /// </summary>
    internal static void DummyReadNextByteAndStackTop(CPU cpu, Memory mem)
    {
        DummyReadNextByte(cpu, mem);
        cpu.FetchByte(mem, (ushort)(CPU.StackBaseAddress + cpu.SP));
    }

    /// <summary>$48 PHA: push A. 3 cycles.</summary>
    public static ulong Pha(CPU cpu, Memory mem)
    {
        DummyReadNextByte(cpu, mem);
        cpu.PushByteToStack(cpu.A, mem);
        return 3;
    }

    /// <summary>$68 PLA: pull A, setting N and Z. 4 cycles.</summary>
    public static ulong Pla(CPU cpu, Memory mem)
    {
        DummyReadNextByteAndStackTop(cpu, mem);
        cpu.A = cpu.PopByteFromStack(mem);
        BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(cpu.A, ref cpu.ProcessorStatus);
        return 4;
    }

    /// <summary>$08 PHP: push status with Break and Unused set on the pushed copy. 3 cycles.</summary>
    public static ulong Php(CPU cpu, Memory mem)
    {
        DummyReadNextByte(cpu, mem);
        var processorStatusCopy = cpu.ProcessorStatus;
        processorStatusCopy.Break = true;
        processorStatusCopy.Unused = true;
        cpu.PushByteToStack(processorStatusCopy.Value, mem);
        return 3;
    }

    /// <summary>$28 PLP: pull status. 4 cycles.</summary>
    public static ulong Plp(CPU cpu, Memory mem)
    {
        DummyReadNextByteAndStackTop(cpu, mem);
        cpu.ProcessorStatus.Value = cpu.PopByteFromStack(mem);
        return 4;
    }

    /// <summary>$4C JMP abs. 3 cycles.</summary>
    public static ulong Jmp_Absolute(CPU cpu, Memory mem)
    {
        cpu.PC = cpu.FetchOperandWord(mem);
        return 3;
    }

    /// <summary>
    /// $20 JSR: fetches the low target byte, reads the stack top, pushes PC (which now points
    /// at the high target byte, i.e. the LAST byte of the instruction), then fetches the high
    /// target byte and jumps. 6 cycles.
    /// </summary>
    public static ulong Jsr(CPU cpu, Memory mem)
    {
        var lowByte = cpu.FetchOperand(mem);
        cpu.FetchByte(mem, (ushort)(CPU.StackBaseAddress + cpu.SP));
        cpu.PushWordToStack(cpu.PC, mem);
        var highByte = cpu.FetchByte(mem, cpu.PC);
        cpu.PC = ByteHelpers.ToLittleEndianWord(lowByte, highByte);
        return 6;
    }

    /// <summary>
    /// $60 RTS: pull the JSR return address, then spend a cycle reading it while adding one.
    /// 6 cycles.
    /// </summary>
    public static ulong Rts(CPU cpu, Memory mem)
    {
        DummyReadNextByteAndStackTop(cpu, mem);
        var returnAddress = cpu.PopWordFromStack(mem);
        cpu.FetchByte(mem, returnAddress);
        cpu.PC = (ushort)(returnAddress + 1);
        return 6;
    }

    /// <summary>$40 RTI: pull status (Break/Unused cleared), then the return PC. 6 cycles.</summary>
    public static ulong Rti(CPU cpu, Memory mem)
    {
        DummyReadNextByteAndStackTop(cpu, mem);
        cpu.ProcessorStatus.Value = cpu.PopByteFromStack(mem);
        cpu.ProcessorStatus.Break = false;
        cpu.ProcessorStatus.Unused = false;
        cpu.PC = cpu.PopWordFromStack(mem);
        return 6;
    }

    /// <summary>$EA NOP. 2 cycles.</summary>
    public static ulong Nop(CPU cpu, Memory mem)
    {
        DummyReadNextByte(cpu, mem);
        return 2;
    }

    /// <summary>
    /// $00 BRK: fetches (and discards) the padding byte as a real bus access, pushes
    /// PC and status (Break set on the pushed copy), sets InterruptDisable, applies the
    /// model's D-clear policy, and loads the IRQ/BRK vector. 7 cycles.
    /// </summary>
    public static ulong Brk(CPU cpu, Memory mem)
    {
        // The padding byte after BRK is fetched as a real bus access (cycle 2) and
        // discarded; the pushed return address is consequently opcode + 2.
        cpu.FetchOperand(mem);
        cpu.PushWordToStack(cpu.PC, mem);

        var processorStatusCopy = cpu.ProcessorStatus;
        processorStatusCopy.Break = true;
        processorStatusCopy.Unused = true;
        cpu.PushByteToStack(processorStatusCopy.Value, mem);

        cpu.ProcessorStatus.InterruptDisable = true;
        // Model policy (per event): CMOS parts clear Decimal on BRK entry, after the
        // status byte (with D intact) was pushed. NMOS leaves D as-is.
        if (cpu.ModelDefinition.Traits.ClearsDecimalOnInterrupt)
            cpu.ProcessorStatus.Decimal = false;

        cpu.PC = cpu.FetchWord(mem, CPU.BrkIRQHandlerVector);
        return 7;
    }
}
