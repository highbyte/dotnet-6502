using Highbyte.DotNet6502.Utils;
using Microsoft.Extensions.Logging.Abstractions;

namespace Highbyte.DotNet6502.Tests;

/// <summary>
/// Tests for the ncr65c02 CPU model.
/// Counterparts of the NMOS baseline tests in <see cref="CpuNmosCharacterizationTests"/>
/// where the 65C02 deliberately behaves differently.
/// </summary>
public class Ncr65c02ModelTests
{
    private static CPU New65c02Cpu()
        => new(new ExecState(), new NullLoggerFactory(), CpuModelIds.Ncr65c02, CpuCompatibilityProfile.OfficialOnly);

    [Fact]
    public void Cpu_Can_Be_Constructed_With_The_Ncr65c02_Model()
    {
        var cpu = New65c02Cpu();

        Assert.Equal(CpuModelIds.Ncr65c02, cpu.ModelDefinition.ModelId);
        Assert.Equal(CpuCompatibilityProfile.OfficialOnly, cpu.CompatibilityProfile);
    }

    [Theory]
    [InlineData(CpuCompatibilityProfile.StableUnofficial)]
    [InlineData(CpuCompatibilityProfile.ExperimentalUnofficial)]
    [InlineData(CpuCompatibilityProfile.FullUnofficial)]
    public void Ncr65c02_Rejects_Unofficial_Compatibility_Profiles(CpuCompatibilityProfile profile)
    {
        Assert.Throws<DotNet6502Exception>(
            () => new CPU(new ExecState(), new NullLoggerFactory(), CpuModelIds.Ncr65c02, profile));
    }

    [Fact]
    public void Ncr65c02_Descriptor_Table_Has_All_256_Bytes_Defined()
    {
        var cpu = New65c02Cpu();

        Assert.True(cpu.ModelDefinition.Traits.AllBytesDefined);
        for (var code = 0; code <= 0xff; code++)
            Assert.NotNull(cpu.Descriptors[(byte)code]);
    }

    [Fact]
    public void JMP_IND_With_Pointer_At_Page_End_Reads_Linearly_And_Takes_6_Cycles()
    {
        // The NMOS page-wrap bug is fixed on the 65C02, at the cost of one extra cycle.
        var cpu = New65c02Cpu();
        ushort startPos = 0x0020;
        cpu.PC = startPos;

        var mem = new Memory();
        mem[0x30FF] = 0x34; // target low byte
        mem[0x3100] = 0x12; // linear read location (65C02)            -> target $1234
        mem[0x3000] = 0x56; // page-wrapped read location (real NMOS)  -> target $5634

        mem.WriteByte(ref startPos, OpCodeId.JMP_IND);
        mem.WriteWord(ref startPos, 0x30FF);

        var result = cpu.ExecuteOneInstructionMinimal(mem);

        Assert.Equal((ushort)0x1234, cpu.PC);
        Assert.Equal(6ul, result.CyclesConsumed);
    }

    [Fact]
    public void IRQ_Entry_Clears_Decimal_Flag_After_Pushing_Status()
    {
        var cpu = New65c02Cpu();
        var mem = new Memory();
        mem[0x1000] = (byte)OpCodeId.NOP;
        cpu.PC = 0x1000;
        cpu.SP = 0xFF;
        cpu.ProcessorStatus.Decimal = true;
        cpu.ProcessorStatus.InterruptDisable = false;
        mem.WriteWord(CPU.BrkIRQHandlerVector, 0x4000);

        cpu.CPUInterrupts.SetIRQSourceActive("device", autoAcknowledge: true);
        cpu.ProcessPendingInterrupts(mem);

        Assert.Equal((ushort)0x4000, cpu.PC);
        // 65C02: D cleared in the handler; the PUSHED status still carries D=1.
        Assert.False(cpu.ProcessorStatus.Decimal);
        var pushedStatus = new ProcessorStatus(mem[(ushort)(0x0100 + cpu.SP + 1)]);
        Assert.True(pushedStatus.Decimal);
    }

