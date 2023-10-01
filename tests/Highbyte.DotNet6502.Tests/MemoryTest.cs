using System.Net;
using System.Reflection.Metadata.Ecma335;
using static Highbyte.DotNet6502.Memory;

namespace Highbyte.DotNet6502.Tests;

public class MemoryTest
{
    [Fact]
    public void Initializing_Memory_With_Defaults_Should_Create_64K_Memory()
    {
        // Arrange
        var mem = new Memory();
        // Act / Assert
        Assert.Equal(64 * 1024, mem.Size);
    }

    [Fact]
    public void A_New_Memory_With_Defaults_Should_Have_A_Mapped_RAM()
    {
        // Arrange
        var mem = new Memory();

        // Act
        mem.Write(0x0010, 0x21);
        mem.Write(0x0010, 0x42);

        // Assert
        Assert.Equal(0x42, mem.Read(0x0010));
    }

    [Fact]
    public void Initializing_RAM_Should_Allow_Read_And_Write()
    {
        // Arrange
        var mem = new Memory(mapToDefaultRAM: false);

        ushort baseAddress = 0x0000;
        var data = new byte[4096];
        mem.MapRAM(baseAddress, data);

        // Act
        ushort address = (ushort)(baseAddress+0x10);
        byte expectedMemValue = 0x42;
        mem.Write(address, 0x00);   // Make sure memory doesn't
        mem.Write(address, expectedMemValue);

        // Assert
        var actualMemValue = mem.Read(address);
        Assert.Equal(expectedMemValue, actualMemValue);
    }

    [Fact]
    public void Initializing_ROM_After_RAM_On_Same_Location_Should_Read_From_ROM_But_Write_To_Ram()
    {
        // Arrange
        var mem = new Memory(mapToDefaultRAM: false);
        ushort baseAddress = 0x0000;
        ushort address = baseAddress;

        // First we map the RAM
        var ramData = new byte[4096];
        byte ramValue = 0x21;
        ramData[address] = ramValue;
        mem.MapRAM(baseAddress, ramData);

        // Then map the ROM on the same location
        var romData = new byte[4096];
        byte romValue = 0x42;
        romData[address] = romValue;
        mem.MapROM(baseAddress, romData);

        // Act
        mem.Write(address, 0x00);   // Should updates the RAM behind the ROM

        // Assert
        Assert.Equal(0x00, ramData[baseAddress]); // The actual RAM memory should have been updated
        var actualMemValue = mem.Read(address); // But when reading the same address via the memory system, we should get the ROM value, not the RAM value.
        Assert.Equal(romValue, actualMemValue);
    }

    [Fact]
    public void Indexing_Works_On_Separate_Memory_Mapped_To_Different_BaseAddresses()
    {
        // Arrange
        var mem = new Memory(mapToDefaultRAM: false);

        // First RAM memory mapped to 0x0000-0x0fff
        var ramData1 = new byte[4096];
        ushort baseAddress1 = 0x0000;
        mem.MapRAM(baseAddress1, ramData1);

        // Second RAM memory mapped to 0x1000-0x1fff
        var ramData2 = new byte[4096];
        ushort baseAddress2 = 0x1000;
        mem.MapRAM(baseAddress2, ramData2);

        // Act
        mem.Write(baseAddress1, 0x01);   // Should updates the first RAM at it's own location 0x0000
        mem.Write(baseAddress2, 0x02);   // Should updates the second RAM at it's own location 0x0000

        // Assert
        Assert.Equal(0x01, ramData1[0]);
        Assert.Equal(0x01, mem.Read(baseAddress1));

        Assert.Equal(0x02, ramData2[0]);
        Assert.Equal(0x02, mem.Read(baseAddress2));
    }

    [Fact]
    public void Can_Map_Individual_Memory_Location_To_A_Delegate_For_Reading()
    {
        // Arrange
        var mem = new Memory(mapToDefaultRAM: false);
        var ramData = new byte[4096];
        ushort baseAddress = 0x0000;
        mem.MapRAM(baseAddress, ramData);

        byte storedValue = 0x41;
        LoadByte reader = _ => { return storedValue;};
        mem.MapReader(0x0010, reader);

        // Act/Assert
        byte actualValue = mem.Read(0x0010);
        Assert.Equal(storedValue, actualValue);
    }

