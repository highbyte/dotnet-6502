using Highbyte.DotNet6502.Systems.Commodore64.TimerAndPeripheral.DiskDrive;
using Highbyte.DotNet6502.Systems.Commodore64.TimerAndPeripheral.IEC;
using Microsoft.Extensions.Logging.Abstractions;

namespace Highbyte.DotNet6502.Systems.Tests.Commodore64.TimerAndPeripheral;
public class IECBusTest
{
    [Fact]
    public void Can_Add_Device_To_Bus()
    {
        // Arrange
        var iecHost = new IECHost();
        var iecBus = new IECBus(iecHost);
        var device = new DiskDrive1541(new NullLoggerFactory());

        // Act
        iecBus.Attach(device);

        // Assert
        Assert.Contains(device, iecBus.Devices);
    }

    [Fact]
    public void If_No_Devices_On_Bus_The_Bus_Line_State_Are_Directly_Related_To_Host_Line_State()
    {
        // Arrange
        var iecHost = new IECHost();
        var iecBus = new IECBus(iecHost);

        // Act
        iecHost.SetLines(
            setATNLine: DeviceLineState.NotHolding,
            setCLKLine: DeviceLineState.Holding,
            setDATALine: DeviceLineState.NotHolding);

        // Assert
        Assert.True(iecBus.ATNLineState == BusLineState.Released);
        Assert.True(iecBus.CLKLineState == BusLineState.Low);
        Assert.True(iecBus.DATALineState == BusLineState.Released);
    }

    [Fact]
    public void If_Device_Is_On_Bus_A_Bus_Line_Low_State_Is_Set_If_Only_Host_Has_Line_Holding()
    {
        // Arrange
        var iecHost = new IECHost();
        var iecBus = new IECBus(iecHost);
        var device = new DiskDrive1541(new NullLoggerFactory());
        iecBus.Attach(device);

        // Act
        iecHost.SetLines(setCLKLine: DeviceLineState.Holding);
        device.SetLines(setCLKLine: DeviceLineState.NotHolding);

        // Assert
        Assert.True(iecBus.CLKLineState == BusLineState.Low);
    }

    [Fact]
    public void If_Device_Is_On_Bus_A_Bus_Line_Low_State_Is_Set_If_Only_Device_Has_Line_Holding()
    {
        // Arrange
        var iecHost = new IECHost();
        var iecBus = new IECBus(iecHost);
        var device = new DiskDrive1541(new NullLoggerFactory());
        iecBus.Attach(device);

        // Act
        iecHost.SetLines(setCLKLine: DeviceLineState.NotHolding);
        device.SetLines(setCLKLine: DeviceLineState.Holding);

        // Assert
        Assert.True(iecBus.CLKLineState == BusLineState.Low);
    }

    [Fact]
    public void If_Device_Is_On_Bus_A_Bus_Line_Released_State_Is_Set_If_Both_Host_Or_Device_Has_Line_NotHolding()
    {
        // Arrange
        var iecHost = new IECHost();
        var iecBus = new IECBus(iecHost);
        var device = new DiskDrive1541(new NullLoggerFactory());
        iecBus.Attach(device);

        // Act
        iecHost.SetLines(setCLKLine: DeviceLineState.NotHolding);
        device.SetLines(setCLKLine: DeviceLineState.NotHolding);

        // Assert
        Assert.True(iecBus.CLKLineState == BusLineState.Released);
    }
}
