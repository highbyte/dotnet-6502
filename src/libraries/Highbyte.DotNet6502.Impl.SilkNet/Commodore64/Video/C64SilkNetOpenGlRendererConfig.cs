namespace Highbyte.DotNet6502.Impl.SilkNet.Commodore64.Video;

public class C64SilkNetOpenGlRendererConfig : ICloneable
{
    public bool UseFineScrollPerRasterLine { get; set; }
    public C64SilkNetOpenGlRendererConfig()
    {
        UseFineScrollPerRasterLine = false; // Setting to true may work, depending on how code is written. Full screen scroll may not work (actual screen memory is not rendered in sync with raster line).
    }
    public object Clone()
    {
        var clone = (C64SilkNetOpenGlRendererConfig)MemberwiseClone();
        return clone;
    }
}
