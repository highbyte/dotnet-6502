using Highbyte.DotNet6502.Instrumentation.Stats;

namespace Highbyte.DotNet6502.Tests.Instrumentation;

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
        var stat = new PerSecondTimedStat();

        // Act
        stat.Update();
        Thread.Sleep(16);
        stat.Update();

        // Assert
        var fps = stat.Value;
        Assert.Equal(Math.Round(fps ?? 0, 2).ToString(), stat.GetDescription());
    }
}
