using Highbyte.DotNet6502.Systems.Oric.Audio;
using Highbyte.DotNet6502.Systems.Snapshots;

namespace Highbyte.DotNet6502.Systems.Oric.Snapshots;

/// <summary>Snapshot module for AY-3-8912 registers and deterministic generator phase.</summary>
public sealed class OricAySnapshotModule : ISnapshotModule
{
    public const string ModuleName = "oric-ay";

    public string Name => ModuleName;
    public int Version => 1;
    public bool Required => true;

    public void Capture(SnapshotModuleWriter writer, SnapshotCaptureContext context)
    {
        var state = ((Oric)context.System).Ay.GetSnapshotState();
        writer.WriteBytes(state.Registers);
        writer.WriteInt32(state.SelectedRegister);
        for (var channel = 0; channel < state.ToneCounters.Length; channel++)
        {
            writer.WriteInt32(state.ToneCounters[channel]);
            writer.WriteBool(state.ToneHigh[channel]);
        }
        writer.WriteUInt64(BitConverter.DoubleToUInt64Bits(state.SampleCycleAccumulator));
        writer.WriteInt32(state.NoiseCounter);
        writer.WriteBool(state.NoiseHigh);
        writer.WriteUInt32(state.NoiseLfsr);
        writer.WriteInt32(state.EnvelopeCounter);
        writer.WriteInt32(state.EnvelopeStep);
        writer.WriteInt32(state.EnvelopeDirection);
        writer.WriteBool(state.EnvelopeHolding);
    }

    public void Restore(SnapshotModuleReader reader, SnapshotRestoreContext context)
    {
        var registers = reader.ReadBytes()
            ?? throw new SnapshotException("oric-ay: register bytes were missing from the snapshot.");
        var selectedRegister = reader.ReadInt32();
        var toneCounters = new int[3];
        var toneHigh = new bool[3];
        for (var channel = 0; channel < toneCounters.Length; channel++)
        {
            toneCounters[channel] = reader.ReadInt32();
            toneHigh[channel] = reader.ReadBool();
        }

        var state = new Ay38912SnapshotState(
            Registers: registers,
            SelectedRegister: selectedRegister,
            ToneCounters: toneCounters,
            ToneHigh: toneHigh,
            SampleCycleAccumulator: BitConverter.UInt64BitsToDouble(reader.ReadUInt64()),
            NoiseCounter: reader.ReadInt32(),
            NoiseHigh: reader.ReadBool(),
            NoiseLfsr: reader.ReadUInt32(),
            EnvelopeCounter: reader.ReadInt32(),
            EnvelopeStep: reader.ReadInt32(),
            EnvelopeDirection: reader.ReadInt32(),
            EnvelopeHolding: reader.ReadBool());

        try
        {
            ((Oric)context.System).Ay.RestoreSnapshotState(state);
        }
        catch (ArgumentException exception)
        {
            throw new SnapshotException($"oric-ay: invalid generator state: {exception.Message}");
        }
    }
}
