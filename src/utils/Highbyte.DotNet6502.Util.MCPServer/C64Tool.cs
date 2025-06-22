using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Util.MCPServer.Contract;
using Highbyte.DotNet6502.Utils;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Highbyte.DotNet6502.Util.MCPServer;

[McpServerToolType]
public static class C64Tool
{
    [McpServerTool, Description("Get C64 emulator state (Uninitialized, Running, Paused)")]
    public static async Task<CallToolResult> GetState(IHostApp hostApp)
    {
        EmulatorState emulatorState = default;

        try
        {
            await hostApp.ExternalControlInvokeOnUIThread(async () =>
            {
                AssertEmulatorIsC64(hostApp);
                emulatorState = hostApp.EmulatorState;
            });

            return BuildCallToolDataResult(emulatorState);

        }
        catch (Exception ex)
        {
            return BuildCallToolErrorResult(ex);
        }
    }

    [McpServerTool, Description("Starts C64 emulator.")]
    public static async Task<CallToolResult> Start(IHostApp hostApp)
    {
        try
        {
            // Safest to run code that uses objects the emulator uses on the UI thread.
            await hostApp.ExternalControlInvokeOnUIThread(async () =>
            {
                AssertC64EmulatorIsPausedOrUninitialzied(hostApp);
                await hostApp.Start();
            });
            return new CallToolResult();
        }
        catch (Exception ex)
        {
            return BuildCallToolErrorResult(ex);
        }

    }

    [McpServerTool, Description("Pause C64 emulator")]
    public static async Task<CallToolResult> Pause(IHostApp hostApp)
    {
        try
        {
            // Safest to run code that uses objects the emulator uses on the UI thread.
            await hostApp.ExternalControlInvokeOnUIThread(async () =>
            {
                AssertC64EmulatorIsRunning(hostApp);
                hostApp.Pause();
            });
            return new CallToolResult();
        }
        catch (Exception ex)
        {
            return BuildCallToolErrorResult(ex);
        }
    }

    [McpServerTool, Description("Stop C64 emulator")]
    public static async Task<CallToolResult> Stop(IHostApp hostApp)
    {
        try
        {
            // Safest to run code that uses objects the emulator uses on the UI thread.
            await hostApp.ExternalControlInvokeOnUIThread(async () =>
            {
                AssertC64EmulatorIsRunningOrPaused(hostApp);
                hostApp.Stop();
            });
            return new CallToolResult();
        }
        catch (Exception ex)
        {
            return BuildCallToolErrorResult(ex);
        }
    }

    [McpServerTool, Description("Runs the C64 emulator for specified number of frames")]
    public static async Task<CallToolResult> RunNumberOfSeconds(IHostApp hostApp, int numberOfSeconds)
    {
        try
        {
            // Safest to run code that uses objects the emulator uses on the UI thread.
            await hostApp.ExternalControlInvokeOnUIThread(async () =>
            {
                if (numberOfSeconds <= 0)
                    throw new ArgumentException("Number of seconds must be greater than zero.", nameof(numberOfSeconds));

                AssertC64EmulatorIsRunning(hostApp);
                var c64 = GetC64(hostApp);
                //var numberOfFrames = numberOfSeconds * c64.Vic2.Vic2Model.??
                int numberOfFrames = (int)(numberOfSeconds * c64.Screen.RefreshFrequencyHz);
                await RunNumberOfFrames(hostApp, numberOfFrames);

            });
            return new CallToolResult();
        }
        catch (Exception ex)
        {
            return BuildCallToolErrorResult(ex);
        }
    }

    [McpServerTool, Description("Runs the C64 emulator for specified number of frames")]
    public static async Task<CallToolResult> RunNumberOfFrames(IHostApp hostApp, int numberOfFrames)
    {
        try
        {
            // Safest to run code that uses objects the emulator uses on the UI thread.
            await hostApp.ExternalControlInvokeOnUIThread(async () =>
            {
                if (numberOfFrames <= 0)
                    throw new ArgumentException("Frame count must be greater than zero.", nameof(numberOfFrames));

                AssertC64EmulatorIsRunning(hostApp);

                for (int i = 0; i < numberOfFrames; i++)
                    hostApp.RunEmulatorOneFrame();

            });
            return new CallToolResult();
        }
        catch (Exception ex)
        {
            return BuildCallToolErrorResult(ex);
        }
    }

