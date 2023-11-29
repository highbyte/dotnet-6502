using Highbyte.DotNet6502.Instrumentation;

namespace Highbyte.DotNet6502.Tests.Instrumentation
{
    public class InstrumentationsTest
    {

        [Fact]
        public void Add_WhenCalled_AddsStatToStatsList()
        {
            // Arrange
            var instrumentations = new Instrumentations();

            // Act
            var stat = new TestStat();
            var addedStat = instrumentations.Add("stat1", stat);

            // Assert
            Assert.Single(instrumentations.Stats);
            Assert.Equal(addedStat, stat);
            Assert.Equal(addedStat, instrumentations.Stats.First().Stat);
            Assert.Equal("stat1", instrumentations.Stats.First().Name);
        }

        [Fact]
        public void Add_WhenCalled_AddsStatToStatsList2()
        {
            // Arrange
            var instrumentations = new Instrumentations();

            // Act
            var addedStat = instrumentations.Add<TestStat>("stat1");

            // Assert
            Assert.Single(instrumentations.Stats);
            Assert.Equal(addedStat, instrumentations.Stats.First().Stat);
            Assert.Equal("stat1", instrumentations.Stats.First().Name);
        }

        [Fact]
        public void Remove_WhenCalled_RemovesStatFromStatsList()
        {
            // Arrange
            var instrumentations = new Instrumentations();
            instrumentations.Add("stat1", new TestStat());
            instrumentations.Add("stat2", new TestStat());

            // Act
            instrumentations.Remove("stat1");

            // Assert
            Assert.Single(instrumentations.Stats);
        }

        [Fact]
        public void Clear_WhenCalled_ClearsStatsList()
        {
            // Arrange
            var instrumentations = new Instrumentations();
            instrumentations.Add("stat1", new TestStat());
            instrumentations.Add("stat2", new TestStat());

            // Act
            instrumentations.Clear();

            // Assert
            Assert.Empty(instrumentations.Stats);
        }
    }
}
