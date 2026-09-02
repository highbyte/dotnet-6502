using Highbyte.DotNet6502.Utils;

namespace Highbyte.DotNet6502.CycleEnginePrototype.Tests;

/// <summary>
/// Both device policies must produce the same ordered bus trace, cycle count and final state for each
/// shape in the slice. Expected traces are hand-derived from the documented 6502 cycle sequences
/// (one bus access per cycle, dummy reads included), so they are readable without decoding an
/// external vector. Final state and total cycles are also checked against the production executor.
/// </summary>
public class SliceTraceTests
{
    public static IEnumerable<object[]> Candidates => EngineFixture.Candidates();

    [Theory, MemberData(nameof(Candidates))]
    public void Lda_Immediate(EngineKind kind)
    {
        byte[] code = [SliceOpcodes.LdaImm, 0x42];
        var f = EngineFixture.Create(kind, code);

        f.Engine.RunInstruction();

        Assert.Equal("R 1000=a9 R 1001=42", f.Trace);
        Assert.Equal(2ul, f.Engine.Cycle);
        Assert.Equal(0x42, f.Cpu.A);
        Assert.Equal((ushort)0x1002, f.Cpu.PC);
        f.AssertMatchesLegacy(code);
    }

    [Theory, MemberData(nameof(Candidates))]
    public void Lda_Absolute(EngineKind kind)
    {
        byte[] code = [SliceOpcodes.LdaAbs, 0x00, 0x20];
        static void Mem(Memory m) => m[0x2000] = 0x33;
        var f = EngineFixture.Create(kind, code, arrangeMem: Mem);

        f.Engine.RunInstruction();

        Assert.Equal("R 1000=ad R 1001=00 R 1002=20 R 2000=33", f.Trace);
        Assert.Equal(4ul, f.Engine.Cycle);
        Assert.Equal(0x33, f.Cpu.A);
        f.AssertMatchesLegacy(code, arrangeMem: Mem);
    }

    [Theory, MemberData(nameof(Candidates))]
    public void Lda_AbsoluteX_Without_Page_Cross_Reads_Once(EngineKind kind)
    {
        byte[] code = [SliceOpcodes.LdaAbsX, 0x00, 0x20];
        static void Cpu(CPU c) => c.X = 1;
        static void Mem(Memory m) => m[0x2001] = 0x44;
        var f = EngineFixture.Create(kind, code, Cpu, Mem);

        f.Engine.RunInstruction();

        Assert.Equal("R 1000=bd R 1001=00 R 1002=20 R 2001=44", f.Trace);
        Assert.Equal(4ul, f.Engine.Cycle);
        Assert.Equal(0x44, f.Cpu.A);
        f.AssertMatchesLegacy(code, Cpu, Mem);
    }

    [Theory, MemberData(nameof(Candidates))]
    public void Lda_AbsoluteX_With_Page_Cross_Dummy_Reads_The_Uncarried_Address(EngineKind kind)
    {
        byte[] code = [SliceOpcodes.LdaAbsX, 0xFF, 0x20];
        static void Cpu(CPU c) => c.X = 1;
        static void Mem(Memory m) { m[0x2000] = 0x99; m[0x2100] = 0x55; }
        var f = EngineFixture.Create(kind, code, Cpu, Mem);

        f.Engine.RunInstruction();

        Assert.Equal("R 1000=bd R 1001=ff R 1002=20 R 2000=99 R 2100=55", f.Trace);
        Assert.Equal(5ul, f.Engine.Cycle);
        Assert.Equal(0x55, f.Cpu.A);
        f.AssertMatchesLegacy(code, Cpu, Mem);
    }

    [Theory, MemberData(nameof(Candidates))]
    public void Sta_AbsoluteX_Always_Dummy_Reads_Then_Writes(EngineKind kind)
    {
        byte[] code = [SliceOpcodes.StaAbsX, 0xFF, 0x20];
        static void Cpu(CPU c) { c.X = 1; c.A = 0x77; }
        static void Mem(Memory m) => m[0x2000] = 0x99;
        var f = EngineFixture.Create(kind, code, Cpu, Mem);

        f.Engine.RunInstruction();

        Assert.Equal("R 1000=9d R 1001=ff R 1002=20 R 2000=99 W 2100=77", f.Trace);
        Assert.Equal(5ul, f.Engine.Cycle);
        f.AssertMatchesLegacy(code, Cpu, Mem);
    }