    [McpServerTool, Description("Runs the C64 emulator for specified number of instructions")]
    public static async Task<CallToolResult> RunNumberOfInstructions(IHostApp hostApp, int numberOfInstructions)
    {
        try
        {
            // Safest to run code that uses objects the emulator uses on the UI thread.
            await hostApp.ExternalControlInvokeOnUIThread(async () =>
            {
                if (numberOfInstructions <= 0)
                    throw new ArgumentException("Instruction count must be greater than zero.", nameof(numberOfInstructions));

                AssertC64EmulatorIsRunning(hostApp);

                for (int i = 0; i < numberOfInstructions; i++)
                    hostApp.CurrentRunningSystem.CPU.ExecuteOneInstruction(hostApp.CurrentRunningSystem.Mem);

            });
            return new CallToolResult();
        }
        catch (Exception ex)
        {
            return BuildCallToolErrorResult(ex);
        }
    }

    [McpServerTool, Description("Returns value of specified memory address in C64 emulator")]
    public static async Task<CallToolResult> ReadMemory(IHostApp hostApp, ushort address)
    {
        try
        {
            byte value = 0;
            // Safest to run code that uses objects the emulator uses on the UI thread.
            await hostApp.ExternalControlInvokeOnUIThread(async () =>
            {
                AssertC64EmulatorIsRunning(hostApp);
                var c64 = GetC64(hostApp);
                value = c64.Mem[address];
            });
            return BuildCallToolDataResult(value);
        }
        catch (Exception ex)
        {
            return BuildCallToolErrorResult(ex);
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

                AssertC64EmulatorIsRunning(hostApp);
                var c64 = GetC64(hostApp);
                if (startAddress + length > c64.Mem.Size)
                    length = (ushort)(c64.Mem.Size - startAddress);
                values = c64.Mem.ReadData(startAddress, length);

            });
            // Convert a byte array to System.Text.Json.JsonArray
            object jsonArray = new JsonArray(values.Select(b => (JsonNode)b).ToArray());
            var result = BuildCallToolDataResult(jsonArray);

