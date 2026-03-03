import * as vscode from 'vscode';
import * as path from 'path';
import * as fs from 'fs';
import * as net from 'net';
import * as child_process from 'child_process';
import { DebugAdapterExecutable, DebugAdapterServer } from 'vscode';
import { MemoryContentProvider, openMemoryViewer } from './memoryViewer';
import * as jsonc from 'jsonc-parser';

export function activate(context: vscode.ExtensionContext) {
    console.log('[6502 Debug] Extension activating...');

    // Register memory content provider
    const memoryProvider = new MemoryContentProvider(context);
    context.subscriptions.push(
        vscode.workspace.registerTextDocumentContentProvider('memory', memoryProvider)
    );

    // Register debug configuration provider
    const configProvider = new DebugConfigurationProvider();
    context.subscriptions.push(
        vscode.debug.registerDebugConfigurationProvider('dotnet6502', configProvider)
    );

    // Register debug adapter
    context.subscriptions.push(
        vscode.debug.registerDebugAdapterDescriptorFactory('dotnet6502', new DebugAdapterExecutableFactory())
    );

    // Kill the emulator process when a launch+emulator debug session ends (safety net
    // in case the .NET app doesn't exit on its own via terminateDebuggee).
    context.subscriptions.push(
        vscode.debug.onDidTerminateDebugSession((session) => {
            if (session.type === 'dotnet6502' &&
                session.configuration.request === 'launch' &&
                session.configuration.debugAdapter === 'emulator') {
                configProvider.killEmulatorProcess();
            }
        })
    );

    // Inline address decorations: show $XXXX after each mapped source line
    const addressDecorManager = new AddressDecorationManager();
    context.subscriptions.push(addressDecorManager);
    context.subscriptions.push(
        vscode.debug.registerDebugAdapterTrackerFactory(
            'dotnet6502',
            new DotNet6502DebugTrackerFactory(addressDecorManager)
        )
    );
    context.subscriptions.push(
        vscode.debug.onDidTerminateDebugSession((session) => {
            if (session.type === 'dotnet6502') {
                addressDecorManager.onSessionEnded(session);
            }
        })
    );
    context.subscriptions.push(
        vscode.window.onDidChangeActiveTextEditor((editor) => {
            if (editor) { addressDecorManager.applyToEditor(editor); }
        })
    );
    
    // Register command to generate build task
    context.subscriptions.push(
        vscode.commands.registerCommand('dotnet6502.generateBuildTask', async (uri: vscode.Uri) => {
            await generateBuildTask(uri);
        })
    );
    
    // Register command to generate launch config
    context.subscriptions.push(
        vscode.commands.registerCommand('dotnet6502.generateLaunchConfig', async (uri: vscode.Uri) => {
            await generateLaunchConfigCommand(uri);
        })
    );

    // Register command to generate emulator launch config
    context.subscriptions.push(
        vscode.commands.registerCommand('dotnet6502.generateEmulatorLaunchConfig', async (uri: vscode.Uri) => {
            await generateEmulatorLaunchConfigCommand(uri);
        })
    );

    // Register command to generate .prg launch config
    context.subscriptions.push(
        vscode.commands.registerCommand('dotnet6502.generatePrgLaunchConfig', async (uri: vscode.Uri) => {
            await generatePrgLaunchConfigCommand(uri);
        })
    );

    // Register command to view memory
    context.subscriptions.push(
        vscode.commands.registerCommand('dotnet6502.viewMemory', async () => {
            await openMemoryViewer(context, memoryProvider);
        })
    );

    context.subscriptions.push(
        vscode.commands.registerCommand('dotnet6502.jumpToLine', async (...args: any[]) => {
            console.log(`[6502 Debug] jumpToLine invoked with ${args.length} args:`, JSON.stringify(args.map(a => a?.toString?.() ?? a)));

            const session = vscode.debug.activeDebugSession;
            if (!session || session.type !== 'dotnet6502') {
                console.log('[6502 Debug] jumpToLine: no active dotnet6502 session');
                return;
            }

            const editor = vscode.window.activeTextEditor;
            if (!editor) {
                console.log('[6502 Debug] jumpToLine: no active editor');
                return;
            }

            // Determine the line number from arguments.
            // VSCode passes different argument formats depending on where the menu was triggered.
            let line: number | undefined;
            let sourcePath = editor.document.uri.fsPath;

            for (const arg of args) {
                if (arg instanceof vscode.Uri) {
                    sourcePath = arg.fsPath;
                } else if (typeof arg === 'number') {
                    line = arg;
                } else if (arg && typeof arg === 'object') {
                    if ('lineNumber' in arg && typeof arg.lineNumber === 'number') {
                        line = arg.lineNumber;
                    }
                }
            }

            // Fall back to cursor position
            if (line === undefined) {
                line = editor.selection.active.line + 1; // Convert 0-based to 1-based
            }

            console.log(`[6502 Debug] jumpToLine: resolved line=${line}, source=${sourcePath}`);
            const source = { path: sourcePath };

            try {
                const result = await session.customRequest('gotoTargets', { source, line });
                console.log(`[6502 Debug] jumpToLine: gotoTargets returned ${result.targets?.length ?? 0} targets`);
                if (result.targets && result.targets.length > 0) {
                    const target = result.targets[0];
                    console.log(`[6502 Debug] jumpToLine: goto targetId=${target.id}, label=${target.label}, line=${target.line}`);
                    await session.customRequest('goto', { threadId: 1, targetId: target.id });
                } else {
                    vscode.window.showWarningMessage(`No executable code at line ${line}`);
                }
            } catch (e: any) {
                console.log(`[6502 Debug] jumpToLine error:`, e);
                vscode.window.showErrorMessage(`Jump to line failed: ${e.message || e}`);
            }
        })
    );

    console.log('[6502 Debug] Extension activated successfully');
}

export function deactivate() {
    console.log('[6502 Debug] Extension deactivating...');
}

/**
 * Returns a platform-aware executable name (appends .exe on Windows).
 */
function platformExecutableName(baseName: string): string {
    return process.platform === 'win32' ? `${baseName}.exe` : baseName;
}

/**
 * Known build output locations for executables within the repo, relative to repo root.
 * Maps base executable name (without .exe) to its project output directory.
 */
const REPO_EXECUTABLE_LOCATIONS: Record<string, string> = {
    'Highbyte.DotNet6502.DebugAdapter.ConsoleApp': 'src/apps/Highbyte.DotNet6502.DebugAdapter',
    'Highbyte.DotNet6502.App.Avalonia.Desktop': 'src/apps/Avalonia/Highbyte.DotNet6502.App.Avalonia.Desktop',
};

