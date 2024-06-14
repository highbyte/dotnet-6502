
using Highbyte.DotNet6502.Impl.AspNet.Commodore64;
using Highbyte.DotNet6502.Systems;

namespace Highbyte.DotNet6502.App.WASM.Skia
{
    public enum C64HostRenderer
    {
        SkiaSharp,
        SkiaSharp2, // Experimental render directly to pixel buffer backed by a SKBitmap + Skia shader (SKSL)
    }
    public class C64HostConfig : IHostSystemConfig, ICloneable
    {

        public C64HostRenderer Renderer { get; set; } = C64HostRenderer.SkiaSharp;

        public C64AspNetConfig InputConfig { get; set; } = new C64AspNetConfig();

        public object Clone()
        {
            var clone = (C64HostConfig)MemberwiseClone();
            clone.InputConfig = (C64AspNetConfig)InputConfig.Clone();
            return clone;
        }
    }
}
