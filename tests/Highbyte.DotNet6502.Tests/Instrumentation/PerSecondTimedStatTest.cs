using Highbyte.DotNet6502.Instrumentation.Stats;

namespace Highbyte.DotNet6502.Tests.Instrumentation
{
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
            stat.Update();
            int sleepMs = 16;   // 16 ms should give arround 60 FPS
            Thread.Sleep(sleepMs);
            stat.Update();

            // Assert
            var perSecond = stat.Value;
            Assert.True(perSecond >= 55);
            Assert.True(perSecond < 65);
        }
    }
}