/**
 * Resolves the executable path. For a bare executable name (no path separators):
 * 1. First checks the system PATH
 * 2. Then tries known repo-relative build output locations (Debug/Release)
 * For a full path, verifies it exists on disk.
 *
 * Returns the resolved path (may be the bare name if found in PATH, or a full
 * repo-relative path), or undefined if not found (shows an error message).
 */
function resolveExecutable(executablePath: string): string | undefined {
    const isBareExecutableName = !executablePath.includes('/') && !executablePath.includes('\\');

    if (!isBareExecutableName) {
        // Full path specified - just verify it exists
        if (fs.existsSync(executablePath)) {
            return executablePath;
        }
        vscode.window.showErrorMessage(
            `Executable not found: ${executablePath}. Please verify the path in 'emulatorExecutable' in your launch configuration.`
        );
        return undefined;
    }

    // Bare executable name - try PATH first
    const findCmd = process.platform === 'win32' ? 'where' : 'which';
    try {
        child_process.execSync(`${findCmd} "${executablePath}"`, { stdio: 'ignore' });
        console.log(`[6502 Debug] Found '${executablePath}' in system PATH`);
        return executablePath;
    } catch {
        console.log(`[6502 Debug] '${executablePath}' not found in system PATH, trying repo-relative paths...`);
    }

    // Not in PATH - try repo-relative build output locations
    // __dirname is tools/vscode-extension/out/, so repo root is three levels up
    const repoRoot = path.join(__dirname, '..', '..', '..');
    const baseName = executablePath.replace(/\.exe$/, '');
    const projectDir = REPO_EXECUTABLE_LOCATIONS[baseName];

    if (projectDir) {
        const buildConfigs = ['Debug', 'Release'];
        for (const buildConfig of buildConfigs) {
            const candidatePath = path.join(repoRoot, projectDir, 'bin', buildConfig, 'net10.0', executablePath);
            if (fs.existsSync(candidatePath)) {
                console.log(`[6502 Debug] Found executable via repo-relative path: ${candidatePath}`);
                return candidatePath;
            }
            console.log(`[6502 Debug]   not found: ${candidatePath}`);
        }
    }

    vscode.window.showErrorMessage(
        `Executable '${executablePath}' not found in system PATH or in repo build output. Either add it to PATH, build the project, or set 'emulatorExecutable' to a full path in your launch configuration.`
    );
    return undefined;
}

/**
 * Debug configuration provider for 6502 debugging.
 *
 * Supports two debug adapter modes:
 * - 'minimal': Uses a minimal standalone debug adapter for generic 6502 debugging
 *              (no full system emulation). Communicates via STDIO.
 *              Default executable: Highbyte.DotNet6502.DebugAdapter.ConsoleApp[.exe]
 * - 'emulator': Launches an emulator host app (Avalonia, SadConsole, SilkNetNative, etc.)
 *               which acts as the debug adapter. Supports full emulation (C64, etc.) and
 *               communicates via TCP.
 *               Default executable: Highbyte.DotNet6502.App.Avalonia.Desktop[.exe]
 *
 * In both modes, the executable is configured via the 'emulatorExecutable' parameter,
 * which defaults to a platform-aware name. The executable is resolved by:
 * 1. Checking the system PATH
 * 2. Trying known repo-relative build output paths (Debug/Release)
 */
class DebugConfigurationProvider implements vscode.DebugConfigurationProvider {
    private emulatorProcess: child_process.ChildProcess | undefined;

    resolveDebugConfiguration(
        folder: vscode.WorkspaceFolder | undefined,
        config: vscode.DebugConfiguration,
        token?: vscode.CancellationToken
    ): vscode.ProviderResult<vscode.DebugConfiguration> {
        // Add workspace folder to config for auto-detection
        if (folder) {
            config.__workspaceFolder = folder.uri.fsPath;
        }
        return config;
    }

