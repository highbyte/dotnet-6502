using Highbyte.DotNet6502.Instrumentation.Stats;

namespace Highbyte.DotNet6502.Tests.Instrumentation
{
    public class ElapsedMillisecondsTimedStatTest
    {
        [Fact]
        public void Returns_ExecutionTime_In_GetStatMilliseconds()
        {
            // Arrange
            var stat = new ElapsedMillisecondsTimedStat(samples: 1);

            int sleepMs = 2;
            stat.Start();
            Thread.Sleep(sleepMs);
            stat.Stop();

            // Act
            var elapsedMs = stat.GetStatMilliseconds();

            // Assert
            Assert.True(elapsedMs >= sleepMs);
#if !DEBUG
            // In debug mode, the elapsed time may not accurate enough to make this test pass (if breakpoint is hit during sleep)
            Assert.True(elapsedMs < sleepMs + 10);
#endif
        }

        [Fact]
        public void When_Used_With_Cont_Returns_ExecutionTime_In_GetStatMilliseconds()
        {
            // Arrange
            var stat = new ElapsedMillisecondsTimedStat(samples: 1);

            int sleepMs = 2;
            stat.Start(cont: true);
            Thread.Sleep(sleepMs);
            stat.Stop(cont: true);

            int sleepNextMs = 3;
            stat.Start(cont: true);
            Thread.Sleep(sleepNextMs);
            stat.Stop(cont: true);
            stat.Stop();

            // Act
            var elapsedMs = stat.GetStatMilliseconds();

            // Assert
            Assert.True(elapsedMs >= (sleepMs + sleepNextMs));

#if !DEBUG
            // In debug mode, the elapsed time may not accurate enough to make this test pass (if breakpoint is hit during sleep) 
            Assert.True(elapsedMs < (sleepMs + sleepNextMs) + 10);
#endif
        }

        [Fact]
        public void GetDescription_WhenUsed_Returns_Null_When_No_Data_Yet()
        {
            // Arrange
            var stat = new ElapsedMillisecondsTimedStat(samples: 1);

            // Act
            // Assert
            Assert.Equal("null", stat.GetDescription());
        }

        [Fact]
        public void GetDescription_WhenUsed_Returns_Special_String_When_Duration_Is_Less_Than_OneHundreds_Of_A_Millisecond()
        {
            // Arrange
            var stat = new ElapsedMillisecondsTimedStat(samples: 1);

            // Act
            stat.SetFakeMSValue(0.0099);

            // Assert
            Assert.Equal("< 0.01ms", stat.GetDescription());
        }

        [Fact]
        public void GetDescription_WhenUsed_Returns_String_With_Milliseconds()
        {
            // Arrange
            var stat = new ElapsedMillisecondsTimedStat(samples: 1);

            // Act
            int sleepMs = 2;
            stat.Start();
            Thread.Sleep(sleepMs);
            stat.Stop();
            // Assert
            var ms = stat.GetStatMilliseconds();
            Assert.NotNull(ms);
            Assert.Equal($"{Math.Round(ms.Value, 2).ToString("0.00")}ms", stat.GetDescription());
        }
    }
}
