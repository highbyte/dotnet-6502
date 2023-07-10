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

    public WaveFormat OutputWaveFormat => sourceProvider.WaveFormat;

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

    public int bufferSizeByte;

    /// <summary>
    /// Gets or sets the number of buffers used
    /// Should be set before a call to InitPlayer
    /// </summary>
    public int NumberOfBuffers { get; set; }

    private unsafe Device* device;
    public ALContext? Alc { get; private set; }
    public AL? Al { get; private set; }

    private unsafe Context* context;

    private FloatBufferFormat sourceALFormat;   // 32 bit floating point buffer
                                                //private BufferFormat sourceALFormat_8_16;        // 8 or 16 bit buffer

    private IWaveProvider? sourceProvider;

    private readonly SynchronizationContext? syncContext;
    private AutoResetEvent? eventWaitHandle;

    //private int alSource;
    private uint alSource;

    //private int[] alBuffers;
    private uint[]? alBuffers;

    private byte[]? sourceBuffer;
    public SilkNetOpenALWavePlayer(string? deviceName = null)
    {
        DeviceName = deviceName;
        syncContext = SynchronizationContext.Current;
    }

    public unsafe void Init(IWaveProvider waveProvider)
    {
        sourceProvider = waveProvider;
        sourceALFormat = BufferFormatFloat32(sourceProvider.WaveFormat);
        bufferSizeByte = sourceProvider.WaveFormat.ConvertLatencyToByteSize(DesiredLatency);

        eventWaitHandle = new AutoResetEvent(false);

        Alc = ALContext.GetApi(soft: true);
        Al = AL.GetApi(true);

        CheckAndRaiseStopOnALError();

        device = Alc.OpenDevice(DeviceName);
        CheckAndRaiseStopOnALError();

        context = Alc.CreateContext(device, null);
        CheckAndRaiseStopOnALError();

        Alc.MakeContextCurrent(context);
        CheckAndRaiseStopOnALError();

        //Al.GenSource(out alSource);
        alSource = Al.GenSource();

        CheckAndRaiseStopOnALError();

        //Al.Source(alSource, ALSourcef.Gain, 1f);
        Al.SetSourceProperty(alSource, SourceFloat.Gain, 1f);
        CheckAndRaiseStopOnALError();

        //alBuffers = new int[NumberOfBuffers];
        alBuffers = new uint[NumberOfBuffers];
        for (var i = 0; i < NumberOfBuffers; i++)
        {
            //AL.GenBuffer(out alBuffers[i]);
            alBuffers[i] = Al.GenBuffer();

            CheckAndRaiseStopOnALError();
        }
        sourceBuffer = new byte[bufferSizeByte];
        ReadAndQueueBuffers(alBuffers);
    }

    private void ReadAndQueueBuffers(uint[] _alBuffers)
    {
        for (var i = 0; i < _alBuffers.Length; i++)
        {
            //read source
            sourceProvider.Read(sourceBuffer, 0, sourceBuffer.Length);

            CheckAndRaiseStopOnALError();

            //fill and queue buffer
            //AL.BufferData(_alBuffers[i], sourceALFormat, sourceBuffer, sourceBuffer.Length, sourceProvider.WaveFormat.SampleRate);
            Al.BufferData(_alBuffers[i], sourceALFormat, sourceBuffer, sourceProvider.WaveFormat.SampleRate);

            CheckAndRaiseStopOnALError();

            // AL.SourceQueueBuffer(alSource, _alBuffers[i]);
            var buffersToQueue = new uint[1] { _alBuffers[i] };
            Al.SourceQueueBuffers(alSource, buffersToQueue);

            CheckAndRaiseStopOnALError();
        }
    }

    public void Pause()
    {
        if (PlaybackState == PlaybackState.Stopped)
            throw new InvalidOperationException("Stopped");

        PlaybackState = PlaybackState.Paused;

        //Context.Al.SourcePause(1, Source);
        Al.SourcePause(alSource);
    }

    public void Play()
    {
        if (alBuffers == null)
            throw new InvalidOperationException("Must call InitPlayer first");
        if (PlaybackState != PlaybackState.Playing)
        {
            if (PlaybackState == PlaybackState.Stopped)
            {
                PlaybackState = PlaybackState.Playing;
                eventWaitHandle.Set();
                ThreadPool.QueueUserWorkItem(state => PlaybackThread(), null);
            }
            else
            {
                PlaybackState = PlaybackState.Playing;
                eventWaitHandle.Set();
            }
        }
    }

    public void Stop()
    {
        if (PlaybackState != PlaybackState.Stopped)
        {
            PlaybackState = PlaybackState.Stopped;
            eventWaitHandle.Set();
        }
    }

    private void PlaybackThread()
    {
        Exception exception = null;
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
                eventWaitHandle.WaitOne(1);
                continue;
            }

            CheckAndRaiseStopOnALError();

            //AL.GetSource(alSource, ALGetSourcei.BuffersProcessed, out processed);
            Al.GetSourceProperty(alSource, GetSourceInteger.BuffersProcessed, out var processed);

            CheckAndRaiseStopOnALError();

            //AL.GetSource(alSource, ALGetSourcei.SourceState, out state);
            Al.GetSourceProperty(alSource, GetSourceInteger.SourceState, out var state);

            CheckAndRaiseStopOnALError();

            if (processed > 0) //there are processed buffers
            {
                //unqueue
                //int[] unqueueBuffers = AL.SourceUnqueueBuffers(alSource, processed);

                var unqueueBuffers = new uint[processed];
                Al.SourceUnqueueBuffers(alSource, unqueueBuffers);

                CheckAndRaiseStopOnALError();
                //refill it back in
                ReadAndQueueBuffers(unqueueBuffers);
            }

            if ((SourceState)state != SourceState.Playing)
            {
                //AL.SourcePlay(alSource);
                Al.SourcePlay(alSource);
                CheckAndRaiseStopOnALError();
            }

            eventWaitHandle.WaitOne(1);
        }

        // StartRelease playing do clean up
        //AL.SourceStop(alSource);
        Al.SourceStop(alSource);
        CheckAndRaiseStopOnALError();

        //detach buffer to be able to delete
        //AL.Source(alSource, ALSourcei.Buffer, 0);
        Al.SetSourceProperty(alSource, SourceInteger.Buffer, 0);

        CheckAndRaiseStopOnALError();

        //AL.DeleteBuffers(alBuffers);
        Al.DeleteBuffers(alBuffers);
        CheckAndRaiseStopOnALError();

        alBuffers = null;

        //AL.DeleteSource(alSource);
        Al.DeleteSource(alSource);
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
        //if (device == null)
        //{
        //    ContextError contextError = Alc.GetError(device);
        //    isErr = contextError != ContextError.NoError;
        //    errStr = isErr ? contextError.ToString() : string.Empty;
        //    return isErr;
        //}

        //errStr = string.Empty;
        //return true;

        // TODO: error is always illegalcommand, even though it seems to work?
        //ALError error = AL.GetError();
        var error = Al.GetError();
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

    private void RaisePlaybackStoppedEvent(Exception e)
    {
        var handler = PlaybackStopped;
        if (handler != null)
        {
            if (syncContext == null)
                handler(this, new StoppedEventArgs(e));
            else
            {
                syncContext.Post(state => handler(this, new StoppedEventArgs(e)), null);
            }
        }
    }

    private bool disposedValue;
    protected unsafe virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                // dispose managed state (managed objects)
                eventWaitHandle?.Dispose();
                //ALC.MakeContextCurrent(ALContext.Null);
                Alc?.MakeContextCurrent(null);
                //ALC.DestroyContext(context);
                Alc?.DestroyContext(context);
                //ALC.CloseDevice(device);
                Alc?.CloseDevice(device);
            }

            // free unmanaged resources (unmanaged objects) and override finalizer
            // set large fields to null
            disposedValue = true;
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
                //return ALFormat.Stereo16;
                return BufferFormat.Stereo16;
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
                //return ALFormat.Mono16;
                return BufferFormat.Mono16;
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
