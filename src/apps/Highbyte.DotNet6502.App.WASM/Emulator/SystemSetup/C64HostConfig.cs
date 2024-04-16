using Highbyte.DotNet6502.Impl.AspNet.Commodore64;
using Highbyte.DotNet6502.Impl.SilkNet.Commodore64.Video;
using Highbyte.DotNet6502.Systems;

namespace Highbyte.DotNet6502.App.WASM.Emulator.SystemSetup
{

    public class C64HostConfig : IHostSystemConfig, ICloneable
    {
        public C64AspNetConfig InputConfig { get; set; } = new C64AspNetConfig();

        public C64SilkNetOpenGlRendererConfig SilkNetOpenGlRendererConfig { get; set; } = new C64SilkNetOpenGlRendererConfig();

        public object Clone()
        {
            var clone = (C64HostConfig)MemberwiseClone();
            clone.InputConfig = (C64AspNetConfig)InputConfig.Clone();
            return clone;
        }
    }
}
