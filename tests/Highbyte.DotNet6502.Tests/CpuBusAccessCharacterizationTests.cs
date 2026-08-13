using Highbyte.DotNet6502.Tests.Helpers;
using Highbyte.DotNet6502.Utils;

namespace Highbyte.DotNet6502.Tests;

/// <summary>
/// Locks in the per-model bus-access sequences of the ordered-bus-accesses work,
/// recorded via <see cref="BusAccessRecorder"/> (production memory-mapped-I/O,
/// no tracing hooks):
/// - NMOS RMW is read-write-write (original value written back, then the result);
///   65C02 RMW is read-read-write.
/// - NMOS indexed reads that cross a page dummy-read the un-carried address first;
///   NMOS indexed stores and RMW always do.
/// Interrupt-entry sequences are still the minimal form (their dummy reads are a
/// later stage of this work).
/// </summary>
public class CpuBusAccessCharacterizationTests
{
    private const ushort StartPc = 0x1000;

    private static (CPU cpu, Memory mem, BusAccessRecorder recorder) NewCpuWithRecorder(ushort watchStart, int watchLength)
    {
        var cpu = new CPU();
        var mem = new Memory();
        cpu.PC = StartPc;
        cpu.SP = 0xFF;
        var recorder = new BusAccessRecorder();
        recorder.Watch(mem, watchStart, watchLength);
        return (cpu, mem, recorder);
    }

    [Fact]
    public void Nmos_RMW_Zp_Is_Read_WriteBack_Write()
    {
        var (cpu, mem, recorder) = NewCpuWithRecorder(0x0040, 1);
        recorder[0x0040] = 0x12;
        mem[StartPc] = (byte)OpCodeId.INC_ZP;
        mem[StartPc + 1] = 0x40;

        cpu.ExecuteOneInstructionMinimal(mem);

        // NMOS RMW: the modify cycle writes the unmodified value back before the result.
        Assert.Equal(new[]
        {
            new BusAccessRecorder.BusAccess(IsRead: true, 0x0040, 0x12),
            new BusAccessRecorder.BusAccess(IsRead: false, 0x0040, 0x12),
            new BusAccessRecorder.BusAccess(IsRead: false, 0x0040, 0x13),
        }, recorder.Accesses);
    }

    [Fact]
    public void Nmos_RMW_Abs_Is_Read_WriteBack_Write()
    {
        var (cpu, mem, recorder) = NewCpuWithRecorder(0x3000, 1);
        recorder[0x3000] = 0b0100_0000;
        mem[StartPc] = (byte)OpCodeId.ASL_ABS;
        mem.WriteWord((ushort)(StartPc + 1), 0x3000);

        cpu.ExecuteOneInstructionMinimal(mem);

        Assert.Equal(new[]
        {
            new BusAccessRecorder.BusAccess(IsRead: true, 0x3000, 0b0100_0000),
            new BusAccessRecorder.BusAccess(IsRead: false, 0x3000, 0b0100_0000),
            new BusAccessRecorder.BusAccess(IsRead: false, 0x3000, 0b1000_0000),
        }, recorder.Accesses);
    }

    [Fact]
    public void Nmos_RMW_AbsX_Is_DummyRead_Read_WriteBack_Write()
    {
        var (cpu, mem, recorder) = NewCpuWithRecorder(0x3000, 0x200);
        recorder[0x30FF] = 0x10;
        cpu.X = 0xFF;
        mem[StartPc] = (byte)OpCodeId.INC_ABS_X;
        mem.WriteWord((ushort)(StartPc + 1), 0x3000);

        cpu.ExecuteOneInstructionMinimal(mem);

        // Indexed NMOS RMW always dummy-reads the un-carried address first (== the
        // effective address here, since no page is crossed) — the 7-cycle pattern.
        Assert.Equal(new[]
        {
            new BusAccessRecorder.BusAccess(IsRead: true, 0x30FF, 0x10),
            new BusAccessRecorder.BusAccess(IsRead: true, 0x30FF, 0x10),
            new BusAccessRecorder.BusAccess(IsRead: false, 0x30FF, 0x10),
            new BusAccessRecorder.BusAccess(IsRead: false, 0x30FF, 0x11),
        }, recorder.Accesses);
    }

    [Fact]
    public void Nmos_Illegal_RMW_Also_Does_The_Double_Write()
    {
        // The undocumented RMW combos share the same silicon behavior; software uses
        // e.g. DCP's double write deliberately.
        var (cpu, mem, recorder) = NewCpuWithRecorder(0x0040, 1);
        recorder[0x0040] = 0x12;
        var cpuFull = new CPU(CpuCompatibilityProfile.FullUnofficial);
        cpuFull.PC = StartPc;
        cpuFull.SP = 0xFF;
        mem[StartPc] = (byte)OpCodeId.DCP_ZP;
        mem[StartPc + 1] = 0x40;

        cpuFull.ExecuteOneInstructionMinimal(mem);

        Assert.Equal(new[]
        {
            new BusAccessRecorder.BusAccess(IsRead: true, 0x0040, 0x12),
            new BusAccessRecorder.BusAccess(IsRead: false, 0x0040, 0x12),
            new BusAccessRecorder.BusAccess(IsRead: false, 0x0040, 0x11),
        }, recorder.Accesses);
    }

