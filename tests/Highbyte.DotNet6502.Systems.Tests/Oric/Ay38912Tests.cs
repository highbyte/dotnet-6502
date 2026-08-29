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
}
