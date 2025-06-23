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
            await hostApp.ExternalControlInvokeOnUIThread(async () =>
            {
                C64ToolHelper.AssertEmulatorIsC64(hostApp);
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
    public static async Task<CallToolResult> Start(IHostApp hostApp)
    {
        try
        {
            // Safest to run code that uses objects the emulator uses on the UI thread.
            await hostApp.ExternalControlInvokeOnUIThread(async () =>
            {
                C64ToolHelper.AssertC64EmulatorIsPausedOrUninitialzied(hostApp);
                await hostApp.Start();
            });
            return new CallToolResult();
        }
        catch (Exception ex)
        {
            return C64ToolHelper.BuildCallToolErrorResult(ex);
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
                C64ToolHelper.AssertC64EmulatorIsRunning(hostApp);
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
    public static async Task<CallToolResult> Stop(IHostApp hostApp)
    {
        try
        {
            // Safest to run code that uses objects the emulator uses on the UI thread.
            await hostApp.ExternalControlInvokeOnUIThread(async () =>
            {
                C64ToolHelper.AssertC64EmulatorIsRunningOrPaused(hostApp);
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
    public static async Task<CallToolResult> RunNumberOfSeconds(IHostApp hostApp, int numberOfSeconds)
    {
        try
        {
            // Safest to run code that uses objects the emulator uses on the UI thread.
            await hostApp.ExternalControlInvokeOnUIThread(async () =>
            {
                if (numberOfSeconds <= 0)
                    throw new ArgumentException("Number of seconds must be greater than zero.", nameof(numberOfSeconds));

                C64ToolHelper.AssertC64EmulatorIsRunning(hostApp);
                var c64 = C64ToolHelper.GetC64(hostApp);
                //var numberOfFrames = numberOfSeconds * c64.Vic2.Vic2Model.??
                int numberOfFrames = (int)(numberOfSeconds * c64.Screen.RefreshFrequencyHz);
                await RunNumberOfFrames(hostApp, numberOfFrames);

            });
            return new CallToolResult();
        }
        catch (Exception ex)
        {
            return C64ToolHelper.BuildCallToolErrorResult(ex);
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

                C64ToolHelper.AssertC64EmulatorIsRunning(hostApp);

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
    public static async Task<CallToolResult> RunNumberOfInstructions(IHostApp hostApp, int numberOfInstructions)
    {
        try
        {
            // Safest to run code that uses objects the emulator uses on the UI thread.
            await hostApp.ExternalControlInvokeOnUIThread(async () =>
            {
                if (numberOfInstructions <= 0)
                    throw new ArgumentException("Instruction count must be greater than zero.", nameof(numberOfInstructions));

                C64ToolHelper.AssertC64EmulatorIsRunning(hostApp);

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
}
