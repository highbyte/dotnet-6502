namespace Highbyte.DotNet6502.Impl.NAudio.Synth;

/// <summary>
/// Square wave generator with configurable duty cycle.
/// Based on code from https://github.com/BertyBasset/C-Analog-Synthesiser
/// </summary>

public class SquareWaveHelper
{
    // _phase is the oscillator's 360 degree modulo phase accumulator
    private double _phase = 0f;

    public SquareWaveHelper()
    {

    }

    public double Read(int sampleRate, double frequency, double gain, double duty)
    {
        var timeIncrement = 1f / (double)sampleRate;

        // Advance Phase Accumulator acording to timeIncrement and current frequency
        var delta = timeIncrement * frequency * 360;
        _phase += delta;

        var originalPhase = _phase;
        _phase %= 360;

        //if (_phase < originalPhase)     // If % takes us back for a new cycle we've completed a cycle and can sync other ocs if needed
        //    TriggerSync();

        // Use Generator to return wave value for current state of the Phase Accumulator

        var sample = GenerateSquare(_phase, duty);
        //Value = _Generator.GenerateSample(_phase, Duty.GetDuty(), delta);

        return sample * gain;
    }

    private double GenerateSquare(double phase, double duty)
    {

        double sample;
        if (phase > 360 * ((duty + 1.0) / 2.0))
            sample = 1;
        else
            sample = 0;

        const double AMPLITUDE_NORMALISATION = 0.7;
        return sample * AMPLITUDE_NORMALISATION;
    }
}
