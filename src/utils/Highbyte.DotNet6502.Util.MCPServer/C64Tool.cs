using System.ComponentModel;
using Highbyte.DotNet6502;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Util.MCPServer.Contract;
using Highbyte.DotNet6502.Util.MCPServer.Emulator;
using Highbyte.DotNet6502.Utils;
using ModelContextProtocol.Server;

[McpServerToolType]
public static class C64Tool
{
    [McpServerTool, Description("Get C64 emulator state (Uninitialized, Running, Paused)")]
    public static EmulatorState GetState(EmbeddedMCPHostApp hostApp)
    {
        AssertEmulatorIsC64(hostApp);
        return hostApp.EmulatorState;
    }

    [McpServerTool, Description("Starts C64 emulator.")]
    public static async Task Start(EmbeddedMCPHostApp hostApp)
    {
        AssertC64EmulatorIsPausedOrUninitialzied(hostApp);
        await hostApp.Start();
    }

    [McpServerTool, Description("Pause C64 emulator")]
    public static async Task Pause(EmbeddedMCPHostApp hostApp)
    {
        AssertC64EmulatorIsRunning(hostApp);
        hostApp.Pause();
    }

    [McpServerTool, Description("Stop C64 emulator")]
    public static async Task Stop(EmbeddedMCPHostApp hostApp)
    {
        AssertC64EmulatorIsRunningOrPaused(hostApp);
        hostApp.Stop();
    }

    [McpServerTool, Description("Runs the C64 emulator for specified number of frames")]
    public static async Task RunNumberOfSeconds(EmbeddedMCPHostApp hostApp, int numberOfSeconds)
    {
        AssertC64EmulatorIsRunning(hostApp);

        if (numberOfSeconds <= 0)
            throw new ArgumentException("Number of seconds must be greater than zero.", nameof(numberOfSeconds));

        var c64 = GetC64(hostApp);
        //var numberOfFrames = numberOfSeconds * c64.Vic2.Vic2Model.??
        int numberOfFrames = (int)(numberOfSeconds * c64.Screen.RefreshFrequencyHz);
        await RunNumberOfFrames(hostApp, numberOfFrames);
    }

    [McpServerTool, Description("Runs the C64 emulator for specified number of frames")]
    public static async Task RunNumberOfFrames(EmbeddedMCPHostApp hostApp, int numberOfFrames)
    {
        AssertC64EmulatorIsRunning(hostApp);

        if (numberOfFrames <= 0)
            throw new ArgumentException("Frame count must be greater than zero.", nameof(numberOfFrames));
        for (int i = 0; i < numberOfFrames; i++)
            hostApp.RunEmulatorOneFrame();
    }

    [McpServerTool, Description("Runs the C64 emulator for specified number of instructions")]
    public static async Task RunNumberOfInstructions(EmbeddedMCPHostApp hostApp, int numberOfInstructions)
    {
        AssertC64EmulatorIsRunning(hostApp);

        if (numberOfInstructions <= 0)
            throw new ArgumentException("Instruction count must be greater than zero.", nameof(numberOfInstructions));
        for (int i = 0; i < numberOfInstructions; i++)
            hostApp.CurrentRunningSystem.CPU.ExecuteOneInstruction(hostApp.CurrentRunningSystem.Mem);
    }

    [McpServerTool, Description("Returns value of specified memory address in C64 emulator")]
    public static byte ReadMemory(EmbeddedMCPHostApp hostApp, ushort address)
    {
        AssertC64EmulatorIsRunning(hostApp);
        var c64 = GetC64(hostApp);
        var value = c64.Mem[address];
        return value;
    }

    [McpServerTool, Description("Returns array of values of from memory start address up to specified length in C64 emulator")]
    public static byte[] ReadMemoryRange(EmbeddedMCPHostApp hostApp, ushort startAddress, ushort length)
    {
        AssertC64EmulatorIsRunning(hostApp);
        var c64 = GetC64(hostApp);

        if (length <= 0)
            throw new ArgumentException("Length must be greater than zero.", nameof(length));
        if (startAddress + length > c64.Mem.Size)
            length = (ushort)(c64.Mem.Size - startAddress);

        var values = c64.Mem.ReadData(startAddress, length);
        return values;
    }

    [McpServerTool, Description("Sets value at specified memory address in C64 emulator")]
    public static void WriteMemory(EmbeddedMCPHostApp hostApp, ushort address, byte value)
    {
        AssertC64EmulatorIsRunning(hostApp);
        var c64 = GetC64(hostApp);
        c64.Mem[address] = value;
    }

    [McpServerTool, Description("Sets array of values starting at specified memory address in C64 emulator. Use this if multiple values need to be set in sequence.")]
    public static void WriteMemory(EmbeddedMCPHostApp hostApp, ushort address, byte[] values)
    {
        AssertC64EmulatorIsRunning(hostApp);
        var c64 = GetC64(hostApp);
        if (address + values.Length > c64.Mem.Size)
            values = values.Take(c64.Mem.Size - address).ToArray();
        c64.Mem.StoreData(address, values);
    }

