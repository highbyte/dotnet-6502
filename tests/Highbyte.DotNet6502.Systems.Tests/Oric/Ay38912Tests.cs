using Highbyte.DotNet6502.Systems.Oric.Audio;

namespace Highbyte.DotNet6502.Systems.Tests.Oric;

public sealed class Ay38912Tests
{
    [Fact]
    public void RegistersApplyHardwareBitMasks()
    {
        var ay = new Ay38912();
        ay.WriteRegister(1, 0xff);
        ay.WriteRegister(6, 0xff);
        ay.WriteRegister(8, 0xff);

        Assert.Equal(0x0f, ay.ReadRegister(1));
        Assert.Equal(0x1f, ay.ReadRegister(6));
        Assert.Equal(0x1f, ay.ReadRegister(8));
    }

    [Fact]
    public void EnabledToneProducesPcmSamples()
    {
        var ay = new Ay38912();
        ay.WriteRegister(0, 1);
        ay.WriteRegister(7, 0x38); // noise disabled, tone enabled on all channels
        ay.WriteRegister(8, 0x0f);
        Span<float> samples = stackalloc float[64];

        var count = ay.AdvanceCycles(1_000, samples);

        Assert.True(count > 0);
        Assert.Contains(samples[..count].ToArray(), sample => sample > 0);
    }

    [Fact]
    public void SingleActiveChannelCanUseTheFullOutputRange()
    {
        var ay = new Ay38912();
        ay.WriteRegister(0, 1);
        ay.WriteRegister(7, 0x3e); // channel A tone enabled; all other tone/noise gates disabled
        ay.WriteRegister(8, 0x0f);
        Span<float> samples = stackalloc float[128];

        var count = ay.AdvanceCycles(5_000, samples);

        Assert.InRange(samples[..count].ToArray().Max(), 0.99f, 1f);
    }

    [Fact]
    public void ThreeActiveChannelsStayWithinTheOutputRange()
    {
        var ay = new Ay38912();
        ay.WriteRegister(0, 1);
        ay.WriteRegister(2, 2);
        ay.WriteRegister(4, 3);
        ay.WriteRegister(7, 0x38); // all tone channels enabled; noise disabled
        ay.WriteRegister(8, 0x0f);
        ay.WriteRegister(9, 0x0f);
        ay.WriteRegister(10, 0x0f);
        Span<float> samples = stackalloc float[128];

        var count = ay.AdvanceCycles(5_000, samples);

        Assert.All(samples[..count].ToArray(), sample => Assert.InRange(sample, 0f, 1f));
        Assert.Contains(samples[..count].ToArray(), sample => sample > 0.9f);
    }

    [Fact]
    public void ChannelsWithToneAndNoiseDisabledProduceSilence()
    {
        var ay = new Ay38912();
        ay.WriteRegister(7, 0x3f);
        ay.WriteRegister(8, 0x0f);
        Span<float> samples = stackalloc float[64];

        var count = ay.AdvanceCycles(1_000, samples);

        Assert.All(samples[..count].ToArray(), sample => Assert.Equal(0f, sample));
    }
}
