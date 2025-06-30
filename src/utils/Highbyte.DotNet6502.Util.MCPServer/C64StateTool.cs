using System.ComponentModel;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Util.MCPServer.Contract;
using Highbyte.DotNet6502.Utils;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Highbyte.DotNet6502.Util.MCPServer;

[McpServerToolType]
public static class C64StateTool
{
    [McpServerTool, Description("Get C64 emulator state (Uninitialized, Running, Paused)")]
    public static async Task<CallToolResult> GetState(IHostApp hostApp, StateManager stateManager)
    {
        EmulatorState emulatorState = default;
        bool isMCPControlEnabled = false;
        try
        {
            C64ToolHelper.AssertEmulatorIsC64(hostApp);
            await hostApp.ExternalControlInvokeOnUIThread(async () =>
            {
                emulatorState = hostApp.EmulatorState;
                isMCPControlEnabled = stateManager.IsMCPControlEnabled(hostApp);
            });
            return C64ToolHelper.BuildCallToolDataResult(
                new
                {
                    emulatorState = emulatorState,
                    isMCPControlEnabled = stateManager.IsMCPControlEnabled(hostApp),
                    IsCPUPaused = stateManager.IsCpuExecutionPaused
                });

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
            if (hostApp.EmulatorState == EmulatorState.Running)
                return C64ToolHelper.BuildCallToolDataResult("C64 emulator is already running.");

            // Safest to run code that uses objects the emulator uses on the UI thread.
            await hostApp.ExternalControlInvokeOnUIThread(async () =>
            {
                await hostApp.Start();
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

    [McpServerTool, Description("Runs the C64 emulator for specified number of seconds")]
    public static async Task<CallToolResult> RunNumberOfSeconds(IHostApp hostApp, StateManager stateManager, int numberOfSeconds)
    {
        try
        {
            if (numberOfSeconds <= 0)
                throw new ArgumentException("Number of seconds must be greater than zero.", nameof(numberOfSeconds));
            C64ToolHelper.AssertC64EmulatorIsRunning(hostApp);
            C64ToolHelper.AssertMCPControlEnabled(hostApp, stateManager);

            ExecutionResult executionResult = new ExecutionResult
            {
                ExecutionPauseWasTriggered = false,
                ExecutionPauseReason = null
            };

            // Safest to run code that uses objects the emulator uses on the UI thread.
            await hostApp.ExternalControlInvokeOnUIThread(async () =>
            {
                var c64 = C64ToolHelper.GetC64(hostApp);
                //var numberOfFrames = numberOfSeconds * c64.Vic2.Vic2Model.??
                int numberOfFrames = (int)(numberOfSeconds * c64.Screen.RefreshFrequencyHz);
                for (int i = 0; i < numberOfFrames; i++)
                {
                    var execEvaluatorTriggerResult = hostApp.RunEmulatorOneFrame();
                    if (execEvaluatorTriggerResult.Triggered)
                    {
                        executionResult.ExecutionPauseWasTriggered = execEvaluatorTriggerResult.Triggered;
                        executionResult.ExecutionPauseReason = execEvaluatorTriggerResult.TriggerType;
                        break;
                    }
                }

                executionResult.NextInstruction = OutputGen.GetNextInstructionDisassembly(c64.CPU, c64.Mem);
            });

            return C64ToolHelper.BuildCallToolDataResult(executionResult);
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

            ExecutionResult executionResult = new ExecutionResult
            {
                ExecutionPauseWasTriggered = false,
                ExecutionPauseReason = null
            };

            // Safest to run code that uses objects the emulator uses on the UI thread.
            await hostApp.ExternalControlInvokeOnUIThread(async () =>
            {
                for (int i = 0; i < numberOfFrames; i++)
                {
                    var execEvaluatorTriggerResult = hostApp.RunEmulatorOneFrame();
                    if (execEvaluatorTriggerResult.Triggered)
                    {
                        executionResult.ExecutionPauseWasTriggered = execEvaluatorTriggerResult.Triggered;
                        executionResult.ExecutionPauseReason = execEvaluatorTriggerResult.TriggerType;
                        break;
                    }
                }

                var c64 = C64ToolHelper.GetC64(hostApp);
                executionResult.NextInstruction = OutputGen.GetNextInstructionDisassembly(c64.CPU, c64.Mem);
            });

            return C64ToolHelper.BuildCallToolDataResult(executionResult);
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

            ExecutionResult executionResult = new ExecutionResult
            {
                ExecutionPauseWasTriggered = false,
                ExecutionPauseReason = null
            };

            // Safest to run code that uses objects the emulator uses on the UI thread.
            await hostApp.ExternalControlInvokeOnUIThread(async () =>
            {
                for (int i = 0; i < numberOfInstructions; i++)
                {
                    var execEvaluatorTriggerResult = hostApp.CurrentSystemRunner.RunEmulatorOneInstruction();
                    if (execEvaluatorTriggerResult.Triggered)
                    {
                        executionResult.ExecutionPauseWasTriggered = execEvaluatorTriggerResult.Triggered;
                        executionResult.ExecutionPauseReason = execEvaluatorTriggerResult.TriggerType;
                        break;
                    }
                }

                var c64 = C64ToolHelper.GetC64(hostApp);
                executionResult.NextInstruction = OutputGen.GetNextInstructionDisassembly(c64.CPU, c64.Mem);
            });
            return C64ToolHelper.BuildCallToolDataResult(executionResult);
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

            ExecutionResult executionResult = new ExecutionResult
            {
                ExecutionPauseWasTriggered = false,
                ExecutionPauseReason = null,
            };

            // Safest to run code that uses objects the emulator uses on the UI thread.
            await hostApp.ExternalControlInvokeOnUIThread(async () =>
            {
                var c64 = C64ToolHelper.GetC64(hostApp);
                executionResult.NextInstruction = OutputGen.GetNextInstructionDisassembly(c64.CPU, c64.Mem);
            });

            return C64ToolHelper.BuildCallToolDataResult(executionResult);
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

    //[McpServerTool, Description("Check if the CPU in the C64 emulator is paused.")]
    //public static async Task<CallToolResult> IsCPUPaused(IHostApp hostApp, StateManager stateManager)
    //{
    //    try
    //    {
    //        return C64ToolHelper.BuildCallToolDataResult(stateManager.IsMCPControlEnabled(hostApp) && stateManager.IsCpuExecutionPaused);
    //    }
    //    catch (Exception ex)
    //    {
    //        return C64ToolHelper.BuildCallToolErrorResult(ex);
    //    }
    //}
}
