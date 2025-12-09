using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using NAudio.Wave;

namespace Highbyte.DotNet6502.App.Avalonia.Browser;

/// <summary>
/// A NAudio WavePlayer that uses browser WebAudio API to play audio for browser/WASM support.
/// This implementation pulls audio samples from NAudio and pushes them to JavaScript WebAudio.
/// </summary>
[SupportedOSPlatform("browser")]
public partial class WebAudioWavePlayer : IWavePlayer
{
    public float Volume { get; set; } = 1.0f;

    public WaveFormat OutputWaveFormat => _sourceProvider!.WaveFormat;

    public PlaybackState PlaybackState { get; private set; }

    /// <summary>
    /// Indicates playback has stopped automatically
    /// </summary>
    public event EventHandler<StoppedEventArgs>? PlaybackStopped;

    /// <summary>
    /// Gets or sets the desired latency in milliseconds
    /// Should be set before a call to Init
    /// </summary>
    public int DesiredLatency { get; set; } = 100;

    private int _bufferSizeSamples;
    private IWaveProvider? _sourceProvider;

    private readonly SynchronizationContext? _syncContext;
    private CancellationTokenSource? _cancellationTokenSource;
    private Task? _playbackTask;

    private bool _isInitialized;

    public WebAudioWavePlayer()
    {
        _syncContext = SynchronizationContext.Current;
        PlaybackState = PlaybackState.Stopped;
    }

    public void Init(IWaveProvider waveProvider)
    {
        //if (_isInitialized)
        //    return;

        _sourceProvider = null;
        _sourceProvider = waveProvider ?? throw new ArgumentNullException(nameof(waveProvider));

        // Validate wave format - WebAudio typically works best with float32 mono or stereo
        if (_sourceProvider.WaveFormat.Encoding != WaveFormatEncoding.IeeeFloat)
            throw new ArgumentException("WebAudioWavePlayer requires IeeeFloat wave format");

        if (_sourceProvider.WaveFormat.BitsPerSample != 32)
            throw new ArgumentException("WebAudioWavePlayer requires 32-bit float samples");

        if (_sourceProvider.WaveFormat.Channels > 2)
            throw new ArgumentException("WebAudioWavePlayer supports mono or stereo only");

        // Calculate buffer size based on desired latency
        // Buffer size in samples = sample rate * (latency in seconds)
        _bufferSizeSamples = (int)(_sourceProvider.WaveFormat.SampleRate * (DesiredLatency / 1000.0));

        // Initialize WebAudio context in JavaScript
        JSInterop.Initialize(
            _sourceProvider.WaveFormat.SampleRate,
            _sourceProvider.WaveFormat.Channels,
            _bufferSizeSamples);

        _isInitialized = true;
    }

    public void Play()
    {
        if (!_isInitialized)
            throw new InvalidOperationException("Must call Init first");

        if (PlaybackState != PlaybackState.Playing)
        {
            if (PlaybackState == PlaybackState.Stopped)
            {
                PlaybackState = PlaybackState.Playing;
                _cancellationTokenSource = new CancellationTokenSource();
                _playbackTask = Task.Run(() => PlaybackLoop(_cancellationTokenSource.Token));
            }
            else if (PlaybackState == PlaybackState.Paused)
            {
                PlaybackState = PlaybackState.Playing;
                // Resume playback in JavaScript
                JSInterop.Resume();
            }
        }
    }

    public void Pause()
    {
        if (PlaybackState == PlaybackState.Stopped)
            throw new InvalidOperationException("Cannot pause when stopped");

        if (PlaybackState == PlaybackState.Playing)
        {
            PlaybackState = PlaybackState.Paused;
            // Pause playback in JavaScript
            JSInterop.Pause();
        }
    }

    public void Stop()
    {
        if (PlaybackState != PlaybackState.Stopped)
        {
            PlaybackState = PlaybackState.Stopped;

            // Cancel playback loop
            _cancellationTokenSource?.Cancel();

            // Wait for playback task to complete
            try
            {
                _playbackTask?.Wait(1000);
            }
            catch (AggregateException)
            {
                // Ignore cancellation exceptions
            }

            _playbackTask = null;
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;

            // Stop playback in JavaScript
            JSInterop.Stop();
        }
    }

