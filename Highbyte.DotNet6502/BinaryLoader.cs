using Highbyte.DotNet6502.Systems;

namespace Highbyte.DotNet6502;

/// <summary>
/// Helper class for loading binary files into 6502 emulator memory.
/// </summary>
public static class BinaryLoader
{
    /// <summary>
    /// Load binary file. Assume first two bytes is load address.
    /// Returns a new Memory instance.
    /// </summary>
    /// <param name="binaryFilePath"></param>
    /// <param name="loadedAtAddress">The address the program as loaded at.</param>
    public static Memory Load(
        string binaryFilePath,
        out ushort loadedAtAddress)
    {
        return Load(binaryFilePath, out loadedAtAddress, out ushort _);
    }

    /// <summary>
    /// Load binary file. If forceLoadAddress is specified, the binary is loaded at that address.
    /// Otherwise it assumes first two bytes is load address.
    /// Returns a new Memory instance.
    /// </summary>
    /// <param name="binaryFilePath"></param>
    /// <param name="loadedAtAddress">The address the program as loaded at.</param>
    /// <param name="fileLength">The file size in bytes</param>
    /// <param name="forceLoadAddress">Optional. If not specified, the two first bytes in file is assumed to be the load address</param>
    public static Memory Load(
        string binaryFilePath,
        out ushort loadedAtAddress,
        out ushort fileLength,
        ushort? forceLoadAddress = null)
    {
        Memory mem = new(mapToDefaultRAM: true);
        Load(mem, binaryFilePath, out loadedAtAddress, out fileLength, forceLoadAddress);
        return mem;
    }

    /// <summary>
    /// Load binary file into memory. If forceLoadAddress is specified, the binary is loaded at that address.
    /// Otherwise it assumes first two bytes is load address.
    /// Loads into the provided Memory instance.
    /// </summary>
    /// <param name="binaryFilePath"></param>
    /// <param name="loadedAtAddress">The address the program as loaded at.</param>
    /// <param name="fileLength">The file size in bytes</param>
    /// <param name="forceLoadAddress">Optional. If not specified, the two first bytes in file is assumed to be the load address</param>
    public static void Load(
        Memory mem,
        string binaryFilePath,
        out ushort loadedAtAddress,
        out ushort fileLength,
        ushort? forceLoadAddress = null)
    {
        byte[] fileData = ReadFile(
            binaryFilePath,
            fileHeaderContainsLoadAddress: !forceLoadAddress.HasValue,
            out ushort? fileHeaderLoadAddress,
            out fileLength
        );
        if (fileHeaderLoadAddress.HasValue)
            loadedAtAddress = fileHeaderLoadAddress.Value;
        else
            loadedAtAddress = forceLoadAddress.Value;

        mem.StoreData(loadedAtAddress, fileData);
    }

    public static void Load(
        Memory mem,
        byte[] fileData,
        out ushort loadedAtAddress,
        out ushort fileLength,
        ushort? forceLoadAddress = null)
    {
        byte[] data = ReadFile(
            fileData,
            fileHeaderContainsLoadAddress: !forceLoadAddress.HasValue,
            out ushort? fileHeaderLoadAddress,
            out fileLength
        );
        if (fileHeaderLoadAddress.HasValue)
            loadedAtAddress = fileHeaderLoadAddress.Value;
        else
            loadedAtAddress = forceLoadAddress.Value;

        mem.StoreData(loadedAtAddress, data);
    }


    public static byte[] ReadFile(
        string binaryFilePath,
        bool fileHeaderContainsLoadAddress,
        out ushort? fileHeaderLoadAddress,
        out ushort codeAndDataFileSize
        )
    {
        binaryFilePath = PathHelper.ExpandOSEnvironmentVariables(binaryFilePath);

        // Load binary file
        byte[] fileData = File.ReadAllBytes(binaryFilePath);

        return ReadFile(fileData, fileHeaderContainsLoadAddress, out fileHeaderLoadAddress, out codeAndDataFileSize);
    }

    public static byte[] ReadFile(
        byte[] fileData,
        bool fileHeaderContainsLoadAddress,
        out ushort? fileHeaderLoadAddress,
        out ushort codeAndDataFileSize
        )
    {
        if (fileHeaderContainsLoadAddress)
        {
            // First two bytes of binary file is assumed to be start address, little endian notation.
            fileHeaderLoadAddress = ByteHelpers.ToLittleEndianWord(fileData[0], fileData[1]);
            // The rest of the bytes are considered the code & data
            byte[] codeAndDataActual = new byte[fileData.Length - 2];
            Array.Copy(fileData, 2, codeAndDataActual, 0, fileData.Length - 2);
            fileData = codeAndDataActual;
        }
        else
        {
            fileHeaderLoadAddress = null;
        }
        codeAndDataFileSize = (ushort)fileData.Length;
        return fileData;
    }
}
