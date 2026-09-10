namespace Highbyte.DotNet6502.Systems.Oric.Audio;

/// <summary>AY-3-8912 register model and deterministic three-channel PCM generator.</summary>
public sealed class Ay38912
{
    public const int RegisterCount = 16;
    public const int DefaultSampleRateHz = 44_100;

    // AY output is logarithmic. Values are normalized from a commonly measured 16-level curve.
    private static readonly float[] s_volumeTable =
    [
        0.0000f, 0.0047f, 0.0067f, 0.0094f,
        0.0135f, 0.0190f, 0.0269f, 0.0379f,
        0.0536f, 0.0757f, 0.1070f, 0.1512f,
        0.2137f, 0.3020f, 0.4268f, 0.6030f,
    ];

    private readonly byte[] _registers = new byte[RegisterCount];
    private readonly int[] _toneCounter = [1, 1, 1];
    private readonly bool[] _toneHigh = [true, true, true];
    private readonly int _clockHz;
    private readonly int _sampleRateHz;
    // Integer phase in sample-rate units: each AY cycle adds sampleRateHz, and a
    // PCM interval spans clockHz units. This also preserves fractional AY cycles.
    private int _samplePhase;
    private double _sampleArea;
    private int _noiseCounter = 1;
    private bool _noiseHigh = true;
    private uint _noiseLfsr = 0x1ffff;
    private int _envelopeCounter = 1;
    private int _envelopeStep;
    private int _envelopeDirection = -1;
    private bool _envelopeHolding;

    public int SelectedRegister { get; private set; }
    public byte PortAOutput => _registers[14];
    public int SampleRateHz => _sampleRateHz;
    public Action<byte>? PortAOutputChanged { get; set; }