    [Fact]
    public void Can_Map_Individual_Memory_Location_To_A_Delegate_For_Writing()
    {
        // Arrange
        var mem = new Memory(mapToDefaultRAM: false);
        var ramData1 = new byte[4096];
        ushort baseAddress = 0x0000;
        mem.MapRAM(baseAddress, ramData1);

        byte storedValue = 0;
        StoreByte writer = (address, newVal) => { storedValue = newVal;};
        mem.MapWriter(0x0010, writer);

        // Act/Assert
        mem.Write(0x010, 0x42);
        Assert.Equal(0x42, storedValue);
    }


    [Fact]
    public void Can_Map_Individual_Memory_Location_To_A_Single_Value_For_Reading()
    {
        // Arrange
        var mem = new Memory(mapToDefaultRAM: false);
        var ramData = new byte[4096];
        ushort baseAddress = 0x0000;
        mem.MapRAM(baseAddress, ramData);

        var memValue = new MemValue{Value = 0x41};
        mem.MapRO(0x0010, memValue);

        // Act/Assert
        byte actualValue = mem.Read(0x0010);
        Assert.Equal(memValue.Value, actualValue);
    }

    [Fact]
    public void Can_Map_Individual_Memory_Location_To_A_Single_Value_For_Writing()
    {
        // Arrange
        var mem = new Memory(mapToDefaultRAM: false);
        var ramData = new byte[4096];
        ushort baseAddress = 0x0000;
        mem.MapRAM(baseAddress, ramData);

        var memValue = new MemValue{Value = 0x00};
        mem.MapWO(0x0010, memValue);

        // Act/Assert
        mem.Write(0x0010, 0x41);
        Assert.Equal(0x41, memValue.Value);
    }

    [Fact]
    public void Can_Map_Individual_Memory_Location_To_A_Single_Value_For_Reading_And_Writing()
    {
        // Arrange
        var mem = new Memory(mapToDefaultRAM: false);
        var ramData = new byte[4096];
        ushort baseAddress = 0x0000;
        mem.MapRAM(baseAddress, ramData);

        var memValue = new  MemValue{Value = 0x00};
        mem.MapRW(0x0010, memValue);

        // Act/Assert
        mem.Write(0x0010, 0x41);
        var actualValue = mem.Read(0x0010);
        Assert.Equal(0x41, actualValue);
    }

    [Fact]
    public void Can_Create_Multiple_Memory_Configurations()
    {
        // Arrange
        var mem = new Memory(numberOfConfigurations:2, mapToDefaultRAM: false);

        // Act/Assert
        Assert.Equal(2, mem.NumberOfConfigurations);
    }

    [Fact]
    public void Default_Configuration_Is_The_First_After_Creating_Multiple_Configurations()
    {
        // Arrange
        var mem = new Memory(numberOfConfigurations:2, mapToDefaultRAM: false);

        // Act/Assert
        Assert.Equal(0, mem.CurrentConfiguration);
    }

    [Fact]
    public void Can_Change_Memory_Configurations()
    {
        // Arrange
        var mem = new Memory(numberOfConfigurations:2, mapToDefaultRAM: false);
        ushort baseAddress = 0x0000;

        // First configuration
        mem.SetMemoryConfiguration(0);
        var ramDataConfig0 = new byte[4096];
        mem.MapRAM(baseAddress, ramDataConfig0);
        mem.Write(0x0000, 0x42);

        // Second configuration (same address range)
        mem.SetMemoryConfiguration(1);
        var ramDataConfig1 = new byte[4096];
        mem.MapRAM(baseAddress, ramDataConfig1);
        mem.Write(0x0000, 0x21);

        // Act/Assert
        Assert.Equal(0x21, mem.Read(0x0000));   // Config 1 (currently from the second one)
        mem.SetMemoryConfiguration(0);          // Switch back to config 0
        Assert.Equal(0x42, mem.Read(0x0000));   // Config 1 (currently from the second one)
    }

