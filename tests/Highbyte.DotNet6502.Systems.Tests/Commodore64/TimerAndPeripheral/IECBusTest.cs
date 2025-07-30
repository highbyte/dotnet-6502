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

    [Fact]
    public void Cannot_Attach_Device_With_Same_DeviceNumber_As_Existing_Device()
    {
        // Arrange
        var iecHost = new IECHost();
        var iecBus = new IECBus(iecHost);
        var device1 = new DiskDrive1541(new NullLoggerFactory());
        var device2 = new DiskDrive1541(new NullLoggerFactory());
        
        // Both devices have default device number 8
        iecBus.Attach(device1);

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => iecBus.Attach(device2));
        Assert.Equal("A device with DeviceNumber 8 is already attached.", exception.Message);
    }

    [Fact]
    public void Can_Attach_Multiple_Devices_With_Different_DeviceNumbers()
    {
        // Arrange
        var iecHost = new IECHost();
        var iecBus = new IECBus(iecHost);
        var device1 = new DiskDrive1541(new NullLoggerFactory());
        var device2 = new DiskDrive1541(new NullLoggerFactory());
        var device3 = new DiskDrive1541(new NullLoggerFactory());
        
        device1.SetDeviceNumber(8);
        device2.SetDeviceNumber(9);
        device3.SetDeviceNumber(10);

        // Act
        iecBus.Attach(device1);
        iecBus.Attach(device2);
        iecBus.Attach(device3);

        // Assert
        Assert.Equal(3, iecBus.Devices.Count);
        Assert.Contains(device1, iecBus.Devices);
        Assert.Contains(device2, iecBus.Devices);
        Assert.Contains(device3, iecBus.Devices);
    }

    [Fact]
    public void Can_Attach_Device_After_Removing_Device_With_Same_Number()
    {
        // Arrange
        var iecHost = new IECHost();
        var iecBus = new IECBus(iecHost);
        var device1 = new DiskDrive1541(new NullLoggerFactory());
        var device2 = new DiskDrive1541(new NullLoggerFactory());
        
        // Both devices have default device number 8
        iecBus.Attach(device1);
        iecBus.RemoveDeviceByNumber(8);

        // Act
        iecBus.Attach(device2);

        // Assert
        Assert.Single(iecBus.Devices);
        Assert.Contains(device2, iecBus.Devices);
        Assert.DoesNotContain(device1, iecBus.Devices);
    }

    [Fact]
    public void IsDeviceAttached_Returns_True_When_Device_With_Number_Is_Attached()
    {
        // Arrange
        var iecHost = new IECHost();
        var iecBus = new IECBus(iecHost);
        var device = new DiskDrive1541(new NullLoggerFactory());
        device.SetDeviceNumber(9);
        iecBus.Attach(device);

        // Act & Assert
        Assert.True(iecBus.IsDeviceAttached(9));
        Assert.False(iecBus.IsDeviceAttached(8));
        Assert.False(iecBus.IsDeviceAttached(10));
    }

    [Fact]
    public void GetDeviceByNumber_Returns_Correct_Device()
    {
        // Arrange
        var iecHost = new IECHost();
        var iecBus = new IECBus(iecHost);
        var device1 = new DiskDrive1541(new NullLoggerFactory());
        var device2 = new DiskDrive1541(new NullLoggerFactory());
        
        device1.SetDeviceNumber(8);
        device2.SetDeviceNumber(9);
        iecBus.Attach(device1);
        iecBus.Attach(device2);

        // Act & Assert
        Assert.Equal(device1, iecBus.GetDeviceByNumber(8));
        Assert.Equal(device2, iecBus.GetDeviceByNumber(9));
        Assert.Null(iecBus.GetDeviceByNumber(10));
    }

    [Theory]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(10)]
    [InlineData(11)]
    public void Can_Attach_Device_With_All_Valid_DeviceNumbers(int deviceNumber)
    {
        // Arrange
        var iecHost = new IECHost();
        var iecBus = new IECBus(iecHost);
        var device = new DiskDrive1541(new NullLoggerFactory());
        device.SetDeviceNumber(deviceNumber);

        // Act
        iecBus.Attach(device);

        // Assert
        Assert.Single(iecBus.Devices);
        Assert.True(iecBus.IsDeviceAttached(deviceNumber));
        Assert.Equal(device, iecBus.GetDeviceByNumber(deviceNumber));
    }

    [Fact]
    public void Validation_Prevents_Bus_Conflicts_In_Complex_Scenario()
    {
        // Arrange
        var iecHost = new IECHost();
        var iecBus = new IECBus(iecHost);
        var devices = new List<DiskDrive1541>();
        
        // Create multiple devices with different numbers
        for (int i = 8; i <= 11; i++)
        {
            var device = new DiskDrive1541(new NullLoggerFactory());
            device.SetDeviceNumber(i);
            devices.Add(device);
            iecBus.Attach(device);
        }

        // Act & Assert - Try to attach devices with conflicting numbers
        for (int i = 8; i <= 11; i++)
        {
            var conflictingDevice = new DiskDrive1541(new NullLoggerFactory());
            conflictingDevice.SetDeviceNumber(i);
            
            var exception = Assert.Throws<InvalidOperationException>(() => iecBus.Attach(conflictingDevice));
            Assert.Equal($"A device with DeviceNumber {i} is already attached.", exception.Message);
        }

        // Verify all original devices are still attached
        Assert.Equal(4, iecBus.Devices.Count);
        for (int i = 8; i <= 11; i++)
        {
            Assert.True(iecBus.IsDeviceAttached(i));
        }
    }
}
