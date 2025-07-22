using Highbyte.DotNet6502.Systems.Commodore64.TimerAndPeripheral.DiskDrive;
using Highbyte.DotNet6502.Systems.Commodore64.TimerAndPeripheral.DiskDrive.D64;
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
    public void DiskDrive_Can_Attach_D64DiskImage()
    {
        // Arrange
        var diskDrive = new DiskDrive1541(new NullLoggerFactory());

        // Act
        diskDrive.SetD64DiskImage(new D64DiskImage());

        // Assert
        Assert.True(diskDrive.IsDisketteInserted);
    }


    [Fact]
    public void DiskDrive_Can_Remove_D64DiskImage()
    {
        // Arrange
        var diskDrive = new DiskDrive1541(new NullLoggerFactory());
        diskDrive.SetD64DiskImage(new D64DiskImage());

        // Act
        diskDrive.RemoveD64DiskImage();

        // Assert
        Assert.False(diskDrive.IsDisketteInserted);
    }

    [Fact]
    public void DiskDrive_Has_Correct_DeviceNumber()
    {
        // Arrange & Act
        var diskDrive = new DiskDrive1541(new NullLoggerFactory());

        // Assert
        Assert.Equal(8, diskDrive.DeviceNumber);
    }

    [Fact]
    public void DiskDrive_Initially_Has_No_Diskette_Inserted()
    {
        // Arrange & Act
        var diskDrive = new DiskDrive1541(new NullLoggerFactory());

        // Assert
        Assert.False(diskDrive.IsDisketteInserted);
    }

    [Fact]
    public void DiskDrive_Initially_Has_Lines_Not_Holding()
    {
        // Arrange & Act
        var diskDrive = new DiskDrive1541(new NullLoggerFactory());

        // Assert
        Assert.Equal(DeviceLineState.NotHolding, diskDrive.SetCLKLine);
        Assert.Equal(DeviceLineState.NotHolding, diskDrive.SetDATALine);
    }

    [Fact]
    public void DiskDrive_Can_Set_CLK_Line_To_Holding()
    {
        // Arrange
        var diskDrive = new DiskDrive1541(new NullLoggerFactory());

        // Act
        diskDrive.SetLines(setCLKLine: DeviceLineState.Holding);

        // Assert
        Assert.Equal(DeviceLineState.Holding, diskDrive.SetCLKLine);
    }

    [Fact]
    public void DiskDrive_Can_Set_DATA_Line_To_Holding()
    {
        // Arrange
        var diskDrive = new DiskDrive1541(new NullLoggerFactory());

        // Act
        diskDrive.SetLines(setDATALine: DeviceLineState.Holding);

        // Assert
        Assert.Equal(DeviceLineState.Holding, diskDrive.SetDATALine);
    }

    [Fact]
    public void DiskDrive_Can_Set_Both_Lines_Simultaneously()
    {
        // Arrange
        var diskDrive = new DiskDrive1541(new NullLoggerFactory());

        // Act
        diskDrive.SetLines(setCLKLine: DeviceLineState.Holding, setDATALine: DeviceLineState.Holding);

        // Assert
        Assert.Equal(DeviceLineState.Holding, diskDrive.SetCLKLine);
        Assert.Equal(DeviceLineState.Holding, diskDrive.SetDATALine);
    }

    [Fact]
    public void DiskDrive_Can_Release_Lines()
    {
        // Arrange
        var diskDrive = new DiskDrive1541(new NullLoggerFactory());
        diskDrive.SetLines(setCLKLine: DeviceLineState.Holding, setDATALine: DeviceLineState.Holding);

        // Act
        diskDrive.SetLines(setCLKLine: DeviceLineState.NotHolding, setDATALine: DeviceLineState.NotHolding);

        // Assert
        Assert.Equal(DeviceLineState.NotHolding, diskDrive.SetCLKLine);
        Assert.Equal(DeviceLineState.NotHolding, diskDrive.SetDATALine);
    }

    [Fact]
    public void DiskDrive_SetBus_Should_Set_Bus_When_Not_Already_Set()
    {
        // Arrange
        var diskDrive = new DiskDrive1541(new NullLoggerFactory());
        var iecHost = new IECHost();
        var iecBus = new IECBus(iecHost);

        // Act & Assert (should not throw)
        diskDrive.SetBus(iecBus);
    }

    [Fact]
    public void DiskDrive_SetBus_Should_Throw_When_Already_Set()
    {
        // Arrange
        var diskDrive = new DiskDrive1541(new NullLoggerFactory());
        var iecHost1 = new IECHost();
        var iecBus1 = new IECBus(iecHost1);
        var iecHost2 = new IECHost();
        var iecBus2 = new IECBus(iecHost2);
        diskDrive.SetBus(iecBus1);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => diskDrive.SetBus(iecBus2));
    }

    [Fact]
    public void DiskDrive_Can_Replace_D64DiskImage()
    {
        // Arrange
        var diskDrive = new DiskDrive1541(new NullLoggerFactory());
        var diskImage1 = new D64DiskImage { DiskName = "Disk1" };
        var diskImage2 = new D64DiskImage { DiskName = "Disk2" };

        // Act
        diskDrive.SetD64DiskImage(diskImage1);
        Assert.True(diskDrive.IsDisketteInserted);

        diskDrive.SetD64DiskImage(diskImage2);

        // Assert
        Assert.True(diskDrive.IsDisketteInserted);
    }

    [Fact]
    public void DiskDrive_Should_Handle_Multiple_Remove_Calls()
    {
        // Arrange
        var diskDrive = new DiskDrive1541(new NullLoggerFactory());
        diskDrive.SetD64DiskImage(new D64DiskImage());

        // Act
        diskDrive.RemoveD64DiskImage();
        diskDrive.RemoveD64DiskImage(); // Should not throw

        // Assert
        Assert.False(diskDrive.IsDisketteInserted);
    }

    [Fact]
    public void DiskDrive_OnBusChangedState_Should_Not_Throw_When_Bus_Is_Null()
    {
        // Arrange
        var diskDrive = new DiskDrive1541(new NullLoggerFactory());

        // Act & Assert (should not throw)
        diskDrive.OnBusChangedState();
    }

    [Fact]
    public void DiskDrive_Tick_Should_Not_Throw_When_Bus_Is_Null()
    {
        // Arrange
        var diskDrive = new DiskDrive1541(new NullLoggerFactory());

        // Act & Assert (should not throw)
        diskDrive.Tick();
    }

    [Fact]
    public void DiskDrive_SetLines_Does_Not_Change_State_When_Same_Values_Provided()
    {
        // Arrange
        var diskDrive = new DiskDrive1541(new NullLoggerFactory());
        var initialCLK = diskDrive.SetCLKLine;
        var initialDATA = diskDrive.SetDATALine;

        // Act
        diskDrive.SetLines(setCLKLine: initialCLK, setDATALine: initialDATA);

        // Assert
        Assert.Equal(initialCLK, diskDrive.SetCLKLine);
        Assert.Equal(initialDATA, diskDrive.SetDATALine);
    }

    [Fact]
    public void DiskDrive_SetLines_Only_Changes_Specified_Lines()
    {
        // Arrange
        var diskDrive = new DiskDrive1541(new NullLoggerFactory());
        diskDrive.SetLines(setCLKLine: DeviceLineState.NotHolding, setDATALine: DeviceLineState.NotHolding);

        // Act - Only change CLK line
        diskDrive.SetLines(setCLKLine: DeviceLineState.Holding);

        // Assert
        Assert.Equal(DeviceLineState.Holding, diskDrive.SetCLKLine);
        Assert.Equal(DeviceLineState.NotHolding, diskDrive.SetDATALine); // Should remain unchanged
    }

    [Fact]
    public void DiskDrive_Can_Handle_Multiple_OnBusChangedState_Calls()
    {
        // Arrange
        var bus = BuildBusWithDiskDrive(out DiskDrive1541 diskDrive);

        // Act & Assert (should not throw)
        for (int i = 0; i < 10; i++)
        {
            diskDrive.OnBusChangedState();
        }
    }

    [Fact]
    public void DiskDrive_Can_Handle_Multiple_Tick_Calls()
    {
        // Arrange
        var bus = BuildBusWithDiskDrive(out DiskDrive1541 diskDrive);

        // Act & Assert (should not throw)
        for (int i = 0; i < 100; i++)
        {
            diskDrive.Tick();
        }
    }

    [Fact]
    public void DiskDrive_With_DiskImage_Should_Allow_Remove()
    {
        // Arrange
        var diskDrive = new DiskDrive1541(new NullLoggerFactory());
        var diskImage = new D64DiskImage
        {
            DiskName = "TestDisk",
            DiskId = "01"
        };
        diskDrive.SetD64DiskImage(diskImage);

        // Act
        diskDrive.RemoveD64DiskImage();

        // Assert
        Assert.False(diskDrive.IsDisketteInserted);
    }

    [Theory]
    [InlineData(DeviceLineState.Holding)]
    [InlineData(DeviceLineState.NotHolding)]
    public void DiskDrive_SetLines_Should_Handle_Different_CLK_States(DeviceLineState clkState)
    {
        // Arrange
        var diskDrive = new DiskDrive1541(new NullLoggerFactory());

        // Act
        diskDrive.SetLines(setCLKLine: clkState);

        // Assert
        Assert.Equal(clkState, diskDrive.SetCLKLine);
    }

    [Theory]
    [InlineData(DeviceLineState.Holding)]
    [InlineData(DeviceLineState.NotHolding)]
    public void DiskDrive_SetLines_Should_Handle_Different_DATA_States(DeviceLineState dataState)
    {
        // Arrange
        var diskDrive = new DiskDrive1541(new NullLoggerFactory());

        // Act
        diskDrive.SetLines(setDATALine: dataState);

        // Assert
        Assert.Equal(dataState, diskDrive.SetDATALine);
    }

    [Fact]
    public void DiskDrive_Rapid_State_Changes_Should_Be_Handled_Gracefully()
    {
        // Arrange
        var diskDrive = new DiskDrive1541(new NullLoggerFactory());

        // Act - Rapid state changes
        for (int i = 0; i < 100; i++)
        {
            var state = i % 2 == 0 ? DeviceLineState.Holding : DeviceLineState.NotHolding;
            diskDrive.SetLines(setCLKLine: state, setDATALine: state);
        }

        // Assert - Final state should match last operation
        Assert.Equal(DeviceLineState.NotHolding, diskDrive.SetCLKLine);
        Assert.Equal(DeviceLineState.NotHolding, diskDrive.SetDATALine);
    }

    [Fact]
    public void DiskDrive_Sequential_Disk_Operations_Should_Work()
    {
        // Arrange
        var diskDrive = new DiskDrive1541(new NullLoggerFactory());
        var disk1 = new D64DiskImage { DiskName = "Disk1" };
        var disk2 = new D64DiskImage { DiskName = "Disk2" };

        // Act & Assert - Insert first disk
        diskDrive.SetD64DiskImage(disk1);
        Assert.True(diskDrive.IsDisketteInserted);

        // Remove first disk
        diskDrive.RemoveD64DiskImage();
        Assert.False(diskDrive.IsDisketteInserted);

        // Insert second disk
        diskDrive.SetD64DiskImage(disk2);
        Assert.True(diskDrive.IsDisketteInserted);

        // Remove second disk
        diskDrive.RemoveD64DiskImage();
        Assert.False(diskDrive.IsDisketteInserted);
    }

    [Fact]
    public void DiskDrive_Complex_Bus_Scenario_Should_Handle_Gracefully()
    {
        // Arrange
        var bus = BuildBusWithDiskDrive(out DiskDrive1541 diskDrive);
        diskDrive.SetD64DiskImage(new D64DiskImage { DiskName = "TestDisk" });

        // Act - Complex scenario with mixed operations
        bus.Host.SetLines(setATNLine: DeviceLineState.Holding, setCLKLine: DeviceLineState.Holding);
        diskDrive.OnBusChangedState();
        diskDrive.Tick();

        diskDrive.SetLines(setCLKLine: DeviceLineState.NotHolding);
        diskDrive.Tick();

        bus.Host.SetLines(setATNLine: DeviceLineState.NotHolding);
        diskDrive.OnBusChangedState();
        diskDrive.Tick();

        // Assert - Drive should maintain its state
        Assert.True(diskDrive.IsDisketteInserted);
        Assert.Equal(8, diskDrive.DeviceNumber);
    }

    [Fact]
    public void DiskDrive_Bus_Attachment_Should_Be_Idempotent_Per_Bus()
    {
        // Arrange
        var diskDrive = new DiskDrive1541(new NullLoggerFactory());
        var iecHost = new IECHost();
        var iecBus = new IECBus(iecHost);

        // Act
        diskDrive.SetBus(iecBus);

        // Assert - Second attempt to set same bus should throw
        Assert.Throws<InvalidOperationException>(() => diskDrive.SetBus(iecBus));
    }

    [Fact]
    public void DiskDrive_Line_States_Should_Be_Independent()
    {
        // Arrange
        var diskDrive = new DiskDrive1541(new NullLoggerFactory());

        // Act - Set CLK line only
        diskDrive.SetLines(setCLKLine: DeviceLineState.Holding);
        var clkAfterFirst = diskDrive.SetCLKLine;
        var dataAfterFirst = diskDrive.SetDATALine;

        // Set DATA line only
        diskDrive.SetLines(setDATALine: DeviceLineState.Holding);
        var clkAfterSecond = diskDrive.SetCLKLine;
        var dataAfterSecond = diskDrive.SetDATALine;

        // Assert
        Assert.Equal(DeviceLineState.Holding, clkAfterFirst);
        Assert.Equal(DeviceLineState.NotHolding, dataAfterFirst);
        Assert.Equal(DeviceLineState.Holding, clkAfterSecond); // Should remain unchanged
        Assert.Equal(DeviceLineState.Holding, dataAfterSecond);
    }

    [Fact]
    public void DiskDrive_SetLines_With_Null_Parameters_Should_Not_Change_State()
    {
        // Arrange
        var diskDrive = new DiskDrive1541(new NullLoggerFactory());
        diskDrive.SetLines(setCLKLine: DeviceLineState.Holding, setDATALine: DeviceLineState.Holding);
        var initialCLK = diskDrive.SetCLKLine;
        var initialDATA = diskDrive.SetDATALine;

        // Act
        diskDrive.SetLines(); // No parameters

        // Assert
        Assert.Equal(initialCLK, diskDrive.SetCLKLine);
        Assert.Equal(initialDATA, diskDrive.SetDATALine);
    }

    [Fact]
    public void DiskDrive_Stress_Test_Multiple_Disk_Swaps()
    {
        // Arrange
        var diskDrive = new DiskDrive1541(new NullLoggerFactory());
        var disks = new List<D64DiskImage>();
        for (int i = 0; i < 10; i++)
        {
            disks.Add(new D64DiskImage { DiskName = $"Disk{i}", DiskId = $"{i:D2}" });
        }

        // Act & Assert
        foreach (var disk in disks)
        {
            diskDrive.SetD64DiskImage(disk);
            Assert.True(diskDrive.IsDisketteInserted);

            diskDrive.RemoveD64DiskImage();
            Assert.False(diskDrive.IsDisketteInserted);
        }
    }

    [Fact]
    public void DiskDrive_Bus_Operations_Without_Disk_Should_Not_Crash()
    {
        // Arrange
        var bus = BuildBusWithDiskDrive(out DiskDrive1541 diskDrive);
        // Note: No disk is inserted

        // Act & Assert - Should handle bus operations gracefully without disk
        for (int i = 0; i < 10; i++)
        {
            bus.Host.SetLines(
                setATNLine: i % 2 == 0 ? DeviceLineState.Holding : DeviceLineState.NotHolding,
                setCLKLine: i % 3 == 0 ? DeviceLineState.Holding : DeviceLineState.NotHolding
            );

            diskDrive.OnBusChangedState();
            diskDrive.Tick();
        }

        Assert.False(diskDrive.IsDisketteInserted);
    }

    [Fact]
    public void DiskDrive_Should_Handle_Empty_D64DiskImage()
    {
        // Arrange
        var diskDrive = new DiskDrive1541(new NullLoggerFactory());
        var emptyDisk = new D64DiskImage
        {
            DiskName = "",
            DiskId = "",
            Files = new List<D64FileEntry>()
        };

        // Act
        diskDrive.SetD64DiskImage(emptyDisk);

        // Assert
        Assert.True(diskDrive.IsDisketteInserted);
    }

    [Fact]
    public void DiskDrive_Concurrent_Line_Operations_Should_Be_Consistent()
    {
        // Arrange
        var diskDrive = new DiskDrive1541(new NullLoggerFactory());

        // Act - Simulate concurrent-like operations
        diskDrive.SetLines(setCLKLine: DeviceLineState.Holding);
        diskDrive.SetLines(setDATALine: DeviceLineState.Holding);
        diskDrive.SetLines(setCLKLine: DeviceLineState.NotHolding);
        diskDrive.SetLines(setDATALine: DeviceLineState.NotHolding);
        diskDrive.SetLines(setCLKLine: DeviceLineState.Holding, setDATALine: DeviceLineState.Holding);

        // Assert
        Assert.Equal(DeviceLineState.Holding, diskDrive.SetCLKLine);
        Assert.Equal(DeviceLineState.Holding, diskDrive.SetDATALine);
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
