using System.Security.Cryptography;

namespace Highbyte.DotNet6502.Systems.Recording;

public enum RecordingMode { Idle, Recording, Playback }
public enum PlaybackOutcome { None, Completed, Stopped, Failed }

/// <summary>
/// Frame-boundary recording/playback engine. The host owns execution, snapshot restoration,
/// pausing, storage lifetime, and guards against live mutations. This service knows no machine
/// or host-specific input types and retains only the current timeline run.
/// </summary>
public sealed class RecordingSession : IDisposable
{
    private readonly IRecordingAdapter _adapter;
    private DemoWriter? _writer;
    private DemoReader? _reader;
    private DemoTimelineCursor? _cursor;
    private byte[]? _input;
    private uint _remaining;
    private bool _framePending;
    public RecordingMode Mode { get; private set; }
    public PlaybackOutcome Outcome { get; private set; }
    public long Frame { get; private set; }
    public long TotalFrames => _reader?.Manifest.TotalFrames ?? Frame;
    public string? Error { get; private set; }
    public bool OwnsInput => Mode == RecordingMode.Playback;
    public const int CheckpointInterval = 300;
    public void ValidateMemoryInspection(ushort address, int length) => _adapter.ValidateMemoryInspection(address, length);

    public RecordingSession(IRecordingAdapter adapter) => _adapter = adapter;

    public void StartRecording(Stream output, Stream initialSnapshot, string? variant = null)
    {
        EnsureIdle();
        _writer = new DemoWriter(output, initialSnapshot, _adapter, variant);
        Frame = 0;
        Error = null;
        Outcome = PlaybackOutcome.None;
        Mode = RecordingMode.Recording;
    }

    /// <summary>Called only after the host has validated and restored the demo's initial snapshot.</summary>
    public void StartPlayback(DemoReader reader)
    {
        EnsureIdle();
        _adapter.ValidateSession();
        reader.ValidateAdapter(_adapter);
        var actual = Convert.ToHexStringLower(_adapter.ComputeStateDigest());
        if (!string.Equals(actual, reader.Manifest.InitialStateDigest, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"Initial replay state differs: expected {reader.Manifest.InitialStateDigest}, actual {actual}.");
        _reader = reader;
        _cursor = reader.OpenTimeline();
        Frame = 0;
        Error = null;
        Outcome = PlaybackOutcome.None;
        _remaining = 0;
        Mode = RecordingMode.Playback;
    }

    /// <summary>Record after live input resolution, or apply playback instead of live input.</summary>
    public bool BeforeFrame()
    {
        if (_framePending)
            throw new InvalidOperationException("The preceding recording frame has not completed.");
        if (Mode == RecordingMode.Recording)
        {
            _input = _adapter.CaptureInput();
            _adapter.ValidateInput(_input);
        }
        else if (Mode == RecordingMode.Playback)
        {
            try
            {
                if (_remaining == 0 && !ReadNextRun())
                    return false;
                _adapter.ApplyInput(_input!);
            }
            catch (Exception ex)
            {
                Fail(ex.Message);
                throw;
            }
        }
        else
            return false;
        _framePending = true;
        return true;
    }

    /// <summary>Advance only after one complete emulated frame. A debugger interruption fails the session.</summary>
    public void AfterFrame(bool completed = true)
    {
        if (!_framePending)
            throw new InvalidOperationException("No recording frame is pending.");
        _framePending = false;
        if (!completed)
        {
            Fail("Execution stopped within a frame; the recording/replay was invalidated.");
            return;
        }
        try
        {
            _adapter.ValidateSession();
            Frame = checked(Frame + 1);
            if (Mode == RecordingMode.Recording)
            {
                _writer!.AppendInput(_input!, _adapter.CanRepeatInput(_input!));
                if (Frame % CheckpointInterval == 0)
                    _writer.WriteCheckpoint(_adapter.ComputeStateDigest());
            }
            else if (Mode == RecordingMode.Playback)
            {
                _remaining--;
                // Consume checkpoints immediately, including the final one, before reporting completion.
                if (_remaining == 0)
                    ReadNextRun();
            }
        }
        catch (Exception ex)
        {
            Fail(ex.Message);
            throw;
        }
    }

    private bool ReadNextRun()
    {
        while (_cursor!.Read() is { } record)
        {
            if (record.Input != null)
            {
                _input = record.Input;
                _remaining = record.Duration;
                return true;
            }
            var actual = _adapter.ComputeStateDigest();
            if (record.Frame != Frame || !CryptographicOperations.FixedTimeEquals(actual, record.Digest!))
                throw new InvalidDataException($"Replay diverged at frame {Frame}: expected {Convert.ToHexStringLower(record.Digest!)}, actual {Convert.ToHexStringLower(actual)}.");
        }
        if (Frame != TotalFrames)
            throw new InvalidDataException("Replay ended before its declared final frame.");
        EndPlayback(PlaybackOutcome.Completed);
        return false;
    }

    public void StopRecording()
    {
        if (Mode != RecordingMode.Recording || _framePending)
            throw new InvalidOperationException("Recording can stop only at a frame boundary.");
        try
        {
            _writer!.Complete(_adapter.ComputeStateDigest());
            Mode = RecordingMode.Idle;
        }
        catch (Exception ex)
        {
            Fail(ex.Message);
            throw;
        }
        finally
        {
            _writer?.Dispose();
            _writer = null;
        }
    }

    public void StopPlayback()
    {
        if (Mode == RecordingMode.Playback)
            EndPlayback(PlaybackOutcome.Stopped);
    }

    public void Fail(string error)
    {
        Error = error;
        _framePending = false;
        _writer?.Dispose();
        _writer = null;
        if (Mode == RecordingMode.Playback)
            EndPlayback(PlaybackOutcome.Failed);
        else
        {
            Mode = RecordingMode.Idle;
            Outcome = PlaybackOutcome.Failed;
        }
    }

    private void EndPlayback(PlaybackOutcome outcome)
    {
        Mode = RecordingMode.Idle;
        Outcome = outcome;
        _cursor?.Dispose();
        _cursor = null;
        _adapter.ReleaseInput();
    }

    private void EnsureIdle()
    {
        if (Mode != RecordingMode.Idle)
            throw new InvalidOperationException("Recording and playback are mutually exclusive.");
    }

    public void Dispose()
    {
        StopPlayback();
        if (Mode == RecordingMode.Recording)
            Fail("Recording was closed before it was saved.");
        _writer?.Dispose();
        _cursor?.Dispose();
    }
}
