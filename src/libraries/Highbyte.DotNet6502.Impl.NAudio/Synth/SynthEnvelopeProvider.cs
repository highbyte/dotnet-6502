using NAudio.Dsp;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using static NAudio.Dsp.EnvelopeGenerator;

namespace Highbyte.DotNet6502.Impl.NAudio.Synth;

/// <summary>
/// NAudio Sample Provider that applies an ADSR envelope to a specifed synth signal type.
/// Based on code from https://github.com/essenbee/synthesizer.
/// </summary>
public class SynthEnvelopeProvider : ISampleProvider
{
    private readonly int _sampleRate;
    private readonly SynthSignalProvider _source;
    private readonly EnvelopeGenerator _adsr;
    public WaveFormat WaveFormat { get; }
    public bool EnablbooleSubOsc
    {
        get => _source.EnableSubOsc;
        set { _source.EnableSubOsc = value; }
    }

    public EnvelopeState ADSRState => _adsr.State;

    private float _attackSeconds;
    public float AttackSeconds
    {
        get => _attackSeconds;
        set
        {
            _attackSeconds = value;
            _adsr.AttackRate = _attackSeconds * WaveFormat.SampleRate;
        }
    }

    private float _decaySeconds;
    public float DecaySeconds
    {
        get => _decaySeconds;
        set
        {
            _decaySeconds = value;
            _adsr.DecayRate = _decaySeconds * WaveFormat.SampleRate;
        }
    }

    public float SustainLevel
    {
        get => _adsr.SustainLevel;
        set { _adsr.SustainLevel = value; }
    }

    private float _releaseSeconds;
    public float ReleaseSeconds
    {
        get => _releaseSeconds;

        set
        {
            _releaseSeconds = value;
            _adsr.ReleaseRate = _releaseSeconds * WaveFormat.SampleRate;
        }
    }

    public double Frequency
    {
        get => _source.Frequency;
        set { _source.Frequency = value; }
    }

    public double LfoFrequency
    {
        get => _source.LfoFrequency;
        set { _source.LfoFrequency = value; }
    }
    public double LfoGain
    {
        get => _source.LfoGain;
        set { _source.LfoGain = value; }
    }

    public double Duty
    {
        get => _source.Duty;
        set { _source.Duty = value; }
    }

    public SignalGeneratorType WaveType => _source.Type;

    public SynthEnvelopeProvider(
        SignalGeneratorType waveType,
        int sampleRate = 44100,
        float gain = 1.0f)
    {
        _sampleRate = sampleRate;
        var channels = 1; // Mono
        WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(_sampleRate, channels);
        _adsr = new EnvelopeGenerator();

        //Defaults
        AttackSeconds = 0.01f;
        DecaySeconds = 0.0f;
        SustainLevel = 1.0f;
        ReleaseSeconds = 0.3f;

        _source = new SynthSignalProvider(_sampleRate, channels)
        {
            Frequency = 110.0,
            Type = waveType,
            Gain = gain,
        };

        // Uncomment to start attack phase immediately when this object is created
        //_adsr.Gate(true);
    }

    public void StartRelease()
    {
        _adsr.Gate(false);
    }

    public void StartAttack()
    {
        _adsr.Gate(true);
    }

    public void ResetADSR()
    {
        _adsr.Reset();
    }


    public int Read(float[] buffer, int offset, int count)
    {
        if (_adsr.State == EnvelopeState.Idle)
            return 0; // we've finished

        var samples = _source.Read(buffer, offset, count);

        for (var i = 0; i < samples; i++)
        {
            buffer[offset++] *= _adsr.Process();
        }

        return samples;
    }
}
