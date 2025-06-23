using System.ComponentModel;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Monitor;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Highbyte.DotNet6502.Util.MCPServer;

[McpServerToolType]
public static class C64BreakpointTool
{
    [McpServerTool, Description("List all breakpoints in the C64 emulator.")]
    public static async Task<CallToolResult> ListBreakpoints(IHostApp hostApp, BreakpointManager breakpointManager)
    {
        var breakpoints = breakpointManager.BreakPoints;
        var result = new List<object>();
        foreach (var bp in breakpoints)
        {
            result.Add(new { Address = bp.Key, Enabled = bp.Value.Enabled });
        }
        return C64ToolHelper.BuildCallToolDataResult(result);
    }

    [McpServerTool, Description("Add a breakpoint at the specified address.")]
    public static async Task<CallToolResult> AddBreakpoint(IHostApp hostApp, BreakpointManager breakpointManager, ushort address)
    {
        var breakpoints = breakpointManager.BreakPoints;
        if (!breakpoints.ContainsKey(address))
            breakpoints.Add(address, new BreakPoint { Enabled = true });
        else
            breakpoints[address].Enabled = true;

        EnableOrDisableBreakpointHandling(hostApp, breakpointManager);            
        return C64ToolHelper.BuildCallToolDataResult(new { Address = address, Enabled = true });
    }

    [McpServerTool, Description("Remove a breakpoint at the specified address.")]
    public static async Task<CallToolResult> RemoveBreakpoint(IHostApp hostApp, BreakpointManager breakpointManager, ushort address)
    {
        var breakpoints = breakpointManager.BreakPoints;
        if (breakpoints.ContainsKey(address))
            breakpoints.Remove(address);

        EnableOrDisableBreakpointHandling(hostApp, breakpointManager);
        return C64ToolHelper.BuildCallToolDataResult(new { Address = address, Removed = true });
    }

    [McpServerTool, Description("Remove all breakpoints.")]
    public static async Task<CallToolResult> RemoveAllBreakpoints(IHostApp hostApp, BreakpointManager breakpointManager)
    {
        var breakpoints = breakpointManager.BreakPoints;
        breakpoints.Clear();
        EnableOrDisableBreakpointHandling(hostApp, breakpointManager);
        return C64ToolHelper.BuildCallToolDataResult(new { RemovedAll = true });
    }

    private static void EnableOrDisableBreakpointHandling(IHostApp hostApp, BreakpointManager breakpointManager)
    {
        if (hostApp.CurrentSystemRunner == null)
        {
            return; // No system runner available, nothing to do.
        }
        if (breakpointManager.BreakPoints.Count == 0)
        {
            breakpointManager.DisableBreakpointHandling(hostApp.CurrentSystemRunner);
        }
        else
        {
            breakpointManager.EnableBreakpointHandling(hostApp.CurrentSystemRunner);
        }
    }
}