    [Theory, MemberData(nameof(Candidates))]
    public void Inc_Absolute_Nmos_Reads_Writes_Back_Then_Writes(EngineKind kind)
    {
        byte[] code = [SliceOpcodes.IncAbs, 0x00, 0x20];
        static void Mem(Memory m) => m[0x2000] = 0x7F;
        var f = EngineFixture.Create(kind, code, arrangeMem: Mem, family: CpuFamily.Nmos);

        f.Engine.RunInstruction();

        Assert.Equal("R 1000=ee R 1001=00 R 1002=20 R 2000=7f W 2000=7f W 2000=80", f.Trace);
        Assert.Equal(6ul, f.Engine.Cycle);
        Assert.True(f.Cpu.ProcessorStatus.Negative);
        f.AssertMatchesLegacy(code, arrangeMem: Mem);
    }

    [Theory, MemberData(nameof(Candidates))]
    public void Inc_Absolute_Cmos_Reads_Twice_Then_Writes(EngineKind kind)
    {
        byte[] code = [SliceOpcodes.IncAbs, 0x00, 0x20];
        static void Mem(Memory m) => m[0x2000] = 0x7F;
        var f = EngineFixture.Create(kind, code, arrangeMem: Mem, family: CpuFamily.Cmos);

        f.Engine.RunInstruction();

        Assert.Equal("R 1000=ee R 1001=00 R 1002=20 R 2000=7f R 2000=7f W 2000=80", f.Trace);
        Assert.Equal(6ul, f.Engine.Cycle);
    }

    [Theory, MemberData(nameof(Candidates))]
    public void Bne_Not_Taken(EngineKind kind)
    {
        byte[] code = [SliceOpcodes.Bne, 0x05];
        static void Cpu(CPU c) => c.ProcessorStatus.Zero = true;
        var f = EngineFixture.Create(kind, code, Cpu);

        f.Engine.RunInstruction();

        Assert.Equal("R 1000=d0 R 1001=05", f.Trace);
        Assert.Equal(2ul, f.Engine.Cycle);
        Assert.Equal((ushort)0x1002, f.Cpu.PC);
        f.AssertMatchesLegacy(code, Cpu);
    }

    [Theory, MemberData(nameof(Candidates))]
    public void Bne_Taken_Same_Page_Dummy_Reads_The_Next_Opcode(EngineKind kind)
    {
        byte[] code = [SliceOpcodes.Bne, 0x05, 0xEE];
        static void Cpu(CPU c) => c.ProcessorStatus.Zero = false;
        var f = EngineFixture.Create(kind, code, Cpu);

        f.Engine.RunInstruction();

        Assert.Equal("R 1000=d0 R 1001=05 R 1002=ee", f.Trace);
        Assert.Equal(3ul, f.Engine.Cycle);
        Assert.Equal((ushort)0x1007, f.Cpu.PC);
        f.AssertMatchesLegacy(code, Cpu);
    }

    [Theory, MemberData(nameof(Candidates))]
    public void Bne_Taken_Across_Page_Dummy_Reads_The_Wrong_Page(EngineKind kind)
    {
        // BNE at $1000 with offset -$10: target $0FF2, PCH fix-up read at $10F2.
        byte[] code = [SliceOpcodes.Bne, 0xF0, 0xEE];
        static void Cpu(CPU c) => c.ProcessorStatus.Zero = false;
        static void Mem(Memory m) => m[0x10F2] = 0xAB;
        var f = EngineFixture.Create(kind, code, Cpu, Mem);

        f.Engine.RunInstruction();

        Assert.Equal("R 1000=d0 R 1001=f0 R 1002=ee R 10f2=ab", f.Trace);
        Assert.Equal(4ul, f.Engine.Cycle);
        Assert.Equal((ushort)0x0FF2, f.Cpu.PC);
        f.AssertMatchesLegacy(code, Cpu, Mem);
    }