    [Fact]
    public void Cmos_RMW_Abs_Is_Read_Read_Write()
    {
        var cpu = new CPU(new ExecState(), new Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory(),
            CpuModelIds.Ncr65c02, CpuCompatibilityProfile.OfficialOnly);
        var mem = new Memory();
        cpu.PC = StartPc;
        cpu.SP = 0xFF;
        var recorder = new BusAccessRecorder();
        recorder.Watch(mem, 0x3000, 1);
        recorder[0x3000] = 0x12;
        mem[StartPc] = (byte)OpCodeId.INC_ABS;
        mem.WriteWord((ushort)(StartPc + 1), 0x3000);

        cpu.ExecuteOneInstructionMinimal(mem);

        // 65C02 RMW: a second read replaces the NMOS write-back cycle.
        Assert.Equal(new[]
        {
            new BusAccessRecorder.BusAccess(IsRead: true, 0x3000, 0x12),
            new BusAccessRecorder.BusAccess(IsRead: true, 0x3000, 0x12),
            new BusAccessRecorder.BusAccess(IsRead: false, 0x3000, 0x13),
        }, recorder.Accesses);
    }

    [Fact]
    public void Nmos_Indexed_Read_With_Page_Cross_Dummy_Reads_The_Uncarried_Address()
    {
        var (cpu, mem, recorder) = NewCpuWithRecorder(0x3000, 0x200);
        recorder[0x3101] = 0x42; // $30FF + 2 crosses into $31xx
        cpu.X = 0x02;
        mem[StartPc] = (byte)OpCodeId.LDA_ABS_X;
        mem.WriteWord((ushort)(StartPc + 1), 0x30FF);

        cpu.ExecuteOneInstructionMinimal(mem);

        // Dummy read at $3001 (high byte not yet carried), then the real read at $3101.
        Assert.Equal(new[]
        {
            new BusAccessRecorder.BusAccess(IsRead: true, 0x3001, 0x00),
            new BusAccessRecorder.BusAccess(IsRead: true, 0x3101, 0x42),
        }, recorder.Accesses);
        Assert.Equal(0x42, cpu.A);
    }

    [Fact]
    public void Nmos_Indexed_Read_Without_Page_Cross_Reads_Once()
    {
        var (cpu, mem, recorder) = NewCpuWithRecorder(0x3000, 0x200);
        recorder[0x3041] = 0x42;
        cpu.X = 0x02;
        mem[StartPc] = (byte)OpCodeId.LDA_ABS_X;
        mem.WriteWord((ushort)(StartPc + 1), 0x303F);

        cpu.ExecuteOneInstructionMinimal(mem);

        // No page cross -> no dummy read (which is why the +1 cycle is conditional).
        Assert.Equal(new[]
        {
            new BusAccessRecorder.BusAccess(IsRead: true, 0x3041, 0x42),
        }, recorder.Accesses);
    }

    [Fact]
    public void Nmos_Indexed_Store_Always_Dummy_Reads_Before_Writing()
    {
        var (cpu, mem, recorder) = NewCpuWithRecorder(0x3000, 0x200);
        cpu.A = 0x77;
        cpu.X = 0x02;
        mem[StartPc] = (byte)OpCodeId.STA_ABS_X;
        mem.WriteWord((ushort)(StartPc + 1), 0x30FF);

        cpu.ExecuteOneInstructionMinimal(mem);

        // Dummy read at the un-carried $3001, then the write to $3101 — the reason
        // indexed stores have no "+1 on page cross": the extra cycle always happens.
        Assert.Equal(new[]
        {
            new BusAccessRecorder.BusAccess(IsRead: true, 0x3001, 0x00),
            new BusAccessRecorder.BusAccess(IsRead: false, 0x3101, 0x77),
        }, recorder.Accesses);
    }

