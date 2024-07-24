using Highbyte.DotNet6502.Systems;

namespace Highbyte.DotNet6502.App.SadConsole.SystemSetup;
public abstract class SadConsoleHostSystemConfigBase : IHostSystemConfig, ICloneable
{
    /// <summary>
    /// Optional. If not specified, default SadConsole font is used.
    /// To use a specific  SadConsole Font, include it in your program output directory.
    /// Example: Fonts/C64.font
    /// </summary>
    /// <value></value>
    public string? Font { get; set; }

    public SadConsoleHostSystemConfigBase()
    {
        Font = null;
    }

    public object Clone()
    {
        var clone = (SadConsoleHostSystemConfigBase)MemberwiseClone();
        return clone;
    }
}