    [Fact]
    public void NMI_Entry_Clears_Decimal_Flag_After_Pushing_Status()
    {
        var cpu = New65c02Cpu();
        var mem = new Memory();
        mem[0x1000] = (byte)OpCodeId.NOP;
        cpu.PC = 0x1000;
        cpu.SP = 0xFF;
        cpu.ProcessorStatus.Decimal = true;
        mem.WriteWord(CPU.NonMaskableIRQHandlerVector, 0x5000);

        cpu.CPUInterrupts.SetNMISourceActive("device");
        cpu.ProcessPendingInterrupts(mem);

        Assert.Equal((ushort)0x5000, cpu.PC);
        Assert.False(cpu.ProcessorStatus.Decimal);
        var pushedStatus = new ProcessorStatus(mem[(ushort)(0x0100 + cpu.SP + 1)]);
        Assert.True(pushedStatus.Decimal);
    }

    [Fact]
    public void BRK_Entry_Clears_Decimal_Flag_After_Pushing_Status()
    {
        var cpu = New65c02Cpu();
        var mem = new Memory();
        cpu.PC = 0x1000;
        cpu.SP = 0xFF;
        mem[0x1000] = (byte)OpCodeId.BRK;
        mem.WriteWord(CPU.BrkIRQHandlerVector, 0x4000);
        cpu.ProcessorStatus.Decimal = true;

        cpu.ExecuteOneInstructionMinimal(mem);

        Assert.Equal((ushort)0x4000, cpu.PC);
        Assert.False(cpu.ProcessorStatus.Decimal);
        var pushedStatus = new ProcessorStatus(mem[(ushort)(0x0100 + cpu.SP + 1)]);
        Assert.True(pushedStatus.Decimal);
    }

    [Fact]
    public void Reset_Clears_Decimal_Flag()
    {
        var cpu = New65c02Cpu();
        var mem = new Memory();
        mem.WriteWord(CPU.ResetVector, 0x8000);
        cpu.ProcessorStatus.Decimal = true;

        cpu.Reset(mem);

        Assert.Equal((ushort)0x8000, cpu.PC);
        Assert.False(cpu.ProcessorStatus.Decimal);
    }

    [Fact]
    public void Ncr65c02_Table_Has_178_Documented_OpCodes_And_78_Defined_Nops()
    {
        // 151 official NMOS opcodes (shared) + 27 new 65C02 bytes = 178 documented;
        // the remaining 78 bytes are defined NOPs.
        var officialNmosCount = InstructionList.GetAllInstructions(CpuCompatibilityProfile.OfficialOnly).OpCodeDictionary.Count;
        Assert.Equal(151, officialNmosCount);

        var cpu = New65c02Cpu();
        Assert.Equal(178, cpu.Descriptors.Count(d => d!.Documented));
        Assert.Equal(78, cpu.Descriptors.Count(d => !d!.Documented));
        Assert.All(cpu.Descriptors.Where(d => !d!.Documented), d => Assert.Equal("NOP", d!.Mnemonic));
    }

    [Fact]
    public void Ncr65c02_Descriptor_Codes_Match_Their_Table_Index()
    {
        var cpu = New65c02Cpu();
        for (var code = 0; code <= 0xff; code++)
            Assert.Equal((byte)code, cpu.Descriptors[(byte)code]!.Code);
    }

    [Theory]
    // byte, expected size, expected cycles — the 65C02's defined-NOP map (base/NCR part).
    [InlineData(0x03, 1, 1)] // $x3 column
    [InlineData(0x07, 1, 1)] // Rockwell RMB territory -> NOP on the base part
    [InlineData(0x0B, 1, 1)] // $xB column
    [InlineData(0xCB, 1, 1)] // WDC WAI territory -> NOP on the base part
    [InlineData(0x02, 2, 2)] // $x2 column leftover
    [InlineData(0x44, 2, 3)]
    [InlineData(0x54, 2, 4)]
    [InlineData(0x5C, 3, 8)]
    [InlineData(0xDC, 3, 4)]
    public void Undefined_Bytes_Execute_As_Defined_Nops(byte code, int expectedSize, int expectedCycles)
    {
        var cpu = New65c02Cpu();
        var mem = new Memory();
        cpu.PC = 0x1000;
        mem[0x1000] = code;

        var descriptor = cpu.Descriptors[code]!;
        Assert.Equal("NOP", descriptor.Mnemonic);
        Assert.Equal(expectedSize, descriptor.Size);

        var result = cpu.ExecuteOneInstructionMinimal(mem);

        Assert.Equal((ulong)expectedCycles, result.CyclesConsumed);
        Assert.Equal((ushort)(0x1000 + expectedSize), cpu.PC); // PC advanced over the whole instruction
        Assert.False(cpu.IsHalted); // notably: NO byte jams a 65C02
    }
}