    [Fact]
    public void Using_MapRAM_With_PreWriteIntercept_Callback_Performs_Callback_With_The_Address_Within_The_Mapped_Block()
    {
        // Arrange
        var mem = new Memory();

        ushort baseAddress = 0x1000;
        var data = new byte[4096];
        var interceptCalled = false;
        ushort interceptedAddress = 0;
        byte interceptedValue = 0;
        mem.MapRAM(baseAddress, data, preWriteIntercept: (address, value) =>
        {
            interceptCalled = true;
            interceptedAddress = address;
            interceptedValue = value;
            return true;
        });

        // Act
        int dataAddress = 10;
        mem.Write((ushort)(baseAddress + dataAddress), 0x42);

        // Assert
        Assert.Equal(dataAddress, interceptedAddress);
    }

    [Fact]
    public void Using_MapRAM_With_PreWriteIntercept_Callback_Performs_Callback_For_All_Mapped_Addresses()
    {
        // Arrange
        var mem = new Memory();

        ushort baseAddress = 0x1000;
        var data = new byte[4096];
        var interceptCalled = false;
        ushort interceptedAddress = 0;
        byte interceptedValue = 0;
        mem.MapRAM(baseAddress, data, preWriteIntercept: (address, value) =>
        {
            interceptCalled = true;
            interceptedAddress = address;
            interceptedValue = value;
            return true;
        });

        // Act / Assert
        int dataAddress = 0;
        for (ushort address = baseAddress; address < baseAddress + data.Length; address++)
        {
            interceptCalled = false;
            interceptedAddress = 0;
            interceptedValue = 0;
            mem.Write(address, 0x42);
            Assert.True(interceptCalled);
            Assert.Equal(dataAddress, interceptedAddress);
            Assert.Equal(0x42, interceptedValue);
            dataAddress++;
        }
    }

    [Fact]
    public void Using_MapRAM_With_PreWriteIntercept_Callback_Does_Not_Perform_Callback_Other_Addresses()
    {
        // Arrange
        var mem = new Memory();

        ushort baseAddress = 0x1000;
        var data = new byte[4096];
        var interceptCallCount = 0;
        mem.MapRAM(baseAddress, data, preWriteIntercept: (address, value) =>
        {
            interceptCallCount++;
            return true;
        });

        // Act / Assert
        for (ushort address = 0; address < baseAddress; address++)
            mem.Write(address, 0x42);
        Assert.Equal(0, interceptCallCount);

        for (ushort address = (ushort)(baseAddress + data.Length); address < mem.Size - 1; address++)
            mem.Write(address, 0x42);
        Assert.Equal(0, interceptCallCount);
    }

    [Fact]
    public void Creating_Memory_Throws_Exception_If_Size_Less_Than_0()
    {
        // Arrange
        Memory mem;

        // Act / Assert
        var ex = Assert.Throws<ArgumentException>(() => mem = new Memory(memorySize: -1));

        Assert.Contains($"Must be greater than 0", ex.Message);
    }

    [Fact]
    public void Creating_Memory_Throws_Exception_If_Size_Exceeds_64K()
    {
        // Arrange
        Memory mem;

        // Act / Assert
        var ex = Assert.Throws<ArgumentException>(() => mem = new Memory(memorySize: 64 * 1024 + 1));

        Assert.Contains($"Must be less than or equal to {Memory.MAX_MEMORY_SIZE}", ex.Message);
    }

    [Fact]
    public void Creating_Memory_Throws_Exception_If_Nr_Of_Configuration_Less_Than_1()
    {
        // Arrange
        Memory mem;

        // Act / Assert
        var ex = Assert.Throws<ArgumentException>(() => mem = new Memory(numberOfConfigurations: 0));

        Assert.Contains($"Must be equal to or greater than 1", ex.Message);
    }

    // TODO: Cloning Memory is not implemented correctly yet.
    //[Fact]
    //public void Cloning_Memory_Makes_Copy_Without_References_To_Original_Object()
    //{
    //    // Arrange
    //    var originalRam = new byte[1024];
    //    originalRam[0x0000] = 0x21;
    //    Memory mem = new Memory();
    //    mem.MapRAM(0x0000, originalRam);

    //    // Act
    //    var memClone = mem.Clone();
    //    memClone[0x0000] = 0x42;    // Change memory in the clone, should not affect the original

    //    // Assert
    //    Assert.Equal(mem.Size, memClone.Size);
    //    Assert.Equal(0x21, originalRam[0x0000]);
    //}
}
