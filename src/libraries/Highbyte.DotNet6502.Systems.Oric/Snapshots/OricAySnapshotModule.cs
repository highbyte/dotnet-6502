using Highbyte.DotNet6502.Systems.Oric.Audio;
using Highbyte.DotNet6502.Systems.Snapshots;

namespace Highbyte.DotNet6502.Systems.Oric.Snapshots;

/// <summary>Snapshot module for AY-3-8912 registers and deterministic generator phase.</summary>
public sealed class OricAySnapshotModule : ISnapshotModule
{
    public const string ModuleName = "oric-ay";

    public string Name => ModuleName;
    public int Version => 2;
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
        writer.WriteInt32(state.SamplePhase);
        writer.WriteUInt64(BitConverter.DoubleToUInt64Bits(state.SampleArea));
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

        var storedVersion = context.Manifest.Modules.FirstOrDefault(module => module.Name == ModuleName)?.Version ?? 1;
        int samplePhase = 0;
        double sampleArea = 0;
        if (storedVersion >= 2)
        {
            samplePhase = reader.ReadInt32();
            sampleArea = BitConverter.UInt64BitsToDouble(reader.ReadUInt64());
        }
        else
        {
            var oldPhase = BitConverter.UInt64BitsToDouble(reader.ReadUInt64());
            if (!double.IsFinite(oldPhase) || oldPhase < 0 ||
                oldPhase >= (double)OricConfig.AyFrequencyHz / ((Oric)context.System).Ay.SampleRateHz)
                throw new SnapshotException("oric-ay: invalid legacy sample phase.");
            // V1 never stored the area of the partly generated sample. Start a fresh
            // interval rather than inventing that history; future V2 restores are exact.
            context.AddWarning("oric-ay: upgraded legacy audio timing; the partial PCM sample was discarded.");
        }

        var state = new Ay38912SnapshotState(
            Registers: registers,
            SelectedRegister: selectedRegister,
            ToneCounters: toneCounters,
            ToneHigh: toneHigh,
            SamplePhase: samplePhase,
            SampleArea: sampleArea,
            NoiseCounter: reader.ReadInt32(),
            NoiseHigh: reader.ReadBool(),
            NoiseLfsr: reader.ReadUInt32(),
            EnvelopeCounter: reader.ReadInt32(),
            EnvelopeStep: reader.ReadInt32(),
            EnvelopeDirection: reader.ReadInt32(),
            EnvelopeHolding: reader.ReadBool());

        try
        {
            if (storedVersion < 2)
            {
                // Preserve the remaining fraction of the old (16x too slow) step.
                // Period zero now uses eight clocks instead of the old 256 clocks.
                var divisor = registers.Length > 12 && registers[11] == 0 && registers[12] == 0 ? 32 : 16;
                state = state with { EnvelopeCounter = (int)Math.Ceiling((double)state.EnvelopeCounter / divisor) };
            }
            ((Oric)context.System).Ay.RestoreSnapshotState(state);
        }
        catch (ArgumentException exception)
        {
            throw new SnapshotException($"oric-ay: invalid generator state: {exception.Message}");
        }
    }
}
