using System.ComponentModel;
using Highbyte.DotNet6502.Systems;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Highbyte.DotNet6502.Util.MCPServer;

[McpServerToolType]
public static class C64StateTool
{
    [McpServerTool, Description("Get C64 emulator state (Uninitialized, Running, Paused)")]
    public static async Task<CallToolResult> GetState(IHostApp hostApp)
    {
        EmulatorState emulatorState = default;

        try
        {
            C64ToolHelper.AssertEmulatorIsC64(hostApp);

            await hostApp.ExternalControlInvokeOnUIThread(async () =>
            {
                emulatorState = hostApp.EmulatorState;
            });

            return C64ToolHelper.BuildCallToolDataResult(emulatorState);

        }
        catch (Exception ex)
        {
            return C64ToolHelper.BuildCallToolErrorResult(ex);
        }
    }

    [McpServerTool, Description("Starts C64 emulator.")]
    public static async Task<CallToolResult> Start(IHostApp hostApp, StateManager stateManager)
    {
        try
        {
            C64ToolHelper.AssertC64EmulatorIsPausedOrUninitialzied(hostApp);

            // Safest to run code that uses objects the emulator uses on the UI thread.
            await hostApp.ExternalControlInvokeOnUIThread(async () =>
            {
                await hostApp.Start();
            });
            stateManager.EnableMCPControl(hostApp);
            return new CallToolResult();
        }
        catch (Exception ex)
        {
            return C64ToolHelper.BuildCallToolErrorResult(ex);
        }

    }

    [McpServerTool, Description("Pause C64 emulator")]
    public static async Task<CallToolResult> Pause(IHostApp hostApp, StateManager stateManager)
    {
        try
        {
            C64ToolHelper.AssertC64EmulatorIsRunning(hostApp);
            C64ToolHelper.AssertMCPControlEnabled(hostApp, stateManager);

            // Safest to run code that uses objects the emulator uses on the UI thread.
            await hostApp.ExternalControlInvokeOnUIThread(async () =>
            {
                hostApp.Pause();
            });
            return new CallToolResult();
        }
        catch (Exception ex)
        {
            return C64ToolHelper.BuildCallToolErrorResult(ex);
        }
    }

    [McpServerTool, Description("Stop C64 emulator")]
    public static async Task<CallToolResult> Stop(IHostApp hostApp, StateManager stateManager)
    {
        try
        {
            C64ToolHelper.AssertC64EmulatorIsRunningOrPaused(hostApp);
            C64ToolHelper.AssertMCPControlEnabled(hostApp, stateManager);

            // Safest to run code that uses objects the emulator uses on the UI thread.
            await hostApp.ExternalControlInvokeOnUIThread(async () =>
            {
                hostApp.Stop();
            });
            return new CallToolResult();
        }
        catch (Exception ex)
        {
            return C64ToolHelper.BuildCallToolErrorResult(ex);
        }
    }

    [McpServerTool, Description("Runs the C64 emulator for specified number of frames")]
    public static async Task<CallToolResult> RunNumberOfSeconds(IHostApp hostApp, StateManager stateManager, int numberOfSeconds)
    {
        try
        {
            if (numberOfSeconds <= 0)
                throw new ArgumentException("Number of seconds must be greater than zero.", nameof(numberOfSeconds));
            C64ToolHelper.AssertC64EmulatorIsRunning(hostApp);
            C64ToolHelper.AssertMCPControlEnabled(hostApp, stateManager);

            // Safest to run code that uses objects the emulator uses on the UI thread.
            await hostApp.ExternalControlInvokeOnUIThread(async () =>
            {
                var c64 = C64ToolHelper.GetC64(hostApp);
                //var numberOfFrames = numberOfSeconds * c64.Vic2.Vic2Model.??
                int numberOfFrames = (int)(numberOfSeconds * c64.Screen.RefreshFrequencyHz);
                await RunNumberOfFrames(hostApp, stateManager, numberOfFrames);

            });
            return new CallToolResult();
        }
        catch (Exception ex)
        {
            return C64ToolHelper.BuildCallToolErrorResult(ex);
        }
    }

    [McpServerTool, Description("Runs the C64 emulator for specified number of frames")]
    public static async Task<CallToolResult> RunNumberOfFrames(IHostApp hostApp, StateManager stateManager, int numberOfFrames)
    {
        try
        {
            if (numberOfFrames <= 0)
                throw new ArgumentException("Frame count must be greater than zero.", nameof(numberOfFrames));
            C64ToolHelper.AssertC64EmulatorIsRunning(hostApp);
            C64ToolHelper.AssertMCPControlEnabled(hostApp, stateManager);

            // Safest to run code that uses objects the emulator uses on the UI thread.
            await hostApp.ExternalControlInvokeOnUIThread(async () =>
            {
                for (int i = 0; i < numberOfFrames; i++)
                    hostApp.RunEmulatorOneFrame();

            });
            return new CallToolResult();
        }
        catch (Exception ex)
        {
            return C64ToolHelper.BuildCallToolErrorResult(ex);
        }
    }