    public Ay38912(int clockHz = OricConfig.AyFrequencyHz, int sampleRateHz = DefaultSampleRateHz)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(clockHz);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sampleRateHz);
        _clockHz = clockHz;
        _sampleRateHz = sampleRateHz;
        Reset();
    }

    public byte ReadRegister(int register) => _registers[register & 0x0f];

    public void SelectRegister(byte value) => SelectedRegister = value & 0x0f;

    public byte ReadData() => _registers[SelectedRegister];

    public void WriteData(byte value) => WriteRegister(SelectedRegister, value);

    public void WriteRegister(int register, byte value)
    {
        register &= 0x0f;
        value &= register switch
        {
            1 or 3 or 5 => 0x0f,
            6 => 0x1f,
            8 or 9 or 10 => 0x1f,
            13 => 0x0f,
            _ => 0xff,
        };
        _registers[register] = value;

        if (register == 13)
            RestartEnvelope(value);
        else if (register == 14)
            PortAOutputChanged?.Invoke(value);
    }

    public int AdvanceCycles(int cycles, Span<float> destination)
    {
        if (cycles <= 0 || destination.IsEmpty)
            return 0;

        var written = 0;
        for (var cycle = 0; cycle < cycles; cycle++)
        {
            TickGenerators();
            var level = MixSample();
            var remaining = _sampleRateHz;
            while (remaining > 0)
            {
                var portion = Math.Min(remaining, _clockHz - _samplePhase);
                _sampleArea += (double)level * portion;
                _samplePhase += portion;
                remaining -= portion;
                if (_samplePhase != _clockHz)
                    continue;

                // Integrate the entire interval, including changes between calls or
                // register writes, instead of aliasing an instantaneous mixer value.
                if (written < destination.Length)
                    destination[written++] = (float)(_sampleArea / _clockHz);
                _samplePhase = 0;
                _sampleArea = 0;
            }
        }
        return written;
    }

    public void Reset()
    {
        Array.Clear(_registers);
        SelectedRegister = 0;
        Array.Fill(_toneCounter, 1);
        Array.Fill(_toneHigh, true);
        _noiseCounter = 1;
        _noiseHigh = true;
        _noiseLfsr = 0x1ffff;
        _samplePhase = 0;
        _sampleArea = 0;
        RestartEnvelope(0);
        PortAOutputChanged?.Invoke(0);
    }

    private void TickGenerators()
    {
        for (var channel = 0; channel < 3; channel++)
        {
            if (--_toneCounter[channel] > 0)
                continue;
            _toneCounter[channel] = 8 * TonePeriod(channel);
            _toneHigh[channel] = !_toneHigh[channel];
        }

        if (--_noiseCounter <= 0)
        {
            _noiseCounter = 16 * Math.Max(1, _registers[6] & 0x1f);
            var feedback = (_noiseLfsr ^ (_noiseLfsr >> 3)) & 1;
            _noiseLfsr = (_noiseLfsr >> 1) | (feedback << 16);
            _noiseHigh = (_noiseLfsr & 1) != 0;
        }

        if (--_envelopeCounter <= 0)
        {
            _envelopeCounter = EnvelopeStepCycles();
            AdvanceEnvelope();
        }
    }

    private int TonePeriod(int channel)
    {
        var fine = _registers[channel * 2];
        var coarse = _registers[channel * 2 + 1] & 0x0f;
        return Math.Max(1, fine | (coarse << 8));
    }

    private int EnvelopeStepCycles()
    {
        // AY has 16 levels: clock / (256 * period) describes a whole ramp,
        // not one step. A zero envelope period runs at half the period-one interval.
        var period = _registers[11] | (_registers[12] << 8);
        return period == 0 ? 8 : 16 * period;
    }

    private float MixSample()
    {
        var mixer = _registers[7];
        var mixed = 0f;
        for (var channel = 0; channel < 3; channel++)
        {
            var volumeRegister = _registers[8 + channel];
            var usesEnvelope = (volumeRegister & 0x10) != 0;
            var fixedVolume = volumeRegister & 0x0f;
            var toneEnabled = (mixer & (1 << channel)) == 0;
            var noiseEnabled = (mixer & (1 << (channel + 3))) == 0;
            // Disabled generators force their mixer inputs high. Disabling both
            // therefore leaves the DAC at its selected volume (used by sampled audio).
            var tonePasses = !toneEnabled || _toneHigh[channel];
            var noisePasses = !noiseEnabled || _noiseHigh;
            if (!tonePasses || !noisePasses)
                continue;

            var volume = usesEnvelope ? _envelopeStep : fixedVolume;
            mixed += s_volumeTable[volume];
        }

        // Fixed headroom prevents one channel's gain changing when another starts
        // or stops. All three channels at maximum volume sum to full scale.
        return mixed / (3 * s_volumeTable[^1]);
    }

    private void RestartEnvelope(byte shape)
    {
        var attack = (shape & 0x04) != 0;
        _envelopeStep = attack ? 0 : 15;
        _envelopeDirection = attack ? 1 : -1;
        _envelopeHolding = false;
        _envelopeCounter = EnvelopeStepCycles();
    }

    private void AdvanceEnvelope()
    {
        if (_envelopeHolding)
            return;

        _envelopeStep += _envelopeDirection;
        if (_envelopeStep is >= 0 and <= 15)
            return;

        var shape = _registers[13];
        var continues = (shape & 0x08) != 0;
        if (!continues)
        {
            _envelopeStep = 0;
            _envelopeHolding = true;
            return;
        }

        if ((shape & 0x02) != 0)
            _envelopeDirection = -_envelopeDirection;
        if ((shape & 0x01) != 0)
        {
            _envelopeStep = _envelopeDirection > 0 ? 15 : 0;
            _envelopeHolding = true;
        }
        else
        {
            _envelopeStep = _envelopeDirection > 0 ? 0 : 15;
        }
    }

    internal Ay38912SnapshotState GetSnapshotState()
        => new(
            _registers.ToArray(),
            SelectedRegister,
            _toneCounter.ToArray(),
            _toneHigh.ToArray(),
            _samplePhase,
            _sampleArea,
            _noiseCounter,
            _noiseHigh,
            _noiseLfsr,
            _envelopeCounter,
            _envelopeStep,
            _envelopeDirection,
            _envelopeHolding);

    internal void RestoreSnapshotState(Ay38912SnapshotState state)
    {
        if (state.Registers.Length != RegisterCount)
            throw new ArgumentException($"AY snapshot must contain {RegisterCount} registers.", nameof(state));
        if (state.ToneCounters.Length != 3 || state.ToneHigh.Length != 3)
            throw new ArgumentException("AY snapshot must contain state for three tone channels.", nameof(state));
        ArgumentOutOfRangeException.ThrowIfNegative(state.SelectedRegister);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(state.SelectedRegister, RegisterCount);
        if (state.ToneCounters.Any(counter => counter <= 0) ||
            state.SamplePhase < 0 || state.SamplePhase >= _clockHz ||
            !double.IsFinite(state.SampleArea) ||
            state.SampleArea < 0 || state.SampleArea > state.SamplePhase ||
            state.NoiseCounter <= 0 ||
            state.NoiseLfsr is 0 or > 0x1ffff ||
            state.EnvelopeCounter <= 0 ||
            state.EnvelopeStep is < 0 or > 15 ||
            state.EnvelopeDirection is not (-1 or 1))
        {
            throw new ArgumentException("AY snapshot contains an invalid generator phase.", nameof(state));
        }

        Array.Copy(state.Registers, _registers, RegisterCount);
        SelectedRegister = state.SelectedRegister;
        Array.Copy(state.ToneCounters, _toneCounter, _toneCounter.Length);
        Array.Copy(state.ToneHigh, _toneHigh, _toneHigh.Length);
        _samplePhase = state.SamplePhase;
        _sampleArea = state.SampleArea;
        _noiseCounter = state.NoiseCounter;
        _noiseHigh = state.NoiseHigh;
        _noiseLfsr = state.NoiseLfsr;
        _envelopeCounter = state.EnvelopeCounter;
        _envelopeStep = state.EnvelopeStep;
        _envelopeDirection = state.EnvelopeDirection;
        _envelopeHolding = state.EnvelopeHolding;
        PortAOutputChanged?.Invoke(PortAOutput);
    }
}

internal readonly record struct Ay38912SnapshotState(
    byte[] Registers,
    int SelectedRegister,
    int[] ToneCounters,
    bool[] ToneHigh,
    int SamplePhase,
    double SampleArea,
    int NoiseCounter,
    bool NoiseHigh,
    uint NoiseLfsr,
    int EnvelopeCounter,
    int EnvelopeStep,
    int EnvelopeDirection,
    bool EnvelopeHolding);
