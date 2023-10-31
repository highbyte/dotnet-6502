using Highbyte.DotNet6502.Impl.SilkNet.Commodore64;
using Highbyte.DotNet6502.Systems;

namespace Highbyte.DotNet6502.App.SilkNetNative.SystemSetup
{
    public enum C64HostRenderer
    {
        SkiaSharp,
        SilkNetOpenGl
    }

    public class C64HostConfig : IHostSystemConfig, ICloneable
    {
        public C64HostRenderer Renderer { get; set; } = C64HostRenderer.SkiaSharp;
        public C64SilkNetConfig InputConfig { get; set; } = new C64SilkNetConfig();

        public object Clone()
        {
            var clone = (C64HostConfig)this.MemberwiseClone();
            clone.InputConfig = (C64SilkNetConfig)InputConfig.Clone();
            return clone;
        }
    }
}
