namespace Highbyte.DotNet6502.Systems.Audio;

/// <summary>
/// Lock-free single-producer single-consumer ring buffer of <see cref="float"/> samples, used by
/// <see cref="AudioSampleCoordinator"/> to bridge the emulator's bursty write cadence and the host
/// audio device's steady drain.
///
/// Thread-safety: exactly one thread may call <see cref="TryWrite"/>; exactly one (different)
/// thread may call <see cref="TryRead"/>. <see cref="Volatile"/> reads/writes give the producer
/// and consumer correct visibility of each other's position without locks.
///
/// The buffer wastes one slot to disambiguate full from empty without an extra count field, so
/// capacity in samples is one less than the underlying array length.
/// </summary>
public sealed class AudioSampleRingBuffer
{
    private readonly float[] _buffer;
    private int _writePos;
    private int _readPos;

    // Diagnostics counters. Each counter has a single writer thread (overruns: the producer in
    // TryWrite; underruns: the consumer in TryRead), so plain increments are safe. Reads (and the
    // diagnostic Reset) go through Volatile for cross-thread visibility.
    private long _overrunCount;
    private long _underrunCount;

    /// <summary>Construct a ring buffer holding up to <paramref name="capacitySamples"/> samples.</summary>
    public AudioSampleRingBuffer(int capacitySamples)
    {
        if (capacitySamples <= 0)
            throw new ArgumentOutOfRangeException(nameof(capacitySamples), capacitySamples, "Capacity must be positive.");
        _buffer = new float[capacitySamples + 1];
    }

    /// <summary>Total sample capacity (excluding the one disambiguating slot).</summary>
    public int Capacity => _buffer.Length - 1;

    /// <summary>Number of producer writes that had to drop one or more samples because the buffer was full.</summary>
    public long OverrunCount => Volatile.Read(ref _overrunCount);

    /// <summary>Number of consumer reads that were starved (fewer samples available than requested).</summary>
    public long UnderrunCount => Volatile.Read(ref _underrunCount);

    /// <summary>Resets the overrun/underrun diagnostic counters to zero.</summary>
    public void ResetCounters()
    {
        Volatile.Write(ref _overrunCount, 0);
        Volatile.Write(ref _underrunCount, 0);
    }

    /// <summary>Samples currently in the buffer (snapshot; may change immediately on a live system).</summary>
    public int Count
    {
        get
        {
            int writePos = Volatile.Read(ref _writePos);
            int readPos = Volatile.Read(ref _readPos);
            int diff = writePos - readPos;
            return diff >= 0 ? diff : diff + _buffer.Length;
        }
    }

    /// <summary>
    /// Producer-side: copy as many of <paramref name="samples"/> as fit. Excess is dropped on the
    /// caller side (overrun) — Currently a simple behaviour; a later improvements can swap in a
    /// back-pressure policy.
    /// </summary>
    /// <returns>Number of samples actually written (may be less than <c>samples.Length</c>).</returns>
    public int TryWrite(ReadOnlySpan<float> samples)
    {
        int readPos = Volatile.Read(ref _readPos);
        int writePos = _writePos;

        int free = readPos - writePos - 1;
        if (free < 0)
            free += _buffer.Length;

        int toWrite = Math.Min(samples.Length, free);
        if (toWrite < samples.Length)
            _overrunCount++; // Buffer full: one or more samples dropped (producer outran consumer).
        if (toWrite == 0)
            return 0;

        int firstChunk = Math.Min(toWrite, _buffer.Length - writePos);
        samples.Slice(0, firstChunk).CopyTo(_buffer.AsSpan(writePos));
        if (firstChunk < toWrite)
            samples.Slice(firstChunk, toWrite - firstChunk).CopyTo(_buffer.AsSpan(0));

        int newWritePos = writePos + toWrite;
        if (newWritePos >= _buffer.Length)
            newWritePos -= _buffer.Length;
        Volatile.Write(ref _writePos, newWritePos);

        return toWrite;
    }

    /// <summary>
    /// Consumer-side: copy up to <paramref name="destination"/>.Length samples into
    /// <paramref name="destination"/>. Returns the number actually read; callers should treat the
    /// unfilled tail as silence on underrun.
    /// </summary>
    public int TryRead(Span<float> destination)
    {
        int writePos = Volatile.Read(ref _writePos);
        int readPos = _readPos;

        int available = writePos - readPos;
        if (available < 0)
            available += _buffer.Length;

        int toRead = Math.Min(destination.Length, available);
        if (toRead < destination.Length)
            _underrunCount++; // Buffer starved: consumer wanted more than available (tail is silence).
        if (toRead == 0)
            return 0;

        int firstChunk = Math.Min(toRead, _buffer.Length - readPos);
        _buffer.AsSpan(readPos, firstChunk).CopyTo(destination);
        if (firstChunk < toRead)
            _buffer.AsSpan(0, toRead - firstChunk).CopyTo(destination.Slice(firstChunk));

        int newReadPos = readPos + toRead;
        if (newReadPos >= _buffer.Length)
            newReadPos -= _buffer.Length;
        Volatile.Write(ref _readPos, newReadPos);

        return toRead;
    }
}
