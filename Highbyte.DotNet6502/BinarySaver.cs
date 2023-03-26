namespace Highbyte.DotNet6502;

/// <summary>
/// Helper class for saving binary files from 6502 emulator memory to file.
/// </summary>
public static class BinarySaver
{
    /// <summary>
    /// Saves binary file from memory. 
    /// If addFileHeaderWithLoadAddress is specified, a file header of two bytes is added with the memory start address (little endian order).
    /// </summary>
    /// <param name="mem">The memory to read from.</param>
    /// <param name="binaryFilePath">The full path of the file to save.</param>
    /// <param name="startAddress">The memory start address.</param>
    /// <param name="endAddress">The memory end address.</param>
    /// <param name="addFileHeaderWithLoadAddress">Set to true to add file header with start address.</param>
    public static void Save(
        Memory mem,
        string binaryFilePath,
        ushort startAddress,
        ushort endAddress,
        bool addFileHeaderWithLoadAddress = true
        )
    {
        var saveData = BuildSaveData(mem, startAddress, endAddress, addFileHeaderWithLoadAddress);
        File.WriteAllBytes(binaryFilePath, saveData);
    }

    /// <summary>
    /// Creates a byte array from the memory, ranging from specified start to end.
    /// If addFileHeaderWithLoadAddress is true, two bytes at the start is added with the start start address (little endian).
    /// </summary>
    /// <param name="mem"></param>
    /// <param name="startAddress"></param>
    /// <param name="endAddress"></param>
    /// <param name="addFileHeaderWithLoadAddress"></param>
    /// <returns></returns>
    public static byte[] BuildSaveData(
        Memory mem,
        ushort startAddress,
        ushort endAddress,
        bool addFileHeaderWithLoadAddress = true
        )
    {
        ushort length = (ushort)(endAddress - startAddress);
        var memData = mem.ReadData(startAddress, length);

        byte[] saveData;
        if (addFileHeaderWithLoadAddress)
        {
            saveData = new byte[memData.Length + 2];
            byte[] headerAddress = startAddress.ToLittleEndianBytes();
            saveData[0] = headerAddress[0];
            saveData[1] = headerAddress[1];
            Array.Copy(memData, 0, saveData, 2, memData.Length);
        }
        else
        {
            saveData = memData;
        }
        return saveData;
    }
}
