using Highbyte.DotNet6502.Impl.AspNet.JSInterop.BlazorWebAudioSync;
using Highbyte.DotNet6502.Impl.AspNet.JSInterop.BlazorWebAudioSync.Options;

namespace Highbyte.DotNet6502.App.SkiaWASM.Pages;

public partial class DebugSound
{
    private AudioContextSync _audioContext;
    OscillatorNodeSync? _oscillator;
    GainNodeSync? _gainNode;
    int? _currentOctave;
    int? _currentPitch;
    float _gain = 0.1f;

    private readonly SemaphoreSlim _semaphoreSlim = new(1);

    protected override async Task OnInitializedAsync()
    {
        _audioContext = await AudioContextSync.CreateAsync(Js);
    }

    protected void StartSound(MouseEventArgs mouseEventArgs)
    {
        int octave = 3;
        int pitch = 5;

        if (_currentOctave != octave || _currentPitch != pitch)
        {
            StopSound(mouseEventArgs);
            _currentOctave = octave;
            _currentPitch = pitch;

            AudioDestinationNodeSync destination = _audioContext.GetDestination();

            _gainNode = GainNodeSync.Create(Js, _audioContext, new() { Gain = _gain });
            _gainNode.Connect(destination);

            OscillatorOptions oscillatorOptions = new()
            {
                Type = OscillatorType.Triangle,
                Frequency = (float)Frequency(octave, pitch)
            };
            _oscillator = OscillatorNodeSync.Create(Js, _audioContext, oscillatorOptions);

            _oscillator.Connect(_gainNode);
            _oscillator.Start();
        }
    }

    protected void StopSound(MouseEventArgs mouseEventArgs)
    {
        if (_oscillator is null || _gainNode is null) return;
        var currentTime = _audioContext.GetCurrentTime();
        var audioParam = _gainNode.GetGain();
        audioParam.LinearRampToValueAtTime(0, currentTime + 0.3);
        _oscillator = null;
        _gainNode = null;
        _currentOctave = null;
        _currentPitch = null;
    }

    protected void StopSoundNow(MouseEventArgs mouseEventArgs)
    {
        if (_oscillator is null || _gainNode is null) return;
        _oscillator.Stop();
        _oscillator = null;
        _gainNode = null;
        _currentOctave = null;
        _currentPitch = null;
    }

    protected void StartSoundADSR(MouseEventArgs mouseEventArgs)
    {
        StopSoundNow(mouseEventArgs);

        int octave = 3;
        int pitch = 5;
        var type = OscillatorType.Triangle;


        // Volume level
        var volume = _gain; // 0.0 - 1.0

        // Time Scale (seconds). How long the sound will play.
        var timeScale = 1.0f; // 0.0 - x

        // Attack Control (time ratio) specifies the length ot the time between the keyboard pressing and the time when the gain reaches maximum.
        // The Attack length is the Attack Control ratio multiplied by the the Time Scale value.
        var attackControl = 0.12f;   // 0.0 - 1.0
        var attackDuration = attackControl * timeScale;

        // Decay Control (time ratio) is the time the gain to decrease from the maximum to the gain specified by Sustain.
        // The Decay time is the Decay Control ratio multiplied by the Time Scale value.
        var decayControl = 0.2f;   // 0.0 - 1.0
        var decayDuration = decayControl * timeScale;

        // The Sustain (volume) level is the gain level after Attack and Decay.
        // The level is the Sustain Control multiplied by the Volume value.
        // While Attack, Decay and Release specify the length of time, Sustain specifies the gain level.
        var sustainLevel = 0.5f; // 0.0 - 1.0

        // t_pressed  is the time the sound started playing
        var t_pressed = _audioContext.GetCurrentTime();

        // Attack -> Decay -> Sustain
        _gainNode = GainNodeSync.Create(Js, _audioContext);
        var destination = _audioContext.GetDestination();
        _gainNode.Connect(destination);
        var gain = _gainNode.GetGain();
        gain.SetValueAtTime(0, t_pressed);
        gain.LinearRampToValueAtTime(volume, t_pressed + attackDuration);
        gain.SetTargetAtTime(sustainLevel * volume, t_pressed + attackDuration, decayDuration);

        // Create oscillator
        OscillatorOptions oscillatorOptions = new()
        {
            Type = type,
            Frequency = (float)Frequency(octave, pitch)
        };
        _oscillator = OscillatorNodeSync.Create(Js, _audioContext, oscillatorOptions);
        if (type == OscillatorType.Custom)
        {
            //_oscillator2.SetPeriodicWave(customWaveform);
        }
        _oscillator.Connect(_gainNode);


        _oscillator.Start();

        //var keyID = note + octave;
        //oscillatorMap.set(keyID, oscillator);
        //gainNodeMap.set(keyID, gainNode);
    }

    protected void StopSoundADSR(MouseEventArgs mouseEventArgs)
    {
        if (_oscillator == null || _gainNode == null) return;

        var t_released = _audioContext.GetCurrentTime();
        // Time Scale (seconds). How long the sound will play.
        var timeScale = 1.0f; // 0.0 - x

        // Release (time) specifies the length of the time between the release of the key and the disappearance of the sound.
        // The Release length is Release ratio multiplied by the Time Scale value.
        var releaseControl = 0.5f; // 0.0 - 1.0
        var releaseDuration = releaseControl * timeScale;

        var gain = _gainNode.GetGain();

        gain.CancelScheduledValues(t_released);

        var currentGainValue = gain.GetCurrentValue();
        gain.SetValueAtTime(currentGainValue, t_released);
        gain.LinearRampToValueAtTime(0, t_released + releaseDuration);

        _oscillator.Stop(t_released + releaseDuration);
    }

    private double Frequency(int octave, int pitch)
    {
        var noteIndex = octave * 12 + pitch;
        var a = Math.Pow(2, 1.0 / 12);
        var A4 = 440;
        var A4Index = 4 * 12 + 10;
        var halfStepDifference = noteIndex - A4Index;
        return A4 * Math.Pow(a, halfStepDifference);
    }
}
