namespace Highbyte.DotNet6502.DebugAdapter.Protocol;

// DAP Protocol Messages - Simplified subset for MVP

public record InitializeRequest(string adapterID, string clientID, string clientName);

public record InitializeResponse(
    bool supportsConfigurationDoneRequest,
    bool supportsFunctionBreakpoints,
    bool supportsConditionalBreakpoints,
    bool supportsHitConditionalBreakpoints,
    bool supportsEvaluateForHovers,
    bool supportsStepBack,
    bool supportsSetVariable,
    bool supportsRestartFrame,
    bool supportsGotoTargetsRequest,
    bool supportsStepInTargetsRequest,
    bool supportsCompletionsRequest,
    bool supportsModulesRequest,
    bool supportsRestartRequest,
    bool supportsExceptionOptions,
    bool supportsValueFormattingOptions,
    bool supportsExceptionInfoRequest,
    bool supportTerminateDebuggee,
    bool supportSuspendDebuggee,
    bool supportsDelayedStackTraceLoading,
    bool supportsLoadedSourcesRequest,
    bool supportsLogPoints,
    bool supportsTerminateThreadsRequest,
    bool supportsSetExpression,
    bool supportsTerminateRequest,
    bool supportsDataBreakpoints,
    bool supportsReadMemoryRequest,
    bool supportsWriteMemoryRequest,
    bool supportsDisassembleRequest
);

public record LaunchRequestArguments(
    string program,
    int? loadAddress = null,
    bool? stopOnEntry = null,
    bool? noDebug = null
);

public record SetBreakpointsArguments(
    Source source,
    Breakpoint[]? breakpoints = null
);

public record Source(
    string? name = null,
    string? path = null,
    int? sourceReference = 0
);

public record Breakpoint(
    int? line = null,
    int? column = null,
    string? condition = null,
    string? hitCondition = null,
    string? logMessage = null
);

public record BreakpointResult(
    int id,
    bool verified,
    string? message = null,
    Source? source = null,
    int? line = null
);

public record SetBreakpointsResponse(
    BreakpointResult[] breakpoints
);

public record ThreadsResponse(
    Thread[] threads
);

public record Thread(
    int id,
    string name
);

public record StackTraceArguments(
    int threadId,
    int? startFrame = null,
    int? levels = null
);

public record StackTraceResponse(
    StackFrame[] stackFrames,
    int? totalFrames = null
);

public record StackFrame(
    int id,
    string name,
    Source? source,
    int line,
    int column,
    string? presentationHint = null,
    bool canRestart = false
);

public record ScopesArguments(
    int frameId
);

public record ScopesResponse(
    Scope[] scopes
);

public record Scope(
    string name,
    int variablesReference,
    bool expensive = false,
    string? presentationHint = null
);

public record VariablesArguments(
    int variablesReference,
    string? filter = null
);

public record VariablesResponse(
    Variable[] variables
);

public record Variable(
    string name,
    string value,
    string? type = null,
    int variablesReference = 0
);

public record ContinueArguments(
    int threadId
);

public record ContinueResponse(
    bool allThreadsContinued = true
);

public record NextArguments(
    int threadId,
    bool? singleThread = null
);

public record StepInArguments(
    int threadId,
    int? targetId = null,
    bool? singleThread = null
);

public record StepOutArguments(
    int threadId,
    bool? singleThread = null
);

public record PauseArguments(
    int threadId
);

public record DisconnectArguments(
    bool? restart = null,
    bool? terminateDebuggee = null,
    bool? suspendDebuggee = null
);

// Events sent from adapter to client
public record InitializedEvent();

public record StoppedEvent(
    string reason,
    int? threadId = null,
    string? text = null,
    bool allThreadsStopped = true,
    bool preserveFocusHint = false
);

public record TerminatedEvent();

public record OutputEvent(
    string category,
    string output
);
