namespace Highbyte.DotNet6502.Systems.Commodore64.Video;

public class Vic2IRQ
{
    // ConfiguredIRQRasterLine = null means not configured yet, and raster IRQ should occur when raster line wraps around from it's max (defined by C64/VIC2 model) to 0.
    public ushort? ConfiguredIRQRasterLine { get; set; } = null;

    private readonly Dictionary<IRQSource, bool> _sourceEnableStatus = new();

    public Vic2IRQ()
    {
        foreach (IRQSource source in Enum.GetValues(typeof(IRQSource)))
        {
            _sourceEnableStatus.Add(source, false);
        }
    }

    public bool IsEnabled(IRQSource source)
    {
        return _sourceEnableStatus[source];
    }
    public void Enable(IRQSource source)
    {
        _sourceEnableStatus[source] = true;
    }
    public void Disable(IRQSource source)
    {
        _sourceEnableStatus[source] = false;
    }

    public bool IsTriggered(IRQSource source, CPU cpu)
    {
        return cpu.CPUInterrupts.IsIRQSourceActive(source.ToString());
    }

    public void Trigger(IRQSource source, CPU cpu)
    {
        cpu.CPUInterrupts.SetIRQSourceActive(source.ToString(), autoAcknowledge: false);
    }
    public void ClearTrigger(IRQSource source, CPU cpu)
    {
        cpu.CPUInterrupts.SetIRQSourceInactive(source.ToString());
    }
}

/// <summary>
/// VIC-II IRQ source flags in IRQ register (0xd019).
/// The enum values represents the bit position of the flag in the register.
/// Ref: https://github.com/mist64/c64ref/blob/master/Source/c64io/c64io_mapc64.txt#L1202
/// </summary>
public enum IRQSource
{
    RasterCompare = 0,
    SpriteToSpriteCollision = 1,
    SpriteToBackgroundCollision = 2,
    LightPenTrigger = 3,
    Any = 7
}