    [Fact]
    public void Nmos_IndirectIndexed_Read_With_Page_Cross_Dummy_Reads_The_Uncarried_Address()
    {
        var (cpu, mem, recorder) = NewCpuWithRecorder(0x0040, 2);
        var dataRecorder = new BusAccessRecorder();
        dataRecorder.Watch(mem, 0x3000, 0x200);
        recorder[0x0040] = 0xFF; // pointer low
        recorder[0x0041] = 0x30; // pointer high -> $30FF
        dataRecorder[0x3101] = 0x55;
        cpu.Y = 0x02;
        mem[StartPc] = (byte)OpCodeId.LDA_IND_IX;
        mem[StartPc + 1] = 0x40;

        cpu.ExecuteOneInstructionMinimal(mem);

        // Pointer bytes read linearly from zero page...
        Assert.Equal(new[]
        {
            new BusAccessRecorder.BusAccess(IsRead: true, 0x0040, 0xFF),
            new BusAccessRecorder.BusAccess(IsRead: true, 0x0041, 0x30),
        }, recorder.Accesses);
        // ...then the dummy read at the un-carried $3001 and the real read at $3101.
        Assert.Equal(new[]
        {
            new BusAccessRecorder.BusAccess(IsRead: true, 0x3001, 0x00),
            new BusAccessRecorder.BusAccess(IsRead: true, 0x3101, 0x55),
        }, dataRecorder.Accesses);
        Assert.Equal(0x55, cpu.A);
    }

    [Fact]
    public void ZeroPage_Pointer_At_FF_Wraps_Within_Zero_Page_For_IndirectIndexed()
    {
        // A (zp),Y pointer at $FF takes its high byte from $00, not $0100 — true on
        // NMOS and CMOS alike. (Same class of fix as the JMP ($xxFF) page wrap.)
        var (cpu, mem, _) = NewCpuWithRecorder(0x3000, 0x100);
        mem[0x00FF] = 0x40; // pointer low
        mem[0x0000] = 0x30; // pointer high (wrapped) -> $3040
        mem[0x0100] = 0x99; // the linear (wrong) high-byte location -> would give $9940
        cpu.Y = 0x02;
        mem[StartPc] = (byte)OpCodeId.LDA_IND_IX;
        mem[StartPc + 1] = 0xFF;
        mem[0x3042] = 0x5A;

        cpu.ExecuteOneInstructionMinimal(mem);

        Assert.Equal(0x5A, cpu.A);
    }

    [Fact]
    public void ZeroPage_Pointer_At_FF_Wraps_Within_Zero_Page_For_IndexedIndirect()
    {
        // (zp,X): the indexed pointer location wraps within zero page, and so does the
        // pointer's own high byte: pointer at $FF reads its high byte from $00.
        var (cpu, mem, _) = NewCpuWithRecorder(0x3000, 0x100);
        mem[0x00FF] = 0x40;
        mem[0x0000] = 0x30; // -> $3040
        mem[0x0100] = 0x99;
        cpu.X = 0x0B;
        mem[StartPc] = (byte)OpCodeId.LDA_IX_IND;
        mem[StartPc + 1] = 0xF4; // $F4 + $0B = $FF
        mem[0x3040] = 0x5B;

        cpu.ExecuteOneInstructionMinimal(mem);

        Assert.Equal(0x5B, cpu.A);
    }

    [Fact]
    public void ZeroPage_Pointer_At_FF_Wraps_Within_Zero_Page_For_65c02_ZpIndirect()
    {
        var cpu = new CPU(new ExecState(), new Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory(),
            CpuModelIds.Ncr65c02, CpuCompatibilityProfile.OfficialOnly);
        var mem = new Memory();
        cpu.PC = StartPc;
        cpu.SP = 0xFF;
        mem[0x00FF] = 0x40;
        mem[0x0000] = 0x30; // -> $3040
        mem[0x0100] = 0x99;
        mem[StartPc] = 0xB2; // LDA (zp)
        mem[StartPc + 1] = 0xFF;
        mem[0x3040] = 0x5C;

        cpu.ExecuteOneInstructionMinimal(mem);

        Assert.Equal(0x5C, cpu.A);
    }