    async resolveDebugConfigurationWithSubstitutedVariables(
        folder: vscode.WorkspaceFolder | undefined,
        config: vscode.DebugConfiguration,
        token?: vscode.CancellationToken
    ): Promise<vscode.DebugConfiguration | undefined> {
        // Attach mode: connect to an already-running emulator via TCP
        if (config.request === 'attach') {
            const debugPort = config.debugPort || 6502;
            console.log(`[6502 Debug] Attach mode: connecting to emulator on port ${debugPort}`);

            const launchOnlyProps = ['system', 'systemVariant', 'waitForSystemReady', 'loadProgram', 'runProgram']
                .filter(p => config[p] !== undefined);
            if (launchOnlyProps.length > 0) {
                vscode.window.showWarningMessage(
                    `6502 Debugger: The following properties are ignored in attach mode: ${launchOnlyProps.join(', ')}. ` +
                    `They only apply to launch configurations.`
                );
            }

            config.__programAlreadyLoaded = true;
            config.__emulatorDebugPort = debugPort;
            config.__waitingForEmulator = true;
            return config;
        }

        // Set default debug adapter if not specified
        if (!config.debugAdapter) {
            config.debugAdapter = 'minimal';
        }

        // Validate debugAdapter parameter
        if (config.debugAdapter !== 'minimal' && config.debugAdapter !== 'emulator') {
            vscode.window.showErrorMessage(
                `Invalid debugAdapter value: ${config.debugAdapter}. Must be 'minimal' or 'emulator'.`
            );
            return undefined;
        }

        // Set default emulatorExecutable based on mode (platform-aware)
        if (!config.emulatorExecutable) {
            if (config.debugAdapter === 'minimal') {
                config.emulatorExecutable = platformExecutableName('Highbyte.DotNet6502.DebugAdapter.ConsoleApp');
            } else {
                // Currently only Highbyte.DotNet6502.App.Avalonia.Desktop is supported as emulator host.
                config.emulatorExecutable = platformExecutableName('Highbyte.DotNet6502.App.Avalonia.Desktop');
            }
            console.log(`[6502 Debug] Using default emulatorExecutable for '${config.debugAdapter}' mode: ${config.emulatorExecutable}`);
        }

        // Resolve the executable: try PATH, then repo-relative build output
        const resolvedExecutable = resolveExecutable(config.emulatorExecutable);
        if (!resolvedExecutable) {
            return undefined;
        }
        config.emulatorExecutable = resolvedExecutable;

        // If debugAdapter is 'emulator', start the emulator host app
        if (config.debugAdapter === 'emulator') {
            console.log('[6502 Debug] debugAdapter is emulator, starting emulator host app');

            const executablePath = config.emulatorExecutable;
            const debugPort = config.debugPort || 6502;
            const system = config.system || 'C64';
            const systemVariant = config.systemVariant;
            const waitForReady = config.waitForSystemReady !== false; // Default true
            const loadPrg = config.loadProgram !== false; // Default true
            const runProgram = config.runProgram === true; // Default false
            let programPath = config.program;

            console.log(`[6502 Debug] Initial programPath: ${programPath}`);
            console.log(`[6502 Debug] preLaunchTask: ${config.preLaunchTask}`);

            // If program path not specified, try to extract from preLaunchTask
            if (!programPath && config.preLaunchTask && folder) {
                const tasks = await vscode.tasks.fetchTasks();
                const task = tasks.find(t => t.name === config.preLaunchTask);
                console.log(`[6502 Debug] Found task: ${task?.name}, definition: ${JSON.stringify(task?.definition)}`);
                if (task) {
                    // For shell tasks, args might be in definition.args or in task execution
                    let args = task.definition.args;

                    // If not in definition, might be a ShellExecution
                    if (!args && task.execution && 'args' in task.execution) {
                        args = (task.execution as any).args;
                    }

                    console.log(`[6502 Debug] Task args: ${JSON.stringify(args)}`);

                    // Determine the task's working directory so output files can be
                    // resolved relative to it (e.g. cwd="${workspaceFolder}/samples").
                    const executionCwd: string | undefined =
                        (task.execution as any)?.options?.cwd ?? task.definition.options?.cwd;
                    const taskCwd = executionCwd
                        ? executionCwd.replace(/\$\{workspaceFolder\}/g, folder.uri.fsPath)
                        : folder.uri.fsPath;
                    console.log(`[6502 Debug] Task cwd (resolved): ${taskCwd}`);

                    if (args && Array.isArray(args)) {
                        // Look for -o argument in cl65 task
                        const oIndex = args.indexOf('-o');
                        if (oIndex >= 0 && oIndex + 1 < args.length) {
                            const outputFile = args[oIndex + 1];
                            programPath = path.join(taskCwd, outputFile);
                            console.log(`[6502 Debug] Extracted program path from task: ${programPath}`);
                        } else {
                            console.log(`[6502 Debug] Could not find -o argument in task args`);
                        }

                        // Auto-detect dbgFile from -Wl --dbgfile,<file> arg if not set in launch config
                        if (!config.dbgFile) {
                            for (const arg of args) {
                                const argStr = typeof arg === 'string' ? arg : (arg as any)?.value;
                                if (typeof argStr === 'string' && argStr.startsWith('--dbgfile,')) {
                                    const dbgFileName = argStr.slice('--dbgfile,'.length);
                                    config.dbgFile = path.join(taskCwd, dbgFileName);
                                    console.log(`[6502 Debug] Extracted dbgFile from task: ${config.dbgFile}`);
                                    break;
                                }
                            }
                        }
                    } else {
                        console.log(`[6502 Debug] Task has no args array`);
                    }
                } else {
                    console.log(`[6502 Debug] Task not found`);
                }
            }

            console.log(`[6502 Debug] Final programPath: ${programPath}, loadPrg: ${loadPrg}, runProgram: ${runProgram}`);

            // Propagate the auto-detected (or config-supplied) program path back onto
            // config so the debug adapter receives it for .dbg file resolution.
            if (programPath && !config.program) {
                config.program = programPath;
            }

            // The emulator host handles loading the program into memory, so tell the
            // debug adapter not to load it again (but still use the path for debug symbols
            // and program bounds).
            config.__programAlreadyLoaded = true;

            // Build command line arguments
            const args = [
                '--enableExternalDebug',
                '--debug-port', debugPort.toString(),
                '--console-log',  // Enable console logging to see errors
                '--system', system,
                '--start'
            ];

            if (systemVariant) {
                args.push('--systemVariant', systemVariant);
            }

            if (waitForReady) {
                args.push('--waitForSystemReady');
            }

            if (loadPrg && programPath) {
                args.push('--loadPrg', programPath);
            }

            if (runProgram) {
                args.push('--runLoadedProgram');
            }

            console.log(`[6502 Debug] Launching emulator host: ${executablePath} ${args.join(' ')}`);

            try {
                // Kill any existing emulator process
                if (this.emulatorProcess) {
                    console.log('[6502 Debug] Killing existing emulator process');
                    this.emulatorProcess.kill();
                    this.emulatorProcess = undefined;
                }

                // Launch the emulator host app
                const path = require('path');
                const executableDir = path.dirname(executablePath);
                
                // Merge current environment with any custom environment variables from config
                const env = {
                    ...process.env,
                    ...(config.env || {})
                };
                
                const spawnOptions: any = {
                    detached: false,
                    stdio: ['ignore', 'pipe', 'pipe'],  // stdin=ignore, stdout=pipe, stderr=pipe
                    cwd: executableDir,
                    env: env
                };
                
                console.log(`[6502 Debug] Spawning: ${executablePath} ${args.join(' ')}`);
                console.log(`[6502 Debug] Environment variables:`, config.env);
                this.emulatorProcess = child_process.spawn(executablePath, args, spawnOptions);

                this.emulatorProcess.stdout?.on('data', (data) => {
                    console.log(`[Emulator Host] ${data.toString()}`);
                });

                this.emulatorProcess.stderr?.on('data', (data) => {
                    console.error(`[Emulator Host Error] ${data.toString()}`);
                });

                this.emulatorProcess.on('exit', (code) => {
                    console.log(`[6502 Debug] Emulator host process exited with code ${code}`);
                    if (code !== 0 && code !== null) {
                        vscode.window.showErrorMessage(`Emulator host app exited with error code ${code}. Check console output for details.`);
                    }
                    this.emulatorProcess = undefined;
                });

                // Emulator host will start the system, load PRG, and then start TCP server
                // We need to wait for the TCP server to be ready before connecting
                console.log(`[6502 Debug] Emulator host launched, will wait for TCP server on port ${debugPort}`);

                // Don't set debugServer yet - we'll set it after verifying the server is ready
                // Store the port for later use
                config.__emulatorDebugPort = debugPort;
                config.__waitingForEmulator = true;

            } catch (error) {
                const errorMsg = `Failed to launch emulator host app: ${error}`;
                console.error('[6502 Debug]', errorMsg);
                vscode.window.showErrorMessage(errorMsg);
                return undefined;
            }
        }

        return config;
    }

    killEmulatorProcess() {
        if (this.emulatorProcess) {
            console.log('[6502 Debug] Killing emulator host process (debug session ended)');
            this.emulatorProcess.kill();
            this.emulatorProcess = undefined;
        }
    }

    dispose() {
        this.killEmulatorProcess();
    }
}

