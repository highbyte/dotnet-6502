namespace Highbyte.DotNet6502.Systems.Commodore64.TimerAndPeripheral.IEC;
public interface IIECDevice
{
    int DeviceNumber { get; } // Device number 1-15
    DeviceLineState SetCLKLine { get; } // Clock line state
    DeviceLineState SetDATALine { get; } // Data line state
    void SetBus(IECBus iECBus);
    void OnBusChangedState();
    void Tick(); // Called regularly (per CPU tick or per IEC step)
}