    [McpServerTool, Description("Runs the C64 emulator for specified number of instructions")]
    public static async Task<CallToolResult> RunNumberOfInstructions(IHostApp hostApp, StateManager stateManager, int numberOfInstructions)
    {
        try
        {
            if (numberOfInstructions <= 0)
                throw new ArgumentException("Instruction count must be greater than zero.", nameof(numberOfInstructions));
            C64ToolHelper.AssertC64EmulatorIsRunning(hostApp);
            C64ToolHelper.AssertMCPControlEnabled(hostApp, stateManager);

            // Safest to run code that uses objects the emulator uses on the UI thread.
            await hostApp.ExternalControlInvokeOnUIThread(async () =>
            {

                for (int i = 0; i < numberOfInstructions; i++)
                    hostApp.CurrentRunningSystem.CPU.ExecuteOneInstruction(hostApp.CurrentRunningSystem.Mem);

            });
            return new CallToolResult();
        }
        catch (Exception ex)
        {
            return C64ToolHelper.BuildCallToolErrorResult(ex);
        }
    }


    [McpServerTool, Description("Checks if C64 emulator has MCP control enabled.")]
    public static async Task<CallToolResult> IsMCPControlEnabled(IHostApp hostApp, StateManager stateManager)
    {
        try
        {
            return C64ToolHelper.BuildCallToolDataResult(stateManager.IsMCPControlEnabled(hostApp));
        }
        catch (Exception ex)
        {
            return C64ToolHelper.BuildCallToolErrorResult(ex);
        }
    }

    [McpServerTool, Description("Enable MCP control of C64 emulator.")]
    public static async Task<CallToolResult> EnableMCPControl(IHostApp hostApp, StateManager stateManager)
    {
        try
        {
            stateManager.EnableMCPControl(hostApp);
            return new CallToolResult();
        }
        catch (Exception ex)
        {
            return C64ToolHelper.BuildCallToolErrorResult(ex);
        }
    }


    [McpServerTool, Description("Disable MCP control of C64 emulator.")]
    public static async Task<CallToolResult> DisableMCPControl(IHostApp hostApp, StateManager stateManager)
    {
        try
        {
            stateManager.DisableMCPControl(hostApp);
            return new CallToolResult();
        }
        catch (Exception ex)
        {
            return C64ToolHelper.BuildCallToolErrorResult(ex);
        }
    }

    [McpServerTool, Description("Check if the CPU in the C64 emulator is paused.")]
    public static async Task<CallToolResult> IsCPUPaused(IHostApp hostApp, StateManager stateManager)
    {
        try
        {
            return C64ToolHelper.BuildCallToolDataResult(stateManager.IsMCPControlEnabled(hostApp) && stateManager.CpuExecutionPaused);
        }
        catch (Exception ex)
        {
            return C64ToolHelper.BuildCallToolErrorResult(ex);
        }
    }

    [McpServerTool, Description("Pause CPU execution in the C64 emulator.")]
    public static async Task<CallToolResult> PauseCPU(IHostApp hostApp, StateManager stateManager)
    {
        try
        {
            C64ToolHelper.AssertMCPControlEnabled(hostApp, stateManager);
            stateManager.PauseCPUExecution();
            return new CallToolResult();
        }
        catch (Exception ex)
        {
            return C64ToolHelper.BuildCallToolErrorResult(ex);
        }
    }

    [McpServerTool, Description("Resume CPU execution in the C64 emulator.")]
    public static async Task<CallToolResult> ResumeCPU(IHostApp hostApp, StateManager stateManager)
    {
        try
        {
            C64ToolHelper.AssertMCPControlEnabled(hostApp, stateManager);
            stateManager.ResumeCPUExecution();
            return new CallToolResult();
        }
        catch (Exception ex)
        {
            return C64ToolHelper.BuildCallToolErrorResult(ex);
        }
    }

    // [McpServerTool, Description("Run CPU in the C64 emulator. Until the next breakpoint or for a max number of frames")]
    // public static async Task<CallToolResult> RunCPU(IHostApp hostApp, StateManager stateManager, int maxNumberOfFrames)
    // {
    //     try
    //     {
    //         C64ToolHelper.AssertMCPControlEnabled(hostApp, stateManager);
    //         hostApp.RunEmulatorOneFrame();
    //         return new CallToolResult();
    //     }
    //     catch (Exception ex)
    //     {
    //         return C64ToolHelper.BuildCallToolErrorResult(ex);
    //     }
    // }
}
