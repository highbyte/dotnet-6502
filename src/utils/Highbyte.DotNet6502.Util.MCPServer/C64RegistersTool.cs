using System.ComponentModel;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Util.MCPServer.Contract;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Highbyte.DotNet6502.Util.MCPServer;

[McpServerToolType]
public static class C64RegistersTool
{
    [McpServerTool, Description("Returns the current value of the C64 CPU registers: A, X, Y, PS, PC, SP")]
    public static async Task<CallToolResult> GetCPURegisters(IHostApp hostApp)
    {
        try
        {
            CPURegisters cpuRegisters = null!;
            // Safest to run code that uses objects the emulator uses on the UI thread.
            await hostApp.ExternalControlInvokeOnUIThread(async () =>
            {
                C64ToolHelper.AssertC64EmulatorIsRunning(hostApp);
                var cpu = C64ToolHelper.GetC64(hostApp).CPU;
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
            return C64ToolHelper.BuildCallToolDataResult(cpuRegisters);
        }
        catch (Exception ex)
        {
            return C64ToolHelper.BuildCallToolErrorResult(ex);
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
                C64ToolHelper.AssertC64EmulatorIsRunning(hostApp);
                var cpu = C64ToolHelper.GetC64(hostApp).CPU;
                cpu.A = value;
            });
            return new CallToolResult();
        }
        catch (Exception ex)
        {
            return C64ToolHelper.BuildCallToolErrorResult(ex);
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
                C64ToolHelper.AssertC64EmulatorIsRunning(hostApp);
                var cpu = C64ToolHelper.GetC64(hostApp).CPU;
                cpu.X = value;
            });
            return new CallToolResult();
        }
        catch (Exception ex)
        {
            return C64ToolHelper.BuildCallToolErrorResult(ex);
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
                C64ToolHelper.AssertC64EmulatorIsRunning(hostApp);
                var cpu = C64ToolHelper.GetC64(hostApp).CPU;
                cpu.Y = value;
            });
            return new CallToolResult();
        }
        catch (Exception ex)
        {
            return C64ToolHelper.BuildCallToolErrorResult(ex);
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
                C64ToolHelper.AssertC64EmulatorIsRunning(hostApp);
                var cpu = C64ToolHelper.GetC64(hostApp).CPU;
                cpu.PC = value;
            });
            return new CallToolResult();
        }
        catch (Exception ex)
        {
            return C64ToolHelper.BuildCallToolErrorResult(ex);
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
                C64ToolHelper.AssertC64EmulatorIsRunning(hostApp);
                var cpu = C64ToolHelper.GetC64(hostApp).CPU;
                cpu.SP = value;
            });
            return new CallToolResult();
        }
        catch (Exception ex)
        {
            return C64ToolHelper.BuildCallToolErrorResult(ex);
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
                C64ToolHelper.AssertC64EmulatorIsRunning(hostApp);
                var cpu = C64ToolHelper.GetC64(hostApp).CPU;
                cpu.ProcessorStatus.Value = value;
            });
            return new CallToolResult();
        }
        catch (Exception ex)
        {
            return C64ToolHelper.BuildCallToolErrorResult(ex);
        }
    }
}
