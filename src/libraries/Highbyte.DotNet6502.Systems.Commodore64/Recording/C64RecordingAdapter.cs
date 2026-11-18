using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Highbyte.DotNet6502.Systems.Commodore64.Snapshots;
using Highbyte.DotNet6502.Systems.Recording;
using Highbyte.DotNet6502.Systems.Snapshots;
using Highbyte.DotNet6502.Systems.Commodore64.Cartridge;
using Highbyte.DotNet6502.Systems.Commodore64.Input;
using Highbyte.DotNet6502.Systems.Commodore64.TimerAndPeripheral.DiskDrive;

namespace Highbyte.DotNet6502.Systems.Commodore64.Recording;

public sealed class C64RecordingAdapter(C64 c64) : IRecordingAdapter
{
    public string SystemName => C64.SystemName;
    public string Schema => "c64-resolved-input";
    public int SchemaVersion => 1;
    public double FramesPerSecond => (double)c64.CpuFrequencyHz / c64.Vic2.Vic2Model.CyclesPerFrame;
    public string CompatibilityId => ComputeCompatibilityId(includeLegacyBuild: false);
    public string LegacyCompatibilityId => ComputeCompatibilityId(includeLegacyBuild: true);

    private string ComputeCompatibilityId(bool includeLegacyBuild)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        if (includeLegacyBuild)
        {
            // Version 1 placed both strings before the machine data in the hash.
            writer.Write(DemoManifest.EngineSourceId);
            writer.Write(typeof(C64).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown");
        }
        writer.Write(c64.Model.Name);
        writer.Write(c64.CPU.CpuModelId);
        writer.Write((int)c64.CPU.CompatibilityProfile);
        writer.Write((int)c64.SidEmulation.Mode);
        writer.Write(c64.Vic2.SpriteManager.PerLineCollisionEnabled);
        writer.Write(c64.RenderProvider?.GetType().FullName ?? "none");
        if (c64.RenderProvider is Render.Rasterizer.Vic2Rasterizer rasterizer)
            writer.Write((int)rasterizer.PixelGeneratorType);
        foreach (var rom in c64.ROMData.OrderBy(r => r.Key, StringComparer.Ordinal))
        {
            writer.Write(rom.Key);
            writer.Write(SHA256.HashData(rom.Value));
        }
        return Convert.ToHexStringLower(SHA256.HashData(stream.ToArray()));
    }

    public void ValidateSession()
    {
        if (!c64.IsAtFrameBoundary)
            throw new InvalidOperationException("Recording/replay requires a complete frame boundary. Resume the machine for a full frame before recording.");
        if (c64.CPU.IsHalted)
            throw new InvalidOperationException("A halted C64 cannot record or replay.");
        if (c64.TextPaste.HasCharactersPending)
            throw new InvalidOperationException("Wait for pending buffer paste to finish before recording/replay.");
        if (c64.InputConsumer is Input.C64InputHandler { CodingAssistantEnabled: true })
            throw new InvalidOperationException("Disable the BASIC coding assistant before recording/replay.");
        if (c64.CartridgeSlot.AttachedCartridge != null && c64.AttachedCrtImageBytes == null)
            throw new InvalidOperationException("Recording/replay does not support external-I/O cartridges such as SwiftLink.");
        if (c64.CartridgeSlot.AttachedCartridge is not null and not ISnapshotableCartridge)
            throw new InvalidOperationException("This cartridge cannot preserve its live state for recording/replay.");
        if (c64.IECBus.Devices.OfType<DiskDrive1541>().Any(drive => drive.HasActiveTransfer))
            throw new InvalidOperationException("Recording/replay does not support in-flight IEC disk transfers.");
    }

    public byte[] CaptureInput() => C64ResolvedInput.Capture(c64.Cia1);
    public void ValidateMemoryInspection(ushort address, int length)
    {
        if (length < 0 || length > 65536) throw new ArgumentOutOfRangeException(nameof(length));
        if (!c64.IsIOVisible) return;
        for (int i = 0; i < length; i++)
        {
            var current = (ushort)(address + i);
            if (current is >= 0xD000 and <= 0xDFFF)
                throw new InvalidOperationException("Reading C64 I/O can change chip state and is disabled during recording/replay. RAM inspection and CPU register queries remain available.");
        }
    }
    public void ValidateInput(ReadOnlySpan<byte> input) => C64ResolvedInput.Validate(input);
    public bool CanRepeatInput(ReadOnlySpan<byte> input) => C64ResolvedInput.CanRepeat(input);
    public void ApplyInput(ReadOnlySpan<byte> input) => C64ResolvedInput.ApplyFrame(c64.Cia1, input);
    public void ReleaseInput() => C64ResolvedInput.Release(c64.Cia1);

    public byte[] ComputeStateDigest()
    {
        using var hash = SHA256.Create();
        using var sink = new CryptoStream(Stream.Null, hash, CryptoStreamMode.Write);
        using var stream = new BufferedStream(sink);
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        Cpu6502Checkpoint.Write(writer, c64.CPU);
        writer.Write(c64.RAM);
        writer.Write(c64.IO);
        writer.Write(CaptureInput());
        var context = new SnapshotCaptureContext(c64, SnapshotSaveOptions.Default);
        var moduleWriter = new SnapshotModuleWriter(stream);
        new C64CiaSnapshotModule().Capture(moduleWriter, context);
        c64.Vic2.WriteCheckpoint(writer);
        c64.Sid.InternalSidState.CaptureBusState(moduleWriter);
        c64.SidEmulation.Core.WriteExecutionState(writer);
        if (c64.CartridgeSlot.AttachedCartridge is ISnapshotableCartridge cartridge)
            writer.Write(cartridge.CaptureSnapshotState());
        foreach (var drive in c64.IECBus.Devices.OfType<DiskDrive1541>())
        {
            writer.Write(drive.IsDisketteInserted);
            if (drive.MountedDiskImage != null) writer.Write(SHA256.HashData(drive.MountedDiskImage.RawDiskData));
        }
        stream.Flush();
        sink.FlushFinalBlock();
        return hash.Hash!;
    }
}
