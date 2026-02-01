using Highbyte.DotNet6502.Utils;

namespace Highbyte.DotNet6502.Tests.Utils;

public class BinaryLoaderTest
{
    private readonly ITestOutputHelper _output;
    public BinaryLoaderTest(ITestOutputHelper testOutputHelper)
    {
        _output = testOutputHelper;
    }

    [Fact]
    public void Load_Can_Load_A_Binary_File_To_Emulator_Memory()
    {
        // Arrange
        byte[] header = { 0x00, 0x01 };
        byte[] data = { 0x02, 0x03, 0x04, 0x05, 0x06, 0x07 };
        var bytes = header.Concat(data).ToArray();
        var filePath = "test.bin";
        File.WriteAllBytes(filePath, bytes);

        var bytesString = string.Join(" ", bytes.Select(b => Convert.ToString(b, 2).PadLeft(8, '0')));
        _output.WriteLine("Bytes saved:");
        _output.WriteLine(bytesString);

        var mem = new Memory();

        // Act
        BinaryLoader.Load(mem, filePath, out var loadedAtAddress, out var fileLength, forceLoadAddress: null);

        // Assert
        Assert.Equal(header.ToLittleEndianWord(), loadedAtAddress);
        Assert.Equal(data.Length, fileLength);

        var memBytes = mem.ReadData(loadedAtAddress, (ushort)data.Length);
        Assert.Equivalent(data, memBytes);
    }

    [Fact]
    public void Load_Can_Load_A_Binary_File_To_Emulator_Memory_At_Specific_Address()
    {
        // Arrange
        byte[] bytes = { 0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07 };
        var filePath = "test.bin";
        File.WriteAllBytes(filePath, bytes);
        ushort loadAddress = 0x0100;

        var bytesString = string.Join(" ", bytes.Select(b => Convert.ToString(b, 2).PadLeft(8, '0')));
        _output.WriteLine("Bytes saved:");
        _output.WriteLine(bytesString);

        var mem = new Memory();

        // Act
        BinaryLoader.Load(mem, filePath, out var loadedAtAddress, out var fileLength, forceLoadAddress: loadAddress);

        // Assert
        Assert.Equal(loadAddress, loadedAtAddress);
        Assert.Equal(bytes.Length, fileLength);

        var memBytes = mem.ReadData(loadedAtAddress, (ushort)bytes.Length);
        Assert.Equivalent(bytes, memBytes);
    }

    [Fact]
    public void Load_Can_Load_A_Binary_Array_To_Emulator_Memory()
    {
        // Arrange
        byte[] header = { 0x00, 0x01 };
        byte[] data = { 0x02, 0x03, 0x04, 0x05, 0x06, 0x07 };
        var bytes = header.Concat(data).ToArray();

        var bytesString = string.Join(" ", bytes.Select(b => Convert.ToString(b, 2).PadLeft(8, '0')));
        _output.WriteLine("Bytes:");
        _output.WriteLine(bytesString);

        var mem = new Memory();

        // Act
        BinaryLoader.Load(mem, bytes, out var loadedAtAddress, out var fileLength, forceLoadAddress: null);

        // Assert
        Assert.Equal(header.ToLittleEndianWord(), loadedAtAddress);
        Assert.Equal(data.Length, fileLength);

        var memBytes = mem.ReadData(loadedAtAddress, (ushort)data.Length);
        Assert.Equivalent(data, memBytes);
    }

    [Fact]
    public void Load_Can_Load_A_Binary_Array_To_Emulator_Memory_At_Specific_Address()
    {
        // Arrange
        byte[] bytes = { 0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07 };

        ushort loadAddress = 0x0100;

        var bytesString = string.Join(" ", bytes.Select(b => Convert.ToString(b, 2).PadLeft(8, '0')));
        _output.WriteLine("Bytes:");
        _output.WriteLine(bytesString);

        var mem = new Memory();

        // Act
        BinaryLoader.Load(mem, bytes, out var loadedAtAddress, out var fileLength, forceLoadAddress: loadAddress);

        // Assert
        Assert.Equal(loadAddress, loadedAtAddress);
        Assert.Equal(bytes.Length, fileLength);

        var memBytes = mem.ReadData(loadedAtAddress, (ushort)bytes.Length);
        Assert.Equivalent(bytes, memBytes);
    }

