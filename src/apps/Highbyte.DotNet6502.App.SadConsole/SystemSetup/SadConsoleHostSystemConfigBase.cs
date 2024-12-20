using Highbyte.DotNet6502.Systems;
using static SadConsole.IFont;

namespace Highbyte.DotNet6502.App.SadConsole.SystemSetup;
public abstract class SadConsoleHostSystemConfigBase : IHostSystemConfig, ICloneable
{
    public void Validate()
    {
        if (!IsValid(out List<string> validationErrors))
            throw new DotNet6502Exception($"Config errors: {string.Join(',', validationErrors)}");
    }

    public abstract bool IsValid(out List<string> validationErrors);

    protected ISystemConfig SystemConfig;

    ISystemConfig IHostSystemConfig.SystemConfig => SystemConfig;


    public abstract bool AudioSupported { get; }

    /// <summary>
    /// Optional. If not specified, default SadConsole font is used.
    /// To use a specific  SadConsole Font, include it in your program output directory.
    /// Example: Fonts/C64.font
    /// </summary>
    /// <value></value>
    public string? Font { get; set; }

    /// <summary>
    /// Default font size for emulator console only. UI is not affected.
    /// Sizes.One is default.
    /// </summary>
    /// <value></value>
    public Sizes DefaultFontSize { get; set; }

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
