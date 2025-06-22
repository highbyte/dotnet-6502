using System.ComponentModel;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Util.MCPServer.Contract;
using Highbyte.DotNet6502.Utils;
using ModelContextProtocol.Server;

namespace Highbyte.DotNet6502.Util.MCPServer;

[McpServerToolType]
public static class C64Tool
{
    [McpServerTool, Description("Get C64 emulator state (Uninitialized, Running, Paused)")]
    public static async Task<EmulatorState> GetState(IHostApp hostApp)
    {
        EmulatorState emulatorState = default;

        await hostApp.ExternalControlInvokeOnUIThread(() =>
        {
            AssertEmulatorIsC64(hostApp);
            emulatorState = hostApp.EmulatorState;
        });

        return emulatorState;
    }

    [McpServerTool, Description("Starts C64 emulator.")]
    public static async Task Start(IHostApp hostApp)
    {
        // Safest to run code that uses objects the emulator uses on the UI thread.
        await hostApp.ExternalControlInvokeOnUIThread(async () =>
        {
            AssertC64EmulatorIsPausedOrUninitialzied(hostApp);
            await hostApp.Start();
        });
    }

    [McpServerTool, Description("Pause C64 emulator")]
    public static async Task Pause(IHostApp hostApp)
    {
        // Safest to run code that uses objects the emulator uses on the UI thread.
        await hostApp.ExternalControlInvokeOnUIThread(async () =>
        {
            AssertC64EmulatorIsRunning(hostApp);
            hostApp.Pause();
        });
    }

    [McpServerTool, Description("Stop C64 emulator")]
    public static async Task Stop(IHostApp hostApp)
    {
        // Safest to run code that uses objects the emulator uses on the UI thread.
        await hostApp.ExternalControlInvokeOnUIThread(async () =>
        {
            AssertC64EmulatorIsRunningOrPaused(hostApp);
            hostApp.Stop();
        });
    }

    [McpServerTool, Description("Runs the C64 emulator for specified number of frames")]
    public static async Task RunNumberOfSeconds(IHostApp hostApp, int numberOfSeconds)
    {
        if (numberOfSeconds <= 0)
            throw new ArgumentException("Number of seconds must be greater than zero.", nameof(numberOfSeconds));

        // Safest to run code that uses objects the emulator uses on the UI thread.
        await hostApp.ExternalControlInvokeOnUIThread(async () =>
        {
            AssertC64EmulatorIsRunning(hostApp);
            var c64 = GetC64(hostApp);
            //var numberOfFrames = numberOfSeconds * c64.Vic2.Vic2Model.??
            int numberOfFrames = (int)(numberOfSeconds * c64.Screen.RefreshFrequencyHz);
            await RunNumberOfFrames(hostApp, numberOfFrames);
        });
    }

    [McpServerTool, Description("Runs the C64 emulator for specified number of frames")]
    public static async Task RunNumberOfFrames(IHostApp hostApp, int numberOfFrames)
    {
        if (numberOfFrames <= 0)
            throw new ArgumentException("Frame count must be greater than zero.", nameof(numberOfFrames));

        // Safest to run code that uses objects the emulator uses on the UI thread.
        await hostApp.ExternalControlInvokeOnUIThread(async () =>
        {
            AssertC64EmulatorIsRunning(hostApp);

            for (int i = 0; i < numberOfFrames; i++)
                hostApp.RunEmulatorOneFrame();
        });
    }

    [McpServerTool, Description("Runs the C64 emulator for specified number of instructions")]
    public static async Task RunNumberOfInstructions(IHostApp hostApp, int numberOfInstructions)
    {
        if (numberOfInstructions <= 0)
            throw new ArgumentException("Instruction count must be greater than zero.", nameof(numberOfInstructions));

        // Safest to run code that uses objects the emulator uses on the UI thread.
        await hostApp.ExternalControlInvokeOnUIThread(async () =>
        {
            AssertC64EmulatorIsRunning(hostApp);

            for (int i = 0; i < numberOfInstructions; i++)
                hostApp.CurrentRunningSystem.CPU.ExecuteOneInstruction(hostApp.CurrentRunningSystem.Mem);
        });
    }

    [McpServerTool, Description("Returns value of specified memory address in C64 emulator")]
    public static async Task<byte> ReadMemory(IHostApp hostApp, ushort address)
    {
        byte value = 0;
        // Safest to run code that uses objects the emulator uses on the UI thread.
        await hostApp.ExternalControlInvokeOnUIThread(() =>
        {
            AssertC64EmulatorIsRunning(hostApp);
            var c64 = GetC64(hostApp);
            value = c64.Mem[address];
        });
        return value;
    }

    [McpServerTool, Description("Returns a range of values from memory start address up to specified length in C64 emulator")]
    public static async Task<byte[]> ReadMemoryRange(IHostApp hostApp, ushort startAddress, ushort length)
    {
        if (length <= 0)
            throw new ArgumentException("Length must be greater than zero.", nameof(length));

        byte[] values = null!;
        await hostApp.ExternalControlInvokeOnUIThread(() =>
        {
            AssertC64EmulatorIsRunning(hostApp);
            var c64 = GetC64(hostApp);
            if (startAddress + length > c64.Mem.Size)
                length = (ushort)(c64.Mem.Size - startAddress);
            values = c64.Mem.ReadData(startAddress, length);
        });
        return values;

   }

