namespace Highbyte.DotNet6502.Systems.Tests.Instrumentation;

public class AveragedStatTest
{
    [Fact]
    public void Value_WhenCalled_ReturnsValue()
    {
        // Arrange
        var stat = new TestAveragedStat(10);
        stat.UpdateStat(1.0);

        // Act
        var value = stat.Value;

        // Assert
        Assert.Equal(1.0, value);
    }

    [Fact]
    public void Value_WhenCalled_ReturnsValue_Average()
    {
        // Arrange
        var stat = new TestAveragedStat(sampleCount: 2);
        stat.UpdateStat(1.0);
        stat.UpdateStat(2.0);

        // Act
        var value = stat.Value;

        // Assert
        Assert.Equal(1.5, value);
    }
}
