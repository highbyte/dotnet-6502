namespace Highbyte.DotNet6502.Impl.SilkNet.Commodore64.Video;

public class C64SilkNetOpenGlRendererConfig : ICloneable
{
    public bool UseFineScrollPerRasterLine { get; set; }
    public C64SilkNetOpenGlRendererConfig()
    {
        UseFineScrollPerRasterLine = false;
    }
    public object Clone()
    {
        var clone = (C64SilkNetOpenGlRendererConfig)MemberwiseClone();
        return clone;
    }
}
