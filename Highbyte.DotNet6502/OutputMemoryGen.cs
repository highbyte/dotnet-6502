using System.Collections.Generic;

namespace Highbyte.DotNet6502;

/// <summary>
/// Helper class formatting output for memory
/// </summary>
public static class OutputMemoryGen
{
    const string HexPrefix = "";
    const int maxBytesPerRow = 16;
    const int maxBytesGroupPerRow = 4;

    const int charactersPerRow = (4+2) + ((maxBytesPerRow*2) + (maxBytesPerRow-1) + ((maxBytesPerRow/maxBytesGroupPerRow)-1));

    public static List<string> GetFormattedMemoryList(Memory mem, ushort startAddress, ushort endAddress)
    {
        ushort currentAddress = startAddress;
        int colIndex = 0;
        int colGroupIndex = 0;
        int rowIndex = 0;
        string row = "";
        List<string> list = new();
        bool cont = true;
        while (cont)
        {
            if(colIndex == 0)
                row += $"{currentAddress.ToHex(HexPrefix, lowerCase: true)}  ";
            if(colIndex != 0)
                row += " ";
            // Extra space every X bytes 
            if(colGroupIndex >= maxBytesGroupPerRow)
            {
                row += " ";
                colGroupIndex=0;
            }

            row += mem[currentAddress].ToHex(HexPrefix, lowerCase: true);

            colIndex++;
            colGroupIndex++;

            if(colIndex >= maxBytesPerRow || currentAddress == endAddress)
            {
                colIndex = 0;
                colGroupIndex = 0;
                rowIndex++;
                list.Add($"{row, -charactersPerRow}");
                row = "";
            }

            if (currentAddress < endAddress && currentAddress != 0xffff)
                currentAddress++;
            else
                cont = false;
        }
        return list;
    }
}
