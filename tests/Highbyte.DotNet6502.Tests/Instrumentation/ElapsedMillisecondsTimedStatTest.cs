using Highbyte.DotNet6502.Instrumentation.Stats;

namespace Highbyte.DotNet6502.Tests.Instrumentation
{
    public class ElapsedMillisecondsTimedStatTest
    {
        [Fact]
        public void Measure_WhenUsed_Returns_ExecutionTime_In_GetStatMilliseconds()
        {
            // Arrange
            var stat = new ElapsedMillisecondsTimedStat(samples: 1);

            int sleepMs = 2;
            using (stat.Measure())
            {
                Thread.Sleep(sleepMs);
            }

            // Act
            var elapsedMs = stat.GetStatMilliseconds();

            // Assert
            Assert.True(elapsedMs >= sleepMs);
            Assert.True(elapsedMs < sleepMs + 1);
        }

        [Fact]
        public void Measure_WhenUsed_With_Cont_Returns_ExecutionTime_In_GetStatMilliseconds()
        {
            // Arrange
            var stat = new ElapsedMillisecondsTimedStat(samples: 1);

            int sleepMs = 2;
            using (stat.Measure())
            {
                Thread.Sleep(sleepMs);
            }
            int sleepNextMs = 3;
            using (stat.Measure(cont: true))
            {
                Thread.Sleep(sleepNextMs);
            }

            // Act
            var elapsedMs = stat.GetStatMilliseconds();

            // Assert
            Assert.True(elapsedMs >= (sleepMs + sleepNextMs));

#if !DEBUG // In debug mode, the elapsed time may not accurate enough to make this test pass (if breakpoint is hit during sleep) 
            Assert.True(elapsedMs < (sleepMs + sleepNextMs) + 1);
#endif
        }
    }
}
