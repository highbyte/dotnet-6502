using Highbyte.DotNet6502.Systems.Instrumentation.Stats;

namespace Highbyte.DotNet6502.Systems.Tests;

public class ElapsedMillisecondsTimedStatSystemTest
{
    [Fact]
    public void StartAndStopPerformsMeasurementIfSystemHasInstrumentationEnabled()
    {
        // Arrange
        var system = new TestSystem { InstrumentationEnabled = true };

        var stat = new ElapsedMillisecondsTimedStatSystem(system);

        var sleepMs = 2;
        stat.Start();
        Thread.Sleep(sleepMs);
        stat.Stop();

        // Act
        var elapsedMs = stat.GetStatMilliseconds();

        // Assert
        Assert.True(elapsedMs >= sleepMs);
    }

    [Fact]
    public void StartAndStopDoesNotPerformMeasurementIfSystemHasInstrumentationDisabled()
    {
        // Arrange
        var system = new TestSystem { InstrumentationEnabled = false };

        var stat = new ElapsedMillisecondsTimedStatSystem(system);

        var sleepMs = 2;
        stat.Start();
        Thread.Sleep(sleepMs);
        stat.Stop();

        // Act
        var elapsedMs = stat.GetStatMilliseconds();

        // Assert
        Assert.Null(elapsedMs);
    }
}
