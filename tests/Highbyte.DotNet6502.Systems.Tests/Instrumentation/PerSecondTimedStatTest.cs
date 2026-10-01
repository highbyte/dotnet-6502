
using Highbyte.DotNet6502.Systems.Instrumentation.Stats;

namespace Highbyte.DotNet6502.Systems.Tests.Instrumentation;

public class PerSecondTimedStatTest
{
    // Test PerSecondTimedStatTest that it calculates per second correctly.
    // This test is not very accurate, but it should be good enough to catch any major errors.
    [Fact]
    public void Update_WhenUsed_Returns_Correct_PerSecond_Value()
    {
        // Arrange
        var stat = new PerSecondTimedStat();

        // Act
        stat.SetFakeFPSValue(60);

        // Assert
        Assert.Equal(60, stat.Value);
    }

    [Fact]
    public void GetDescription_WhenUsed_Returns_Null_When_No_Data_Yet()
    {
        // Arrange
        var stat = new PerSecondTimedStat();

        // Act
        // Assert
        Assert.Equal("null", stat.GetDescription());
    }

    [Fact]
    public void GetDescription_WhenUsed_Returns_Special_String_When_FPS_Is_Less_Than_OneHundreds_Of_A_Second()
    {
        // Arrange
        var stat = new PerSecondTimedStat();

        // Act
        stat.SetFakeFPSValue(0.009);

        // Assert
        Assert.Equal("< 0.01", stat.GetDescription());
    }

    [Fact]
    public void GetDescription_WhenUsed_Returns_String_With_FPS()
    {
        // Arrange
        var clock = new ManualTimeProvider();
        var stat = new PerSecondTimedStat(clock);

        // Act
        stat.Update();
        clock.AdvanceMilliseconds(16);
        stat.Update();

        // Assert
        var fps = stat.Value;
        Assert.Equal(Math.Round(fps ?? 0, 2).ToString(), stat.GetDescription());
    }
    [Fact]
    public void Repeated_Timestamps_Do_Not_Throw_Or_Change_A_Valid_Rate()
    {
        var clock = new ManualTimeProvider();
        var stat = new PerSecondTimedStat(clock);

        stat.Update();
        stat.Update();
        Assert.Null(stat.Value);
        Assert.False(stat.ShouldShow());

        clock.AdvanceMilliseconds(16);
        stat.Update();
        Assert.Equal(62.5, stat.Value);

        stat.Update();
        Assert.Equal(62.5, stat.Value);
        clock.AdvanceMilliseconds(16);
        stat.Update();
        Assert.Equal(62.5, stat.Value);
    }

    [Fact]
    public void Reset_Discards_The_Previous_Timestamp_And_Average()
    {
        var clock = new ManualTimeProvider();
        var stat = new PerSecondTimedStat(clock);
        stat.Update();
        clock.AdvanceMilliseconds(16);
        stat.Update();

        stat.ResetAverage();
        Assert.Null(stat.Value);
        clock.AdvanceMilliseconds(1000);
        stat.Update();
        Assert.Null(stat.Value);
        clock.AdvanceMilliseconds(20);
        stat.Update();
        Assert.Equal(50, stat.Value);
    }

    [Fact]
    public void Varying_Intervals_Are_Averaged_Before_Computing_The_Rate()
    {
        var clock = new ManualTimeProvider();
        var stat = new PerSecondTimedStat(clock);
        stat.Update();
        clock.AdvanceMilliseconds(20);
        stat.Update();
        clock.AdvanceMilliseconds(40);
        stat.Update();

        var expectedAverageMs = (20.0 * 59 + 40) / 60;
        Assert.Equal(1000 / expectedAverageMs, stat.Value!.Value, precision: 10);
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        private long _timestamp;
        public override long TimestampFrequency => 1000;
        public override long GetTimestamp() => _timestamp;
        public void AdvanceMilliseconds(long milliseconds) => _timestamp += milliseconds;
    }
}
