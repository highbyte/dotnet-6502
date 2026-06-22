using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Systems.Commodore64.Cartridge;
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

    [Theory]
    [InlineData(true, true, 31)]
    [InlineData(true, false, 15)]
    [InlineData(false, false, 7)]
    [InlineData(false, true, 23)]
    public void Cartridge_Lines_And_Cpu_Port_Derive_The_Memory_Configuration(
        bool gameHigh,
        bool exromHigh,
        byte expectedBank)
    {
        var c64 = BuildC64();

        c64.AttachCartridge(new LineStateCartridge(new C64CartridgeLines(gameHigh, exromHigh)));

        Assert.Equal(expectedBank, c64.CurrentBank);
        Assert.Equal(expectedBank, c64.Mem.CurrentConfiguration);
    }

    [Fact]
    public void Cartridge_Line_Changes_And_Detach_Recompute_The_Memory_Configuration()
    {
        var c64 = BuildC64();
        var cartridge = new LineStateCartridge(C64CartridgeLines.Released);
        c64.AttachCartridge(cartridge);

        cartridge.SetLines(new C64CartridgeLines(GameHigh: false, ExromHigh: false));
        Assert.Equal(7, c64.CurrentBank);

        c64.Mem.Write(0x0001, 0x06);
        Assert.Equal(6, c64.CurrentBank);

        c64.DetachCartridge();
        Assert.Equal(30, c64.CurrentBank);
        Assert.Equal(30, c64.Mem.CurrentConfiguration);
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

    private sealed class LineStateCartridge(C64CartridgeLines lines) : IC64Cartridge
    {
        public string Name => "Line state test cartridge";
        public C64CartridgeLines Lines { get; private set; } = lines;
        public event Action? LinesChanged;

        public void SetLines(C64CartridgeLines lines)
        {
            Lines = lines;
            LinesChanged?.Invoke();
        }

        public bool HandlesIORead(ushort address) => false;
        public byte ReadIO(ushort address) => throw new InvalidOperationException();
        public bool HandlesIOWrite(ushort address) => false;
        public void WriteIO(ushort address, byte value) => throw new InvalidOperationException();
        public bool HasROML => false;
        public byte ReadROML(ushort address) => throw new InvalidOperationException();
        public bool HasROMH => false;
        public byte ReadROMH(ushort address) => throw new InvalidOperationException();
        public void Tick() { }
        public void Reset() { }
        public void Dispose() { }
    }
}
