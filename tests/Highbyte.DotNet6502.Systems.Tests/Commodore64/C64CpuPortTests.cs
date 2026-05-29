using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Systems.Commodore64.Config;
using Microsoft.Extensions.Logging.Abstractions;

namespace Highbyte.DotNet6502.Systems.Tests.Commodore64;

public class C64CpuPortTests
{
    private const ushort KernalProbeAddress = 0xFF84;
    private const ushort TestProgramAddress = 0xC000;

    [Fact]
    public void Startup_6510_Port_Uses_C64_Defaults()
    {
        var c64 = BuildC64();

        Assert.Equal(0x2F, c64.Mem.Read(0x0000));
        Assert.Equal(0x37, c64.Mem.Read(0x0001));
        Assert.Equal(31, c64.CurrentBank);
    }

    [Fact]
    public void Kernal_Rom_Stays_Visible_When_Bank_Lines_Are_Configured_As_Inputs()
    {
        var c64 = BuildC64();
        SeedKernalAndRamProbe(c64, kernalValue: 0x42, ramValue: 0x00);

        c64.Mem.Write(0x0000, 0x00);
        c64.Mem.Write(0x0001, 0x00);

        Assert.Equal(0x17, c64.Mem.Read(0x0001));
        Assert.Equal(0x07, c64.CurrentBank & 0x07);
        Assert.Equal(0x42, c64.Mem.Read(KernalProbeAddress));
    }

    [Fact]
    public void Dec_And_Inc_Zero_Page_01_Use_The_Real_Port_Value()
    {
        var c64 = BuildC64();
        SeedKernalAndRamProbe(c64, kernalValue: 0x42, ramValue: 0x00);

        c64.Mem.Write((ushort)(TestProgramAddress + 0), 0xC6); // DEC $01
        c64.Mem.Write((ushort)(TestProgramAddress + 1), 0x01);
        c64.Mem.Write((ushort)(TestProgramAddress + 2), 0xE6); // INC $01
        c64.Mem.Write((ushort)(TestProgramAddress + 3), 0x01);

        c64.CPU.PC = TestProgramAddress;

        c64.CPU.ExecuteOneInstructionMinimal(c64.Mem);
        Assert.Equal(0x36, c64.Mem.Read(0x0001));
        Assert.Equal(0x06, c64.CurrentBank & 0x07);
        Assert.Equal(0x42, c64.Mem.Read(KernalProbeAddress));

        c64.CPU.ExecuteOneInstructionMinimal(c64.Mem);
        Assert.Equal(0x37, c64.Mem.Read(0x0001));
        Assert.Equal(0x07, c64.CurrentBank & 0x07);
        Assert.Equal(0x42, c64.Mem.Read(KernalProbeAddress));
    }

    private static C64 BuildC64()
    {
        return C64.BuildC64(new C64Config
        {
            LoadROMs = false,
            C64Model = "C64PAL",
            Vic2Model = "PAL"
        }, NullLoggerFactory.Instance);
    }

    private static void SeedKernalAndRamProbe(C64 c64, byte kernalValue, byte ramValue)
    {
        c64.ROMData[C64SystemConfig.KERNAL_ROM_NAME][KernalProbeAddress - 0xE000] = kernalValue;
        c64.RAM[KernalProbeAddress] = ramValue;
    }
}
