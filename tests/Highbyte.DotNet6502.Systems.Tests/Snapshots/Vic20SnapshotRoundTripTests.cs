using Highbyte.DotNet6502.Systems.Snapshots;
using Highbyte.DotNet6502.Systems.Vic20.Config;
using Highbyte.DotNet6502.Systems.Vic20.Snapshots;
using Highbyte.DotNet6502.Systems.Vic20.TimerAndPeripheral;
using Microsoft.Extensions.Logging.Abstractions;
using Vic20System = Highbyte.DotNet6502.Systems.Vic20.Vic20;

namespace Highbyte.DotNet6502.Systems.Tests.Snapshots;

public class Vic20SnapshotRoundTripTests
{
    // Program loaded into main RAM ($1000) that writes color RAM and advances X/Y in a loop:
    //   1000: A9 07      LDA #$07
    //   1002: 8D 00 94   STA $9400   (color RAM)
    //   1005: E8         INX
    //   1006: C8         INY
    //   1007: 4C 05 10   JMP $1005
    private const ushort ProgramStart = 0x1000;
    private static readonly byte[] Program =
    {
        0xA9, 0x07,
        0x8D, 0x00, 0x94,
        0xE8,
        0xC8,
        0x4C, 0x05, 0x10,
    };

    private static Vic20System BuildVic20()
        => new Vic20System(new Vic20Config(), NullLoggerFactory.Instance);

    [Fact]
    public void Vic20_implements_snapshot_provider_with_core_and_via_modules()
    {
        var provider = (ISystemSnapshotProvider)BuildVic20();
        Assert.Equal(Vic20System.SystemName, provider.MachineId.SystemName);
        var names = provider.GetSnapshotModules().Select(m => m.Name).ToArray();
        Assert.Contains(Cpu6502SnapshotModule.ModuleName, names);
        Assert.Contains(Vic20CoreSnapshotModule.ModuleName, names);
        Assert.Contains(Vic20ViaSnapshotModule.ModuleName, names);
    }

    [Fact]
    public void Vic20_round_trip_restores_memory_video_via_state_and_resumes()
    {
        var source = BuildVic20();

        // Diverge VIC-I register + VIA state from defaults.
        source.Mem.Write(0x9005, 0xAB);             // a VIC-I register (RAM-mapped)
        source.Mem.Write(ViaAddr.VIA1_DDRA, 0xFF);
        source.Mem.Write(ViaAddr.VIA1_DDRB, 0x00);
        source.Mem.Write(ViaAddr.VIA1_T1CL, 0x34);  // Timer 1 latch low
        source.Mem.Write(ViaAddr.VIA1_T1CH, 0x12);  // loads counter $1234 and starts Timer 1
        source.Mem.Write(ViaAddr.VIA1_IER, 0xC0);   // enable Timer 1 interrupt
        source.Mem.Write(ViaAddr.VIA2_PORTB, 0xFE); // keyboard column strobe

        // Load + run the program so RAM, color RAM and CPU registers diverge.
        for (int i = 0; i < Program.Length; i++)
            source.Mem.Write((ushort)(ProgramStart + i), Program[i]);
        source.CPU.PC = ProgramStart;
        for (int i = 0; i < 20; i++)
            source.ExecuteOneInstruction(out _);

        Assert.Equal((byte)0x07, source.Mem.Read(0x9400));

        // Snapshot the source state.
        var srcReg = source.Mem.Read(0x9005);
        var srcDdra = source.Mem.Read(ViaAddr.VIA1_DDRA);
        var srcDdrb = source.Mem.Read(ViaAddr.VIA1_DDRB);
        var srcT1cl = source.Mem.Read(ViaAddr.VIA1_T1CL);
        var srcIer = source.Mem.Read(ViaAddr.VIA1_IER);
        var srcPortB = source.Mem.Read(ViaAddr.VIA2_PORTB);

        using var snapshotStream = new MemoryStream();
        var service = new SnapshotService();
        service.Save(source, snapshotStream);

        // Restore into a fresh, perturbed machine.
        snapshotStream.Position = 0;
        var restored = BuildVic20();
        restored.CPU.PC = 0x0000;
        restored.Mem.Write(0x9005, 0x00);
        var result = service.Restore(restored, snapshotStream);

        Assert.Empty(result.Warnings);
        // Core memory + VIC-I register + color RAM.
        Assert.Equal(srcReg, restored.Mem.Read(0x9005));
        Assert.Equal((byte)0x07, restored.Mem.Read(0x9400));
        Assert.Equal(Program[0], restored.Mem.Read(ProgramStart));
        // VIA state (held outside memory).
        Assert.Equal(srcDdra, restored.Mem.Read(ViaAddr.VIA1_DDRA));
        Assert.Equal(srcDdrb, restored.Mem.Read(ViaAddr.VIA1_DDRB));
        Assert.Equal(srcT1cl, restored.Mem.Read(ViaAddr.VIA1_T1CL));
        Assert.Equal(srcIer, restored.Mem.Read(ViaAddr.VIA1_IER));
        Assert.Equal(srcPortB, restored.Mem.Read(ViaAddr.VIA2_PORTB));
        // CPU registers.
        Assert.Equal(source.CPU.PC, restored.CPU.PC);
        Assert.Equal(source.CPU.X, restored.CPU.X);
        Assert.Equal(source.CPU.Y, restored.CPU.Y);

        // Continued execution mirrors the source machine.
        for (int i = 0; i < 30; i++)
        {
            source.ExecuteOneInstruction(out _);
            restored.ExecuteOneInstruction(out _);
        }
        Assert.Equal(source.CPU.PC, restored.CPU.PC);
        Assert.Equal(source.CPU.X, restored.CPU.X);
        Assert.Equal(source.CPU.Y, restored.CPU.Y);
    }
}
