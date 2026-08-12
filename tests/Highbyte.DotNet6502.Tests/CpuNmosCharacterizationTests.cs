using Highbyte.DotNet6502.Utils;

namespace Highbyte.DotNet6502.Tests;

/// <summary>
/// Characterization tests locking in the current NMOS 6502 behavior of interrupt/BRK entry
/// and Reset, recorded before the CPU model refactor (feature cpu-models-65c02).
///
/// The refactor must keep every behavior below intact for the NMOS model. The 65C02 model
/// deliberately differs on some of them (it clears the Decimal flag on IRQ/NMI/BRK entry
/// and on Reset); those differences get their own tests when that model is added, while
/// these tests continue to guard the NMOS baseline.
/// </summary>
public class CpuNmosCharacterizationTests
{
    private static (CPU cpu, Memory mem) NewCpuAt(ushort pc)
    {
        var cpu = new CPU();
        var mem = new Memory();
        mem[pc] = (byte)OpCodeId.NOP;
        cpu.PC = pc;
        cpu.SP = 0xFF;
        return (cpu, mem);
    }

    [Fact]
    public void IRQ_Entry_Preserves_Decimal_Flag_And_Sets_InterruptDisable()
    {
        var (cpu, mem) = NewCpuAt(0x1000);
        cpu.ProcessorStatus.Decimal = true;
        cpu.ProcessorStatus.InterruptDisable = false;
        mem.WriteWord(CPU.BrkIRQHandlerVector, 0x4000);

        cpu.CPUInterrupts.SetIRQSourceActive("device", autoAcknowledge: true);
        cpu.ProcessPendingInterrupts(mem);

        Assert.Equal((ushort)0x4000, cpu.PC);
        // NMOS: D is NOT cleared on interrupt entry (65C02 clears it).
        Assert.True(cpu.ProcessorStatus.Decimal);
        Assert.True(cpu.ProcessorStatus.InterruptDisable);

        // The status byte pushed to the stack carries D=1, B=0 (hardware interrupt), Unused=1.
        var pushedStatus = new ProcessorStatus(mem[(ushort)(0x0100 + cpu.SP + 1)]);
        Assert.True(pushedStatus.Decimal);
        Assert.False(pushedStatus.Break);
        Assert.True(pushedStatus.Unused);
    }

    [Fact]
    public void NMI_Entry_Preserves_Decimal_Flag_And_Sets_InterruptDisable()
    {
        var (cpu, mem) = NewCpuAt(0x1000);
        cpu.ProcessorStatus.Decimal = true;
        cpu.ProcessorStatus.InterruptDisable = false;
        mem.WriteWord(CPU.NonMaskableIRQHandlerVector, 0x5000);

        cpu.CPUInterrupts.SetNMISourceActive("device");
        cpu.ProcessPendingInterrupts(mem);

        Assert.Equal((ushort)0x5000, cpu.PC);
        // NMOS: D is NOT cleared on NMI entry (65C02 clears it).
        Assert.True(cpu.ProcessorStatus.Decimal);
        Assert.True(cpu.ProcessorStatus.InterruptDisable);

        var pushedStatus = new ProcessorStatus(mem[(ushort)(0x0100 + cpu.SP + 1)]);
        Assert.True(pushedStatus.Decimal);
        Assert.False(pushedStatus.Break);
        Assert.True(pushedStatus.Unused);
    }

    [Fact]
    public void BRK_Entry_Preserves_Decimal_Flag_And_Sets_InterruptDisable()
    {
        var cpu = new CPU();
        var mem = new Memory();
        cpu.PC = 0x1000;
        cpu.SP = 0xFF;
        mem[0x1000] = (byte)OpCodeId.BRK;
        mem.WriteWord(CPU.BrkIRQHandlerVector, 0x4000);
        cpu.ProcessorStatus.Decimal = true;
        cpu.ProcessorStatus.InterruptDisable = false;

        cpu.ExecuteOneInstructionMinimal(mem);

        Assert.Equal((ushort)0x4000, cpu.PC);
        // NMOS: D is NOT cleared on BRK entry (65C02 clears it).
        Assert.True(cpu.ProcessorStatus.Decimal);
        Assert.True(cpu.ProcessorStatus.InterruptDisable);

        // The status byte pushed by BRK carries D=1 and B=1 (software interrupt).
        var pushedStatus = new ProcessorStatus(mem[(ushort)(0x0100 + cpu.SP + 1)]);
        Assert.True(pushedStatus.Decimal);
        Assert.True(pushedStatus.Break);
        Assert.True(pushedStatus.Unused);
    }

    [Fact]
    public void Reset_Sets_PC_From_Vector_And_Unhalts_But_Touches_Nothing_Else()
    {
        var cpu = new CPU();
        var mem = new Memory();
        mem.WriteWord(CPU.ResetVector, 0x8000);

        cpu.A = 0x11;
        cpu.X = 0x22;
        cpu.Y = 0x33;
        cpu.SP = 0x77;
        cpu.ProcessorStatus.Decimal = true;
        cpu.ProcessorStatus.InterruptDisable = false;
        cpu.ProcessorStatus.Carry = true;
        cpu.Halt();

        cpu.Reset(mem);

        Assert.Equal((ushort)0x8000, cpu.PC);
        Assert.False(cpu.IsHalted);
        // Current minimal Reset: registers and every status flag are left untouched.
        // (Real NMOS hardware sets I on reset; 65C02 additionally clears D. Any future
        // change to this is a deliberate, model-specific decision -- not an accident.)
        Assert.Equal(0x11, cpu.A);
        Assert.Equal(0x22, cpu.X);
        Assert.Equal(0x33, cpu.Y);
        Assert.Equal(0x77, cpu.SP);
        Assert.True(cpu.ProcessorStatus.Decimal);
        Assert.False(cpu.ProcessorStatus.InterruptDisable);
        Assert.True(cpu.ProcessorStatus.Carry);
    }
}
