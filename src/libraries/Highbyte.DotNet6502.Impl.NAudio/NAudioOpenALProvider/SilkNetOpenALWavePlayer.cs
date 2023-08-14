using NAudio.Wave;
using Silk.NET.OpenAL;
using Silk.NET.OpenAL.Extensions.EXT;

namespace Highbyte.DotNet6502.Impl.NAudio.NAudioOpenALProvider;

/// <summary>
/// A NAudio WavePlayer that uses OpenAL (OpenAL bindings provided by Silk.NET.OpenAL) to play audio for cross platform support.
/// (NAudio built-in audio output is Windows only)
/// </summary>
public class SilkNetOpenALWavePlayer : IWavePlayer
{
    public float Volume { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

    public WaveFormat OutputWaveFormat => _sourceProvider!.WaveFormat;

    public PlaybackState PlaybackState { get; private set; }

    /// <summary>
    /// Gets or sets the Device
    /// Should be set before a call to InitPlayer
    /// </summary>
    public string? DeviceName { get; set; }
    /// <summary>
    /// Indicates playback has stopped automatically
    /// </summary>
    public event EventHandler<StoppedEventArgs>? PlaybackStopped;

    /// <summary>
    /// Gets or sets the desired latency in milliseconds
    /// Should be set before a call to InitPlayer
    /// </summary>
    public int DesiredLatency { get; set; }

    private int _bufferSizeByte;

    /// <summary>
    /// Gets or sets the number of buffers used
    /// Should be set before a call to InitPlayer
    /// </summary>
    public int NumberOfBuffers { get; set; }

    private unsafe Device* _device;
    public ALContext? Alc { get; private set; }
    public AL? Al { get; private set; }

    private unsafe Context* _context;

    private FloatBufferFormat _sourceALFormat;   // 32 bit floating point buffer
    //private BufferFormat sourceALFormat_8_16;        // 8 or 16 bit buffer

    private IWaveProvider? _sourceProvider;

    private readonly SynchronizationContext? _syncContext;
    private AutoResetEvent? _eventWaitHandle;

    //private int _alSource;
    private uint _alSource;

    //private int[] alBuffers;
    private uint[]? _alBuffers;

    private byte[]? _sourceBuffer;
    public SilkNetOpenALWavePlayer(string? deviceName = null)
    {
        DeviceName = deviceName;
        _syncContext = SynchronizationContext.Current;
    }

    public unsafe void Init(IWaveProvider waveProvider)
    {
        _sourceProvider = waveProvider;
        _sourceALFormat = BufferFormatFloat32(_sourceProvider.WaveFormat);
        _bufferSizeByte = _sourceProvider.WaveFormat.ConvertLatencyToByteSize(DesiredLatency);

        _eventWaitHandle = new AutoResetEvent(false);

        Alc = ALContext.GetApi(soft: true);
        Al = AL.GetApi(true);

        CheckAndRaiseStopOnALError();

        _device = Alc.OpenDevice(DeviceName);
        CheckAndRaiseStopOnALError();

        _context = Alc.CreateContext(_device, null);
        CheckAndRaiseStopOnALError();

        Alc.MakeContextCurrent(_context);
        CheckAndRaiseStopOnALError();

        //Al.GenSource(out _alSource);
        _alSource = Al.GenSource();

        CheckAndRaiseStopOnALError();

        //Al.Source(_alSource, ALSourcef.Gain, 1f);
        Al.SetSourceProperty(_alSource, SourceFloat.Gain, 1f);
        CheckAndRaiseStopOnALError();

        //alBuffers = new int[NumberOfBuffers];
        _alBuffers = new uint[NumberOfBuffers];
        for (var i = 0; i < NumberOfBuffers; i++)
        {
            //AL.GenBuffer(out alBuffers[i]);
            _alBuffers[i] = Al.GenBuffer();

            CheckAndRaiseStopOnALError();
        }
        _sourceBuffer = new byte[_bufferSizeByte];
        ReadAndQueueBuffers(_alBuffers);
    }

    private void ReadAndQueueBuffers(uint[] alBuffers)
    {
        for (var i = 0; i < alBuffers.Length; i++)
        {
            //read source
            _sourceProvider!.Read(_sourceBuffer, 0, _sourceBuffer!.Length);

            CheckAndRaiseStopOnALError();

            //fill and queue buffer
            //AL.BufferData(alBuffers[i], _sourceALFormat, _sourceBuffer, _sourceBuffer.Length, _sourceProvider.WaveFormat.SampleRate);
            Al!.BufferData(alBuffers[i], _sourceALFormat, _sourceBuffer, _sourceProvider.WaveFormat.SampleRate);

            CheckAndRaiseStopOnALError();

            // AL.SourceQueueBuffer(_alSource, alBuffers[i]);
            var buffersToQueue = new uint[1] { alBuffers[i] };
            Al!.SourceQueueBuffers(_alSource, buffersToQueue);

            CheckAndRaiseStopOnALError();
        }
    }

    public void Pause()
    {
        if (PlaybackState == PlaybackState.Stopped)
            throw new InvalidOperationException("Stopped");

        PlaybackState = PlaybackState.Paused;

        //Context.Al.SourcePause(1, Source);
        Al!.SourcePause(_alSource);
    }

    public void Play()
    {
        if (_alBuffers == null)
            throw new InvalidOperationException("Must call InitPlayer first");
        if (PlaybackState != PlaybackState.Playing)
        {
            if (PlaybackState == PlaybackState.Stopped)
            {
                PlaybackState = PlaybackState.Playing;
                _eventWaitHandle!.Set();
                ThreadPool.QueueUserWorkItem(state => PlaybackThread(), null);
            }
            else
            {
                PlaybackState = PlaybackState.Playing;
                _eventWaitHandle!.Set();
            }
        }
    }

    public void Stop()
    {
        if (PlaybackState != PlaybackState.Stopped)
        {
            PlaybackState = PlaybackState.Stopped;
            _eventWaitHandle!.Set();
        }
    }

    private void PlaybackThread()
    {
        Exception? exception = null;
        try
        {
            DoPlayback();
        }
        catch (Exception e)
        {
            exception = e;
        }
        finally
        {
            PlaybackState = PlaybackState.Stopped;
            // we're exiting our background thread
            RaisePlaybackStoppedEvent(exception);
        }
    }

    private void DoPlayback()
    {

        while (PlaybackState == PlaybackState.Playing || PlaybackState == PlaybackState.Paused)
        {
            if (PlaybackState == PlaybackState.Paused)
            {
                _eventWaitHandle!.WaitOne(1);
                continue;
            }

            CheckAndRaiseStopOnALError();

            //AL.GetSource(_alSource, ALGetSourcei.BuffersProcessed, out processed);
            Al!.GetSourceProperty(_alSource, GetSourceInteger.BuffersProcessed, out var processed);

            CheckAndRaiseStopOnALError();

            //AL.GetSource(_alSource, ALGetSourcei.SourceState, out state);
            Al!.GetSourceProperty(_alSource, GetSourceInteger.SourceState, out var state);

            CheckAndRaiseStopOnALError();

            if (processed > 0) //there are processed buffers
            {
                //unqueue
                //int[] unqueueBuffers = AL.SourceUnqueueBuffers(_alSource, processed);

                var unqueueBuffers = new uint[processed];
                Al.SourceUnqueueBuffers(_alSource, unqueueBuffers);

                CheckAndRaiseStopOnALError();
                //refill it back in
                ReadAndQueueBuffers(unqueueBuffers);
            }

            if ((SourceState)state != SourceState.Playing)
            {
                //AL.SourcePlay(_alSource);
                Al.SourcePlay(_alSource);
                CheckAndRaiseStopOnALError();
            }

            _eventWaitHandle!.WaitOne(1);
        }

        // StartRelease playing do clean up
        //AL.SourceStop(_alSource);
        Al!.SourceStop(_alSource);
        CheckAndRaiseStopOnALError();

        //detach buffer to be able to delete
        //AL.Source(_alSource, ALSourcei.Buffer, 0);
        Al.SetSourceProperty(_alSource, SourceInteger.Buffer, 0);

        CheckAndRaiseStopOnALError();

        //AL.DeleteBuffers(alBuffers);
        Al.DeleteBuffers(_alBuffers);
        CheckAndRaiseStopOnALError();

        _alBuffers = null;

        //AL.DeleteSource(_alSource);
        Al.DeleteSource(_alSource);
        CheckAndRaiseStopOnALError();
    }

    /// <summary>
    /// Check for AL error
    /// </summary>
    /// <param name="errStr">Error string if error, empty string if not.</param>
    /// <returns>true if error</returns>
    public unsafe bool CheckALError(out string errStr)
    {
        //bool isErr;
        //if (_device == null)
        //{
        //    ContextError contextError = Alc.GetError(_device);
        //    isErr = contextError != ContextError.NoError;
        //    errStr = isErr ? contextError.ToString() : string.Empty;
        //    return isErr;
        //}

        //errStr = string.Empty;
        //return true;

        // TODO: error is always illegalcommand, even though it seems to work?
        //ALError error = AL.GetError();
        var error = Al!.GetError();
        var isErr = error != AudioError.NoError;
        //errStr = isErr ? Al.GetErrorString(error) : string.Empty;
        errStr = isErr ? error.ToString() : string.Empty;
        return isErr;
    }

    public void CheckAndRaiseStopOnALError()
    {
        if (CheckALError(out var errStr))
            RaisePlaybackStoppedEvent(new Exception($"ALError:{errStr}"));
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
    protected unsafe virtual void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            if (disposing)
            {
                // dispose managed state (managed objects)
                _eventWaitHandle?.Dispose();
                //ALC.MakeContextCurrent(ALContext.Null);
                Alc?.MakeContextCurrent(null);
                //ALC.DestroyContext(_context);
                Alc?.DestroyContext(_context);
                //ALC.CloseDevice(_device);
                Alc?.CloseDevice(_device);
            }

            // free unmanaged resources (unmanaged objects) and override finalizer
            // set large fields to null
            _disposedValue = true;
        }
    }

