using Highbyte.DotNet6502.Utils;

namespace Highbyte.DotNet6502.CycleEnginePrototype.Tests;

public class InterruptAndStallTests
{
    public static IEnumerable<object[]> Candidates => EngineFixture.Candidates();

    [Theory, MemberData(nameof(Candidates))]
    public void Irq_Entry_Is_Seven_Bus_Cycles_Starting_With_Two_Reads_At_Pc(EngineKind kind)
    {
        byte[] code = [SliceOpcodes.Nop];
        static void Cpu(CPU c)
        {
            c.ProcessorStatus.Value = 0x00;
            c.CPUInterrupts.SetIRQSourceActive("device", autoAcknowledge: true);
        }
        static void Mem(Memory m) => m.WriteWord(CPU.BrkIRQHandlerVector, 0x1F80);
        var f = EngineFixture.Create(kind, code, Cpu, Mem);

        f.Engine.RunInstruction();

        Assert.Equal("R 1000=ea R 1000=ea W 01ff=10 W 01fe=00 W 01fd=20 R fffe=80 R ffff=1f", f.Trace);
        Assert.Equal(7ul, f.Engine.Cycle);
        Assert.Equal((ushort)0x1F80, f.Cpu.PC);
        Assert.True(f.Cpu.ProcessorStatus.InterruptDisable);
        Assert.False(f.Cpu.CPUInterrupts.IRQLineEnabled);   // auto-acknowledged on service
    }

    [Theory, MemberData(nameof(Candidates))]
    public void Nmi_Entry_Uses_The_Nmi_Vector_And_Clears_The_Latch(EngineKind kind)
    {
        byte[] code = [SliceOpcodes.Nop];
        static void Cpu(CPU c) => c.CPUInterrupts.SetNMISourceActive("restore");
        static void Mem(Memory m) => m.WriteWord(CPU.NonMaskableIRQHandlerVector, 0x1F90);
        var f = EngineFixture.Create(kind, code, Cpu, Mem);

        f.Engine.RunInstruction();

        Assert.EndsWith("R fffa=90 R fffb=1f", f.Trace);
        Assert.Equal(7ul, f.Engine.Cycle);
        Assert.Equal((ushort)0x1F90, f.Cpu.PC);
        Assert.False(f.Cpu.CPUInterrupts.NMIPending);
    }

    [Theory, MemberData(nameof(Candidates))]
    public void Irq_Is_Held_Off_While_InterruptDisable_Is_Set(EngineKind kind)
    {
        byte[] code = [SliceOpcodes.Nop, 0x12];
        static void Cpu(CPU c) => c.CPUInterrupts.SetIRQSourceActive("device", autoAcknowledge: true);
        var f = EngineFixture.Create(kind, code, Cpu);     // fixture sets I

        f.Engine.RunInstruction();

        Assert.Equal("R 1000=ea R 1001=12", f.Trace);
        Assert.True(f.Cpu.CPUInterrupts.IRQLineEnabled);
    }

    [Theory, MemberData(nameof(Candidates))]
    public void Read_Cycles_Stall_While_Ba_Is_Low(EngineKind kind)
    {
        // Bad line $33 (YSCROLL 3), positioned so the opcode fetch lands on cycle 9 and the
        // operand fetch on cycle 12, where BA goes low until cycle 54: 42 stalled cycles.
        byte[] code = [SliceOpcodes.LdaAbs, 0x00, 0x20];
        var f = EngineFixture.Create(kind, code, badLines: true);
        f.System.SetRasterPosition(0x33, 8);

        f.Engine.RunInstruction();

        Assert.Equal("R 1000=ad R 1001=00 R 1002=20 R 2000=00", f.Trace);
        Assert.Equal(4ul + 42ul, f.Engine.Cycle);
        f.Engine.FlushDevices();
        Assert.Equal(f.Engine.Cycle, f.System.MasterCycle);
        Assert.Equal(0x33, f.System.RasterLine);
        Assert.Equal(8 + 46, f.System.RasterCycle);
    }

    [Theory, MemberData(nameof(Candidates))]
    public void Write_Cycles_Do_Not_Stall_While_Ba_Is_Low(EngineKind kind)
    {
        // Opcode fetch on cycle 8, low/high on 9/10, dummy read on 11, and the write on cycle 12
        // where BA is low: the write proceeds, no stall.
        byte[] code = [SliceOpcodes.StaAbsX, 0x00, 0x20];
        static void Cpu(CPU c) { c.X = 1; c.A = 0x77; }
        var f = EngineFixture.Create(kind, code, Cpu, badLines: true);
        f.System.SetRasterPosition(0x33, 7);

        f.Engine.RunInstruction();

        Assert.Equal("R 1000=9d R 1001=00 R 1002=20 R 2001=00 W 2001=77", f.Trace);
        Assert.Equal(5ul, f.Engine.Cycle);
        f.Engine.FlushDevices();
        Assert.Equal(12, f.System.RasterCycle);
    }

    [Theory, MemberData(nameof(Candidates))]
    public void No_Stall_On_A_Line_That_Is_Not_A_Bad_Line(EngineKind kind)
    {
        byte[] code = [SliceOpcodes.LdaAbs, 0x00, 0x20];
        var f = EngineFixture.Create(kind, code, badLines: true);
        f.System.SetRasterPosition(0x34, 8);

        f.Engine.RunInstruction();

        Assert.Equal(4ul, f.Engine.Cycle);
    }
}
