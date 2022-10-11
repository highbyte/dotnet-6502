namespace Highbyte.DotNet6502.Monitor
{
    public class BreakPointExecEvaluator : IExecEvaluator
    {
        private readonly Dictionary<ushort, BreakPoint> _breakPoints;

        public bool Continue { get; private set; }

        public BreakPointExecEvaluator(Dictionary<ushort, BreakPoint> breakPoints)
        {
            _breakPoints = breakPoints;
        }

        public void Check(ExecState execState, CPU cpu, Memory mem)
        {
            var pc = cpu.PC;
            if (_breakPoints.ContainsKey(pc) && _breakPoints[pc].Enabled)
            {
                Continue = false;
            }
        }

        public void Reset()
        {
            Continue = true;
        }
    }
}