class DebugAdapterExecutableFactory implements vscode.DebugAdapterDescriptorFactory {
    async createDebugAdapterDescriptor(
        session: vscode.DebugSession,
        executable: vscode.DebugAdapterExecutable | undefined
    ): Promise<vscode.DebugAdapterDescriptor | undefined> {
        
        console.log('[6502 Debug] createDebugAdapterDescriptor called for session:', session.name);
        console.log('[6502 Debug] __waitingForEmulator:', session.configuration.__waitingForEmulator);

        // Emulator mode: wait for the emulator's TCP debug server, then connect
        if (session.configuration.__waitingForEmulator) {
            const port = session.configuration.__emulatorDebugPort;
            const isAttach = session.configuration.request === 'attach';
            // Attach mode: emulator should already be running, so use a short default timeout.
            // Launch mode: emulator needs time to boot, so use a longer default timeout.
            const defaultTimeout = isAttach ? 5 : 120;
            const timeoutSeconds = session.configuration.startupTimeout || defaultTimeout;
            const timeoutMs = timeoutSeconds * 1000;
            console.log(`[6502 Debug] Waiting for emulator host TCP server on port ${port} (timeout: ${timeoutSeconds}s, mode: ${isAttach ? 'attach' : 'launch'})...`);

            const isReady = await this.waitForTcpServerListening(port, timeoutMs);
            if (!isReady) {
                const hint = isAttach ? ' Is the emulator running with --enableExternalDebug?' : '';
                vscode.window.showErrorMessage(`Emulator host TCP debug server did not respond within ${timeoutSeconds} seconds on port ${port}.${hint}`);
                return undefined;
            }

            console.log(`[6502 Debug] Emulator host TCP server is ready on port ${port}`);
            return this.createTcpDebugAdapter(port);
        }

        // Minimal mode: launch the debug adapter as a child process (STDIO)
        const executablePath = session.configuration.emulatorExecutable;
        console.log(`[6502 Debug] Launching minimal debug adapter executable: ${executablePath}`);
        return this.createExecutableDebugAdapter(executablePath);
    }

    private async waitForTcpServerListening(port: number, timeoutMs: number): Promise<boolean> {
        // Wait for the TCP server to start accepting connections
        // Note: This will create one connection that the server accepts but we immediately close
        // The server will handle this gracefully (it expects a DAP initialize message)
        const startTime = Date.now();
        const net = require('net');
        
        while (Date.now() - startTime < timeoutMs) {
            try {
                await new Promise<void>((resolve, reject) => {
                    const socket = new net.Socket();
                    socket.setTimeout(500);
                    
                    socket.on('connect', () => {
                        socket.destroy();
                        resolve();
                    });
                    
                    socket.on('timeout', () => {
                        socket.destroy();
                        reject(new Error('timeout'));
                    });
                    
                    socket.on('error', () => {
                        socket.destroy();
                        reject(new Error('connection refused'));
                    });
                    
                    socket.connect(port, '127.0.0.1');
                });
                
                // Port is listening - wait a moment for the server to reject the empty connection
                await new Promise(resolve => setTimeout(resolve, 200));
                return true;
            } catch (error) {
                // Port not ready yet, wait and retry
                await new Promise(resolve => setTimeout(resolve, 500));
            }
        }
        
        return false;
    }
    
    private createTcpDebugAdapter(port: number): vscode.DebugAdapterDescriptor | undefined {
        try {
            // Return a DebugAdapterServer that connects to the specified port
            const server = new DebugAdapterServer(port, '127.0.0.1');
            console.log(`[6502 Debug] ✓ Created DebugAdapterServer for port ${port}`);
            return server;
        } catch (error) {
            const errorMsg = `[6502 Debug] Error creating TCP debug adapter: ${error}`;
            console.error(errorMsg);
            vscode.window.showErrorMessage(errorMsg);
            return undefined;
        }
    }
    
    private createExecutableDebugAdapter(executablePath: string): vscode.DebugAdapterDescriptor | undefined {
        try {
            console.log('[6502 Debug] ✓ Using debug adapter:', executablePath);
            const debugAdapterExecutable = new DebugAdapterExecutable(executablePath, []);
            console.log('[6502 Debug] ✓ Created DebugAdapterExecutable, returning to VSCode');
            return debugAdapterExecutable;
        } catch (error) {
            const errorMsg = `[6502 Debug] Error in createDebugAdapterDescriptor: ${error}`;
            console.error(errorMsg);
            vscode.window.showErrorMessage(errorMsg);
            return undefined;
        }
    }
}

/**
 * Generate a build task for a .asm file in tasks.json
 */