    private async Task PlaybackLoop(CancellationToken cancellationToken)
    {
        Exception? exception = null;
        try
        {
            await DoPlayback(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Normal cancellation, not an error
        }
        catch (Exception e)
        {
            exception = e;
        }
        finally
        {
            PlaybackState = PlaybackState.Stopped;
            RaisePlaybackStoppedEvent(exception);
        }
    }

    private async Task DoPlayback(CancellationToken cancellationToken)
    {
        // Buffer for reading samples from NAudio
        // Size in bytes = samples * channels * bytes per sample
        var bytesPerSample = _sourceProvider!.WaveFormat.BitsPerSample / 8;
        var bufferSizeBytes = _bufferSizeSamples * _sourceProvider.WaveFormat.Channels * bytesPerSample;
        var buffer = new byte[bufferSizeBytes];

        // Calculate the exact duration of one buffer in milliseconds
        // This is how long it takes the audio hardware to play the samples we send
        var bufferDurationMs = (_bufferSizeSamples * 1000.0) / _sourceProvider.WaveFormat.SampleRate;

        // Use a stopwatch to maintain accurate timing regardless of processing delays
        var stopwatch = Stopwatch.StartNew();
        var nextSendTime = stopwatch.ElapsedMilliseconds;

        while (PlaybackState == PlaybackState.Playing || PlaybackState == PlaybackState.Paused)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (PlaybackState == PlaybackState.Paused)
            {
                await Task.Delay(10, cancellationToken);
                // Reset timing when paused to avoid burst sending on resume
                nextSendTime = stopwatch.ElapsedMilliseconds;
                continue;
            }

            // Read audio samples from NAudio source provider
            var bytesRead = _sourceProvider.Read(buffer, 0, buffer.Length);

            if (bytesRead > 0)
            {
                // Convert byte array to float array for JavaScript
                var sampleCount = bytesRead / bytesPerSample;
                var floatSamples = new float[sampleCount];

                // Convert bytes to floats
                Buffer.BlockCopy(buffer, 0, floatSamples, 0, bytesRead);

                // Apply volume
                if (Math.Abs(Volume - 1.0f) > 0.001f)
                {
                    for (int i = 0; i < floatSamples.Length; i++)
                    {
                        floatSamples[i] *= Volume;
                    }
                }

                // Send audio data to JavaScript
                // Convert float array to byte array for JSImport
                var audioDataBytes = new byte[floatSamples.Length * sizeof(float)];
                Buffer.BlockCopy(floatSamples, 0, audioDataBytes, 0, audioDataBytes.Length);
                JSInterop.QueueAudioData(audioDataBytes, floatSamples.Length);
            }

            // Calculate when we should send the next buffer
            nextSendTime += (long)bufferDurationMs;

            // Calculate how long to wait (accounting for time already spent processing)
            var currentTime = stopwatch.ElapsedMilliseconds;
            var delayMs = (int)(nextSendTime - currentTime);

            // Only delay if we're ahead of schedule
            if (delayMs > 0)
            {
                await Task.Delay(delayMs, cancellationToken);
            }
            else if (delayMs < -bufferDurationMs * 2)
            {
                // We're way behind schedule, reset timing to avoid playing catch-up
                nextSendTime = stopwatch.ElapsedMilliseconds;
            }
        }
    }

    private void RaisePlaybackStoppedEvent(Exception? e)
    {
        var handler = PlaybackStopped;
        if (handler != null)
        {
            if (_syncContext == null)
            {
                handler(this, new StoppedEventArgs(e));
            }
            else
            {
                _syncContext.Post(state => handler(this, new StoppedEventArgs(e)), null);
            }
        }
    }

    private bool _disposedValue;
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            if (disposing)
            {
                Stop();
                JSInterop.Cleanup();
            }

            _disposedValue = true;
        }
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }



    // ================================================================================
    // JavaScript Interop Methods
    // ================================================================================

    [SupportedOSPlatform("browser")]
    private static partial class JSInterop
    {
        [JSImport("WebAudioWavePlayer.initialize", "WebAudioWavePlayer")]
        public static partial void Initialize(int sampleRate, int channels, int bufferSize);

        [JSImport("WebAudioWavePlayer.queueAudioData", "WebAudioWavePlayer")]
        public static partial void QueueAudioData(byte[] audioDataBytes, int sampleCount);

        [JSImport("WebAudioWavePlayer.resume", "WebAudioWavePlayer")]
        public static partial void Resume();

        [JSImport("WebAudioWavePlayer.pause", "WebAudioWavePlayer")]
        public static partial void Pause();

        [JSImport("WebAudioWavePlayer.stop", "WebAudioWavePlayer")]
        public static partial void Stop();

        [JSImport("WebAudioWavePlayer.cleanup", "WebAudioWavePlayer")]
        public static partial void Cleanup();
    }
}