    // // override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
    // ~AudioPlayer()
    // {
    //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
    //     Dispose(disposing: false);
    // }

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    private BufferFormat TranslateFormat(WaveFormat format)
    {
        if (format.Channels == 2)
        {
            //if (format.BitsPerSample == 32)
            //{
            //    //return ALFormat.StereoFloat32Ext;
            //}
            if (format.BitsPerSample == 16)
            {
                //return ALFormat.Stereo16;
                return BufferFormat.Stereo16;
            }
            else if (format.BitsPerSample == 8)
            {
                //return ALFormat.Stereo16;
                return BufferFormat.Stereo8;
            }
        }
        else if (format.Channels == 1)
        {
            //if (format.BitsPerSample == 32)
            //{
            //    //return ALFormat.MonoFloat32Ext;
            //}
            if (format.BitsPerSample == 16)
            {
                //return ALFormat.Mono16;
                return BufferFormat.Mono16;
            }
            else if (format.BitsPerSample == 8)
            {
                //return ALFormat.Mono8;
                return BufferFormat.Mono8;

            }
        }

        throw new FormatException("Cannot translate WaveFormat.");
    }

    private FloatBufferFormat BufferFormatFloat32(WaveFormat format)
    {
        if (format.Channels == 2)
        {
            if (format.BitsPerSample == 32)
                //return ALFormat.StereoFloat32Ext;
                return FloatBufferFormat.Stereo;
        }
        else if (format.Channels == 1)
        {
            if (format.BitsPerSample == 32)
                //return ALFormat.MonoFloat32Ext;
                return FloatBufferFormat.Mono;
        }
        throw new FormatException("Cannot translate WaveFormat.");
    }
}
