using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Systems.Commodore64.Config;
using Highbyte.DotNet6502.Systems.Commodore64.Snapshots;
using Highbyte.DotNet6502.Systems.Snapshots;
using Microsoft.Extensions.Logging.Abstractions;

namespace Highbyte.DotNet6502.Systems.Tests.Snapshots;

public class C64CoreSnapshotRoundTripTests
{
    // Small program at $C000 that writes to RAM and advances X/Y in a loop, so continued execution
    // after restore is observable:
    //   C000: A9 AA      LDA #$AA
    //   C002: 8D 00 40   STA $4000
    //   C005: E8         INX
    //   C006: C8         INY
    //   C007: 4C 05 C0   JMP $C005
    private const ushort ProgramStart = 0xC000;
    private static readonly byte[] Program =
    {
        0xA9, 0xAA,
        0x8D, 0x00, 0x40,
        0xE8,
        0xC8,
        0x4C, 0x05, 0xC0,
    };

    private static C64 BuildC64()
        => C64.BuildC64(new C64Config
        {
            LoadROMs = false,
            C64Model = "C64PAL",
            Vic2Model = "PAL"
        }, NullLoggerFactory.Instance);

    private static void LoadProgram(C64 c64)
    {
        for (int i = 0; i < Program.Length; i++)
            c64.RAM[ProgramStart + i] = Program[i];
        c64.CPU.PC = ProgramStart;
    }

    [Fact]
    public void C64_implements_snapshot_provider_with_cpu_and_core_modules()
    {
        var c64 = BuildC64();
        var provider = (ISystemSnapshotProvider)c64;

        Assert.Equal(C64.SystemName, provider.MachineId.SystemName);
        var moduleNames = provider.GetSnapshotModules().Select(m => m.Name).ToArray();
        Assert.Contains(Cpu6502SnapshotModule.ModuleName, moduleNames);
        Assert.Contains(C64CoreSnapshotModule.ModuleName, moduleNames);
    }

    [Fact]
    public void C64_round_trip_restores_registers_memory_bank_and_resumes()
    {
        // Arrange: run enough instructions to write RAM and diverge CPU registers from reset.
        var source = BuildC64();
        LoadProgram(source);
        for (int i = 0; i < 30; i++)
            source.ExecuteOneInstruction(out _);
        Assert.Equal((byte)0xAA, source.RAM[0x4000]);

        // Act: save, then restore into a fresh C64 that has been perturbed.
        using var snapshotStream = new MemoryStream();
        var service = new SnapshotService();
        service.Save(source, snapshotStream);

        snapshotStream.Position = 0;
        var restored = BuildC64();
        restored.CPU.PC = 0x0000;
        restored.CPU.A = 0x11;
        restored.RAM[0x4000] = 0x00;
        var result = service.Restore(restored, snapshotStream);

        // Assert: CPU registers, RAM and the active bank match the source at save time.
        Assert.Empty(result.Warnings);
        Assert.Equal(source.CPU.PC, restored.CPU.PC);
        Assert.Equal(source.CPU.SP, restored.CPU.SP);
        Assert.Equal(source.CPU.A, restored.CPU.A);
        Assert.Equal(source.CPU.X, restored.CPU.X);
        Assert.Equal(source.CPU.Y, restored.CPU.Y);
        Assert.Equal(source.CPU.ProcessorStatus.Value, restored.CPU.ProcessorStatus.Value);
        Assert.Equal(source.CurrentBank, restored.CurrentBank);
        Assert.Equal((byte)0xAA, restored.RAM[0x4000]);
        Assert.Equal(source.Mem.Read(0x0001), restored.Mem.Read(0x0001));

        // Assert: continued execution from the restored state mirrors the source machine.
        for (int i = 0; i < 40; i++)
        {
            source.ExecuteOneInstruction(out _);
            restored.ExecuteOneInstruction(out _);
        }
        Assert.Equal(source.CPU.PC, restored.CPU.PC);
        Assert.Equal(source.CPU.X, restored.CPU.X);
        Assert.Equal(source.CPU.Y, restored.CPU.Y);
    }

