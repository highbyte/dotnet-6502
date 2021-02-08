using System;
using System.IO;

namespace Highbyte.DotNet6502
{
    /// <summary>
    /// Helper class for loading binary files into 6502 emulator memory.
    /// </summary>
    public static class BinaryLoader
    {
        /// <summary>
        /// </summary>
        /// <param name="binaryFilePath"></param>
        /// <param name="loadedAtAddress">The address the program as loaded at.</param>
        public static Memory Load(
            string binaryFilePath,
            out ushort loadedAtAddress)
        {
            return Load(binaryFilePath, out loadedAtAddress, out int _);
        }

        /// <summary>
        /// Load binary file into memory
        /// </summary>
        /// <param name="binaryFilePath"></param>
        /// <param name="loadedAtAddress">The address the program as loaded at.</param>
        /// <param name="fileLength">The file size in bytes</param>
        /// <param name="loadAddress">Optional. If not specified, the two first bytes in file is assumed to be the load address</param>
        public static Memory Load(
            string binaryFilePath,
            out ushort loadedAtAddress,
            out int fileLength,
            ushort? forceLoadAddress = null)
        {
            // Load binary file
            byte[] fileData = File.ReadAllBytes(binaryFilePath);

            if(!forceLoadAddress.HasValue)
            {
                // First two bytes of binary file is assumed to be start address, little endian notation.
                loadedAtAddress = ByteHelpers.ToLittleEndianWord(fileData[0], fileData[1]);
                // The rest of the bytes are considered the code & data
                byte[] codeAndDataActual = new byte[fileData.Length-2];
                Array.Copy(fileData, 2, codeAndDataActual, 0, fileData.Length-2);
                fileData = codeAndDataActual;
            }
            else
            {
                loadedAtAddress = forceLoadAddress.Value;
            }

            fileLength = fileData.Length;

            Memory mem = new();
            mem.StoreData(loadedAtAddress, fileData);
            return mem;
        }
    }
}