async function generateBuildTask(uri: vscode.Uri): Promise<void> {
    if (!uri || !uri.fsPath.endsWith('.asm')) {
        vscode.window.showErrorMessage('Please select a .asm file');
        return;
    }

    const fileName = path.basename(uri.fsPath);
    const fileBasename = path.basename(uri.fsPath, '.asm');
    const workspaceFolder = vscode.workspace.getWorkspaceFolder(uri);
    
    if (!workspaceFolder) {
        vscode.window.showErrorMessage('File must be in a workspace folder');
        return;
    }

    // Prompt for start address
    const startAddress = await vscode.window.showInputBox({
        prompt: 'Enter start/load address for C64 program',
        value: '0xc000',
        placeHolder: '0xc000',
        validateInput: (value) => {
            if (!value.match(/^(0x[0-9a-fA-F]+|\$[0-9a-fA-F]+|[0-9]+)$/)) {
                return 'Enter a valid address (e.g., 0xc000, $c000, or 49152)';
            }
            return undefined;
        }
    });

    if (!startAddress) {
        return; // User cancelled
    }

    // Create the task definition
    const taskLabel = `Build ${fileBasename}.asm (C64)`;
    const newTask = {
        label: taskLabel,
        type: 'shell',
        command: 'cl65',
        args: [
            '-g',
            fileName,
            '-o',
            `${fileBasename}.prg`,
            '-C',
            'c64-asm.cfg',
            '--start-addr',
            startAddress,
            '-Wl', `-Ln,${fileBasename}.lbl`,
            '-Wl', `--dbgfile,${fileBasename}.dbg`,
            '-Wl', `-m,${fileBasename}.map`
        ],
        options: {
            cwd: path.dirname(uri.fsPath)
        },
        problemMatcher: '$ca65',
        group: {
            kind: 'build',
            isDefault: false
        }
    };

    // Get or create tasks.json
    const tasksJsonPath = path.join(workspaceFolder.uri.fsPath, '.vscode', 'tasks.json');
    let tasksConfig: any;
    let content: string;
    let fileExists = fs.existsSync(tasksJsonPath);

    try {
        if (fileExists) {
            // Read existing tasks.json
            content = fs.readFileSync(tasksJsonPath, 'utf8');
            // Parse JSONC (JSON with Comments and trailing commas)
            tasksConfig = jsonc.parse(content);
        } else {
            // Create new tasks.json structure
            tasksConfig = {
                version: '2.0.0',
                tasks: []
            };
            content = JSON.stringify(tasksConfig, null, 2);
            // Ensure .vscode directory exists
            const vscodeDir = path.join(workspaceFolder.uri.fsPath, '.vscode');
            if (!fs.existsSync(vscodeDir)) {
                fs.mkdirSync(vscodeDir, { recursive: true });
            }
        }

        // Check if task with same label already exists
        if (!tasksConfig.tasks) {
            tasksConfig.tasks = [];
        }

        const existingIndex = tasksConfig.tasks.findIndex((t: any) => t.label === taskLabel);
        if (existingIndex >= 0) {
            // Update existing task
            const overwrite = await vscode.window.showWarningMessage(
                `Task "${taskLabel}" already exists. Overwrite?`,
                'Yes', 'No'
            );
            if (overwrite === 'Yes') {
                // Use jsonc.modify to update existing task while preserving comments
                const edits = jsonc.modify(content, ['tasks', existingIndex], newTask, {
                    formattingOptions: { tabSize: 2, insertSpaces: true }
                });
                content = jsonc.applyEdits(content, edits);
            } else {
                return;
            }
        } else {
            // Add new task using jsonc.modify to preserve comments
            const edits = jsonc.modify(content, ['tasks', -1], newTask, {
                formattingOptions: { tabSize: 2, insertSpaces: true }
            });
            content = jsonc.applyEdits(content, edits);
        }

        // Write tasks.json with preserved comments
        fs.writeFileSync(tasksJsonPath, content, 'utf8');

        // Show success message with action
        const result = await vscode.window.showInformationMessage(
            `Build task "${taskLabel}" created successfully!`,
            'Open tasks.json',
            'Create Launch Config'
        );

        if (result === 'Open tasks.json') {
            const doc = await vscode.workspace.openTextDocument(tasksJsonPath);
            await vscode.window.showTextDocument(doc);
        } else if (result === 'Create Launch Config') {
            await generateLaunchConfig(workspaceFolder, taskLabel, fileBasename);
        }

    } catch (error) {
        vscode.window.showErrorMessage(`Failed to create task: ${error}`);
        console.error('[6502 Debug] Error generating task:', error);
    }
}

/**
 * Generate a launch configuration that uses the build task
 */
async function generateLaunchConfig(
    workspaceFolder: vscode.WorkspaceFolder,
    taskLabel: string,
    fileBasename: string
): Promise<void> {
    const launchJsonPath = path.join(workspaceFolder.uri.fsPath, '.vscode', 'launch.json');
    let launchConfig: any;
    let content: string;
    let fileExists = fs.existsSync(launchJsonPath);

    try {
        if (fileExists) {
            content = fs.readFileSync(launchJsonPath, 'utf8');
            launchConfig = jsonc.parse(content);
        } else {
            launchConfig = {
                version: '0.2.0',
                configurations: []
            };
            content = JSON.stringify(launchConfig, null, 2);
        }

        const configName = `Debug ${fileBasename}.asm`;
        const newConfig = {
            type: 'dotnet6502',
            request: 'launch',
            name: configName,
            preLaunchTask: taskLabel,
            stopOnEntry: true,
            stopOnBRK: true
        };

        if (!launchConfig.configurations) {
            launchConfig.configurations = [];
        }

        const existingIndex = launchConfig.configurations.findIndex((c: any) => c.name === configName);
        if (existingIndex >= 0) {
            // Update existing configuration using jsonc.modify to preserve comments
            const edits = jsonc.modify(content, ['configurations', existingIndex], newConfig, {
                formattingOptions: { tabSize: 2, insertSpaces: true }
            });
            content = jsonc.applyEdits(content, edits);
        } else {
            // Add new configuration using jsonc.modify to preserve comments
            const edits = jsonc.modify(content, ['configurations', -1], newConfig, {
                formattingOptions: { tabSize: 2, insertSpaces: true }
            });
            content = jsonc.applyEdits(content, edits);
        }

        // Write launch.json with preserved comments
        fs.writeFileSync(launchJsonPath, content, 'utf8');

        vscode.window.showInformationMessage(
            `Launch configuration "${configName}" created! Press F5 to debug.`
        );

        const doc = await vscode.workspace.openTextDocument(launchJsonPath);
        await vscode.window.showTextDocument(doc);

    } catch (error) {
        vscode.window.showErrorMessage(`Failed to create launch config: ${error}`);
        console.error('[6502 Debug] Error generating launch config:', error);
    }
}

/**
 * Command to generate launch config - prompts user to select an existing task
 */
async function generateLaunchConfigCommand(uri: vscode.Uri): Promise<void> {
    if (!uri || !uri.fsPath.endsWith('.asm')) {
        vscode.window.showErrorMessage('Please select a .asm file');
        return;
    }

    const fileName = path.basename(uri.fsPath);
    const fileBasename = path.basename(uri.fsPath, '.asm');
    const workspaceFolder = vscode.workspace.getWorkspaceFolder(uri);
    
    if (!workspaceFolder) {
        vscode.window.showErrorMessage('File must be in a workspace folder');
        return;
    }

    // Try to find existing build tasks for this file
    const tasksJsonPath = path.join(workspaceFolder.uri.fsPath, '.vscode', 'tasks.json');
    let availableTasks: string[] = [];
    let suggestedTask: string | undefined;

    if (fs.existsSync(tasksJsonPath)) {
        try {
            const content = fs.readFileSync(tasksJsonPath, 'utf8');
            const tasksConfig = jsonc.parse(content);
            
            if (tasksConfig.tasks) {
                // Find tasks that might be for this file
                availableTasks = tasksConfig.tasks.map((t: any) => t.label);
                
                // Try to find a task that matches this file with priority:
                // 1. Label contains "Build <filename>.asm (C64)" - extension-generated pattern
                // 2. Label contains the base filename
                // 3. Args contain the filename
                
                // Priority 1: Extension-generated task pattern
                suggestedTask = tasksConfig.tasks.find((t: any) => 
                    t.label === `Build ${fileName} (C64)`
                )?.label;
                
                // Priority 2: Label contains base filename
                if (!suggestedTask) {
                    suggestedTask = tasksConfig.tasks.find((t: any) => 
                        t.label.toLowerCase().includes(fileBasename.toLowerCase())
                    )?.label;
                }
                
                // Priority 3: Command or args contain filename
                if (!suggestedTask) {
                    suggestedTask = tasksConfig.tasks.find((t: any) =>
                        (t.command && typeof t.command === 'string' && t.command.includes(fileName)) ||
                        (t.args && t.args.some((arg: string) => arg.includes(fileName)))
                    )?.label;
                }
            }
        } catch (error) {
            // Ignore parse errors
        }
    }

    let taskLabel: string | undefined;

    if (availableTasks.length === 0) {
        // No tasks found - suggest creating one first
        const result = await vscode.window.showWarningMessage(
            `No build tasks found. Would you like to create one first?`,
            'Generate Build Task',
            'Cancel'
        );
        
        if (result === 'Generate Build Task') {
            await generateBuildTask(uri);
        }
        return;
    } else if (availableTasks.length === 1) {
        // Only one task - use it automatically
        taskLabel = availableTasks[0];
    } else if (suggestedTask) {
        // Multiple tasks but we found a good match - use it automatically
        taskLabel = suggestedTask;
    } else {
        // Multiple tasks, no clear match - let user choose
        taskLabel = await vscode.window.showQuickPick(availableTasks, {
            placeHolder: 'Select a build task to use as preLaunchTask',
            canPickMany: false
        });
        
        if (!taskLabel) {
            return; // User cancelled
        }
    }

    // Generate the launch config
    await generateLaunchConfig(workspaceFolder, taskLabel, fileBasename);
}

