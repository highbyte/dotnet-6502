using Highbyte.DotNet6502.Impl.AspNet.Audio.WebAudioAPISynth;
using Highbyte.DotNet6502.Impl.AspNet.Audio.WebAudioAPISynth.JSInterop;
using Highbyte.DotNet6502.Impl.AspNet.Audio.WebAudioAPISynth.JSInterop.BlazorWebAudioSync;
using Highbyte.DotNet6502.Impl.AspNet.Audio.WebAudioAPISynth.JSInterop.BlazorWebAudioSync.Options;

namespace Highbyte.DotNet6502.Impl.AspNet.Audio;

public class WebAudioNoiseOscillator
{
    private readonly WebAudioVoiceContext _voiceContext;
    private WASMAudioHandlerContext _audioHandlerContext => _voiceContext.AudioHandlerContext;

    private Action<string> _addDebugMessage => _voiceContext.AddDebugMessage;

    // Noise oscillator
    private AudioBufferSync _noiseBuffer = default!;
    internal AudioBufferSourceNodeSync? NoiseGenerator;

    public WebAudioNoiseOscillator(WebAudioVoiceContext voiceContext)
    {
        _voiceContext = voiceContext;
    }

    internal void Create(float playbackRate)
    {
        if (_noiseBuffer == null)
            PrepareNoiseGenerator();
        _addDebugMessage($"Creating NoiseGenerator");
        NoiseGenerator = AudioBufferSourceNodeSync.Create(
            _audioHandlerContext.JSRuntime,
            _audioHandlerContext.AudioContext,
            new AudioBufferSourceNodeOptions
            {
                PlaybackRate = playbackRate,    // Factor of sample rate. 1.0 = same speed as original.
                Loop = true,
                Buffer = _noiseBuffer
            });
    }

    internal void Start()
    {
        if (NoiseGenerator == null)
            throw new DotNet6502Exception($"NoiseGenerator is null. Call Create() first.");
        _addDebugMessage($"Starting NoiseGenerator");
        NoiseGenerator!.Start();
        //voiceContext!.NoiseGenerator.Start(0, 0, currentTime + wasmVoiceParameter.AttackDurationSeconds + wasmVoiceParameter.ReleaseDurationSeconds);
    }

    internal void StopNow()
    {
        if (NoiseGenerator == null)
            return;
        NoiseGenerator!.Stop();
        NoiseGenerator = null;  // Make sure the NoiseGenerator is not reused. After .Stop() it isn't designed be used anymore.
        _addDebugMessage($"Stopped and removed NoiseGenerator.");
    }

    internal void StopLater(double when)
    {
        if (NoiseGenerator == null)
            throw new DotNet6502Exception($"NoiseGenerator is null. Call Create() first.");
        _addDebugMessage($"Planning stopp of NoiseGenerator: {when}");
        NoiseGenerator!.Stop(when);
    }

    internal void Connect()
    {
        if (NoiseGenerator == null)
            throw new DotNet6502Exception($"NoiseGenerator is null. Call Create() first.");
        NoiseGenerator!.Connect(_voiceContext.GainNode!);
    }

    internal void Disconnect()
    {
        if (NoiseGenerator == null)
            return;
        NoiseGenerator!.Disconnect();
    }

    private void PrepareNoiseGenerator()
    {
        var noiseDuration = 1.0f;  // Seconds

        var sampleRate = _audioHandlerContext.AudioContext.GetSampleRate();
        var bufferSize = (int)(sampleRate * noiseDuration);
        // Create an empty buffer
        _noiseBuffer = AudioBufferSync.Create(
            _audioHandlerContext.AudioContext.WebAudioHelper,
            _audioHandlerContext.JSRuntime,
            new AudioBufferOptions
            {
                Length = bufferSize,
                SampleRate = sampleRate,
            });

        // Note: Too slow to call Float32Array index in a loop
        //var data = noiseBuffer.GetChannelData(0);
        //var random = new Random();
        //for (var i = 0; i < bufferSize; i++)
        //{
        //    data[i] = ((float)random.NextDouble()) * 2 - 1;
        //}

        // Optimized by filling a .NET array, and then creating a Float32Array from that array in one call.
        var values = new float[bufferSize];
        var random = new Random();
        for (var i = 0; i < bufferSize; i++)
        {
            values[i] = (float)random.NextDouble() * 2 - 1;
        }
        var data = Float32ArraySync.Create(_audioHandlerContext.AudioContext.WebAudioHelper, _audioHandlerContext.JSRuntime, values);
        _noiseBuffer.CopyToChannel(data, 0);
    }

    internal float GetPlaybackRateFromFrequency(float frequency)
    {
        const float playbackRateMin = 0.0f; // Should be used for the minimum SID frequency ( 0 Hz)
        const float playbackRateMax = 1.0f; // Should be used for the maximum SID frequency ( ca 4000 Hz)
        const float sidFreqMin = 0;
        const float sidFreqMax = 4000;
        var playbackRate = playbackRateMin + (float)(frequency - sidFreqMin) / (sidFreqMax - sidFreqMin) * (playbackRateMax - playbackRateMin);
        return playbackRate;
    }
}
