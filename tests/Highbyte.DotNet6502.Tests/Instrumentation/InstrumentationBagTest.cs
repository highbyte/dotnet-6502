using Highbyte.DotNet6502.Instrumentation;

namespace Highbyte.DotNet6502.Tests.Instrumentation
{
    /// <summary>
    /// InstrumentationBag is a static class that uses Instrumentations class under the hood.
    /// </summary>
    public class InstrumentationBagTest
    {

        [Fact]
        public void Add_WhenCalled_AddsStatToStatsList()
        {
            // Arrange
            InstrumentationBag.Clear();

            // Act
            var stat = new TestStat();
            var addedStat = InstrumentationBag.Add("stat1", stat);

            // Assert
            Assert.Single(InstrumentationBag.Stats);
            Assert.Equal(addedStat, stat);
            Assert.Equal(addedStat, InstrumentationBag.Stats.First().Stat);
            Assert.Equal("stat1", InstrumentationBag.Stats.First().Name);
        }

        [Fact]
        public void Add_WhenCalled_AddsStatToStatsList2()
        {
            // Arrange
            InstrumentationBag.Clear();

            // Act
            var addedStat = InstrumentationBag.Add<TestStat>("stat1");

            // Assert
            Assert.Single(InstrumentationBag.Stats);
            Assert.Equal(addedStat, InstrumentationBag.Stats.First().Stat);
            Assert.Equal("stat1", InstrumentationBag.Stats.First().Name);
        }

        [Fact]
        public void Remove_WhenCalled_RemovesStatFromStatsList()
        {
            // Arrange
            InstrumentationBag.Clear();
            InstrumentationBag.Add("stat1", new TestStat());
            InstrumentationBag.Add("stat2", new TestStat());

            // Act
            InstrumentationBag.Remove("stat1");

            // Assert
            Assert.Single(InstrumentationBag.Stats);
        }

        [Fact]
        public void Clear_WhenCalled_ClearsStatsList()
        {
            // Arrange
            InstrumentationBag.Clear();

            InstrumentationBag.Add("stat1", new TestStat());
            InstrumentationBag.Add("stat2", new TestStat());

            // Act
            InstrumentationBag.Clear();

            // Assert
            Assert.Empty(InstrumentationBag.Stats);
        }
    }
}
