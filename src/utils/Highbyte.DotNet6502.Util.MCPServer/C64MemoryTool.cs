using System.ComponentModel;
using System.Text.Json.Nodes;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Utils;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Highbyte.DotNet6502.Util.MCPServer;

[McpServerToolType]
public static class C64MemoryTool
{
    [McpServerTool, Description("Returns value of specified memory address in C64 emulator")]
    public static async Task<CallToolResult> ReadMemory(IHostApp hostApp, ushort address)
    {
        try
        {
            byte value = 0;
            // Safest to run code that uses objects the emulator uses on the UI thread.
            await hostApp.ExternalControlInvokeOnUIThread(async () =>
            {
                C64ToolHelper.AssertC64EmulatorIsRunning(hostApp);
                var c64 = C64ToolHelper.GetC64(hostApp);
                value = c64.Mem[address];
            });
            return C64ToolHelper.BuildCallToolDataResult(value);
        }
        catch (Exception ex)
        {
            return C64ToolHelper.BuildCallToolErrorResult(ex);
        }
    }

    [McpServerTool(UseStructuredContent = true, ReadOnly = true), Description("Returns a range of values from memory start address up to specified length in C64 emulator")]
    public static async Task<CallToolResult> ReadMemoryRange(IHostApp hostApp, ushort startAddress, ushort length)
    {
        try
        {
            byte[] values = null!;
            // Safest to run code that uses objects the emulator uses on the UI thread.
            await hostApp.ExternalControlInvokeOnUIThread(async () =>
            {
                if (length <= 0)
                    throw new ArgumentException("Length must be greater than zero.", nameof(length));

                C64ToolHelper.AssertC64EmulatorIsRunning(hostApp);
                var c64 = C64ToolHelper.GetC64(hostApp);
                if (startAddress + length > c64.Mem.Size)
                    length = (ushort)(c64.Mem.Size - startAddress);
                values = c64.Mem.ReadData(startAddress, length);

            });
            // Convert a byte array to System.Text.Json.JsonArray
            object jsonArray = new JsonArray(values.Select(b => (JsonNode)b).ToArray());
            var result = C64ToolHelper.BuildCallToolDataResult(jsonArray);

            return result;
        }
        catch (Exception ex)
        {
            return C64ToolHelper.BuildCallToolErrorResult(ex);
        }
    }


    [McpServerTool, Description("Writes value at specified memory address in C64 emulator")]
    public static async Task<CallToolResult> WriteMemory(IHostApp hostApp, ushort address, byte value)
    {
        try
        {
            // Safest to run code that uses objects the emulator uses on the UI thread.
            await hostApp.ExternalControlInvokeOnUIThread(async () =>
            {
                C64ToolHelper.AssertC64EmulatorIsRunning(hostApp);
                var c64 = C64ToolHelper.GetC64(hostApp);
                c64.Mem[address] = value;
            });
            return new CallToolResult();
        }
        catch (Exception ex)
        {
            return C64ToolHelper.BuildCallToolErrorResult(ex);
        }
    }

    /// <summary>
    /// </summary>
    /// <param name="hostApp"></param>
    /// <param name="address"></param>
    /// <param name="values">Array of bytes to write to memory></param>
    [McpServerTool, Description("Writes a range of values (byte array) starting at specified memory address in C64 emulator. Expects 'values' as an array of integers.")]
    public static async Task<CallToolResult> WriteMemoryRange(IHostApp hostApp, ushort address, byte[] values)
    {
        try
        {
            // Safest to run code that uses objects the emulator uses on the UI thread.
            await hostApp.ExternalControlInvokeOnUIThread(async () =>
            {
                C64ToolHelper.AssertC64EmulatorIsRunning(hostApp);
                var c64 = C64ToolHelper.GetC64(hostApp);
                if (address + values.Length > c64.Mem.Size)
                    values = values.Take(c64.Mem.Size - address).ToArray();
                c64.Mem.StoreData(address, values);
            });
            return new CallToolResult();
        }
        catch (Exception ex)
        {
            return C64ToolHelper.BuildCallToolErrorResult(ex);
        }
    }

    /// <summary>
    /// </summary>
    /// <param name="hostApp"></param>
    /// <param name="address"></param>
    /// <param name="hexValues">Sequence of 8-bit bytes in hex separated by space to write to memory></param>
    [McpServerTool, Description("Writes a range of values starting at specified memory address in C64 emulator.")]
    public static async Task<CallToolResult> WriteMemoryRangeAsHexString(IHostApp hostApp, ushort address, string hexValues)
    {
        try
        {
            // Safest to run code that uses objects the emulator uses on the UI thread.
            await hostApp.ExternalControlInvokeOnUIThread(async () =>
            {
                // Convert hex string to byte array
                if (string.IsNullOrWhiteSpace(hexValues))
                    throw new ArgumentException("Hex values cannot be null or empty.", nameof(hexValues));
                var values = hexValues
                    .Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(hex => Convert.ToByte(hex, 16))
                    .ToArray();

                await WriteMemoryRange(hostApp, address, values);
            });
            return new CallToolResult();
        }
        catch (Exception ex)
        {
            return C64ToolHelper.BuildCallToolErrorResult(ex);
        }
    }
}