/**
 * Command to generate emulator launch config - prompts user to select an existing task
 */
async function generateEmulatorLaunchConfigCommand(uri: vscode.Uri): Promise<void> {
    if (!uri || !uri.fsPath.endsWith('.asm')) {
        vscode.window.showErrorMessage('Please select a .asm file');
        return;
    }

    const fileName = path.basename(uri.fsPath);
    const fileBasename = path.basename(uri.fsPath, '.asm');
    const workspaceFolder = vscode.workspace.getWorkspaceFolder(uri);

    if (!workspaceFolder) {
        vscode.window.showErrorMessage('File must be in a workspace folder');
        return;
    }

    // Try to find existing build tasks for this file
    const tasksJsonPath = path.join(workspaceFolder.uri.fsPath, '.vscode', 'tasks.json');
    let availableTasks: string[] = [];
    let suggestedTask: string | undefined;

    if (fs.existsSync(tasksJsonPath)) {
        try {
            const content = fs.readFileSync(tasksJsonPath, 'utf8');
            const tasksConfig = jsonc.parse(content);

            if (tasksConfig.tasks) {
                // Find tasks that might be for this file
                availableTasks = tasksConfig.tasks.map((t: any) => t.label);

                // Try to find a task that matches this file with priority:
                // 1. Label contains "Build <filename>.asm (C64)" - extension-generated pattern
                // 2. Label contains the base filename
                // 3. Args contain the filename

                // Priority 1: Extension-generated task pattern
                suggestedTask = tasksConfig.tasks.find((t: any) =>
                    t.label === `Build ${fileName} (C64)`
                )?.label;

                // Priority 2: Label contains base filename
                if (!suggestedTask) {
                    suggestedTask = tasksConfig.tasks.find((t: any) =>
                        t.label.toLowerCase().includes(fileBasename.toLowerCase())
                    )?.label;
                }

                // Priority 3: Command or args contain filename
                if (!suggestedTask) {
                    suggestedTask = tasksConfig.tasks.find((t: any) =>
                        (t.command && typeof t.command === 'string' && t.command.includes(fileName)) ||
                        (t.args && t.args.some((arg: string) => arg.includes(fileName)))
                    )?.label;
                }
            }
        } catch (error) {
            // Ignore parse errors
        }
    }

    let taskLabel: string | undefined;

    if (availableTasks.length === 0) {
        // No tasks found - suggest creating one first
        const result = await vscode.window.showWarningMessage(
            `No build tasks found. Would you like to create one first?`,
            'Generate Build Task',
            'Cancel'
        );

        if (result === 'Generate Build Task') {
            await generateBuildTask(uri);
        }
        return;
    } else if (availableTasks.length === 1) {
        // Only one task - use it automatically
        taskLabel = availableTasks[0];
    } else if (suggestedTask) {
        // Multiple tasks but we found a good match - use it automatically
        taskLabel = suggestedTask;
    } else {
        // Multiple tasks, no clear match - let user choose
        taskLabel = await vscode.window.showQuickPick(availableTasks, {
            placeHolder: 'Select a build task to use as preLaunchTask',
            canPickMany: false
        });

        if (!taskLabel) {
            return; // User cancelled
        }
    }

    // Generate the emulator launch config
    await generateEmulatorLaunchConfig(workspaceFolder, taskLabel, fileBasename);
}

/**
 * Generate a launch configuration for the C64 emulator that uses the build task
 */
async function generateEmulatorLaunchConfig(
    workspaceFolder: vscode.WorkspaceFolder,
    taskLabel: string,
    fileBasename: string
): Promise<void> {
    const launchJsonPath = path.join(workspaceFolder.uri.fsPath, '.vscode', 'launch.json');
    let launchConfig: any;
    let content: string;
    let fileExists = fs.existsSync(launchJsonPath);

    try {
        if (fileExists) {
            content = fs.readFileSync(launchJsonPath, 'utf8');
            launchConfig = jsonc.parse(content);
        } else {
            launchConfig = {
                version: '0.2.0',
                configurations: []
            };
            content = JSON.stringify(launchConfig, null, 2);
        }

        const configName = `Launch Full Emulator Host with C64 to Source Debug ${fileBasename}.asm`;
        const newConfig = {
            type: 'dotnet6502',
            request: 'launch',
            name: configName,
            preLaunchTask: taskLabel,
            debugAdapter: 'emulator',
            system: 'C64',
            waitForSystemReady: true,
            loadProgram: true,
            runProgram: true,
            stopOnEntry: false,
            stopOnBRK: true
        };

        if (!launchConfig.configurations) {
            launchConfig.configurations = [];
        }

        const existingIndex = launchConfig.configurations.findIndex((c: any) => c.name === configName);
        if (existingIndex >= 0) {
            // Update existing configuration using jsonc.modify to preserve comments
            const edits = jsonc.modify(content, ['configurations', existingIndex], newConfig, {
                formattingOptions: { tabSize: 2, insertSpaces: true }
            });
            content = jsonc.applyEdits(content, edits);
        } else {
            // Add new configuration using jsonc.modify to preserve comments
            const edits = jsonc.modify(content, ['configurations', -1], newConfig, {
                formattingOptions: { tabSize: 2, insertSpaces: true }
            });
            content = jsonc.applyEdits(content, edits);
        }

        // Write launch.json with preserved comments
        fs.writeFileSync(launchJsonPath, content, 'utf8');

        vscode.window.showInformationMessage(
            `Launch configuration "${configName}" created! Press F5 to debug.`
        );

        const doc = await vscode.workspace.openTextDocument(launchJsonPath);
        await vscode.window.showTextDocument(doc);

    } catch (error) {
        vscode.window.showErrorMessage(`Failed to create launch config: ${error}`);
        console.error('[6502 Debug] Error generating emulator launch config:', error);
    }
}

