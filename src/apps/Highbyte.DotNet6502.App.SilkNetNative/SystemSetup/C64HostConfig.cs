using Highbyte.DotNet6502.Impl.SilkNet.Commodore64;
using Highbyte.DotNet6502.Impl.SilkNet.Commodore64.Video;
using Highbyte.DotNet6502.Systems;

namespace Highbyte.DotNet6502.App.SilkNetNative.SystemSetup
{
    public enum C64HostRenderer
    {
        SkiaSharp,
        SkiaSharp2, // Experimental render directly to pixel buffer backed by a SKBitmap
        SkiaSharp3, // Experimental render directly to pixel buffer backed by a SKBitmap + Skia shader (SKSL)
        SilkNetOpenGl
    }

    public class C64HostConfig : IHostSystemConfig, ICloneable
    {
        public C64HostRenderer Renderer { get; set; } = C64HostRenderer.SkiaSharp;
        public C64SilkNetOpenGlRendererConfig SilkNetOpenGlRendererConfig { get; set; } = new C64SilkNetOpenGlRendererConfig();
        public C64SilkNetConfig InputConfig { get; set; } = new C64SilkNetConfig();

        public object Clone()
        {
            var clone = (C64HostConfig)this.MemberwiseClone();
            clone.InputConfig = (C64SilkNetConfig)InputConfig.Clone();
            clone.SilkNetOpenGlRendererConfig = (C64SilkNetOpenGlRendererConfig)SilkNetOpenGlRendererConfig.Clone();
            return clone;
        }
    }
}
