using Highbyte.DotNet6502.Systems;

namespace Highbyte.DotNet6502.Tests.Systems;

public class SystemRunnerTests
{
    [Fact]
    public void CallingCleanUpWillCleanUpRenderer()
    {
        // Arrange
        var system = new TestSystem();
        var systemRunner = new SystemRunner(system);
        var renderer = new TestRenderer();
        systemRunner.Renderer = renderer;

        // Act
        systemRunner.Cleanup();

        // Assert
        Assert.True(renderer.CleanUpWasCalled);
    }

    [Fact]
    public void CallingCleanUpWillStopPlayingAndCleanUpAudioHandler()
    {
        // Arrange
        var system = new TestSystem();
        var systemRunner = new SystemRunner(system);

        var audioHandler = new TestAudioHandler();
        systemRunner.AudioHandler = audioHandler;

        // Act
        systemRunner.Cleanup();

        // Assert
        Assert.True(audioHandler.StopPlayingWasCalled);
        //Assert.True(audioHandler.CleanupWasCalled);
    }

}