    [McpServerTool, Description("Returns the current value of the C64 CPU registers: A, X, Y, PS, PC, SP")]
    public static CPURegisters GetCPURegisters(EmbeddedMCPHostApp hostApp)
    {
        AssertC64EmulatorIsRunning(hostApp);
        var cpu = GetC64(hostApp).CPU;

        return new CPURegisters
        {
            A = cpu.A,
            X = cpu.X,
            Y = cpu.Y,
            PC = cpu.PC,
            SP = cpu.SP,
            ProcessorStatus = new ProcessorStatus(cpu.ProcessorStatus.Value)
        };
    }

    [McpServerTool, Description("Sets the CPU register A")]
    public static void SetCPURegisterA(EmbeddedMCPHostApp hostApp, byte value)
    {
        AssertC64EmulatorIsRunning(hostApp);
        var cpu = GetC64(hostApp).CPU;
        cpu.A = value;
    }

    [McpServerTool, Description("Sets the CPU register X")]
    public static void SetCPURegisterX(EmbeddedMCPHostApp hostApp, byte value)
    {
        AssertC64EmulatorIsRunning(hostApp);
        var cpu = GetC64(hostApp).CPU;
        cpu.X = value;
    }

    [McpServerTool, Description("Sets the CPU register Y")]
    public static void SetCPURegisterY(EmbeddedMCPHostApp hostApp, byte value)
    {
        AssertC64EmulatorIsRunning(hostApp);
        var cpu = GetC64(hostApp).CPU;
        cpu.Y = value;
    }

    [McpServerTool, Description("Sets the CPU register PC (Program Counter)")]
    public static void SetCPURegisterPC(EmbeddedMCPHostApp hostApp, ushort value)
    {
        AssertC64EmulatorIsRunning(hostApp);
        var cpu = GetC64(hostApp).CPU;
        cpu.PC = value;
    }

    [McpServerTool, Description("Sets the CPU register SP (Stack Pointer)")]
    public static void SetCPURegisterSP(EmbeddedMCPHostApp hostApp, byte value)
    {
        AssertC64EmulatorIsRunning(hostApp);
        var cpu = GetC64(hostApp).CPU;
        cpu.SP = value;
    }

    [McpServerTool, Description("Sets the CPU register PS (Processor Status)")]
    public static void SetCPURegisterPS(EmbeddedMCPHostApp hostApp, byte value)
    {
        AssertC64EmulatorIsRunning(hostApp);
        var cpu = GetC64(hostApp).CPU;
        cpu.ProcessorStatus.Value = value;
    }

    private static void AssertC64EmulatorIsRunning(EmbeddedMCPHostApp hostApp)
    {
        AssertEmulatorIsC64(hostApp);
        if (hostApp.EmulatorState != EmulatorState.Running)
        {
            throw new InvalidOperationException($"C64 emulator is not running. Current state: {hostApp.EmulatorState}");
        }
    }

    private static void AssertC64EmulatorIsRunningOrPaused(EmbeddedMCPHostApp hostApp)
    {
        AssertEmulatorIsC64(hostApp);
        if (hostApp.EmulatorState != EmulatorState.Running && hostApp.EmulatorState != EmulatorState.Paused)
        {
            throw new InvalidOperationException($"C64 emulator is not running or paused. Current state: {hostApp.EmulatorState}");
        }
    }

    private static void AssertC64EmulatorIsPausedOrUninitialzied(EmbeddedMCPHostApp hostApp)
    {
        AssertEmulatorIsC64(hostApp);
        if (hostApp.EmulatorState != EmulatorState.Paused && hostApp.EmulatorState != EmulatorState.Uninitialized)
        {
            throw new InvalidOperationException($"C64 emulator not paused uninitialzied. Current state: {hostApp.EmulatorState}");
        }
    }

    private static void AssertC64EmulatorIsUninitialzied(EmbeddedMCPHostApp hostApp)
    {
        AssertEmulatorIsC64(hostApp);
        if (hostApp.EmulatorState != EmulatorState.Uninitialized)
        {
            throw new InvalidOperationException($"C64 emulator is running or paused. Current state: {hostApp.EmulatorState}");
        }
    }

    private static void AssertEmulatorIsC64(EmbeddedMCPHostApp hostApp)
    {
        if (hostApp.SelectedSystemName != C64.SystemName)
        {
            throw new InvalidOperationException("Current emulated system is not a C64 instance.");
        }
    }

    private static C64 GetC64(EmbeddedMCPHostApp hostApp)
    {
        if (hostApp.CurrentRunningSystem is C64 c64)
        {
            return c64;
        }
        throw new InvalidOperationException("Current running system is not a C64 instance.");
    }
}
