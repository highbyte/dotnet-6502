 using System.Diagnostics;
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NAudio.Wave;

namespace Highbyte.DotNet6502.Impl.NAudio.WavePlayers.WebAudioAPI;

/// <summary>
/// A NAudio WavePlayer that uses browser WebAudio API to play audio for browser/WASM support.
/// This implementation pulls audio samples from NAudio and pushes them to JavaScript WebAudio.
/// </summary>
[SupportedOSPlatform("browser")]
public partial class WebAudioWavePlayer : IWavePlayer, IUsesProfile
{
    public float Volume { get; set; } = 1.0f;

    public WaveFormat OutputWaveFormat => _sourceProvider!.WaveFormat;

    private PlaybackState _playbackState;
    public PlaybackState PlaybackState
    {
        get => _playbackState;
        private set
        {
            var originalState = _playbackState;
            _playbackState = value;
            _logger?.LogInformation($"PlaybackState changed from {originalState} to {_playbackState}");
        }
    }

    private readonly ILogger _logger;

    /// <summary>
    /// Indicates playback has stopped automatically
    /// </summary>
    public event EventHandler<StoppedEventArgs>? PlaybackStopped;

    /// <summary>
    /// Gets or sets the desired latency in milliseconds.
    /// Should be set before a call to Init.
    /// Consider using Settings property for more control.
    /// </summary>
    public int DesiredLatency
    {
        get => _settings.DesiredLatencyMs;
        set => _settings.DesiredLatencyMs = value;
    }

    /// <summary>
    /// Gets or sets the audio player settings.
    /// Should be set before a call to Init.
    /// Use predefined profiles like WebAudioWavePlayerSettings.LowLatency or WebAudioWavePlayerSettings.Balanced.
    /// </summary>
    public WebAudioWavePlayerSettings Settings
    {
        get => _settings;
        set => _settings = value ?? throw new ArgumentNullException(nameof(value));
    }
    private WebAudioWavePlayerSettings _settings = WebAudioWavePlayerSettings.Balanced;

    public WavePlayerSettingsProfile ProfileType => _settings.ProfileType;

    private static ILogger s_js_logger = NullLogger.Instance;

    /// <summary>
    /// Sets the logger for JavaScript log messages.
    /// Call this before Init() to receive logs from JavaScript.
    /// </summary>
    /// <param name="logger">The logger instance to use, or null to disable logging.</param>
    public static void SetLogger(ILogger? logger)
    {
        s_js_logger = logger ?? NullLogger.Instance;
    }

    private int _bufferSizeSamples;
    private IWaveProvider? _sourceProvider;

    private readonly SynchronizationContext? _syncContext;
    private CancellationTokenSource? _cancellationTokenSource;
    private Task? _playbackTask;

    private bool _isInitialized;

    public WebAudioWavePlayer(
        ILoggerFactory loggerFactory
        )
    {
        _syncContext = SynchronizationContext.Current;
        PlaybackState = PlaybackState.Stopped;
        _logger = loggerFactory.CreateLogger(typeof(WebAudioWavePlayer).Name);
    }

