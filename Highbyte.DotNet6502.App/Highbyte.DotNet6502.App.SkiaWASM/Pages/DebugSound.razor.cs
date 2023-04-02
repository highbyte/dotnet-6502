using Highbyte.DotNet6502.Instructions;
using System.Reflection.Metadata;
using KristofferStrube.Blazor.WebAudio;
using static System.Formats.Asn1.AsnWriter;

namespace Highbyte.DotNet6502.App.SkiaWASM.Pages;

public partial class DebugSound
{
    private AudioContext _audioContext;

    OscillatorNode? _oscillator;
    GainNode? _gainNode;
    int? _currentOctave;
    int? _currentPitch;

    float _gain = 0.1f;

    private readonly SemaphoreSlim _semaphoreSlim = new(1);

    protected override async Task OnInitializedAsync()
    {
        _audioContext = await AudioContext.CreateAsync(Js);
    }

    protected async Task StartSound(MouseEventArgs mouseEventArgs)
    {
        int octave = 3;
        int pitch = 5;

        await _semaphoreSlim.WaitAsync();
        if (_currentOctave != octave || _currentPitch != pitch)
        {
            await StopSound(mouseEventArgs);
            _currentOctave = octave;
            _currentPitch = pitch;

            AudioDestinationNode destination = await _audioContext.GetDestinationAsync();

            _gainNode = await GainNode.CreateAsync(Js, _audioContext, new() { Gain = _gain });
            await _gainNode.ConnectAsync(destination);

            OscillatorOptions oscillatorOptions = new()
            {
                Type = OscillatorType.Triangle,
                Frequency = (float)Frequency(octave, pitch)
            };
            _oscillator = await OscillatorNode.CreateAsync(Js, _audioContext, oscillatorOptions);

            await _oscillator.ConnectAsync(_gainNode);
            await _oscillator.StartAsync();
        }
        _semaphoreSlim.Release();
    }

    protected async Task StopSound(MouseEventArgs mouseEventArgs)
    {
        if (_oscillator is null || _gainNode is null) return;
        var currentTime = await _audioContext.GetCurrentTimeAsync();
        var audioParam = await _gainNode.GetGainAsync();
        await audioParam.LinearRampToValueAtTimeAsync(0, currentTime + 0.3);
        _oscillator = null;
        _gainNode = null;
        _currentOctave = null;
        _currentPitch = null;
    }
    protected async Task StopSoundNow(MouseEventArgs mouseEventArgs)
    {
        if (_oscillator is null || _gainNode is null) return;
        await _oscillator.StopAsync();
        _oscillator = null;
        _gainNode = null;
        _currentOctave = null;
        _currentPitch = null;
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

    protected async Task StartSoundADSR(MouseEventArgs mouseEventArgs)
    {
        await StopSoundNow(mouseEventArgs);

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
        var t_pressed = await _audioContext.GetCurrentTimeAsync();

        // Attack -> Decay -> Sustain
        _gainNode = await GainNode.CreateAsync(Js, _audioContext);
        var destination = await _audioContext.GetDestinationAsync();
        await _gainNode.ConnectAsync(destination);
        var gain = await _gainNode.GetGainAsync();
        await gain.SetValueAtTimeAsync(0, t_pressed);
        await gain.LinearRampToValueAtTimeAsync(volume, t_pressed + attackDuration);
        await gain.SetTargetAtTimeAsync(sustainLevel * volume, t_pressed + attackDuration, decayDuration);

        // Create oscillator
        OscillatorOptions oscillatorOptions = new()
        {
            Type = type,
            Frequency = (float)Frequency(octave, pitch)
        };
        _oscillator = await OscillatorNode.CreateAsync(Js, _audioContext, oscillatorOptions);
        if (type == OscillatorType.Custom)
        {
            //_oscillator.SetPeriodicWave(customWaveform);
        }
        await _oscillator.ConnectAsync(_gainNode);


        await _oscillator.StartAsync();

        //var keyID = note + octave;
        //oscillatorMap.set(keyID, oscillator);
        //gainNodeMap.set(keyID, gainNode);
    }

    protected async Task StopSoundADSR(MouseEventArgs mouseEventArgs)
    {
        if (_oscillator == null || _gainNode == null) return;

        var t_released = await _audioContext.GetCurrentTimeAsync();
        // Time Scale (seconds). How long the sound will play.
        var timeScale = 1.0f; // 0.0 - x

        // Release (time) specifies the length of the time between the release of the key and the disappearance of the sound.
        // The Release length is Release ratio multiplied by the Time Scale value.
        var releaseControl = 0.5f; // 0.0 - 1.0
        var releaseDuration = releaseControl * timeScale;

        var gain = await _gainNode.GetGainAsync();

        await gain.CancelScheduledValuesAsync(t_released);

        var currentGainValue = await gain.GetCurrentValueAsync();
        await gain.SetValueAtTimeAsync(currentGainValue, t_released);
        await gain.LinearRampToValueAtTimeAsync(0, t_released + releaseDuration);

        await _oscillator.StopAsync(t_released + releaseDuration);
    }

}
