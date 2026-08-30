using System.Text;
using Highbyte.DotNet6502.Systems.Oric;
using Highbyte.DotNet6502.Systems.Oric.Audio;
using Highbyte.DotNet6502.Systems.Oric.Render;
using Highbyte.DotNet6502.Systems.Oric.Snapshots;
using Highbyte.DotNet6502.Systems.Oric.Tape;
using Highbyte.DotNet6502.Systems.Snapshots;
using Microsoft.Extensions.Logging.Abstractions;
using OricMachine = Highbyte.DotNet6502.Systems.Oric.Oric;

namespace Highbyte.DotNet6502.Systems.Tests.Snapshots;

public sealed class OricSnapshotRoundTripTests
{
    private const ushort ProgramStart = 0x0600;

    private static readonly byte[] s_program =
    [
        0xe8,                   // INX
        0xc8,                   // INY
        0xea,                   // NOP
        0x4c, 0x00, 0x06,       // JMP $0600
    ];

    [Fact]
    public void Oric_implements_snapshot_provider_with_all_machine_modules()
    {
        var provider = (ISystemSnapshotProvider)BuildOric();

        Assert.Equal(OricMachine.SystemName, provider.MachineId.SystemName);
        Assert.Equal(OricMachine.SnapshotVersion, provider.MachineId.SupportedSnapshotVersion);
        Assert.Equal(
            [
                Cpu6502SnapshotModule.ModuleName,
                OricCoreSnapshotModule.ModuleName,
                OricViaSnapshotModule.ModuleName,
                OricAySnapshotModule.ModuleName,
                OricRasterSnapshotModule.ModuleName,
                OricTapeSnapshotModule.ModuleName,
            ],
            provider.GetSnapshotModules().Select(module => module.Name));
    }

    [Fact]
    public void Round_trip_restores_machine_timing_audio_and_inserted_tape_then_resumes_deterministically()
    {
        var source = BuildOric();
        for (var index = 0; index < s_program.Length; index++)
            source.Mem[(ushort)(ProgramStart + index)] = s_program[index];
        source.Mem[0x9000] = 0xa5;
        source.Mem[0xbb80] = (byte)'S';
        source.CPU.PC = ProgramStart;
        source.CPU.A = 0x42;

        ConfigureVia(source);
        ConfigureAy(source);

        var firstTape = BuildTap("FIRST", [0x01, 0x02]);
        var secondTape = BuildTap("SECOND", [0x03, 0x04, 0x05]);
        byte[] tapeData = [.. firstTape, 0x00, .. secondTape];
        source.InsertTape(tapeData, "session.tap");
        Assert.True(source.SeekToNextTapeRecord());

        for (var instruction = 0; instruction < 80; instruction++)
            source.ExecuteOneInstruction(out _);

        Span<float> warmupSamples = stackalloc float[128];
        Assert.True(source.Ay.AdvanceCycles(1_234, warmupSamples) > 0);

        using var snapshotStream = new MemoryStream();
        var service = new SnapshotService();
        service.Save(source, snapshotStream);

        snapshotStream.Position = 0;
        var restored = BuildOric();
        var result = service.Restore(restored, snapshotStream);

        Assert.Empty(result.Warnings);
        Assert.Equal(OricMachine.SystemName, result.Manifest.Machine.SystemName);
        Assert.Contains(result.Manifest.Media, media => media.Id == OricTapeSnapshotModule.MediaId);

        Assert.Equal(source.CPU.PC, restored.CPU.PC);
        Assert.Equal(source.CPU.A, restored.CPU.A);
        Assert.Equal(source.CPU.X, restored.CPU.X);
        Assert.Equal(source.CPU.Y, restored.CPU.Y);
        Assert.Equal(source.CPU.ExecState.CyclesConsumed, restored.CPU.ExecState.CyclesConsumed);
        Assert.Equal((byte)0xa5, restored.Mem[0x9000]);
        Assert.Equal((byte)'S', restored.Mem[0xbb80]);

        Assert.Equal(source.Via.PortAOutput, restored.Via.PortAOutput);
        Assert.Equal(source.Via.PortBOutput, restored.Via.PortBOutput);
        Assert.Equal(source.Via.DataDirectionA, restored.Via.DataDirectionA);
        Assert.Equal(source.Via.DataDirectionB, restored.Via.DataDirectionB);
        Assert.Equal(source.Via.InterruptEnable, restored.Via.InterruptEnable);
        Assert.Equal(source.Via.InterruptFlags, restored.Via.InterruptFlags);
        Assert.Equal(source.Via.Ca2, restored.Via.Ca2);
        Assert.Equal(source.Via.Cb2, restored.Via.Cb2);
        Assert.Equal(source.Via.Read(0x0a), restored.Via.Read(0x0a));
        Assert.Equal(source.Via.Read(0x0b), restored.Via.Read(0x0b));
        Assert.Equal(source.Via.Read(0x0c), restored.Via.Read(0x0c));

        Assert.Equal(source.Ay.SelectedRegister, restored.Ay.SelectedRegister);
        for (var register = 0; register < Ay38912.RegisterCount; register++)
            Assert.Equal(source.Ay.ReadRegister(register), restored.Ay.ReadRegister(register));

        Assert.Equal(source.RasterClock.FrameNumber, restored.RasterClock.FrameNumber);
        Assert.Equal(source.RasterClock.RasterLine, restored.RasterClock.RasterLine);
        Assert.Equal(source.RasterClock.CycleInLine, restored.RasterClock.CycleInLine);

        Assert.True(restored.Tape.IsInserted);
        Assert.Equal("session.tap", restored.Tape.SourceName);
        Assert.Equal(source.Tape.Length, restored.Tape.Length);
        Assert.Equal(source.Tape.Position, restored.Tape.Position);
        Assert.Equal("SECOND", restored.Tape.CurrentFile?.Name);

        for (var instruction = 0; instruction < 120; instruction++)
        {
            source.ExecuteOneInstruction(out _);
            restored.ExecuteOneInstruction(out _);
        }

        Assert.Equal(source.CPU.PC, restored.CPU.PC);
        Assert.Equal(source.CPU.X, restored.CPU.X);
        Assert.Equal(source.CPU.Y, restored.CPU.Y);
        Assert.Equal(source.RasterClock.RasterLine, restored.RasterClock.RasterLine);
        Assert.Equal(source.RasterClock.CycleInLine, restored.RasterClock.CycleInLine);
        Assert.Equal(ReadTimerOne(source), ReadTimerOne(restored));
        Assert.Equal(ReadTimerTwo(source), ReadTimerTwo(restored));
        Assert.Equal(source.Via.InterruptFlags, restored.Via.InterruptFlags);

        source.ExecuteOneFrame();
        restored.ExecuteOneFrame();
        Assert.Equal(source.RasterClock.FrameNumber, restored.RasterClock.FrameNumber);
        var sourceFrame = source.RenderProviders.OfType<OricRasterizer>().Single().CurrentFrontBuffer.ToArray();
        var restoredFrame = restored.RenderProviders.OfType<OricRasterizer>().Single().CurrentFrontBuffer.ToArray();
        Assert.Equal(sourceFrame, restoredFrame);

        var sourceSamples = new float[128];
        var restoredSamples = new float[128];
        var sourceCount = source.Ay.AdvanceCycles(2_000, sourceSamples);
        var restoredCount = restored.Ay.AdvanceCycles(2_000, restoredSamples);
        Assert.Equal(sourceCount, restoredCount);
        Assert.Equal(sourceSamples[..sourceCount], restoredSamples[..restoredCount]);
    }

