using Highbyte.DotNet6502.Systems.Apple2.Audio.Sample;
using Highbyte.DotNet6502.Systems.Apple2.Config;
using Highbyte.DotNet6502.Systems.Apple2.Peripherals;
using Microsoft.Extensions.Logging.Abstractions;
using Apple2System = Highbyte.DotNet6502.Systems.Apple2.Apple2;

namespace Highbyte.DotNet6502.Systems.Tests.Apple2;

/// <summary>
/// That a running machine actually has audio wired, end to end through the paths the app uses.
///
/// These exist because the first working audio implementation shipped completely silent: the
/// resampler was correct and unit-tested, the ROM bell measured at the right pitch, and the config
/// held the right provider and target — but nothing ever constructed the provider, so
/// <see cref="ISystem.AudioProvider"/> was null, the host built no audio coordinator, and not one
/// test noticed because every one of them built the provider directly. The gap was only found by
/// comparing the app's log against the C64's.
///
/// So these assert the wiring rather than the audio: that enabling audio produces a provider, that
/// the machine ticks it as it runs, and that the user-facing setting reaches the machine config.
/// </summary>
public class Apple2AudioWiringTests
{
    private static Apple2System BuildApple2(bool audioEnabled)
        => new(new Apple2Config { AudioEnabled = audioEnabled }, NullLoggerFactory.Instance);

    [Fact]
    public void Enabling_Audio_Gives_The_Machine_A_Provider()
    {
        var apple2 = BuildApple2(audioEnabled: true);

        Assert.NotNull(apple2.AudioProvider);
        Assert.IsType<Apple2SpeakerSampleProvider>(apple2.AudioProvider);

        // The host reads it through ISystem; a provider only visible on the concrete type would
        // leave the host building no coordinator at all.
        Assert.Same(apple2.AudioProvider, ((ISystem)apple2).AudioProvider);
    }

    [Fact]
    public void Disabling_Audio_Leaves_No_Provider()
    {
        var apple2 = BuildApple2(audioEnabled: false);

        // Null is how the machine says "silent": the host then builds no audio coordinator.
        Assert.Null(apple2.AudioProvider);
        Assert.Empty(apple2.AudioProviders);
    }

    /// <summary>
    /// The one that would have caught the original bug. A provider that exists but is never ticked
    /// produces nothing, and looks identical from the outside to one that is working.
    /// </summary>
    [Fact]
    public void Running_The_Machine_Drives_The_Provider()
    {
        var apple2 = BuildApple2(audioEnabled: true);
        var provider = (Apple2SpeakerSampleProvider)apple2.AudioProvider!;

        var samples = 0;
        provider.Init(written => { samples += written.Length; return written.Length; });

        apple2.ExecuteOneFrame();

        // One frame is ~17,030 cycles, about 740 samples at 44.1 kHz. Any figure near that proves
        // the system is calling OnAfterInstruction as it executes.
        Assert.True(samples > 500, $"Expected the machine to drive the provider, but it produced {samples} samples.");
    }

    [Fact]
    public void A_Toggled_Speaker_Reaches_The_Provider_While_Running()
    {
        var apple2 = BuildApple2(audioEnabled: true);
        var provider = (Apple2SpeakerSampleProvider)apple2.AudioProvider!;
        provider.Init(_ => 0);

        // Run a frame first: the provider ignores toggles from before it was wired up, so that it
        // does not backfill stale sound the moment audio is switched on.
        apple2.ExecuteOneFrame();

        var before = provider.Core.Level;
        _ = apple2.Mem[Apple2Speaker.ToggleAddress];
        apple2.ExecuteOneFrame();

        Assert.NotEqual(before, provider.Core.Level);
        Assert.Equal(1UL, apple2.Speaker.ToggleCount);
    }

    /// <summary>
    /// The user-facing setting has to reach the machine config, or the checkbox does nothing —
    /// the same copy step <c>MonitorColor</c> needs.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void The_Configured_Setting_Reaches_The_Machine(bool audioEnabled)
    {
        var systemConfig = new Apple2SystemConfig { AudioEnabled = audioEnabled };
        var apple2Config = new Apple2Config();

        // Mirrors what Apple2SystemConfigurerCore does when building the machine.
        apple2Config.AudioEnabled = systemConfig.AudioEnabled;
        apple2Config.AudioProviderType = systemConfig.AudioProviderType;

        var apple2 = new Apple2System(apple2Config, NullLoggerFactory.Instance);

        Assert.Equal(audioEnabled, apple2.AudioProvider != null);
    }

    [Fact]
    public void The_Only_Provider_Is_Selected_When_The_Config_Names_None()
    {
        // A config that has never been through the dialog leaves AudioProviderType null. That must
        // still yield the speaker rather than silence.
        var apple2 = new Apple2System(
            new Apple2Config { AudioEnabled = true, AudioProviderType = null },
            NullLoggerFactory.Instance);

        Assert.IsType<Apple2SpeakerSampleProvider>(apple2.AudioProvider);
    }
}