    /// <summary>
    /// Creates a WebAudioWavePlayer with the specified settings.
    /// </summary>
    /// <param name="settings">The settings to use. Use predefined profiles like WebAudioWavePlayerSettings.LowLatency.</param>
    //public WebAudioWavePlayer(WebAudioWavePlayerSettings settings)
    public WebAudioWavePlayer(WebAudioWavePlayerSettings settings, ILoggerFactory loggerFactory) : this(loggerFactory)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
    }

    public void Init(IWaveProvider waveProvider)
    {
        _logger?.LogInformation("Initializing WebAudioWavePlayer");

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
        _bufferSizeSamples = (int)(_sourceProvider.WaveFormat.SampleRate * (_settings.DesiredLatencyMs / 1000.0));

        // Register the log callback before initializing
        JSInterop.RegisterLogCallback(OnLogMessage);

        // Initialize WebAudio context in JavaScript with all settings
        JSInterop.Initialize(
            _sourceProvider.WaveFormat.SampleRate,
            _sourceProvider.WaveFormat.Channels,
            _bufferSizeSamples,
            _settings.RingBufferCapacityMultiplier,
            _settings.MinBufferBeforePlayMultiplier,
            _settings.ScriptProcessorBufferSize,
            _settings.StatsIntervalMs);

        _isInitialized = true;
    }

    public void Play()
    {
        _logger?.LogInformation("WebAudioWavePlayer Play called");

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
            throw new InvalidOperationException("Cannot pause wave player, already stopped.");

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
            // Cancel playback loop
            _cancellationTokenSource?.Cancel();

            // Stop playback in JavaScript
            JSInterop.Stop();

            // Don't block waiting for the task (_playbackTask?.Wait(1000)) in WebAssembly - the cancellation token
            // will cause the playback loop to exit gracefully on its own.
            // Blocking with Wait() can cause deadlocks in single-threaded WASM.

            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
            _playbackTask = null;

            PlaybackState = PlaybackState.Stopped;
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
            _logger?.LogInformation("Playback loop canceled.");
        }
        catch (Exception e)
        {
            _logger?.LogError(e, "Playback loop error.");
            exception = e;
        }
        finally
        {
            _logger?.LogInformation("Playback loop ending");
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

        // Calculate the exact duration of one buffer in ticks for high precision
        // Stopwatch.Frequency gives ticks per second
        var bufferDurationTicks = (long)(_bufferSizeSamples * (double)Stopwatch.Frequency / _sourceProvider.WaveFormat.SampleRate);
        var bufferDurationMs = (_bufferSizeSamples * 1000.0) / _sourceProvider.WaveFormat.SampleRate;

        // Use a stopwatch to maintain accurate timing regardless of processing delays
        var stopwatch = Stopwatch.StartNew();
        var nextSendTicks = stopwatch.ElapsedTicks;

        while (PlaybackState == PlaybackState.Playing || PlaybackState == PlaybackState.Paused)
        {
            // Check for cancellation early and exit gracefully without processing more audio
            if (cancellationToken.IsCancellationRequested)
                break;

            if (PlaybackState == PlaybackState.Paused)
            {
                await Task.Delay(10, cancellationToken);
                // Reset timing when paused to avoid burst sending on resume
                nextSendTicks = stopwatch.ElapsedTicks;
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
                // Convert float array to byte array forJSImport
                var audioDataBytes = new byte[floatSamples.Length * sizeof(float)];
                Buffer.BlockCopy(floatSamples, 0, audioDataBytes, 0, audioDataBytes.Length);
                JSInterop.QueueAudioData(audioDataBytes, floatSamples.Length);
            }

            // Calculate when we should send the next buffer (using high-precision ticks)
            nextSendTicks += bufferDurationTicks;

            // Calculate how long to wait (accounting for time already spent processing)
            var currentTicks = stopwatch.ElapsedTicks;
            var delayTicks = nextSendTicks - currentTicks;
            var delayMs = (int)(delayTicks * 1000 / Stopwatch.Frequency);

            // Only delay if we're ahead of schedule
            if (delayMs > 0)
            {
                await Task.Delay(delayMs, cancellationToken);
            }
            else if (delayMs < -bufferDurationMs * 2)
            {
                // We're way behind schedule, reset timing to avoid playing catch-up
                nextSendTicks = stopwatch.ElapsedTicks;
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

    /// <summary>
    /// Called from JavaScript to send log messages to .NET.
    /// </summary>
    [JSExport]
    internal static void OnLogMessage(int level, string message)
    {
        var logLevel = level switch
        {
            0 => LogLevel.Debug,
            1 => LogLevel.Information,
            2 => LogLevel.Warning,
            3 => LogLevel.Error,
            _ => LogLevel.Information
        };
        s_js_logger.Log(logLevel, "[JS] {Message}", message);
    }

    // ================================================================================
    // JavaScript Interop Methods
    // ================================================================================

    [SupportedOSPlatform("browser")]
    private static partial class JSInterop
    {
        [JSImport("WebAudioWavePlayer.registerLogCallback", "WebAudioWavePlayer")]
        public static partial void RegisterLogCallback([JSMarshalAs<JSType.Function<JSType.Number, JSType.String>>] Action<int, string> callback);

        [JSImport("WebAudioWavePlayer.initialize", "WebAudioWavePlayer")]
        public static partial void Initialize(
            int sampleRate,
            int channels,
            int bufferSize,
            double ringBufferCapacityMultiplier,
            double minBufferBeforePlayMultiplier,
            int scriptProcessorBufferSize,
            int statsIntervalMs);

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