/**
 * Command to generate emulator launch config for .prg files
 */
async function generatePrgLaunchConfigCommand(uri: vscode.Uri): Promise<void> {
    if (!uri || !uri.fsPath.endsWith('.prg')) {
        vscode.window.showErrorMessage('Please select a .prg file');
        return;
    }

    const fileName = path.basename(uri.fsPath);
    const fileBasename = path.basename(uri.fsPath, '.prg');
    const workspaceFolder = vscode.workspace.getWorkspaceFolder(uri);

    if (!workspaceFolder) {
        vscode.window.showErrorMessage('File must be in a workspace folder');
        return;
    }

    await generatePrgLaunchConfig(workspaceFolder, fileName, fileBasename);
}

/**
 * Generate a launch configuration for .prg files with C64 emulator
 */
async function generatePrgLaunchConfig(
    workspaceFolder: vscode.WorkspaceFolder,
    fileName: string,
    fileBasename: string
): Promise<void> {
    const launchJsonPath = path.join(workspaceFolder.uri.fsPath, '.vscode', 'launch.json');
    let launchConfig: any;
    let content: string;
    let fileExists = fs.existsSync(launchJsonPath);

    try {
        if (fileExists) {
            content = fs.readFileSync(launchJsonPath, 'utf8');
            launchConfig = jsonc.parse(content);
        } else {
            launchConfig = {
                version: '0.2.0',
                configurations: []
            };
            content = JSON.stringify(launchConfig, null, 2);
        }

        const configName = `Launch C64 emulator with ${fileBasename}.prg`;
        const newConfig = {
            type: 'dotnet6502',
            request: 'launch',
            name: configName,
            debugAdapter: 'emulator',
            program: `\${workspaceFolder}/${fileName}`,
            system: 'C64',
            waitForSystemReady: true,
            loadProgram: true,
            runProgram: true,
            stopOnEntry: false,
            stopOnBRK: false
        };

        if (!launchConfig.configurations) {
            launchConfig.configurations = [];
        }

        const existingIndex = launchConfig.configurations.findIndex((c: any) => c.name === configName);
        if (existingIndex >= 0) {
            // Update existing configuration using jsonc.modify to preserve comments
            const edits = jsonc.modify(content, ['configurations', existingIndex], newConfig, {
                formattingOptions: { tabSize: 2, insertSpaces: true }
            });
            content = jsonc.applyEdits(content, edits);
        } else {
            // Add new configuration using jsonc.modify to preserve comments
            const edits = jsonc.modify(content, ['configurations', -1], newConfig, {
                formattingOptions: { tabSize: 2, insertSpaces: true }
            });
            content = jsonc.applyEdits(content, edits);
        }

        // Write launch.json with preserved comments
        fs.writeFileSync(launchJsonPath, content, 'utf8');

        vscode.window.showInformationMessage(
            `Launch configuration "${configName}" created! Press F5 to debug.`
        );

        const doc = await vscode.workspace.openTextDocument(launchJsonPath);
        await vscode.window.showTextDocument(doc);

    } catch (error) {
        vscode.window.showErrorMessage(`Failed to create launch config: ${error}`);
        console.error('[6502 Debug] Error generating .prg launch config:', error);
    }
}

/**
 * Manages inline address decorations: shows the 6502 address ($XXXX) as
 * dim after-line text for each source line that has a known address mapping.
 * Fetches the source→address map from the debug adapter on first stop.
 *
 * Two decoration types are used:
 *  - `decorationType`        (static)  — all non-macro lines, fixed from the .dbg file.
 *  - `dynamicDecorationType` (dynamic) — only the current stopped line, and only when
 *                                         that line has no static decoration (i.e. it is
 *                                         inside a macro body). Updated on every stop so
 *                                         repeated macro calls show the correct address.
 */
class AddressDecorationManager implements vscode.Disposable {
    private readonly decorationType: vscode.TextEditorDecorationType;
    private readonly dynamicDecorationType: vscode.TextEditorDecorationType;
    // sessionId → (dbg filename key → (1-based lineNumber → formatted address))
    private readonly sessionMaps = new Map<string, Map<string, Map<number, string>>>();
    private activeSessionId: string | undefined;
    // Current stopped position for dynamic (macro) decoration
    private currentStopInfo: { sessionId: string; file: string; line: number; addr: string } | undefined;

    constructor() {
        const afterStyle = {
            color: new vscode.ThemeColor('editorLineNumber.foreground'),
            fontStyle: 'italic',
        };
        this.decorationType = vscode.window.createTextEditorDecorationType({ after: afterStyle });
        this.dynamicDecorationType = vscode.window.createTextEditorDecorationType({ after: afterStyle });
    }

    async fetchAndApply(session: vscode.DebugSession): Promise<void> {
        if (this.sessionMaps.has(session.id)) { return; } // Already fetched for this session

        try {
            const response = await session.customRequest('getSourceAddressMap');
            if (!response?.files) { return; }

            const fileMap = new Map<string, Map<number, string>>();
            for (const [fileName, lineObj] of Object.entries(response.files as Record<string, Record<string, number>>)) {
                const lineMap = new Map<number, string>();
                for (const [lineStr, addr] of Object.entries(lineObj)) {
                    const hex = (addr as number).toString(16).toUpperCase().padStart(4, '0');
                    lineMap.set(parseInt(lineStr), `$${hex}`);
                }
                fileMap.set(fileName, lineMap);
            }
            this.sessionMaps.set(session.id, fileMap);
            this.activeSessionId = session.id;

            for (const editor of vscode.window.visibleTextEditors) {
                this.applyToEditor(editor);
            }
        } catch {
            // No .dbg file loaded in this session — decorations simply won't appear
        }
    }

    /**
     * Called when the adapter sends a `stackTrace` response after a `stopped` event.
     * Applies a dynamic decoration to the current stopped line if it is a macro body
     * line (has no static decoration).
     */
    onStackFrame(session: vscode.DebugSession, frame: any): void {
        const addrRef = frame.instructionPointerReference as string | undefined;
        const sourcePath = frame.source?.path as string | undefined;
        const line = frame.line as number | undefined;

        if (!addrRef || !sourcePath || !line) {
            this.currentStopInfo = undefined;
            for (const editor of vscode.window.visibleTextEditors) {
                editor.setDecorations(this.dynamicDecorationType, []);
            }
            return;
        }

        const addrNum = parseInt(addrRef.replace(/^0x/i, ''), 16);
        const hex = addrNum.toString(16).toUpperCase().padStart(4, '0');

        this.currentStopInfo = { sessionId: session.id, file: sourcePath, line, addr: `$${hex}` };

        for (const editor of vscode.window.visibleTextEditors) {
            this.applyDynamicToEditor(editor);
        }
    }

