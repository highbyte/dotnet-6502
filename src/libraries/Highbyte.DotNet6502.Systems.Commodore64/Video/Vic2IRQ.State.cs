namespace Highbyte.DotNet6502.Systems.Commodore64.Video;

public partial class Vic2IRQ
{
    // Sets the enable/trigger flags directly without touching the CPU interrupt line. The CPU's
    // IRQ source state is restored separately by the cpu-6502 snapshot module, so re-raising it
    // here (as Enable/Trigger would) is unnecessary and would risk double-restoring it.
    internal void RestoreSnapshotState(IRQSource source, bool enabled, bool triggered)
    {
        if (enabled)
            _enabledMask |= Bit(source);
        else
            _enabledMask &= (byte)~Bit(source);
        if (triggered)
            _latchedMask |= Bit(source);
        else
            _latchedMask &= (byte)~Bit(source);
    }
}
