using Highbyte.DotNet6502.Systems.Audio;

namespace Highbyte.DotNet6502.Systems.Tests.Audio;

public class AudioSampleRingBufferTests
{
    [Fact]
    public void Capacity_excludes_the_disambiguating_slot()
    {
        var buffer = new AudioSampleRingBuffer(capacitySamples: 8);
        Assert.Equal(8, buffer.Capacity);
        Assert.Equal(0, buffer.Count);
    }

    [Fact]
    public void Constructor_rejects_non_positive_capacity()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new AudioSampleRingBuffer(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new AudioSampleRingBuffer(-1));
    }

    [Fact]
    public void Write_then_Read_round_trips_the_samples()
    {
        var buffer = new AudioSampleRingBuffer(capacitySamples: 16);
        var input = new float[] { 0.1f, 0.2f, 0.3f, 0.4f };

        int wrote = buffer.TryWrite(input);
        Assert.Equal(input.Length, wrote);
        Assert.Equal(input.Length, buffer.Count);

        var output = new float[input.Length];
        int read = buffer.TryRead(output);
        Assert.Equal(input.Length, read);
        Assert.Equal(input, output);
        Assert.Equal(0, buffer.Count);
    }

    [Fact]
    public void Read_into_larger_destination_returns_only_available_count()
    {
        var buffer = new AudioSampleRingBuffer(capacitySamples: 16);
        buffer.TryWrite(new float[] { 1f, 2f });

        var output = new float[5];
        int read = buffer.TryRead(output);
        Assert.Equal(2, read);
        Assert.Equal(1f, output[0]);
        Assert.Equal(2f, output[1]);
        // Tail is unfilled (caller should treat as silence).
        Assert.Equal(0f, output[2]);
    }

    [Fact]
    public void Write_more_than_capacity_returns_short_count()
    {
        var buffer = new AudioSampleRingBuffer(capacitySamples: 4);
        var input = new float[] { 1f, 2f, 3f, 4f, 5f, 6f };

        int wrote = buffer.TryWrite(input);
        Assert.Equal(4, wrote);
        Assert.Equal(4, buffer.Count);
    }

    [Fact]
    public void Write_into_a_full_buffer_returns_zero()
    {
        var buffer = new AudioSampleRingBuffer(capacitySamples: 3);
        buffer.TryWrite(new float[] { 1f, 2f, 3f });

        int wrote = buffer.TryWrite(new float[] { 4f });
        Assert.Equal(0, wrote);
    }

    [Fact]
    public void Read_from_empty_returns_zero()
    {
        var buffer = new AudioSampleRingBuffer(capacitySamples: 8);
        var output = new float[4];

        int read = buffer.TryRead(output);
        Assert.Equal(0, read);
    }

    [Fact]
    public void Wraparound_preserves_sample_order()
    {
        var buffer = new AudioSampleRingBuffer(capacitySamples: 4);

        // Fill, drain half, write more — the second write wraps the internal index.
        buffer.TryWrite(new float[] { 1f, 2f, 3f, 4f });
        var firstDrain = new float[3];
        Assert.Equal(3, buffer.TryRead(firstDrain));
        Assert.Equal(new float[] { 1f, 2f, 3f }, firstDrain);

        Assert.Equal(3, buffer.TryWrite(new float[] { 5f, 6f, 7f }));

        var output = new float[4];
        int read = buffer.TryRead(output);
        Assert.Equal(4, read);
        Assert.Equal(new float[] { 4f, 5f, 6f, 7f }, output);
    }

    [Fact]
    public void Many_interleaved_writes_and_reads_keep_order()
    {
        var buffer = new AudioSampleRingBuffer(capacitySamples: 8);
        float expected = 0f;
        float nextWriteValue = 0f;

        for (int round = 0; round < 100; round++)
        {
            var chunk = new float[5];
            for (int i = 0; i < chunk.Length; i++)
                chunk[i] = nextWriteValue++;
            int wrote = buffer.TryWrite(chunk);

            var read = new float[wrote];
            int actuallyRead = buffer.TryRead(read);
            Assert.Equal(wrote, actuallyRead);

            for (int i = 0; i < actuallyRead; i++)
            {
                Assert.Equal(expected, read[i]);
                expected++;
            }
        }
    }
}
