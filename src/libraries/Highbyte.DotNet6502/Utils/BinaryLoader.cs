namespace Highbyte.DotNet6502.Utils;

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
        return Load(binaryFilePath, out loadedAtAddress, out var _);
    }

    /// <summary>
    /// Load binary file. If forceLoadAddress is specified, the binary is loaded at that address.
    /// Otherwise it assumes first two bytes is load address.
    /// Returns a new Memory instance.
    /// </summary>
    /// <param name="binaryFilePath">The binary file to load.</param>
    /// <param name="loadedAtAddress">Is set to the address the binary file was loaded at.</param>
    /// <param name="fileLength">Is set to file size in bytes.</param>
    /// <param name="forceLoadAddress">Optional. If not specified, the two first bytes in file is assumed to be the load address.</param>
    public static Memory Load(
        string binaryFilePath,
        out ushort loadedAtAddress,
        out ushort fileLength,
        ushort? forceLoadAddress = null)
    {
        Memory mem = new(mapToDefaultRAM: true);
        mem.Load(binaryFilePath, out loadedAtAddress, out fileLength, forceLoadAddress);
        return mem;
    }


    /// <summary>
    /// Load binary file into memory. Assume first two bytes is load address.
    /// </summary>
    /// <param name="mem"></param>
    /// <param name="binaryFilePath"></param>
    /// <param name="loadedAtAddress">Is set to the address the binary file was loaded to.</param>
    public static void Load(
        this Memory mem,
        string binaryFilePath,
        out ushort loadedAtAddress)
    {
        mem.Load(binaryFilePath, out loadedAtAddress, out var _);
    }

    /// <summary>
    /// Load binary file into memory. If forceLoadAddress is specified, the binary is loaded at that address.
    /// Otherwise it assumes first two bytes is load address.
    /// Loads into the provided Memory instance.
    /// </summary>
    /// <param name="mem"></param>
    /// <param name="binaryFilePath">The binary file to load.</param>
    /// <param name="loadedAtAddress">Is set to the address the binary file was loaded at.</param>
    /// <param name="fileLength">Is set to file size in bytes.</param>
    /// <param name="forceLoadAddress">Optional. If not specified, the two first bytes in file is assumed to be the load address.</param>
    public static void Load(
        this Memory mem,
        string binaryFilePath,
        out ushort loadedAtAddress,
        out ushort fileLength,
        ushort? forceLoadAddress = null)
    {
        var fileData = ReadFile(
            binaryFilePath,
            fileHeaderContainsLoadAddress: !forceLoadAddress.HasValue,
            out var fileHeaderLoadAddress,
            out fileLength
        );
        if (fileHeaderLoadAddress.HasValue)
            loadedAtAddress = fileHeaderLoadAddress.Value;
        else
            loadedAtAddress = forceLoadAddress ?? throw new ArgumentNullException(nameof(forceLoadAddress), "No load address specified and file does not contain a load address in the header.");

        mem.StoreData(loadedAtAddress, fileData);
    }

    /// <summary>
    /// Load byte array into memory. If forceLoadAddress is specified, the binary is loaded at that address.
    /// Otherwise it assumes first two bytes is load address.
    /// Loads into the provided Memory instance.
    /// </summary>
    /// <param name="fileData">Byte array of the data to load</param>
    /// <param name="loadedAtAddress">Is set to the address the byte array was loaded to</param>
    /// <param name="fileLength">Is set to the byte array size</param>
    /// <param name="forceLoadAddress">Optional. If not specified, the two first bytes in file is assumed to be the load address</param>

    /// <exception cref="ArgumentNullException"></exception>
    public static void Load(
        this Memory mem,
        byte[] fileData,
        out ushort loadedAtAddress,
        out ushort fileLength,
        ushort? forceLoadAddress = null)
    {
        var data = ReadFile(
            fileData,
            fileHeaderContainsLoadAddress: !forceLoadAddress.HasValue,
            out var fileHeaderLoadAddress,
            out fileLength
        );
        if (fileHeaderLoadAddress.HasValue)
            loadedAtAddress = fileHeaderLoadAddress.Value;
        else
            loadedAtAddress = forceLoadAddress ?? throw new ArgumentNullException(nameof(forceLoadAddress), "No load address specified and file does not contain a load address in the header.");

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
        var fileData = File.ReadAllBytes(binaryFilePath);

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
            var codeAndDataActual = new byte[fileData.Length - 2];
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
