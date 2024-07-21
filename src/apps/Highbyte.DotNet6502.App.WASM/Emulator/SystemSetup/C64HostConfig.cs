using Highbyte.DotNet6502.Impl.AspNet.Commodore64.Input;
using Highbyte.DotNet6502.Systems;

namespace Highbyte.DotNet6502.App.WASM.Emulator.SystemSetup;

public enum C64HostRenderer
{
    SkiaSharp,
    SkiaSharp2,  // Experimental render directly to pixel buffer backed by a SKBitmap + Skia shader (SKSL)
    SkiaSharp2b, // Experimental render after each instruction directly to pixel buffer backed by a SKBitmap + Skia shader (SKSL)
}
public class C64HostConfig : IHostSystemConfig, ICloneable
{

    public C64HostRenderer Renderer { get; set; } = C64HostRenderer.SkiaSharp;

    public C64AspNetInputConfig InputConfig { get; set; } = new C64AspNetInputConfig();

    public object Clone()
    {
        var clone = (C64HostConfig)MemberwiseClone();
        clone.InputConfig = (C64AspNetInputConfig)InputConfig.Clone();
        return clone;
    }
}
