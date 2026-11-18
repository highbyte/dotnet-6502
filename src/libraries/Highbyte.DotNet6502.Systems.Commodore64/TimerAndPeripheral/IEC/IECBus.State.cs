using Highbyte.DotNet6502.Systems.Snapshots;

namespace Highbyte.DotNet6502.Systems.Commodore64.TimerAndPeripheral.IEC;

public partial class IECBus
{
    internal void CaptureHostState(SnapshotModuleWriter writer)
    {
        writer.WriteBool(Host.SetATNLine == DeviceLineState.Holding);
        writer.WriteBool(Host.SetCLKLine == DeviceLineState.Holding);
        writer.WriteBool(Host.SetDATALine == DeviceLineState.Holding);
        writer.WriteBool(ATNLineStatePrevious == BusLineState.Low);
        writer.WriteBool(CLKLineStatePrevious == BusLineState.Low);
        writer.WriteBool(DATALineStatePrevious == BusLineState.Low);
    }

    internal void RestoreHostState(SnapshotModuleReader reader)
    {
        static DeviceLineState Pin(bool low) => low ? DeviceLineState.Holding : DeviceLineState.NotHolding;
        static BusLineState Line(bool low) => low ? BusLineState.Low : BusLineState.Released;
        Host.RestoreLines(Pin(reader.ReadBool()), Pin(reader.ReadBool()), Pin(reader.ReadBool()));
        ATNLineStatePrevious = Line(reader.ReadBool());
        CLKLineStatePrevious = Line(reader.ReadBool());
        DATALineStatePrevious = Line(reader.ReadBool());
    }
}
