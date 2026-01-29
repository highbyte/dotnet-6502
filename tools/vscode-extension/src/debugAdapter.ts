import { spawn, ChildProcess } from 'child_process';
import { 
    DebugSession, 
    InitializedEvent, 
    StoppedEvent, 
    TerminatedEvent,
    OutputEvent,
    Thread,
    StackFrame,
    Scope,
    Source
} from '@vscode/debugadapter';
import { DebugProtocol } from '@vscode/debugprotocol';

interface LaunchRequestArguments extends DebugProtocol.LaunchRequestArguments {
    program: string;
    loadAddress?: number;
    stopOnEntry?: boolean;
}

export class DebugAdapter6502Session extends DebugSession {
    private adapterProcess?: ChildProcess;

    public constructor() {
        super();
        this.setDebuggerLinesStartAt1(true);
        this.setDebuggerColumnsStartAt1(true);
    }

    protected initializeRequest(
        response: DebugProtocol.InitializeResponse,
        args: DebugProtocol.InitializeRequestArguments
    ): void {
        response.body = response.body || {};
        response.body.supportsConfigurationDoneRequest = true;
        
        this.sendResponse(response);
        this.sendEvent(new InitializedEvent());
    }

    protected async launchRequest(
        response: DebugProtocol.LaunchResponse,
        args: LaunchRequestArguments
    ): Promise<void> {
        // For now, just send response
        // The actual debug adapter is launched by VSCode based on the descriptor factory
        this.sendResponse(response);
    }

    protected disconnectRequest(
        response: DebugProtocol.DisconnectResponse,
        args: DebugProtocol.DisconnectArguments,
        request?: DebugProtocol.Request
    ): void {
        this.sendResponse(response);
    }
}
