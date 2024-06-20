using Highbyte.DotNet6502.Systems;

namespace Highbyte.DotNet6502.Instrumentation.Stats;

public class ElapsedMillisecondsTimedStatSystem : ElapsedMillisecondsTimedStat
{
    public ISystem System => _system;
    private readonly ISystem _system;

    public ElapsedMillisecondsTimedStatSystem(ISystem system)
        : this(system, 10)
    {
    }

    public ElapsedMillisecondsTimedStatSystem(ISystem system, int samples)
        : base(samples)
    {
        _system = system;
    }

    public override void Start(bool cont = false)
    {
        if (_system.InstrumentationEnabled)
            base.Start(cont);
    }

    public override void Stop(bool cont = false)
    {
        if (_system.InstrumentationEnabled)
            base.Stop(cont);
    }
}
