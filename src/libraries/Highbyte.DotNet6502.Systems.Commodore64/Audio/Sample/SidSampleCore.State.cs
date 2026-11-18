namespace Highbyte.DotNet6502.Systems.Commodore64.Audio.Sample;

public sealed partial class SidSampleCore
{
    // Chip state is explicit and portable; never serialize the runtime layout of Voice.
    internal byte[] CaptureState()
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        WriteExecutionState(writer);
        writer.Write(_sampleRateHz);
        writer.Write(_sampleRateCounter);
        writer.Write(_volumeAccumulator);
        writer.Write(_cyclesSinceSample);
        writer.Write(_filterLow);
        writer.Write(_filterBand);
        return stream.ToArray();
    }

    internal void WriteExecutionState(BinaryWriter writer)
    {
        writer.Write((int)_mode);
        writer.Write(_sidClockHz);
        writer.Write(_registers);
        foreach (var voice in _voices)
        {
            writer.Write(voice.Accumulator);
            writer.Write(voice.NoiseLfsr);
            writer.Write(voice.Frequency);
            writer.Write(voice.PulseWidth);
            writer.Write(voice.WaveformBits);
            writer.Write(voice.SyncEnabled);
            writer.Write(voice.RingModEnabled);
            writer.Write(voice.TestBit);
            writer.Write(voice.Gate);
            writer.Write(voice.AttackRate);
            writer.Write(voice.DecayRate);
            writer.Write(voice.SustainLevel);
            writer.Write(voice.ReleaseRate);
            writer.Write(voice.Envelope);
            writer.Write(voice.RateCounter);
            writer.Write(voice.ExpCounter);
            writer.Write((byte)voice.AdsrPhase);
            writer.Write(voice.MsbJustRose);
        }
    }

    internal void RestoreState(byte[] state)
    {
        using var stream = new MemoryStream(state, writable: false);
        using var reader = new BinaryReader(stream);
        var savedMode = (SidEmulationMode)reader.ReadInt32();
        if (!Enum.IsDefined(savedMode) || reader.ReadInt32() != _sidClockHz)
            throw new InvalidDataException("SID snapshot mode or clock is incompatible.");
        // The saved oscillator state and its execution mode must resume together,
        // even if the host's preferred mode for new machines has changed.
        _mode = savedMode;
        reader.BaseStream.ReadExactly(_registers);
        for (int i = 0; i < VoiceCount; i++)
        {
            ref var voice = ref _voices[i];
            voice.Accumulator = reader.ReadUInt32();
            voice.NoiseLfsr = reader.ReadUInt32();
            voice.Frequency = reader.ReadUInt16();
            voice.PulseWidth = reader.ReadUInt16();
            voice.WaveformBits = reader.ReadByte();
            voice.SyncEnabled = reader.ReadBoolean();
            voice.RingModEnabled = reader.ReadBoolean();
            voice.TestBit = reader.ReadBoolean();
            voice.Gate = reader.ReadBoolean();
            voice.AttackRate = reader.ReadByte();
            voice.DecayRate = reader.ReadByte();
            voice.SustainLevel = reader.ReadByte();
            voice.ReleaseRate = reader.ReadByte();
            voice.Envelope = reader.ReadByte();
            voice.RateCounter = reader.ReadUInt16();
            voice.ExpCounter = reader.ReadByte();
            voice.AdsrPhase = (AdsrPhase)reader.ReadByte();
            voice.MsbJustRose = reader.ReadBoolean();
            if (voice.Accumulator > 0xFFFFFF || voice.NoiseLfsr > 0x7FFFFF || voice.WaveformBits > 15
                || voice.AttackRate > 15 || voice.DecayRate > 15 || voice.ReleaseRate > 15
                || !Enum.IsDefined(voice.AdsrPhase))
                throw new InvalidDataException("Invalid SID voice state.");
        }
        int sampleRate = reader.ReadInt32();
        _sampleRateCounter = reader.ReadInt32();
        _volumeAccumulator = reader.ReadInt32();
        _cyclesSinceSample = reader.ReadInt32();
        _filterLow = reader.ReadSingle();
        _filterBand = reader.ReadSingle();
        if (sampleRate != _sampleRateHz || stream.Position != stream.Length
            || !float.IsFinite(_filterLow) || !float.IsFinite(_filterBand))
            throw new InvalidDataException("Invalid or incompatible SID resampling state.");
        RefreshAggregateFlags();
        RecalculateFilterDamping(_registers[FilterResRoutOffset]);
        RecalculateFilterCutoff();
        _filterRoutingBits = (byte)(_registers[FilterResRoutOffset] & 7);
        _filterTypeBits = (byte)(_registers[VolumeRegisterOffset] & 0x70);
        _voice3Off = (_registers[VolumeRegisterOffset] & 0x80) != 0;
    }
}