            return result;
        }
        catch (Exception ex)
        {
            return BuildCallToolErrorResult(ex);
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
                AssertC64EmulatorIsRunning(hostApp);
                var c64 = GetC64(hostApp);
                c64.Mem[address] = value;
            });
            return new CallToolResult();
        }
        catch (Exception ex)
        {
            return BuildCallToolErrorResult(ex);
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
                AssertC64EmulatorIsRunning(hostApp);
                var c64 = GetC64(hostApp);
                if (address + values.Length > c64.Mem.Size)
                    values = values.Take(c64.Mem.Size - address).ToArray();
                c64.Mem.StoreData(address, values);
            });
            return new CallToolResult();
        }
        catch (Exception ex)
        {
            return BuildCallToolErrorResult(ex);
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
            return BuildCallToolErrorResult(ex);
        }
    }

    [McpServerTool, Description("Returns the current value of the C64 CPU registers: A, X, Y, PS, PC, SP")]
    public static async Task<CallToolResult> GetCPURegisters(IHostApp hostApp)
    {
        try
        {
            CPURegisters cpuRegisters = null!;
            // Safest to run code that uses objects the emulator uses on the UI thread.
            await hostApp.ExternalControlInvokeOnUIThread(async () =>
            {
                AssertC64EmulatorIsRunning(hostApp);
                var cpu = GetC64(hostApp).CPU;
                cpuRegisters = new CPURegisters
                {
                    A = cpu.A,
                    X = cpu.X,
                    Y = cpu.Y,
                    PC = cpu.PC,
                    SP = cpu.SP,
                    ProcessorStatus = new ProcessorStatus(cpu.ProcessorStatus.Value)
                };
            });
            return BuildCallToolDataResult(cpuRegisters);
        }
        catch (Exception ex)
        {
            return BuildCallToolErrorResult(ex);
        }
    }

    [McpServerTool, Description("Sets the CPU register A")]
    public static async Task<CallToolResult> SetCPURegisterA(IHostApp hostApp, byte value)
    {
        try
        {
            // Safest to run code that uses objects the emulator uses on the UI thread.
            await hostApp.ExternalControlInvokeOnUIThread(async () =>
            {
                AssertC64EmulatorIsRunning(hostApp);
                var cpu = GetC64(hostApp).CPU;
                cpu.A = value;
            });
            return new CallToolResult();
        }
        catch (Exception ex)
        {
            return BuildCallToolErrorResult(ex);
        }
    }

    [McpServerTool, Description("Sets the CPU register X")]
    public static async Task<CallToolResult> SetCPURegisterX(IHostApp hostApp, byte value)
    {
        try
        {
            // Safest to run code that uses objects the emulator uses on the UI thread.
            await hostApp.ExternalControlInvokeOnUIThread(async () =>
            {
                AssertC64EmulatorIsRunning(hostApp);
                var cpu = GetC64(hostApp).CPU;
                cpu.X = value;
            });
            return new CallToolResult();
        }
        catch (Exception ex)
        {
            return BuildCallToolErrorResult(ex);
        }
    }

    [McpServerTool, Description("Sets the CPU register Y")]
    public static async Task<CallToolResult> SetCPURegisterY(IHostApp hostApp, byte value)
    {
        try
        {
            // Safest to run code that uses objects the emulator uses on the UI thread.
            await hostApp.ExternalControlInvokeOnUIThread(async () =>
            {
                AssertC64EmulatorIsRunning(hostApp);
                var cpu = GetC64(hostApp).CPU;
                cpu.Y = value;
            });
            return new CallToolResult();
        }
        catch (Exception ex)
        {
            return BuildCallToolErrorResult(ex);
        }
    }

    [McpServerTool, Description("Sets the CPU register PC (Program Counter)")]
    public static async Task<CallToolResult> SetCPURegisterPC(IHostApp hostApp, ushort value)
    {
        try
        {
            // Safest to run code that uses objects the emulator uses on the UI thread.
            await hostApp.ExternalControlInvokeOnUIThread(async () =>
            {
                AssertC64EmulatorIsRunning(hostApp);
                var cpu = GetC64(hostApp).CPU;
                cpu.PC = value;
            });
            return new CallToolResult();
        }
        catch (Exception ex)
        {
            return BuildCallToolErrorResult(ex);
        }
    }

    [McpServerTool, Description("Sets the CPU register SP (Stack Pointer)")]
    public static async Task<CallToolResult> SetCPURegisterSP(IHostApp hostApp, byte value)
    {
        try
        {
            // Safest to run code that uses objects the emulator uses on the UI thread.
            await hostApp.ExternalControlInvokeOnUIThread(async () =>
            {
                AssertC64EmulatorIsRunning(hostApp);
                var cpu = GetC64(hostApp).CPU;
                cpu.SP = value;
            });
            return new CallToolResult();
        }
        catch (Exception ex)
        {
            return BuildCallToolErrorResult(ex);
        }
    }

    [McpServerTool, Description("Sets the CPU register PS (Processor Status)")]
    public static async Task<CallToolResult> SetCPURegisterPS(IHostApp hostApp, byte value)
    {
        try
        {
            // Safest to run code that uses objects the emulator uses on the UI thread.
            await hostApp.ExternalControlInvokeOnUIThread(async () =>
            {
                AssertC64EmulatorIsRunning(hostApp);
                var cpu = GetC64(hostApp).CPU;
                cpu.ProcessorStatus.Value = value;
            });
            return new CallToolResult();
        }
        catch (Exception ex)
        {
            return BuildCallToolErrorResult(ex);
        }
    }


    private static void AssertC64EmulatorIsRunning(IHostApp hostApp)
    {
        AssertEmulatorIsC64(hostApp);
        if (hostApp.EmulatorState != EmulatorState.Running)
        {
            throw new InvalidOperationException($"C64 emulator is not running. Current state: {hostApp.EmulatorState}");
        }
    }

    private static void AssertC64EmulatorIsRunningOrPaused(IHostApp hostApp)
    {
        AssertEmulatorIsC64(hostApp);
        if (hostApp.EmulatorState != EmulatorState.Running && hostApp.EmulatorState != EmulatorState.Paused)
        {
            throw new InvalidOperationException($"C64 emulator is not running or paused. Current state: {hostApp.EmulatorState}");
        }
    }

    private static void AssertC64EmulatorIsPausedOrUninitialzied(IHostApp hostApp)
    {
        AssertEmulatorIsC64(hostApp);
        if (hostApp.EmulatorState != EmulatorState.Paused && hostApp.EmulatorState != EmulatorState.Uninitialized)
        {
            throw new InvalidOperationException($"C64 emulator not paused uninitialzied. Current state: {hostApp.EmulatorState}");
        }
    }

    private static void AssertC64EmulatorIsUninitialzied(IHostApp hostApp)
    {
        AssertEmulatorIsC64(hostApp);
        if (hostApp.EmulatorState != EmulatorState.Uninitialized)
        {
            throw new InvalidOperationException($"C64 emulator is running or paused. Current state: {hostApp.EmulatorState}");
        }
    }

    private static void AssertEmulatorIsC64(IHostApp hostApp)
    {
        if (hostApp.SelectedSystemName != C64.SystemName)
        {
            throw new InvalidOperationException("Current emulated system is not a C64 instance.");
        }
    }

    private static C64 GetC64(IHostApp hostApp)
    {
        if (hostApp.CurrentRunningSystem is C64 c64)
        {
            return c64;
        }
        throw new InvalidOperationException("Current running system is not a C64 instance.");
    }

    private static CallToolResult BuildCallToolDataResult(object data)
    {
        // NOTE: Doesn't seem to work with returning StructuredContent, it's always empty. 
        //JsonNode? jsonNode = JsonSerializer.SerializeToNode(data, s_jsonSerializerOptions);
        //return new CallToolResult
        //{
        //    StructuredContent = jsonNode
        //};
        Type type = data.GetType();
        bool needsJsonElementName;
        if (type.IsEnum || type == typeof(JsonArray))
        {
            needsJsonElementName = true;
        }
        else if (type.IsArray)
        {
            var elementType = type.GetElementType();
            needsJsonElementName = elementType.IsPrimitive || elementType == typeof(string) || elementType == typeof(decimal);
        }
        else
        {
            needsJsonElementName = type.IsPrimitive || type == typeof(string) || type == typeof(decimal);
        }

        object objectToSerialize;
        if (needsJsonElementName)
        {
            objectToSerialize = new
            {
                Data = data
            };
        }
        else
        {
            objectToSerialize = data;
        }
        string json = JsonSerializer.Serialize(objectToSerialize, s_jsonSerializerOptions);
        return new CallToolResult
        {
            Content = new List<ContentBlock>
                {
                    new TextContentBlock
                    {
                        Text = json
                    }
                }
        };
    }

    //private static CallToolResult BuildCallToolDataResult(JsonNode jsonNode)
    //{
    //    // NOTE: Doesn't seem to work with returning StructuredContent, it's always empty. 
    //    //return new CallToolResult
    //    //{
    //    //    StructuredContent = jsonNode
    //    //};
    //}

    private static CallToolResult BuildCallToolErrorResult(Exception ex)
    {
        return new CallToolResult
        {
            IsError = true,
            Content = new List<ContentBlock>
                {
                    new TextContentBlock
                    {
                        Text = $"C64 emulator error: {ex.Message}"
                    },
                },
        };
    }


    private static readonly JsonSerializerOptions s_jsonSerializerOptions = BuildJsonSerializerOptions();

    private static JsonSerializerOptions BuildJsonSerializerOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            //WriteIndented = true,
            //DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
