using Highbyte.DotNet6502.Systems;

namespace Highbyte.DotNet6502.App.SilkNetNative.SystemSetup;

public class GenericComputerHostConfig : IHostSystemConfig, ICloneable
{

    public object Clone()
    {
        var clone = (GenericComputerHostConfig)this.MemberwiseClone();
        return clone;
    }
}
