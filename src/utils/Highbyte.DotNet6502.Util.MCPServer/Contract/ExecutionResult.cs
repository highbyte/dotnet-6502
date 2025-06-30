
namespace Highbyte.DotNet6502.Util.MCPServer.Contract;
public class ExecutionResult
{
    public CPURegisters CPURegisters { get; set; }
    public bool ExecutionPauseWasTriggered { get; set; }
    public ExecEvaluatorTriggerReasonType? ExecutionPauseReason { get; set; }
    public string NextInstruction { get; set; }
}
