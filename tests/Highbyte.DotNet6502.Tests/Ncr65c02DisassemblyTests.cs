using Highbyte.DotNet6502.Utils;
using Microsoft.Extensions.Logging.Abstractions;

namespace Highbyte.DotNet6502.Tests;

/// <summary>
/// Model-aware disassembly tests (feature cpu-models-65c02, M1 step 7): the same byte
/// must disassemble according to the CPU's model, new 65C02 addressing modes must
/// format correctly, and listing advance must use per-model instruction sizes.
/// </summary>
public class Ncr65c02DisassemblyTests
{
    private static CPU New65c02Cpu()
        => new(new ExecState(), new NullLoggerFactory(), CpuModelIds.Ncr65c02, CpuCompatibilityProfile.OfficialOnly);

    [Fact]
    public void The_Same_Byte_Disassembles_Differently_Per_Model()
    {
        // $9C: STZ abs on the 65C02; not an implemented instruction on NMOS profiles.
        var mem = new Memory();
        mem[0x1000] = 0x9C;
        mem[0x1001] = 0x34;
        mem[0x1002] = 0x12;

        var cmosCpu = New65c02Cpu();
        Assert.Equal("STZ $1234", OutputGen.BuildInstructionString(cmosCpu, mem, 0x1000));

        var nmosCpu = new CPU(CpuCompatibilityProfile.FullUnofficial);
        Assert.Equal("???", OutputGen.BuildInstructionString(nmosCpu, mem, 0x1000));
    }

    [Fact]
    public void ZeroPage_Indirect_Mode_Formats_With_Parentheses()
    {
        var mem = new Memory();
        mem[0x1000] = 0xB2; // LDA (zp)
        mem[0x1001] = 0x40;

        Assert.Equal("LDA ($40)", OutputGen.BuildInstructionString(New65c02Cpu(), mem, 0x1000));
    }

    [Fact]
    public void Absolute_Indexed_Indirect_Mode_Formats_With_Parentheses_And_X()
    {
        var mem = new Memory();
        mem[0x1000] = 0x7C; // JMP (abs,X)
        mem[0x1001] = 0x00;
        mem[0x1002] = 0x30;

        Assert.Equal("JMP ($3000,X)", OutputGen.BuildInstructionString(New65c02Cpu(), mem, 0x1000));
    }

    [Fact]
    public void Defined_Nop_Bytes_Disassemble_As_NOP_Not_Unknown()
    {
        var mem = new Memory();
        mem[0x1000] = 0x03; // 1-byte defined NOP on the 65C02

        var instructionString = OutputGen.BuildInstructionString(New65c02Cpu(), mem, 0x1000);
        Assert.StartsWith("NOP", instructionString);
    }

    [Fact]
    public void Listing_Advance_Uses_Per_Model_Instruction_Sizes()
    {
        // A disassembly listing must not desync after a 65C02-only 3-byte instruction.
        var mem = new Memory();
        mem[0x1000] = 0x9C; // STZ abs: 3 bytes on 65C02; undefined (1 byte) on NMOS

        var cmosCpu = New65c02Cpu();
        Assert.Equal((ushort)0x1003, cmosCpu.GetNextInstructionAddress(mem, 0x1000));
        Assert.Equal(3, cmosCpu.GetOpCodeSize(0x9C));
        Assert.True(cmosCpu.IsOpCodeDefined(0x9C));

        var nmosCpu = new CPU(CpuCompatibilityProfile.FullUnofficial);
        Assert.Equal((ushort)0x1001, nmosCpu.GetNextInstructionAddress(mem, 0x1000));
        Assert.Equal(1, nmosCpu.GetOpCodeSize(0x9C));
        Assert.False(nmosCpu.IsOpCodeDefined(0x9C));
    }
}
