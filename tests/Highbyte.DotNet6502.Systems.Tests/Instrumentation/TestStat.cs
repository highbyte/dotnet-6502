using Highbyte.DotNet6502.Systems.Instrumentation.Stats;

namespace Highbyte.DotNet6502.Systems.Tests.Instrumentation;

internal class TestStat : IStat
{
    public string GetDescription() => "TestStat";
    public bool ShouldShow() => true;
}

internal class TestAveragedStat : AveragedStat
{
    public TestAveragedStat(int sampleCount) : base(sampleCount)
    {
    }

    public void UpdateStat(double value)
    {
        SetValue(value);
    }

    public override string GetDescription() => "TestAveragedStat";
}