    [McpServerTool, Description("Writes value at specified memory address in C64 emulator")]
    public static async Task WriteMemory(IHostApp hostApp, ushort address, byte value)
    {
        await hostApp.ExternalControlInvokeOnUIThread(() =>
        {
            AssertC64EmulatorIsRunning(hostApp);
            var c64 = GetC64(hostApp);
            c64.Mem[address] = value;
        });
    }

    /// <summary>
    /// </summary>
    /// <param name="hostApp"></param>
    /// <param name="address"></param>
    /// <param name="values">Array of bytes to write to memory></param>
    [McpServerTool, Description("Writes a range of values (byte array) starting at specified memory address in C64 emulator. Expects 'values' as an array of integers.")]
    public static async Task WriteMemoryRange(IHostApp hostApp, ushort address, byte[] values)
    {
        await hostApp.ExternalControlInvokeOnUIThread(() =>
        {
            AssertC64EmulatorIsRunning(hostApp);
            var c64 = GetC64(hostApp);
            if (address + values.Length > c64.Mem.Size)
                values = values.Take(c64.Mem.Size - address).ToArray();
            c64.Mem.StoreData(address, values);
        });
    }

    /// <summary>
    /// </summary>
    /// <param name="hostApp"></param>
    /// <param name="address"></param>
    /// <param name="hexValues">Sequence of 8-bit bytes in hex separated by space to write to memory></param>
    [McpServerTool, Description("Writes a range of values starting at specified memory address in C64 emulator.")]
    public static async Task WriteMemoryRangeAsHexString(IHostApp hostApp, ushort address, string hexValues)
    {
        // Convert hex string to byte array
        if (string.IsNullOrWhiteSpace(hexValues))
            throw new ArgumentException("Hex values cannot be null or empty.", nameof(hexValues));
        var values = hexValues
            .Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(hex => Convert.ToByte(hex, 16))
            .ToArray();

        await WriteMemoryRange(hostApp, address, values);
    }


    [McpServerTool, Description("Returns the current value of the C64 CPU registers: A, X, Y, PS, PC, SP")]
    public static async Task<CPURegisters> GetCPURegisters(IHostApp hostApp)
    {
        CPURegisters cpuRegisters = null!;
        await hostApp.ExternalControlInvokeOnUIThread(() =>
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
        return cpuRegisters;
    }

    [McpServerTool, Description("Sets the CPU register A")]
    public static async Task SetCPURegisterA(IHostApp hostApp, byte value)
    {
        await hostApp.ExternalControlInvokeOnUIThread(() =>
        {
            AssertC64EmulatorIsRunning(hostApp);
            var cpu = GetC64(hostApp).CPU;
            cpu.A = value;
        });
    }

    [McpServerTool, Description("Sets the CPU register X")]
    public static async Task SetCPURegisterX(IHostApp hostApp, byte value)
    {
        await hostApp.ExternalControlInvokeOnUIThread(() =>
        {
            AssertC64EmulatorIsRunning(hostApp);
            var cpu = GetC64(hostApp).CPU;
            cpu.X = value;
        });
    }

    [McpServerTool, Description("Sets the CPU register Y")]
    public static async Task SetCPURegisterY(IHostApp hostApp, byte value)
    {
        await hostApp.ExternalControlInvokeOnUIThread(() =>
        {
            AssertC64EmulatorIsRunning(hostApp);
            var cpu = GetC64(hostApp).CPU;
            cpu.Y = value;
        });
    }

    [McpServerTool, Description("Sets the CPU register PC (Program Counter)")]
    public static async Task SetCPURegisterPC(IHostApp hostApp, ushort value)
    {
        await hostApp.ExternalControlInvokeOnUIThread(() =>
        {
            AssertC64EmulatorIsRunning(hostApp);
            var cpu = GetC64(hostApp).CPU;
            cpu.PC = value;
        });
    }

    [McpServerTool, Description("Sets the CPU register SP (Stack Pointer)")]
    public static async Task SetCPURegisterSP(IHostApp hostApp, byte value)
    {
        await hostApp.ExternalControlInvokeOnUIThread(() =>
        {
            AssertC64EmulatorIsRunning(hostApp);
            var cpu = GetC64(hostApp).CPU;
            cpu.SP = value;
        });
    }

    [McpServerTool, Description("Sets the CPU register PS (Processor Status)")]
    public static async Task SetCPURegisterPS(IHostApp hostApp, byte value)
    {
        await hostApp.ExternalControlInvokeOnUIThread(() =>
        {
            AssertC64EmulatorIsRunning(hostApp);
            var cpu = GetC64(hostApp).CPU;
            cpu.ProcessorStatus.Value = value;
        });
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
}