    [Theory, MemberData(nameof(Candidates))]
    public void Nop_Dummy_Reads_The_Next_Byte(EngineKind kind)
    {
        byte[] code = [SliceOpcodes.Nop, 0x12];
        var f = EngineFixture.Create(kind, code);

        f.Engine.RunInstruction();

        Assert.Equal("R 1000=ea R 1001=12", f.Trace);
        Assert.Equal(2ul, f.Engine.Cycle);
        Assert.Equal((ushort)0x1001, f.Cpu.PC);
        f.AssertMatchesLegacy(code);
    }

    [Theory, MemberData(nameof(Candidates))]
    public void Pha_Dummy_Reads_Then_Pushes(EngineKind kind)
    {
        byte[] code = [SliceOpcodes.Pha, 0x12];
        static void Cpu(CPU c) => c.A = 0x42;
        var f = EngineFixture.Create(kind, code, Cpu);

        f.Engine.RunInstruction();

        Assert.Equal("R 1000=48 R 1001=12 W 01ff=42", f.Trace);
        Assert.Equal(3ul, f.Engine.Cycle);
        Assert.Equal(0xFE, f.Cpu.SP);
        f.AssertMatchesLegacy(code, Cpu);
    }

    [Theory, MemberData(nameof(Candidates))]
    public void Jsr_Reads_The_Stack_Before_Pushing_And_Fetches_High_Byte_Last(EngineKind kind)
    {
        byte[] code = [SliceOpcodes.Jsr, 0x00, 0x1F];
        var f = EngineFixture.Create(kind, code);

        f.Engine.RunInstruction();

        Assert.Equal("R 1000=20 R 1001=00 R 01ff=00 W 01ff=10 W 01fe=02 R 1002=1f", f.Trace);
        Assert.Equal(6ul, f.Engine.Cycle);
        Assert.Equal((ushort)0x1F00, f.Cpu.PC);
        Assert.Equal(0xFD, f.Cpu.SP);
        f.AssertMatchesLegacy(code);
    }

    [Theory, MemberData(nameof(Candidates))]
    public void Rts_Dummy_Reads_Pulls_And_Increments(EngineKind kind)
    {
        byte[] code = [SliceOpcodes.Rts, 0x12];
        static void Cpu(CPU c) => c.SP = 0xFD;
        static void Mem(Memory m) { m[0x01FE] = 0x02; m[0x01FF] = 0x10; m[0x1002] = 0x34; }
        var f = EngineFixture.Create(kind, code, Cpu, Mem);

        f.Engine.RunInstruction();

        Assert.Equal("R 1000=60 R 1001=12 R 01fd=00 R 01fe=02 R 01ff=10 R 1002=34", f.Trace);
        Assert.Equal(6ul, f.Engine.Cycle);
        Assert.Equal((ushort)0x1003, f.Cpu.PC);
        Assert.Equal(0xFF, f.Cpu.SP);
        f.AssertMatchesLegacy(code, Cpu, Mem);
    }

    [Theory, MemberData(nameof(Candidates))]
    public void Rti_Pulls_Status_Then_Return_Address(EngineKind kind)
    {
        byte[] code = [SliceOpcodes.Rti, 0x12];
        static void Cpu(CPU c) => c.SP = 0xFC;
        static void Mem(Memory m) { m[0x01FD] = 0x23; m[0x01FE] = 0x00; m[0x01FF] = 0x1F; }
        var f = EngineFixture.Create(kind, code, Cpu, Mem);

        f.Engine.RunInstruction();

        Assert.Equal("R 1000=40 R 1001=12 R 01fc=00 R 01fd=23 R 01fe=00 R 01ff=1f", f.Trace);
        Assert.Equal(6ul, f.Engine.Cycle);
        Assert.Equal((ushort)0x1F00, f.Cpu.PC);
        Assert.Equal(0xFF, f.Cpu.SP);
        Assert.Equal(0x23 | 0x20, f.Cpu.ProcessorStatus.Value & 0xEF);
        f.AssertMatchesLegacy(code, Cpu, Mem, ignoreBreakAndUnused: true);
    }
}
