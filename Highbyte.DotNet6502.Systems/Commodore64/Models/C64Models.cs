using Highbyte.DotNet6502.Systems.Commodore64.Video;

namespace Highbyte.DotNet6502.Systems.Commodore64.Models;


public class C64ModelPAL : C64ModelBase
{
    public override string Name => "C64PAL";
    public override float CPUFrequencyHz => 985248.6f;

    public override List<Vic2ModelBase> Vic2Models => new()
    {
        new Vic2ModelPAL()
    };
}

public class C64ModelNTSC : C64ModelBase
{
    public override string Name => "C64NTSC";
    public override float CPUFrequencyHz => 1022727.3f;
    public override List<Vic2ModelBase> Vic2Models => new()
    {
        new Vic2ModelNTSC(),
        new Vic2ModelNTSC_old(),
    };
}

public abstract class C64ModelBase
{
    public abstract string Name { get; }
    public abstract float CPUFrequencyHz { get; }
    public abstract List<Vic2ModelBase> Vic2Models { get; }
}