using Highbyte.DotNet6502.Systems.Audio;

namespace Highbyte.DotNet6502.Systems.Tests.Audio;

public class AudioSampleCoordinatorTests
{
    [Fact]
    public void Init_primes_the_configured_number_of_silent_samples()
    {
        var provider = new TestSampleProvider();
        var target = new TestSampleTarget(readCountOnInit: 8);
        var coordinator = new AudioSampleCoordinator(
            provider,
            target,
            ringBufferCapacitySamples: 16,
            primeSilenceSamples: 4);

        coordinator.Init();

        Assert.Equal(4, target.SamplesReadOnInit);
        Assert.Equal(new float[] { 0f, 0f, 0f, 0f }, target.InitReadBuffer[..target.SamplesReadOnInit]);
    }

    [Fact]
    public void Init_can_skip_prime_silence()
    {
        var provider = new TestSampleProvider();
        var target = new TestSampleTarget(readCountOnInit: 8);
        var coordinator = new AudioSampleCoordinator(
            provider,
            target,
            ringBufferCapacitySamples: 16,
            primeSilenceSamples: 0);

        coordinator.Init();

        Assert.Equal(0, target.SamplesReadOnInit);
    }

    private sealed class TestSampleProvider : IAudioSampleProvider
    {
        public int SampleRateHz => 44100;
        public int ChannelCount => 1;
        public string Name => nameof(TestSampleProvider);

        public void Init(AudioSampleWriteCallback writeSamples)
        {
        }
    }

    private sealed class TestSampleTarget : IAudioSampleTarget
    {
        private readonly int _readCountOnInit;

        public TestSampleTarget(int readCountOnInit)
        {
            _readCountOnInit = readCountOnInit;
            InitReadBuffer = new float[readCountOnInit];
        }

        public string Name => nameof(TestSampleTarget);
        public float[] InitReadBuffer { get; }
        public int SamplesReadOnInit { get; private set; }

        public void Init(int sampleRateHz, int channelCount, AudioSampleReadCallback readSamples)
        {
            SamplesReadOnInit = readSamples(InitReadBuffer);
        }

        public void StartPlaying()
        {
        }

        public void StopPlaying()
        {
        }

        public void PausePlaying()
        {
        }

        public void Cleanup()
        {
        }
    }
}
