using Highbyte.DotNet6502.Impl.SilkNet.Commodore64;

namespace Highbyte.DotNet6502.App.SkiaNative.SystemSetup
{
    public class C64HostConfig : IHostSystemConfig, ICloneable
    {
        public C64SilkNetConfig InputConfig { get; set; } = new C64SilkNetConfig();

        public object Clone()
        {
            var clone = (C64HostConfig)this.MemberwiseClone();
            clone.InputConfig = (C64SilkNetConfig)InputConfig.Clone();
            return clone;
        }
    }
}
