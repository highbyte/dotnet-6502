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
        try
        {
            var breakpoints = breakpointManager.BreakPoints;
            var result = new List<object>();
            foreach (var bp in breakpoints)
            {
                result.Add(new { Address = bp.Key, Enabled = bp.Value.Enabled });
            }
            return C64ToolHelper.BuildCallToolDataResult(result);
        }
        catch (Exception ex)
        {
            return C64ToolHelper.BuildCallToolErrorResult(ex);
        }
    }

    [McpServerTool, Description("Add a breakpoint at the specified address.")]
    public static async Task<CallToolResult> AddBreakpoint(IHostApp hostApp, BreakpointManager breakpointManager, ushort address)
    {
        try
        {
            var breakpoints = breakpointManager.BreakPoints;
            if (!breakpoints.ContainsKey(address))
                breakpoints.Add(address, new BreakPoint { Enabled = true });
            else
                breakpoints[address].Enabled = true;
            return new CallToolResult();
        }
        catch (Exception ex)
        {
            return C64ToolHelper.BuildCallToolErrorResult(ex);
        }
    }

    [McpServerTool, Description("Remove a breakpoint at the specified address.")]
    public static async Task<CallToolResult> RemoveBreakpoint(IHostApp hostApp, BreakpointManager breakpointManager, ushort address)
    {
        try
        {
            var breakpoints = breakpointManager.BreakPoints;
            if (breakpoints.ContainsKey(address))
                breakpoints.Remove(address);
            return new CallToolResult();
        }
        catch (Exception ex)
        {
            return C64ToolHelper.BuildCallToolErrorResult(ex);
        }
    }

    [McpServerTool, Description("Remove all breakpoints.")]
    public static async Task<CallToolResult> RemoveAllBreakpoints(IHostApp hostApp, BreakpointManager breakpointManager)
    {
        try
        {
            var breakpoints = breakpointManager.BreakPoints;
            breakpoints.Clear();
            return new CallToolResult();
        }
        catch (Exception ex)
        {
            return C64ToolHelper.BuildCallToolErrorResult(ex);
        }
    }
}