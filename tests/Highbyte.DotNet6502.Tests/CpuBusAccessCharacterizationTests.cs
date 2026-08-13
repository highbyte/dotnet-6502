using Highbyte.DotNet6502.Tests.Helpers;
using Highbyte.DotNet6502.Utils;

namespace Highbyte.DotNet6502.Tests;

/// <summary>
/// Characterization tests locking in the CURRENT bus-access sequences of the
/// instruction categories the ordered-bus-accesses work will deliberately change.
/// Recorded via <see cref="BusAccessRecorder"/> (production memory-mapped-I/O,
/// no tracing hooks).
///
/// Today the emulator performs the MINIMAL access sequence per instruction: one read
/// and/or one write at the effective address. Real hardware does more:
/// - NMOS RMW is read-write-write (original value written back, then the result);
///   65C02 RMW is read-read-write.
/// - Indexed reads that cross a page perform a dummy read at the un-carried address.
/// - Indexed stores always perform a dummy read before the write.
/// When M2 lands those sequences per model, each test here flips to the hardware
/// sequence as an explicit, reviewed change — exactly like the JMP ($xxFF)
/// characterization test did in M1.
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
    public void Nmos_RMW_AbsX_Is_Read_WriteBack_Write_At_The_Final_Address()
    {
        var (cpu, mem, recorder) = NewCpuWithRecorder(0x3000, 0x200);
        recorder[0x30FF] = 0x10;
        cpu.X = 0xFF;
        mem[StartPc] = (byte)OpCodeId.INC_ABS_X;
        mem.WriteWord((ushort)(StartPc + 1), 0x3000);

        cpu.ExecuteOneInstructionMinimal(mem);

        Assert.Equal(new[]
        {
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
    public void Indexed_Read_With_Page_Cross_Currently_Reads_Only_The_Final_Address()
    {
        var (cpu, mem, recorder) = NewCpuWithRecorder(0x3000, 0x200);
        recorder[0x3101] = 0x42; // $30FF + 2 crosses into $31xx
        cpu.X = 0x02;
        mem[StartPc] = (byte)OpCodeId.LDA_ABS_X;
        mem.WriteWord((ushort)(StartPc + 1), 0x30FF);

        cpu.ExecuteOneInstructionMinimal(mem);

        // Real NMOS: dummy read at $3001 (high byte not yet carried), then read $3101.
        Assert.Equal(new[]
        {
            new BusAccessRecorder.BusAccess(IsRead: true, 0x3101, 0x42),
        }, recorder.Accesses);
        Assert.Equal(0x42, cpu.A);
    }

    [Fact]
    public void Indexed_Store_Currently_Writes_Only_The_Final_Address()
    {
        var (cpu, mem, recorder) = NewCpuWithRecorder(0x3000, 0x200);
        cpu.A = 0x77;
        cpu.X = 0x02;
        mem[StartPc] = (byte)OpCodeId.STA_ABS_X;
        mem.WriteWord((ushort)(StartPc + 1), 0x30FF);

        cpu.ExecuteOneInstructionMinimal(mem);

        // Real NMOS: dummy read at $3001 first, then the write to $3101.
        Assert.Equal(new[]
        {
            new BusAccessRecorder.BusAccess(IsRead: false, 0x3101, 0x77),
        }, recorder.Accesses);
    }

    [Fact]
    public void IndirectIndexed_Read_Currently_Reads_Pointer_Then_Final_Address_Only()
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
        // ...then only the final data address (real NMOS: dummy read at $3001 first).
        Assert.Equal(new[]
        {
            new BusAccessRecorder.BusAccess(IsRead: true, 0x3101, 0x55),
        }, dataRecorder.Accesses);
        Assert.Equal(0x55, cpu.A);
    }

    [Fact]
    public void IRQ_Entry_Currently_Pushes_Three_Bytes_And_Reads_The_Vector()
    {
        var cpu = new CPU();
        var mem = new Memory();
        mem[0x1000] = (byte)OpCodeId.NOP;
        cpu.PC = 0x1000;
        cpu.SP = 0xFF;
        cpu.ProcessorStatus.InterruptDisable = false;

        var stackRecorder = new BusAccessRecorder();
        stackRecorder.Watch(mem, 0x0100, 0x100);
        var vectorRecorder = new BusAccessRecorder();
        vectorRecorder.Watch(mem, CPU.BrkIRQHandlerVector, 2);
        vectorRecorder[CPU.BrkIRQHandlerVector] = 0x00;
        vectorRecorder[(ushort)(CPU.BrkIRQHandlerVector + 1)] = 0x40;

        cpu.CPUInterrupts.SetIRQSourceActive("device", autoAcknowledge: true);
        cpu.ProcessPendingInterrupts(mem);

        // Stack: PC high, PC low, then status (with B clear). Real hardware also performs
        // two dummy reads before the pushes (7-cycle sequence); those are absent today.
        Assert.Equal(3, stackRecorder.Accesses.Count);
        Assert.All(stackRecorder.Accesses, a => Assert.False(a.IsRead));
        Assert.Equal(0x01FF, stackRecorder.Accesses[0].Address); // PC high
        Assert.Equal(0x01FE, stackRecorder.Accesses[1].Address); // PC low
        Assert.Equal(0x01FD, stackRecorder.Accesses[2].Address); // status

        // Vector: low byte then high byte, one read each.
        Assert.Equal(new[]
        {
            new BusAccessRecorder.BusAccess(IsRead: true, CPU.BrkIRQHandlerVector, 0x00),
            new BusAccessRecorder.BusAccess(IsRead: true, (ushort)(CPU.BrkIRQHandlerVector + 1), 0x40),
        }, vectorRecorder.Accesses);
        Assert.Equal((ushort)0x4000, cpu.PC);
    }
}
