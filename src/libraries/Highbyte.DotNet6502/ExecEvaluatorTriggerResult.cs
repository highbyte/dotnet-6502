namespace Highbyte.DotNet6502;

public class ExecEvaluatorTriggerResult
{
    public bool Triggered { get; private set; }
    public string? TriggerDescription { get; private set; }
    public ExecEvaluatorTriggerReasonType? TriggerType { get; private set; }

    private static readonly ExecEvaluatorTriggerResult s_notTriggered = new ExecEvaluatorTriggerResult { Triggered = false };
    public static ExecEvaluatorTriggerResult NotTriggered => s_notTriggered;
    public static ExecEvaluatorTriggerResult CreateTrigger(ExecEvaluatorTriggerReasonType triggerReasonType, string? triggerDescription = null)
    {
        return new ExecEvaluatorTriggerResult
        {
            Triggered = true,
            TriggerDescription = triggerDescription,
            TriggerType = triggerReasonType
        };
    }
}

public enum ExecEvaluatorTriggerReasonType
{
    DebugBreakPoint,
    UnknownInstruction,
    BRKInstruction,
    Other
}
