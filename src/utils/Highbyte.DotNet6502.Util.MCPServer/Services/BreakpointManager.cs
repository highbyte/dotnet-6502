using Highbyte.DotNet6502.Monitor;
using Highbyte.DotNet6502.Systems;

public class BreakpointManager
{
    private readonly Dictionary<ushort, BreakPoint> _breakPoints = new();

    public Dictionary<ushort, BreakPoint> BreakPoints => _breakPoints;

    public void EnableBreakpointHandling(SystemRunner systemRunner)
    {

    }

    public void DisableBreakpointHandling(SystemRunner systemRunner)
    {
        // Implementation to disable breakpoint handling if needed
    }

    public void AddBreakpoint(ushort address)
    {
        if (!_breakPoints.ContainsKey(address))
            _breakPoints.Add(address, new BreakPoint { Enabled = true });
        else
            _breakPoints[address].Enabled = true;
    }

    public void RemoveBreakpoint(ushort address)
    {
        if (_breakPoints.ContainsKey(address))
            _breakPoints.Remove(address);
    }

    public void RemoveAllBreakpoints()
    {
        _breakPoints.Clear();
    }
}