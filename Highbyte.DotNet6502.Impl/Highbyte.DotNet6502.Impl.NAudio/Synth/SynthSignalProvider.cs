using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace Highbyte.DotNet6502.Impl.NAudio.Synth
{
    /// <summary>
    /// NAudio Sample Provider that generates synth wave forms.
    /// Based on code from https://github.com/essenbee/synthesizer.
    /// </summary>
    public class SynthSignalProvider : ISampleProvider
    {
        private readonly WaveFormat _waveFormat;
        private readonly Random _random = new Random();
        private readonly double[] _pinkNoiseBuffer = new double[7];
        private const double TwoPi = 2 * Math.PI;
        private int _nSample;
        private double _phi;

        public WaveFormat WaveFormat => _waveFormat;
        public double Frequency { get; set; }
        public double FrequencyLog => Math.Log(Frequency);
        public double FrequencyEnd { get; set; }
        public double FrequencyEndLog => Math.Log(FrequencyEnd);
        public double Gain { get; set; }
        public bool[] PhaseReverse { get; }
        public SignalGeneratorType Type { get; set; }
        public double SweepLengthSecs { get; set; }
        public double LfoFrequency { get; set; }
        public double LfoGain { get; set; }
        public bool EnableSubOsc { get; set; }

        public double SubOscillatorFrequency => Frequency / 2.0;

        private SquareWaveHelper _squareWaveHelper;
        public double Duty { get; set; } // Duty cycle for square wave. 0.5 (50%) is a square wave.

        public SynthSignalProvider() : this(44100, 2)
        {
        }

        public SynthSignalProvider(int sampleRate, int channel,
            double lfoFrequency = 0.0, double lfoGain = 0.0)
        {
            _phi = 0;
            _waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, channel);

            // Default
            Type = SignalGeneratorType.Sin;
            Frequency = 440.0;
            Gain = 1;
            PhaseReverse = new bool[channel];
            SweepLengthSecs = 2;
            LfoFrequency = lfoFrequency;
            LfoGain = lfoGain;

            Duty = 0.5;
            _squareWaveHelper = new SquareWaveHelper();

        }

        private double lfoSample(int n)
        {
            //`
            //` <formula S_n = A_l \cdot sin(\frac{2\pi \cdot f_l \cdot n}{sr})>
            //`
            if (LfoGain == 0.0)
                return 0.0;

            var multiple = TwoPi * LfoFrequency / _waveFormat.SampleRate;
            return LfoGain * Math.Sin(n * multiple);
        }

        public int Read(float[] buffer, int offset, int count)
        {
            var outIndex = offset;

            // Generator current value
            double sampleValue;

            // Complete Buffer
            for (var sampleCount = 0; sampleCount < count / _waveFormat.Channels; sampleCount++)
            {
                switch (Type)
                {
                    case SignalGeneratorType.Sin:

                        // Sinus Generator

                        var multiple = TwoPi * Frequency / _waveFormat.SampleRate;

                        if (EnableSubOsc)
                        {
                            var subOsc = Math.Sin(_nSample * (TwoPi * SubOscillatorFrequency / _waveFormat.SampleRate)
                                + lfoSample(_nSample));
                            sampleValue = Gain * Math.Sin(_nSample * multiple + lfoSample(_nSample)
                                + 0.5 * Gain * subOsc);
                        }
                        else
                        {
                            sampleValue = Gain * Math.Sin(_nSample * multiple + lfoSample(_nSample));
                        }

                        _nSample++;

                        break;


                    case SignalGeneratorType.Square:

                        // Square Generator
                        //multiple = TwoPi * Frequency / waveFormat.SampleRate;
                        //var sampleSaw = Math.Sin(nSample * multiple + lfoSample(nSample));
                        //sampleValue = sampleSaw > 0 ? Gain : -Gain;

                        sampleValue = _squareWaveHelper.Read(WaveFormat.SampleRate, Frequency, Gain, Duty);

                        _nSample++;
                        break;

                    case SignalGeneratorType.Triangle:

                        // Triangle Generator

                        multiple = TwoPi * Frequency / _waveFormat.SampleRate;
                        sampleValue = Math.Asin(Math.Sin(_nSample * multiple + lfoSample(_nSample))) * (2.0 / Math.PI);
                        sampleValue *= Gain;

                        _nSample++;
                        break;

                    case SignalGeneratorType.SawTooth:

                        // SawTooth Generator

                        multiple = 2 * Frequency / _waveFormat.SampleRate;
                        var sampleSaw = (_nSample * multiple + lfoSample(_nSample)) % 2 - 1;
                        sampleValue = Gain * sampleSaw;

                        _nSample++;
                        break;

                    case SignalGeneratorType.White:

                        // White Noise Generator
                        sampleValue = Gain * NextRandomTwo();
                        break;

                    case SignalGeneratorType.Pink:

                        // Pink Noise Generator

                        var white = NextRandomTwo();
                        _pinkNoiseBuffer[0] = 0.99886 * _pinkNoiseBuffer[0] + white * 0.0555179;
                        _pinkNoiseBuffer[1] = 0.99332 * _pinkNoiseBuffer[1] + white * 0.0750759;
                        _pinkNoiseBuffer[2] = 0.96900 * _pinkNoiseBuffer[2] + white * 0.1538520;
                        _pinkNoiseBuffer[3] = 0.86650 * _pinkNoiseBuffer[3] + white * 0.3104856;
                        _pinkNoiseBuffer[4] = 0.55000 * _pinkNoiseBuffer[4] + white * 0.5329522;
                        _pinkNoiseBuffer[5] = -0.7616 * _pinkNoiseBuffer[5] - white * 0.0168980;
                        var pink = _pinkNoiseBuffer[0] + _pinkNoiseBuffer[1] + _pinkNoiseBuffer[2] + _pinkNoiseBuffer[3] + _pinkNoiseBuffer[4] + _pinkNoiseBuffer[5] + _pinkNoiseBuffer[6] + white * 0.5362;
                        _pinkNoiseBuffer[6] = white * 0.115926;
                        sampleValue = Gain * (pink / 5);
                        break;

                    case SignalGeneratorType.Sweep:

                        // Sweep Generator
                        var f = Math.Exp(FrequencyLog + _nSample * (FrequencyEndLog - FrequencyLog) / (SweepLengthSecs * _waveFormat.SampleRate));

                        multiple = TwoPi * f / _waveFormat.SampleRate;
                        _phi += multiple;
                        sampleValue = Gain * Math.Sin(_phi);
                        _nSample++;
                        if (_nSample > SweepLengthSecs * _waveFormat.SampleRate)
                        {
                            _nSample = 0;
                            _phi = 0;
                        }
                        break;

                    default:
                        sampleValue = 0.0;
                        break;
                }

                // phase Reverse Per Channel
                for (var i = 0; i < _waveFormat.Channels; i++)
                {
                    if (PhaseReverse[i])
                        buffer[outIndex++] = (float)-sampleValue;
                    else
                        buffer[outIndex++] = (float)sampleValue;
                }
            }
            return count;
        }

        private double NextRandomTwo()
        {
            return 2 * _random.NextDouble() - 1;
        }
    }
}