    [Fact]
    public void IRQ_Entry_Is_Two_DummyReads_Three_Pushes_Then_Vector_Reads()
    {
        var cpu = new CPU();
        var mem = new Memory();
        mem[0x1000] = (byte)OpCodeId.NOP;
        cpu.PC = 0x1000;
        cpu.SP = 0xFF;
        cpu.ProcessorStatus.InterruptDisable = false;
        cpu.ExecuteOneInstructionMinimal(mem); // NOP -> PC = 0x1001

        // One recorder over the program byte, the stack page, and the vector, so the
        // COMPLETE 7-access entry sequence is asserted in order.
        var recorder = new BusAccessRecorder();
        recorder.Watch(mem, 0x1001, 1);
        recorder.Watch(mem, 0x0100, 0x100);
        recorder.Watch(mem, CPU.BrkIRQHandlerVector, 2);
        recorder[CPU.BrkIRQHandlerVector] = 0x00;
        recorder[(ushort)(CPU.BrkIRQHandlerVector + 1)] = 0x40;

        cpu.CPUInterrupts.SetIRQSourceActive("device", autoAcknowledge: true);
        cpu.ProcessPendingInterrupts(mem);

        Assert.Equal(7, recorder.Accesses.Count);
        // Two dummy reads of the next opcode byte while the interrupt takes over...
        Assert.Equal(new BusAccessRecorder.BusAccess(IsRead: true, 0x1001, 0x00), recorder.Accesses[0]);
        Assert.Equal(new BusAccessRecorder.BusAccess(IsRead: true, 0x1001, 0x00), recorder.Accesses[1]);
        // ...then PC high, PC low, status (B clear)...
        Assert.Equal((false, (ushort)0x01FF), (recorder.Accesses[2].IsRead, recorder.Accesses[2].Address));
        Assert.Equal((false, (ushort)0x01FE), (recorder.Accesses[3].IsRead, recorder.Accesses[3].Address));
        Assert.Equal((false, (ushort)0x01FD), (recorder.Accesses[4].IsRead, recorder.Accesses[4].Address));
        // ...then the vector, low byte first.
        Assert.Equal(new BusAccessRecorder.BusAccess(IsRead: true, CPU.BrkIRQHandlerVector, 0x00), recorder.Accesses[5]);
        Assert.Equal(new BusAccessRecorder.BusAccess(IsRead: true, (ushort)(CPU.BrkIRQHandlerVector + 1), 0x40), recorder.Accesses[6]);
        Assert.Equal((ushort)0x4000, cpu.PC);
    }

    [Fact]
    public void NMI_Entry_Is_Two_DummyReads_Three_Pushes_Then_Vector_Reads()
    {
        var cpu = new CPU();
        var mem = new Memory();
        mem[0x1000] = (byte)OpCodeId.NOP;
        cpu.PC = 0x1000;
        cpu.SP = 0xFF;
        cpu.ExecuteOneInstructionMinimal(mem);

        var recorder = new BusAccessRecorder();
        recorder.Watch(mem, 0x1001, 1);
        recorder.Watch(mem, 0x0100, 0x100);
        recorder.Watch(mem, CPU.NonMaskableIRQHandlerVector, 2);
        recorder[CPU.NonMaskableIRQHandlerVector] = 0x00;
        recorder[(ushort)(CPU.NonMaskableIRQHandlerVector + 1)] = 0x50;

        cpu.CPUInterrupts.SetNMISourceActive("device");
        cpu.ProcessPendingInterrupts(mem);

        Assert.Equal(7, recorder.Accesses.Count);
        Assert.True(recorder.Accesses[0].IsRead && recorder.Accesses[0].Address == 0x1001);
        Assert.True(recorder.Accesses[1].IsRead && recorder.Accesses[1].Address == 0x1001);
        Assert.All(recorder.Accesses.Skip(2).Take(3), a => Assert.False(a.IsRead));
        Assert.True(recorder.Accesses[5].IsRead && recorder.Accesses[5].Address == CPU.NonMaskableIRQHandlerVector);
        Assert.True(recorder.Accesses[6].IsRead && recorder.Accesses[6].Address == CPU.NonMaskableIRQHandlerVector + 1);
        Assert.Equal((ushort)0x5000, cpu.PC);
    }

    [Fact]
    public void BRK_Reads_Its_Padding_Byte_As_A_Real_Bus_Access()
    {
        var cpu = new CPU();
        var mem = new Memory();
        cpu.PC = 0x1000;
        cpu.SP = 0xFF;
        mem[0x1000] = (byte)OpCodeId.BRK;
        mem[0x1001] = 0xEA; // padding byte (fetched and discarded)
        mem.WriteWord(CPU.BrkIRQHandlerVector, 0x4000);

        var recorder = new BusAccessRecorder();
        recorder.Watch(mem, 0x1000, 2);
        recorder.Watch(mem, 0x0100, 0x100);

        cpu.ExecuteOneInstructionMinimal(mem);

        // Opcode fetch, padding-byte fetch, then the three pushes (vector unwatched).
        Assert.Equal(5, recorder.Accesses.Count);
        Assert.Equal(new BusAccessRecorder.BusAccess(IsRead: true, 0x1000, (byte)OpCodeId.BRK), recorder.Accesses[0]);
        Assert.Equal(new BusAccessRecorder.BusAccess(IsRead: true, 0x1001, 0xEA), recorder.Accesses[1]);
        Assert.All(recorder.Accesses.Skip(2), a => Assert.False(a.IsRead));
        // Pushed return address is still PC+2 from the opcode (the byte AFTER the padding).
        Assert.Equal(0x10, recorder[0x01FF]); // PC high
        Assert.Equal(0x02, recorder[0x01FE]); // PC low
        Assert.Equal((ushort)0x4000, cpu.PC);
    }
}