    [Fact]
    public void C64_round_trip_restores_vic2_display_pointers_and_bank()
    {
        // Set VIC-II screen/charset pointer ($D018) and VIC bank (CIA2 $DD00) to non-default values,
        // then snapshot. These drive cached VIC-II state that is re-derived on restore.
        var source = BuildC64();
        source.Mem.Write(0xD018, 0x14); // video matrix base $0400, char base $1000
        source.Mem.Write(0xDD00, 0x02); // CIA2 PRA bits 0-1 = %10 -> VIC bank 1
        var sourceVideoMatrix = source.Vic2.VideoMatrixBaseAddress;
        var sourceBank = source.Vic2.CurrentVIC2Bank;

        using var snapshotStream = new MemoryStream();
        new SnapshotService().Save(source, snapshotStream);

        snapshotStream.Position = 0;
        var restored = BuildC64(); // fresh: default VIC bank / pointers
        Assert.NotEqual(sourceBank, restored.Vic2.CurrentVIC2Bank);
        new SnapshotService().Restore(restored, snapshotStream);

        Assert.Equal(sourceVideoMatrix, restored.Vic2.VideoMatrixBaseAddress);
        Assert.Equal(sourceBank, restored.Vic2.CurrentVIC2Bank);
    }

    [Fact]
    public void C64_round_trip_restores_cia1_timer_state()
    {
        // Program CIA1 Timer A with a latch value and start it, then snapshot.
        var source = BuildC64();
        source.Mem.Write(0xDC04, 0x34); // Timer A latch low
        source.Mem.Write(0xDC05, 0x12); // Timer A latch high
        source.Mem.Write(0xDC0E, 0b0001_0001); // force-load latch + start Timer A

        var sourceTimerLo = source.Mem.Read(0xDC04);
        var sourceTimerHi = source.Mem.Read(0xDC05);

        using var snapshotStream = new MemoryStream();
        new SnapshotService().Save(source, snapshotStream);

        snapshotStream.Position = 0;
        var restored = BuildC64(); // fresh: Timer A not running, counter 0
        new SnapshotService().Restore(restored, snapshotStream);

        // The CIA Timer A current-value registers read back the restored live counter.
        Assert.Equal(sourceTimerLo, restored.Mem.Read(0xDC04));
        Assert.Equal(sourceTimerHi, restored.Mem.Read(0xDC05));
    }

    [Fact]
    public void C64_round_trip_restores_cia1_keyboard_port_registers()
    {
        // The KERNAL configures CIA1 Port A as outputs (DDRA=$FF) to drive keyboard columns; these
        // CIA1 port/DDR registers are cached outside IO storage. If not restored, the keyboard scan
        // reads no keys after restore.
        var source = BuildC64();
        source.Mem.Write(0xDC02, 0xFF); // DDRA = all outputs
        source.Mem.Write(0xDC03, 0x00); // DDRB = all inputs
        source.Mem.Write(0xDC00, 0xFE); // Port A column select

        using var snapshotStream = new MemoryStream();
        new SnapshotService().Save(source, snapshotStream);

        snapshotStream.Position = 0;
        var restored = BuildC64(); // fresh: DDRA defaults to 0
        Assert.NotEqual((byte)0xFF, restored.Mem.Read(0xDC02));
        new SnapshotService().Restore(restored, snapshotStream);

        Assert.Equal(source.Mem.Read(0xDC02), restored.Mem.Read(0xDC02));
        Assert.Equal(source.Mem.Read(0xDC03), restored.Mem.Read(0xDC03));
        Assert.Equal(source.Mem.Read(0xDC00), restored.Mem.Read(0xDC00));
    }

    [Fact]
    public void C64_round_trip_preserves_changed_cpu_port_bank()
    {
        // Switch the 6510 CPU port to bank out BASIC/KERNAL ROM (all-RAM bank), then snapshot.
        var source = BuildC64();
        source.Mem.Write(0x0001, 0x30); // data register: lower banking bits low -> RAM visible
        var sourceBank = source.CurrentBank;

        using var snapshotStream = new MemoryStream();
        new SnapshotService().Save(source, snapshotStream);

        snapshotStream.Position = 0;
        var restored = BuildC64(); // fresh: defaults to bank 31
        Assert.NotEqual(sourceBank, restored.CurrentBank);

        new SnapshotService().Restore(restored, snapshotStream);

        Assert.Equal(sourceBank, restored.CurrentBank);
        Assert.Equal(source.Mem.Read(0x0001), restored.Mem.Read(0x0001));
    }
}
