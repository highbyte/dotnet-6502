
using Highbyte.DotNet6502.Impl.AspNet.Commodore64;
using Highbyte.DotNet6502.Systems;

namespace Highbyte.DotNet6502.App.SkiaWASM.Skia
{
    public class C64HostConfig : IHostSystemConfig, ICloneable
    {
        public C64AspNetConfig InputConfig { get; set; } = new C64AspNetConfig();

        public object Clone()
        {
            var clone = (C64HostConfig)MemberwiseClone();
            clone.InputConfig = (C64AspNetConfig)InputConfig.Clone();
            return clone;
        }
    }
}
