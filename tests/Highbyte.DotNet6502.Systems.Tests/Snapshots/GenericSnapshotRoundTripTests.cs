using Highbyte.DotNet6502.Systems.Generic;
using Highbyte.DotNet6502.Systems.Generic.Snapshots;
using Highbyte.DotNet6502.Systems.Snapshots;
using Highbyte.DotNet6502.Utils;
using Microsoft.Extensions.Logging.Abstractions;

namespace Highbyte.DotNet6502.Systems.Tests.Snapshots;

public class GenericSnapshotRoundTripTests
{
    // A tiny program loaded at 0xC000 that increments memory locations so that continued
    // execution after restore is observable:
    //   C000: A9 05      LDA #$05
    //   C002: 8D 00 20   STA $2000
    //   C005: E8         INX
    //   C006: C8         INY
    //   C007: 4C 05 C0   JMP $C005   (loop INX/INY forever)
    private const ushort ProgramStart = 0xC000;
    private static readonly byte[] Program =
    {
        0xA9, 0x05,
        0x8D, 0x00, 0x20,
        0xE8,
        0xC8,
        0x4C, 0x05, 0xC0,
    };

    private static GenericComputer BuildComputer()
    {
        var computer = new GenericComputerBuilder(new NullLoggerFactory())
            .WithCPU()
            .WithMemory(1024 * 64)
            .Build();
        computer.Mem.StoreData(ProgramStart, Program);
        computer.CPU.PC = ProgramStart;
        return computer;
    }

    [Fact]
    public void Generic_round_trip_restores_registers_memory_and_resumes_at_same_instruction()
    {
        // Arrange: run a handful of instructions so CPU registers and memory have diverged from reset.
        var source = BuildComputer();
        for (int i = 0; i < 10; i++)
            source.ExecuteOneInstruction(out _);

        // Sanity: the program ran (wrote to $2000, advanced X/Y, left BASIC-ish reset state).
        Assert.Equal((byte)0x05, source.Mem[0x2000]);

        // Act: save the running state, then restore into a fresh, identically built computer.
        using var snapshotStream = new MemoryStream();
        var service = new SnapshotService();
        service.Save(source, snapshotStream);

        snapshotStream.Position = 0;
        var restored = BuildComputer();
        // Perturb the fresh computer so the restore is actually doing the work.
        restored.CPU.PC = 0x0000;
        restored.CPU.A = 0xFF;
        var result = service.Restore(restored, snapshotStream);

        // Assert: CPU registers, flags and memory match the source at save time.
        Assert.Empty(result.Warnings);
        Assert.Equal(source.CPU.PC, restored.CPU.PC);
        Assert.Equal(source.CPU.SP, restored.CPU.SP);
        Assert.Equal(source.CPU.A, restored.CPU.A);
        Assert.Equal(source.CPU.X, restored.CPU.X);
        Assert.Equal(source.CPU.Y, restored.CPU.Y);
        Assert.Equal(source.CPU.ProcessorStatus.Value, restored.CPU.ProcessorStatus.Value);
        Assert.Equal((byte)0x05, restored.Mem[0x2000]);

        // Assert: continued execution from the restored state mirrors the source machine.
        for (int i = 0; i < 25; i++)
        {
            source.ExecuteOneInstruction(out _);
            restored.ExecuteOneInstruction(out _);
        }
        Assert.Equal(source.CPU.PC, restored.CPU.PC);
        Assert.Equal(source.CPU.X, restored.CPU.X);
        Assert.Equal(source.CPU.Y, restored.CPU.Y);
    }

    [Fact]
    public void Saved_package_contains_manifest_and_expected_modules()
    {
        var source = BuildComputer();
        using var snapshotStream = new MemoryStream();
        new SnapshotService().Save(source, snapshotStream);

        snapshotStream.Position = 0;
        using var archive = new System.IO.Compression.ZipArchive(snapshotStream, System.IO.Compression.ZipArchiveMode.Read);
        Assert.NotNull(archive.GetEntry(SnapshotService.ManifestEntryName));
        Assert.NotNull(archive.GetEntry($"{SnapshotService.ModulesDirectory}/{Cpu6502SnapshotModule.ModuleName}.bin"));
        Assert.NotNull(archive.GetEntry($"{SnapshotService.ModulesDirectory}/{GenericMemorySnapshotModule.ModuleName}.bin"));
    }

    [Fact]
    public void Config_blocks_round_trip_as_opaque_json()
    {
        var source = BuildComputer();
        var service = new SnapshotService();

        var options = new SnapshotSaveOptions
        {
            Config = new SnapshotConfigContent
            {
                SystemConfigJson = "{\"keyboardJoystickEnabled\":true}",
                HostJson = "{\"hostApp\":\"desktop\",\"settings\":{\"audioEnabled\":false}}",
            },
        };

        using var snapshotStream = new MemoryStream();
        service.Save(source, snapshotStream, options);

        // Present in the package under the config/ directory.
        snapshotStream.Position = 0;
        using (var archive = new System.IO.Compression.ZipArchive(snapshotStream, System.IO.Compression.ZipArchiveMode.Read, leaveOpen: true))
        {
            Assert.NotNull(archive.GetEntry(SnapshotService.ConfigSystemEntryName));
            Assert.NotNull(archive.GetEntry(SnapshotService.ConfigHostEntryName));
        }

        // Surfaced verbatim on restore.
        snapshotStream.Position = 0;
        var restored = BuildComputer();
        var result = service.Restore(restored, snapshotStream);

        Assert.NotNull(result.Config);
        Assert.Equal(options.Config.SystemConfigJson, result.Config!.SystemConfigJson);
        Assert.Equal(options.Config.HostJson, result.Config!.HostJson);
    }

    [Fact]
    public void No_config_block_written_or_returned_when_not_requested()
    {
        var source = BuildComputer();
        var service = new SnapshotService();

        using var snapshotStream = new MemoryStream();
        service.Save(source, snapshotStream); // no config

        snapshotStream.Position = 0;
        using (var archive = new System.IO.Compression.ZipArchive(snapshotStream, System.IO.Compression.ZipArchiveMode.Read, leaveOpen: true))
        {
            Assert.Null(archive.GetEntry(SnapshotService.ConfigSystemEntryName));
            Assert.Null(archive.GetEntry(SnapshotService.ConfigHostEntryName));
        }

        snapshotStream.Position = 0;
        var restored = BuildComputer();
        var result = service.Restore(restored, snapshotStream);
        Assert.Null(result.Config);
    }
}