    [Fact]
    public void Restore_warns_when_target_vsync_modification_differs()
    {
        var source = BuildOric(vSyncHackEnabled: true);
        using var snapshotStream = new MemoryStream();
        var service = new SnapshotService();
        service.Save(source, snapshotStream);

        snapshotStream.Position = 0;
        var result = service.Restore(BuildOric(vSyncHackEnabled: false), snapshotStream);

        Assert.Contains(result.Warnings, warning => warning.Contains("VSync", StringComparison.Ordinal));
    }

    private static OricMachine BuildOric(bool vSyncHackEnabled = false)
        => new(
            new OricConfig { AudioEnabled = false, VSyncHackEnabled = vSyncHackEnabled },
            NullLoggerFactory.Instance);

    private static void ConfigureVia(OricMachine oric)
    {
        oric.Via.Write(0x03, 0xff);
        oric.Via.Write(0x02, 0x1f);
        oric.Via.Write(0x01, 0x0e);
        oric.Via.Write(0x00, 0x13);
        oric.Via.Write(0x0a, 0x5a);
        oric.Via.Write(0x0b, 0x40);
        oric.Via.Write(0x0c, 0xee);
        oric.Via.Write(0x04, 0x56);
        oric.Via.Write(0x05, 0x04);
        oric.Via.Write(0x08, 0xaa);
        oric.Via.Write(0x09, 0x08);
        oric.Via.Write(0x0e, 0xe0);
    }

    private static void ConfigureAy(OricMachine oric)
    {
        oric.Ay.WriteRegister(0, 0x09);
        oric.Ay.WriteRegister(1, 0x03);
        oric.Ay.WriteRegister(6, 0x07);
        oric.Ay.WriteRegister(7, 0x38);
        oric.Ay.WriteRegister(8, 0x1f);
        oric.Ay.WriteRegister(11, 0x34);
        oric.Ay.WriteRegister(12, 0x02);
        oric.Ay.WriteRegister(13, 0x0e);
        oric.Ay.SelectRegister(9);
    }

    private static ushort ReadTimerOne(OricMachine oric)
    {
        var high = oric.Via.Read(0x05);
        var low = oric.Via.Read(0x04);
        return (ushort)((high << 8) | low);
    }

    private static ushort ReadTimerTwo(OricMachine oric)
    {
        var high = oric.Via.Read(0x09);
        var low = oric.Via.Read(0x08);
        return (ushort)((high << 8) | low);
    }

    private static byte[] BuildTap(string name, byte[] payload)
    {
        var startAddress = OricMachine.BasicProgramDefaultStartAddress;
        var endAddress = (ushort)(startAddress + payload.Length - 1);
        return
        [
            0x16, 0x16, 0x16, 0x24,
            0x00, 0x00, OricTapFile.BasicFileType, 0x00,
            (byte)(endAddress >> 8), (byte)endAddress,
            (byte)(startAddress >> 8), (byte)startAddress,
            0x00,
            .. Encoding.ASCII.GetBytes(name), 0x00,
            .. payload,
        ];
    }
}