    [Fact]
    public void Load_Can_Load_A_Binary_File_And_Return_A_Emulator_Memory_Object()
    {
        // Arrange
        byte[] header = { 0x00, 0x01 };
        byte[] data = { 0x02, 0x03, 0x04, 0x05, 0x06, 0x07 };
        var bytes = header.Concat(data).ToArray();
        var filePath = "test.bin";
        File.WriteAllBytes(filePath, bytes);

        var bytesString = string.Join(" ", bytes.Select(b => Convert.ToString(b, 2).PadLeft(8, '0')));
        _output.WriteLine("Bytes saved:");
        _output.WriteLine(bytesString);

        // Act
        var mem = BinaryLoader.Load(filePath, out var loadedAtAddress, out var fileLength, forceLoadAddress: null);

        // Assert
        Assert.Equal(header.ToLittleEndianWord(), loadedAtAddress);
        Assert.Equal(data.Length, fileLength);

        var memBytes = mem.ReadData(loadedAtAddress, (ushort)data.Length);
        Assert.Equivalent(data, memBytes);
    }

    [Fact]
    public void ReadFile_Can_Read_A_Binary_File()
    {
        // Arrange
        byte[] bytes = { 0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07 };
        var filePath = "test.bin";
        File.WriteAllBytes(filePath, bytes);

        var bytesString = string.Join(" ", bytes.Select(b => Convert.ToString(b, 2).PadLeft(8, '0')));
        _output.WriteLine("Bytes saved:");
        _output.WriteLine(bytesString);

        // Act
        var loadedBytes = BinaryLoader.ReadFile(filePath, fileHeaderContainsLoadAddress: false, out _, out var codeAndDataFileSize);

        // Assert
        Assert.Equal(bytes.Length, loadedBytes.Length);
        Assert.Equal(bytes.Length, codeAndDataFileSize);

        Assert.Equivalent(bytes, loadedBytes);
    }

    [Fact]
    public void ReadFile_Can_Read_A_Binary_File_With_Address_Header()
    {
        // Arrange
        byte[] header = { 0x00, 0x01 };
        byte[] data = { 0x02, 0x03, 0x04, 0x05, 0x06, 0x07 };
        var bytes = header.Concat(data).ToArray();
        var filePath = "test.bin";
        File.WriteAllBytes(filePath, bytes);

        var bytesString = string.Join(" ", bytes.Select(b => Convert.ToString(b, 2).PadLeft(8, '0')));
        _output.WriteLine("Bytes saved:");
        _output.WriteLine(bytesString);

        // Act
        var loadedBytes = BinaryLoader.ReadFile(filePath, fileHeaderContainsLoadAddress: true, out var fileHeaderLoadAddress, out var codeAndDataFileSize);

        // Assert
        Assert.Equal(data.Length, loadedBytes.Length);
        Assert.Equal(data.Length, codeAndDataFileSize);
        Assert.Equal((ushort)0x0100, fileHeaderLoadAddress);

        Assert.Equivalent(data, loadedBytes);
    }

    [Fact]
    public void Load_Can_Load_Raw_Binary_Without_Header()
    {
        // Arrange
        byte[] data = { 0xA9, 0x01, 0x85, 0x00, 0xA2, 0x05 }; // LDA #$01, STA $00, LDX #$05
        var filePath = "test_raw.bin";
        File.WriteAllBytes(filePath, data);
        ushort loadAddress = 0xc000;

        var bytesString = string.Join(" ", data.Select(b => $"{b:X2}"));
        _output.WriteLine("Bytes saved:");
        _output.WriteLine(bytesString);

        var mem = new Memory();

        // Act
        BinaryLoader.Load(mem, filePath, out var loadedAtAddress, out var fileLength, forceLoadAddress: loadAddress, fileContainsLoadAddress: false);

        // Assert
        Assert.Equal(loadAddress, loadedAtAddress);
        Assert.Equal(data.Length, fileLength);

        var memBytes = mem.ReadData(loadedAtAddress, (ushort)data.Length);
        Assert.Equivalent(data, memBytes);
    }

    [Fact]
    public void Load_Throws_If_Raw_Binary_Without_Header_And_No_LoadAddress()
    {
        // Arrange
        byte[] data = { 0xA9, 0x01, 0x85, 0x00 };
        var filePath = "test_raw_no_addr.bin";
        File.WriteAllBytes(filePath, data);

        var mem = new Memory();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
        {
            BinaryLoader.Load(mem, filePath, out var _, out var _, forceLoadAddress: null, fileContainsLoadAddress: false);
        });
    }
}
