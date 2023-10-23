namespace Highbyte.DotNet6502.Tests;

public class MemoryHelperTest
{

    [Fact]
    public void Set_Bit_Updates_Memory_As_Expected()
    {
        // Arrange
        var mem = new Memory();
        mem[0x1000] = 0b00010110;

        // Act
        mem.SetBit(0x1000, 3);

        // Assert
        Assert.Equal(0b00011110, mem[0x1000]);
    }

    [Fact]
    public void Clear_Bit_Updates_Memory_As_Expected()
    {
        // Arrange
        var mem = new Memory();
        mem[0x1000] = 0b00011110;

        // Act
        mem.ClearBit(0x1000, 3);

        // Assert
        Assert.Equal(0b00010110, mem[0x1000]);
    }


    [Fact]
    public void StoreData_Loads_Data_Into_Memory()
    {
        // Arrange
        var data = new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 };

        ushort address = 0x1000;
        var mem = new Memory();

        // Act
        mem.StoreData(address, data);

        // Assert
        var storedData = mem.ReadData(address, (ushort)data.Length);

        for (int i = 0; i < data.Length; i++)
        {
            Assert.Equal(data[i], storedData[i]);
        }
    }

    [Fact]
    public void StoreData_Throws_Exception_If_Address_And_Data_Length_Exceeds_64K()
    {
        // Arrange
        var data = new byte[] { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1 };

        ushort address = 0xfffa;
        var mem = new Memory();

        // Act / Assert
        var ex = Assert.Throws<DotNet6502Exception>(() => mem.StoreData(address, data));

        Assert.Contains("exceeds maximum memory limit", ex.Message);
    }

    [Fact]
    public void StoreData_Throws_Exception_If_Data_Length_Exceeds_64K()
    {
        // Arrange
        var data = new byte[(64 * 1024) + 1];

        ushort address = 0x0;
        var mem = new Memory();

        // Act / Assert
        var ex = Assert.Throws<DotNet6502Exception>(() => mem.StoreData(address, data));

        Assert.Contains("exceeds maximum memory limit", ex.Message);
    }


    [Fact]
    public void ReadData_Loads_Data_From_Memory()
    {
        // Arrange
        var storedData = new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 };

        ushort address = 0x1000;
        var mem = new Memory();
        mem.StoreData(address, storedData);

        // Act
        var data = mem.ReadData(address, 10);

        // Assert
        for (int i = 0; i < data.Length; i++)
        {
            Assert.Equal(storedData[i], data[i]);
        }
    }

    [Fact]
    public void ReadData_Throws_Exception_If_Address_And_Length_Exceeds_64K()
    {
        // Arrange
        ushort address = 0xfffa;
        var mem = new Memory();

        // Act / Assert
        var ex = Assert.Throws<DotNet6502Exception>(() => mem.ReadData(address, 10));

        Assert.Contains("exceeds maximum memory limit", ex.Message);
    }


    [Fact]
    public void IsBitSet_Returns_True_If_Specified_Bit_Is_Set()
    {
        // Arrange
        ushort address = 0x1000;
        var mem = new Memory();
        mem[address] = 0b00010000;  // Bit 4 set

        // Act / Assert
        Assert.True(mem.IsBitSet(address, 4));
    }


}
