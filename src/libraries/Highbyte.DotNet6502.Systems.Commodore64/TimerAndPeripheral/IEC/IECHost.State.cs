namespace Highbyte.DotNet6502.Systems.Commodore64.TimerAndPeripheral.IEC;

public partial class IECHost
{
    // Snapshot restore sets the pins without sending a new serial-bus edge to the drive.
    internal void RestoreLines(DeviceLineState atn, DeviceLineState clk, DeviceLineState data)
    {
        _setATNLine = atn;
        _setCLKLine = clk;
        _setDATALine = data;
    }
}
