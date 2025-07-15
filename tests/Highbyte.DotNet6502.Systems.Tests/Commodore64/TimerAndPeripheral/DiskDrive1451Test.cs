using Highbyte.DotNet6502.Systems.Commodore64.TimerAndPeripheral.DiskDrive;
using Highbyte.DotNet6502.Systems.Commodore64.TimerAndPeripheral.IEC;
using Microsoft.Extensions.Logging.Abstractions;

namespace Highbyte.DotNet6502.Systems.Tests.Commodore64.TimerAndPeripheral;

public class DiskDrive1541Test
{
    [Fact]
    public void Holding_ATN_Line_On_Host_Will_Make_DiskDrive_Hold_Its_DATA_Line()
    {
        // Arrange
        var bus = BuildBusWithDiskDrive(out DiskDrive1541 diskDrive);

        // Act
        bus.Host.SetLines(setATNLine: DeviceLineState.Holding);

        // Assert
        Assert.True(diskDrive.SetDATALine == DeviceLineState.Holding);
    }


    [Fact]
    public void DiskDrive_Can_Remove_D64DiskImage()
    {
        // Arrange
        var diskDrive = new DiskDrive1541(new NullLoggerFactory());

        // Act
        diskDrive.RemoveD64DiskImage();

        // Assert
        Assert.False(diskDrive.IsDisketteInserted);
    }

    private IECBus BuildBusWithDiskDrive(out DiskDrive1541 diskDrive)
    {
        var iecHost = new IECHost();
        var iecBus = new IECBus(iecHost);
        diskDrive = new DiskDrive1541(new NullLoggerFactory());
        iecBus.Attach(diskDrive);
        return iecBus;
    }
}