    onSessionEnded(session: vscode.DebugSession): void {
        this.sessionMaps.delete(session.id);
        if (this.activeSessionId === session.id) {
            this.activeSessionId = undefined;
            for (const editor of vscode.window.visibleTextEditors) {
                editor.setDecorations(this.decorationType, []);
                editor.setDecorations(this.dynamicDecorationType, []);
            }
        }
        if (this.currentStopInfo?.sessionId === session.id) {
            this.currentStopInfo = undefined;
        }
    }

    /** Returns true when the editor's absolute file-system path corresponds to
     * the key stored in the .dbg source-address map.  The map key may be:
     *   • a bare filename:    "init.s"
     *   • a relative path:   "kernal/init.s"
     *   • an absolute path:  "C:/path/to/kernal/init.s"
     * We normalise path separators to '/' and test for an exact match or a
     * slash-bounded suffix match, so "kernal/init.s" matches ".../kernal/init.s"
     * but NOT ".../basic/init.s".
     */
    private matchesEditorPath(editorFsPath: string, mapKey: string): boolean {
        // Lower-case both paths for Windows drive-letter insensitivity:
        // VSCode gives "c:\..." but C# Path APIs give "C:\...", so without
        // lowercasing the exact-match check fails on the drive letter.
        const editorNorm = editorFsPath.replace(/\\/g, '/').toLowerCase();
        const keyNorm = mapKey.replace(/\\/g, '/').toLowerCase();
        return editorNorm === keyNorm || editorNorm.endsWith('/' + keyNorm);
    }

    applyToEditor(editor: vscode.TextEditor): void {
        if (!this.activeSessionId) { editor.setDecorations(this.decorationType, []); return; }
        const fileMap = this.sessionMaps.get(this.activeSessionId);
        if (!fileMap) { editor.setDecorations(this.decorationType, []); return; }

        // Match by path suffix so files with the same basename in different directories
        // (e.g. "kernal/init.s" vs "basic/init.s") are distinguished correctly.
        const editorFsPath = editor.document.uri.fsPath;
        let lineAddrMap: Map<number, string> | undefined;
        for (const [key, val] of fileMap) {
            if (this.matchesEditorPath(editorFsPath, key)) { lineAddrMap = val; break; }
        }

        if (!lineAddrMap) { editor.setDecorations(this.decorationType, []); return; }

        const decorations: vscode.DecorationOptions[] = [];
        for (const [lineNum, addr] of lineAddrMap) {
            const zeroIdx = lineNum - 1;
            if (zeroIdx >= 0 && zeroIdx < editor.document.lineCount) {
                const lineEnd = editor.document.lineAt(zeroIdx).range.end;
                decorations.push({
                    range: new vscode.Range(lineEnd, lineEnd),
                    renderOptions: { after: { contentText: `  ${addr}` } }
                });
            }
        }
        editor.setDecorations(this.decorationType, decorations);

        // Also apply dynamic decoration for the current stopped line (if any)
        this.applyDynamicToEditor(editor);
    }

    /**
     * Applies the dynamic decoration for the current stopped line, but only when
     * that line is not already covered by a static decoration (i.e. macro body lines).
     */
    private applyDynamicToEditor(editor: vscode.TextEditor): void {
        if (!this.currentStopInfo || this.currentStopInfo.sessionId !== this.activeSessionId) {
            editor.setDecorations(this.dynamicDecorationType, []);
            return;
        }

        // The stop file is always an absolute path (from HandleStackTraceAsync).
        // Compare by full path to avoid collisions between files with the same
        // basename in different directories (e.g. kernal/init.s vs basic/init.s).
        const editorFsPath = editor.document.uri.fsPath;
        if (!this.matchesEditorPath(editorFsPath, this.currentStopInfo.file)) {
            editor.setDecorations(this.dynamicDecorationType, []);
            return;
        }

        // If this line already has a static decoration, the dynamic one is not needed
        const fileMap = this.sessionMaps.get(this.currentStopInfo.sessionId);
        if (fileMap) {
            for (const [key, lineMap] of fileMap) {
                if (this.matchesEditorPath(editorFsPath, key) && lineMap.has(this.currentStopInfo.line)) {
                    editor.setDecorations(this.dynamicDecorationType, []);
                    return;
                }
            }
        }

        // Macro body line — apply the dynamic decoration with the actual PC for this invocation
        const zeroIdx = this.currentStopInfo.line - 1;
        if (zeroIdx >= 0 && zeroIdx < editor.document.lineCount) {
            const lineEnd = editor.document.lineAt(zeroIdx).range.end;
            editor.setDecorations(this.dynamicDecorationType, [{
                range: new vscode.Range(lineEnd, lineEnd),
                renderOptions: { after: { contentText: `  ${this.currentStopInfo.addr}` } }
            }]);
        } else {
            editor.setDecorations(this.dynamicDecorationType, []);
        }
    }

    dispose(): void {
        this.decorationType.dispose();
        this.dynamicDecorationType.dispose();
    }
}

/**
 * Hooks into DAP message traffic to:
 *  1. Trigger static address map fetching on the first `stopped` event.
 *  2. Intercept each `stackTrace` response (sent automatically by VSCode after
 *     every stop) to apply a dynamic decoration on the current stopped line —
 *     which is especially useful inside macro bodies where addresses differ per call.
 */
class DotNet6502DebugTrackerFactory implements vscode.DebugAdapterTrackerFactory {
    constructor(private readonly manager: AddressDecorationManager) {}

    createDebugAdapterTracker(session: vscode.DebugSession): vscode.DebugAdapterTracker {
        let pendingStop = false;
        return {
            onDidSendMessage: (message: any) => {
                if (message.type === 'event' && message.event === 'stopped') {
                    this.manager.fetchAndApply(session);
                    pendingStop = true;
                }
                // VSCode automatically sends stackTrace after every stopped event.
                // Intercept the response to get the top frame's PC without extra requests.
                if (pendingStop && message.type === 'response' && message.command === 'stackTrace') {
                    pendingStop = false;
                    const frame = message.body?.stackFrames?.[0];
                    if (frame) {
                        this.manager.onStackFrame(session, frame);
                    }
                }
            }
        };
    }
}
