import * as vscode from 'vscode';
import * as path from 'node:path';
import * as fs from 'node:fs';
import * as net from 'node:net';
import * as child_process from 'node:child_process';
import { DebugAdapterExecutable, DebugAdapterServer } from 'vscode';
import { MemoryContentProvider, openMemoryViewer } from './memoryViewer';
import * as jsonc from 'jsonc-parser';

const outputChannel = vscode.window.createOutputChannel('dotnet-6502 Debugger');
const DEFAULT_DEBUG_HOST = '127.0.0.1';

export function activate(context: vscode.ExtensionContext) {
    outputChannel.appendLine('[6502 Debug] Extension activating...');

    const memoryProvider = new MemoryContentProvider();
    const configProvider = new DebugConfigurationProvider();
    const addressDecorManager = new AddressDecorationManager();

    context.subscriptions.push(
        outputChannel,
        // Memory + debug infrastructure
        vscode.workspace.registerTextDocumentContentProvider('memory', memoryProvider),
        vscode.debug.registerDebugConfigurationProvider('dotnet6502', configProvider),
        vscode.debug.registerDebugAdapterDescriptorFactory('dotnet6502', new DebugAdapterExecutableFactory()),
        // Kill the emulator process when a launch+emulator debug session ends (safety net
        // in case the .NET app doesn't exit on its own via terminateDebuggee).
        vscode.debug.onDidTerminateDebugSession((session) => {
            if (session.type === 'dotnet6502' &&
                session.configuration.request === 'launch' &&
                session.configuration.debugAdapter === 'emulator') {
                configProvider.killEmulatorProcess();
            }
        }),
        // Inline address decorations: show $XXXX after each mapped source line
        addressDecorManager,
        vscode.debug.registerDebugAdapterTrackerFactory(
            'dotnet6502',
            new DotNet6502DebugTrackerFactory(addressDecorManager)
        ),
        vscode.debug.onDidTerminateDebugSession((session) => {
            if (session.type === 'dotnet6502') {
                addressDecorManager.onSessionEnded(session);
            }
        }),
        vscode.window.onDidChangeActiveTextEditor((editor) => {
            if (editor) { addressDecorManager.applyToEditor(editor); }
        }),
        // Commands
        vscode.commands.registerCommand('dotnet6502.generateBuildTask', async (uri: vscode.Uri) => {
            await generateBuildTask(uri);
        }),
        vscode.commands.registerCommand('dotnet6502.generateLaunchConfig', async (uri: vscode.Uri) => {
            await generateLaunchConfigCommand(uri);
        }),
        vscode.commands.registerCommand('dotnet6502.generateEmulatorLaunchConfig', async (uri: vscode.Uri) => {
            await generateEmulatorLaunchConfigCommand(uri);
        }),
        vscode.commands.registerCommand('dotnet6502.generatePrgLaunchConfig', async (uri: vscode.Uri) => {
            await generatePrgLaunchConfigCommand(uri);
        }),
        vscode.commands.registerCommand('dotnet6502.generateRemoteAttachConfig', async (uri: vscode.Uri) => {
            await generateRemoteAttachConfigCommand(uri);
        }),
        vscode.commands.registerCommand('dotnet6502.viewMemory', async () => {
            await openMemoryViewer(memoryProvider);
        }),
        vscode.commands.registerCommand('dotnet6502.jumpToLine', async (...args: any[]) => {
            console.log(`[6502 Debug] jumpToLine invoked with ${args.length} args:`, JSON.stringify(args.map(a => a?.toString?.() ?? a)));

            const session = vscode.debug.activeDebugSession;
            if (session?.type !== 'dotnet6502') {
                console.log('[6502 Debug] jumpToLine: no active dotnet6502 session');
                return;
            }

            const editor = vscode.window.activeTextEditor;
            if (!editor) {
                console.log('[6502 Debug] jumpToLine: no active editor');
                return;
            }

            const { line, sourcePath } = parseJumpToLineArgs(args, editor);
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

    checkDependencies();

    outputChannel.appendLine('[6502 Debug] Extension activated successfully');
}

export function deactivate() {
    outputChannel.appendLine('[6502 Debug] Extension deactivating...');
}

/**
 * Resolves the target line and source path from VSCode's jump-to-line command arguments.
 * VSCode passes different argument shapes depending on the trigger point (editor gutter,
 * command palette, line decorator), so we accept all known forms and fall back to the
 * editor's cursor position when no explicit line is supplied.
 */
function parseJumpToLineArgs(
    args: any[],
    editor: vscode.TextEditor
): { line: number; sourcePath: string } {
    let line: number | undefined;
    let sourcePath = editor.document.uri.fsPath;

    for (const arg of args) {
        if (arg instanceof vscode.Uri) {
            sourcePath = arg.fsPath;
        } else if (typeof arg === 'number') {
            line = arg;
        } else if (arg && typeof arg === 'object' && 'lineNumber' in arg && typeof arg.lineNumber === 'number') {
            line = arg.lineNumber;
        }
    }

    // Convert 0-based cursor position to 1-based line number for the fallback.
    return { line: line ?? editor.selection.active.line + 1, sourcePath };
}

/**
 * Command to generate a remote attach launch configuration with pathMappings pre-filled.
 * Prompts the user for: remote host, port, remote workspace root, remote dbgFile path.
 */
async function generateRemoteAttachConfigCommand(uri: vscode.Uri | undefined): Promise<void> {
    const workspaceFolder = uri
        ? vscode.workspace.getWorkspaceFolder(uri)
        : vscode.workspace.workspaceFolders?.[0];

    if (!workspaceFolder) {
        vscode.window.showErrorMessage('A workspace folder is required to generate a remote attach config.');
        return;
    }

    const remoteHost = await vscode.window.showInputBox({
        prompt: 'Remote debug adapter host IP or hostname',
        placeHolder: '192.168.1.100',
        validateInput: v => v.trim() ? undefined : 'Host is required'
    });
    if (!remoteHost) { return; }

    const remotePortStr = await vscode.window.showInputBox({
        prompt: 'Remote debug adapter TCP port',
        value: '6502',
        validateInput: v => /^\d+$/.test(v.trim()) ? undefined : 'Enter a valid port number'
    });
    if (!remotePortStr) { return; }
    const remotePort = Number.parseInt(remotePortStr.trim(), 10);

    const remoteRoot = await vscode.window.showInputBox({
        prompt: 'Project root directory on the remote machine (remoteRoot)',
        placeHolder: '/home/ubuntu/project',
        validateInput: v => v.trim() ? undefined : 'Remote root is required'
    });
    if (!remoteRoot) { return; }

    const remoteDbgFile = await vscode.window.showInputBox({
        prompt: 'Path to the .dbg file on the remote machine (leave blank to skip)',
        placeHolder: `${remoteRoot.trim()}/program.dbg`
    });

    const configName = `Remote Attach to ${remoteHost.trim()} (dotnet-6502)`;
    const newConfig: Record<string, unknown> = {
        type: 'dotnet6502',
        request: 'attach',
        name: configName,
        debugHost: remoteHost.trim(),
        debugPort: remotePort,
        pathMappings: [
            {
                localRoot: '${workspaceFolder}',
                remoteRoot: remoteRoot.trim()
            }
        ],
        useRemoteSources: true,
        stopOnEntry: true
    };
    if (remoteDbgFile?.trim()) {
        newConfig.dbgFile = remoteDbgFile.trim();
    }

    await upsertLaunchConfiguration(workspaceFolder, newConfig, 'remote-attach');
}

// ---------------------------------------------------------------------------
// Dependency checking
// ---------------------------------------------------------------------------

interface Dependency {
    /** Human-readable name shown in notifications */
    name: string;
    /** Executable to look for in PATH */
    checkExecutable: string;
    /** Short description of what it is */
    description: string;
    /** Terminal command to pre-fill (not auto-run). Undefined = no simple command available. */
    installCommand: string | undefined;
    /** URL to open for more info / manual install instructions */
    docsUrl: string;
}

function isInPath(executable: string): boolean {
    const findCmd = process.platform === 'win32' ? 'where' : 'which';
    const result = child_process.spawnSync(findCmd, [executable], { stdio: 'ignore' });
    return result.status === 0;
}

function getEmulatorDependency(): Dependency {
    let installCommand: string | undefined;
    if (process.platform === 'darwin' && isInPath('brew')) {
        installCommand = 'brew tap highbyte/dotnet-6502 && brew install --cask dotnet-6502';
    } else if (process.platform === 'linux' && isInPath('brew')) {
        installCommand = 'brew tap highbyte/dotnet-6502 && brew install --formula dotnet-6502';
    } else if (process.platform === 'win32' && isInPath('scoop')) {
        installCommand = 'scoop bucket add dotnet-6502 https://github.com/highbyte/scoop-dotnet-6502 && scoop install dotnet-6502';
    }
    // If no package manager found, installCommand remains undefined and docs are shown instead
    return {
        name: 'dotnet-6502 emulator',
        checkExecutable: 'dotnet-6502',
        description: 'Required for emulator mode debugging (debugAdapter: "emulator").',
        installCommand,
        docsUrl: 'https://highbyte.github.io/dotnet-6502/docs/desktop-apps/installation/',
    };
}

function getCc65Dependency(): Dependency {
    // cc65 is checked via ca65 (the assembler), which is always installed alongside it
    let installCommand: string | undefined;
    if (process.platform === 'darwin' && isInPath('brew')) {
        installCommand = 'brew install cc65';
    }
    // Linux and Windows have no simple one-liner; docs are opened instead
    return {
        name: 'cc65 toolchain',
        checkExecutable: 'ca65',
        description: 'Required for building .asm files and source-level debugging.',
        installCommand,
        docsUrl: 'https://cc65.github.io/getting-started.html',
    };
}

function checkDependencies(): void {
    const missing = [getEmulatorDependency(), getCc65Dependency()].filter(dep => {
        const found = isInPath(dep.checkExecutable);
        outputChannel.appendLine(`[checkDependencies] '${dep.checkExecutable}' ${found ? 'found' : 'not found'} in PATH`);
        return !found;
    });

    if (missing.length === 0) { return; }

    const platformHint = process.platform === 'win32' ? '(Scoop)' : '(Homebrew)';
    const names = missing.map(d => d.name).join(', ');
    const hasInstallCommands = missing.some(d => d.installCommand);

    const actions: string[] = [];
    if (hasInstallCommands) {
        actions.push(`Show install commands ${platformHint}`);
    }
    actions.push('More info');

    vscode.window.showWarningMessage(
        `dotnet-6502: required tools not found: ${names}. Install them to use all extension features.`,
        ...actions
    ).then(chosen => {
        if (chosen?.startsWith('Show install commands')) {
            const withCommand = missing.filter(d => d.installCommand);
            const withoutCommand = missing.filter(d => !d.installCommand);

            // Combine all available install commands into one terminal command chain
            if (withCommand.length > 0) {
                const combined = withCommand.map(d => d.installCommand!).join(' && ');
                const terminal = vscode.window.createTerminal('dotnet-6502 setup');
                terminal.show();

                // Send the command only after the shell is ready to avoid a race condition
                // that causes the text to appear twice (once in raw output, once at the prompt).
                // Use the shell integration event (VS Code 1.90+) when available, which fires
                // exactly when the shell is ready. Fall back to a timeout for older versions.
                let sent = false;
                const sendCommand = () => {
                    if (!sent) {
                        sent = true;
                        terminal.sendText(combined, false); // false = pre-fill but don't run
                    }
                };

                if ('onDidChangeTerminalShellIntegration' in vscode.window) {
                    const disposable = (vscode.window as any).onDidChangeTerminalShellIntegration((e: any) => {
                        if (e.terminal === terminal) {
                            disposable.dispose();
                            sendCommand();
                        }
                    });
                }
                // Fallback: also use a timeout in case shell integration is disabled or unavailable
                setTimeout(sendCommand, 2000);
            }

            // For tools with no install command, open their docs
            for (const dep of withoutCommand) {
                vscode.env.openExternal(vscode.Uri.parse(dep.docsUrl));
            }
        } else if (chosen === 'More info') {
            for (const dep of missing) {
                vscode.env.openExternal(vscode.Uri.parse(dep.docsUrl));
            }
        }
    });
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
 * Package manager names for executables distributed via Homebrew/Scoop.
 * Maps base executable name (without .exe) to the shorter name available in PATH
 * after a package manager install. These are tried first before the original name.
 */
const PACKAGE_MANAGER_NAMES: Record<string, string> = {
    'Highbyte.DotNet6502.App.Avalonia.Desktop': 'dotnet-6502',
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
        return verifyFullPathExecutable(executablePath);
    }

    const baseName = executablePath.replace(/\.exe$/, '');

    const fromPath = findExecutableInSystemPath(executablePath, baseName);
    if (fromPath) { return fromPath; }

    const fromRepo = findExecutableInRepoBuildOutput(executablePath, baseName);
    if (fromRepo) { return fromRepo; }

    outputChannel.show(true);
    vscode.window.showErrorMessage(
        `Executable '${executablePath}' not found in system PATH or in repo build output. Either add it to PATH, build the project, or set 'emulatorExecutable' to a full path in your launch configuration.`
    );
    return undefined;
}

function verifyFullPathExecutable(executablePath: string): string | undefined {
    if (fs.existsSync(executablePath)) {
        return executablePath;
    }
    vscode.window.showErrorMessage(
        `Executable not found: ${executablePath}. Please verify the path in 'emulatorExecutable' in your launch configuration.`
    );
    return undefined;
}

/**
 * Searches the system PATH for the executable, preferring the package-manager
 * canonical name (e.g. installed via Homebrew/Scoop) before falling back to the
 * original executable name.
 */
function findExecutableInSystemPath(executablePath: string, baseName: string): string | undefined {
    const findCmd = process.platform === 'win32' ? 'where' : 'which';
    const packageManagerName = PACKAGE_MANAGER_NAMES[baseName];
    const pathCandidates = packageManagerName
        ? [packageManagerName, executablePath]
        : [executablePath];

    for (const candidate of pathCandidates) {
        const result = child_process.spawnSync(findCmd, [candidate], { stdio: 'ignore' });
        if (result.status === 0) {
            outputChannel.appendLine(`[resolveExecutable] Found '${candidate}' in system PATH`);
            return candidate;
        }
        outputChannel.appendLine(`[resolveExecutable] '${candidate}' not found in system PATH`);
    }
    outputChannel.appendLine(`[resolveExecutable] Not found in PATH, trying repo-relative paths...`);
    return undefined;
}

/**
 * Tries known repo-relative build-output locations for the executable.
 * Repo roots are discovered by walking up from each workspace folder until a
 * .git directory is found (or up to 5 levels), plus __dirname three levels up
 * to support the Extension Development Host running from source.
 */
function findExecutableInRepoBuildOutput(executablePath: string, baseName: string): string | undefined {
    const repoRootCandidates = collectRepoRootCandidates();

    outputChannel.appendLine(`[resolveExecutable] workspace folders: ${(vscode.workspace.workspaceFolders ?? []).map(f => f.uri.fsPath).join(', ') || '(none)'}`);
    outputChannel.appendLine(`[resolveExecutable] repo root candidates: ${repoRootCandidates.join(', ')}`);

    const projectDir = REPO_EXECUTABLE_LOCATIONS[baseName];
    if (!projectDir) { return undefined; }

    for (const repoRoot of repoRootCandidates) {
        for (const buildConfig of ['Debug', 'Release']) {
            const candidatePath = path.join(repoRoot, projectDir, 'bin', buildConfig, 'net10.0', executablePath);
            if (fs.existsSync(candidatePath)) {
                outputChannel.appendLine(`[resolveExecutable] Found: ${candidatePath}`);
                return candidatePath;
            }
            outputChannel.appendLine(`[resolveExecutable] Not found: ${candidatePath}`);
        }
    }
    return undefined;
}

function collectRepoRootCandidates(): string[] {
    const repoRootCandidates: string[] = [];
    for (const folder of vscode.workspace.workspaceFolders ?? []) {
        let dir = folder.uri.fsPath;
        for (let i = 0; i <= 5; i++) {
            if (!repoRootCandidates.includes(dir)) {
                repoRootCandidates.push(dir);
            }
            if (fs.existsSync(path.join(dir, '.git'))) {
                outputChannel.appendLine(`[resolveExecutable] Found .git at: ${dir}`);
                break;
            }
            const parent = path.dirname(dir);
            if (parent === dir) { break; } // filesystem root
            dir = parent;
        }
    }
    repoRootCandidates.push(path.join(__dirname, '..', '..', '..'));
    return repoRootCandidates;
}

function resolveTaskCwd(task: vscode.Task, folder: vscode.WorkspaceFolder): string {
    // Output files (e.g. cwd="${workspaceFolder}/samples") are resolved relative to this.
    const executionCwd: string | undefined =
        (task.execution as any)?.options?.cwd ?? task.definition.options?.cwd;
    return executionCwd
        ? executionCwd.replace(/\$\{workspaceFolder\}/g, folder.uri.fsPath)
        : folder.uri.fsPath;
}

function extractProgramPathFromArgs(args: any[], taskCwd: string): string | undefined {
    // Look for -o argument in cl65 task
    const oIndex = args.indexOf('-o');
    if (oIndex >= 0 && oIndex + 1 < args.length) {
        const programPath = path.join(taskCwd, args[oIndex + 1]);
        outputChannel.appendLine(`[6502 Debug] Extracted program path from task: ${programPath}`);
        return programPath;
    }
    outputChannel.appendLine(`[6502 Debug] Could not find -o argument in task args`);
    return undefined;
}

function extractDbgFileFromArgs(args: any[], taskCwd: string): string | undefined {
    // Auto-detect dbgFile from -Wl --dbgfile,<file> arg.
    for (const arg of args) {
        const argStr = typeof arg === 'string' ? arg : arg?.value;
        if (typeof argStr === 'string' && argStr.startsWith('--dbgfile,')) {
            return path.join(taskCwd, argStr.slice('--dbgfile,'.length));
        }
    }
    return undefined;
}

interface EmulatorArgsOptions {
    debugPort: number;
    system: string;
    systemVariant?: string;
    waitForReady: boolean;
    loadPrg: boolean;
    programPath?: string;
    runProgram: boolean;
}

function buildEmulatorArgs(opts: EmulatorArgsOptions): string[] {
    const args = [
        '--enableExternalDebug',
        '--debug-port', opts.debugPort.toString(),
        '--console-log',        // Enable console logging (stdout is piped to VSCode)
        '--no-console-window',  // Suppress the separate log window; logs flow via the pipe
        '--system', opts.system,
        '--start'
    ];
    if (opts.systemVariant) { args.push('--systemVariant', opts.systemVariant); }
    if (opts.waitForReady) { args.push('--waitForSystemReady'); }
    if (opts.loadPrg && opts.programPath) { args.push('--loadPrg', opts.programPath); }
    if (opts.runProgram) { args.push('--runLoadedProgram'); }
    return args;
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

    private getDebugHost(config: vscode.DebugConfiguration): string {
        const debugHost = typeof config.debugHost === 'string' ? config.debugHost.trim() : '';
        return debugHost || DEFAULT_DEBUG_HOST;
    }

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
        if (config.request === 'attach') {
            return this.applyAttachConfig(config);
        }

        if (!this.applyDebugAdapterDefaults(config)) {
            return undefined;
        }

        const resolvedExecutable = resolveExecutable(config.emulatorExecutable);
        if (!resolvedExecutable) {
            return undefined;
        }
        config.emulatorExecutable = resolvedExecutable;

        if (config.debugAdapter === 'emulator') {
            const launched = await this.launchEmulatorMode(config, folder);
            if (!launched) { return undefined; }
        }

        return config;
    }

    private applyAttachConfig(config: vscode.DebugConfiguration): vscode.DebugConfiguration {
        const debugPort = config.debugPort || 6502;
        const debugHost = this.getDebugHost(config);
        outputChannel.appendLine(`[6502 Debug] Attach mode: connecting to emulator on ${debugHost}:${debugPort}`);

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
        config.__emulatorDebugHost = debugHost;
        config.__waitingForEmulator = true;
        return config;
    }

    private applyDebugAdapterDefaults(config: vscode.DebugConfiguration): boolean {
        if (!config.debugAdapter) {
            config.debugAdapter = 'minimal';
        }

        if (config.debugAdapter !== 'minimal' && config.debugAdapter !== 'emulator') {
            vscode.window.showErrorMessage(
                `Invalid debugAdapter value: ${config.debugAdapter}. Must be 'minimal' or 'emulator'.`
            );
            return false;
        }

        if (!config.emulatorExecutable) {
            // Currently only Highbyte.DotNet6502.App.Avalonia.Desktop is supported as emulator host.
            const defaultExecutable = config.debugAdapter === 'minimal'
                ? 'Highbyte.DotNet6502.DebugAdapter.ConsoleApp'
                : 'Highbyte.DotNet6502.App.Avalonia.Desktop';
            config.emulatorExecutable = platformExecutableName(defaultExecutable);
            outputChannel.appendLine(`[6502 Debug] Using default emulatorExecutable for '${config.debugAdapter}' mode: ${config.emulatorExecutable}`);
        }
        return true;
    }

    private async launchEmulatorMode(
        config: vscode.DebugConfiguration,
        folder: vscode.WorkspaceFolder | undefined
    ): Promise<boolean> {
        outputChannel.appendLine('[6502 Debug] debugAdapter is emulator, starting emulator host app');

        const executablePath: string = config.emulatorExecutable;
        const debugPort: number = config.debugPort || 6502;
        const debugHost = this.getDebugHost(config);
        let programPath: string | undefined = config.program;

        outputChannel.appendLine(`[6502 Debug] Initial programPath: ${programPath}`);
        outputChannel.appendLine(`[6502 Debug] preLaunchTask: ${config.preLaunchTask}`);

        if (!programPath && config.preLaunchTask && folder) {
            programPath = await this.extractProgramAndDbgFileFromTask(config, folder);
        }

        const loadPrg = config.loadProgram !== false; // Default true
        const runProgram = config.runProgram === true; // Default false
        outputChannel.appendLine(`[6502 Debug] Final programPath: ${programPath}, loadPrg: ${loadPrg}, runProgram: ${runProgram}`);

        // Propagate the auto-detected (or config-supplied) program path back onto
        // config so the debug adapter receives it for .dbg file resolution.
        if (programPath && !config.program) {
            config.program = programPath;
        }
        // The emulator host handles loading the program into memory, so tell the
        // debug adapter not to load it again (but still use the path for debug symbols
        // and program bounds).
        config.__programAlreadyLoaded = true;

        const args = buildEmulatorArgs({
            debugPort,
            system: config.system || 'C64',
            systemVariant: config.systemVariant,
            waitForReady: config.waitForSystemReady !== false, // Default true
            loadPrg,
            programPath,
            runProgram
        });
        outputChannel.appendLine(`[6502 Debug] Launching emulator host: ${executablePath} ${args.join(' ')}`);

        if (!this.spawnEmulatorHost(executablePath, args, config)) {
            return false;
        }

        // Emulator host will start the system, load PRG, and then start TCP server.
        // The descriptor factory waits for that server before connecting.
        outputChannel.appendLine(`[6502 Debug] Emulator host launched, will wait for TCP server on ${debugHost}:${debugPort}`);
        config.__emulatorDebugPort = debugPort;
        config.__emulatorDebugHost = debugHost;
        config.__waitingForEmulator = true;
        return true;
    }

    /**
     * Mines the cl65 (or compatible) preLaunchTask to auto-detect the produced
     * program path (-o) and the debug-symbols file (--dbgfile,). Mutates
     * config.dbgFile when found and not already set; returns the program path.
     */
    private async extractProgramAndDbgFileFromTask(
        config: vscode.DebugConfiguration,
        folder: vscode.WorkspaceFolder
    ): Promise<string | undefined> {
        const tasks = await vscode.tasks.fetchTasks();
        const task = tasks.find(t => t.name === config.preLaunchTask);
        outputChannel.appendLine(`[6502 Debug] Found task: ${task?.name}, definition: ${JSON.stringify(task?.definition)}`);
        if (!task) {
            outputChannel.appendLine(`[6502 Debug] Task not found`);
            return undefined;
        }

        // For shell tasks, args might be in definition.args or in task execution.
        let args = task.definition.args;
        if (!args && task.execution && 'args' in task.execution) {
            args = (task.execution as any).args;
        }
        outputChannel.appendLine(`[6502 Debug] Task args: ${JSON.stringify(args)}`);

        const taskCwd = resolveTaskCwd(task, folder);
        outputChannel.appendLine(`[6502 Debug] Task cwd (resolved): ${taskCwd}`);

        if (!args || !Array.isArray(args)) {
            outputChannel.appendLine(`[6502 Debug] Task has no args array`);
            return undefined;
        }

        const programPath = extractProgramPathFromArgs(args, taskCwd);

        if (!config.dbgFile) {
            const dbgFile = extractDbgFileFromArgs(args, taskCwd);
            if (dbgFile) {
                config.dbgFile = dbgFile;
                outputChannel.appendLine(`[6502 Debug] Extracted dbgFile from task: ${dbgFile}`);
            }
        }
        return programPath;
    }

    private spawnEmulatorHost(
        executablePath: string,
        args: string[],
        config: vscode.DebugConfiguration
    ): boolean {
        try {
            if (this.emulatorProcess) {
                outputChannel.appendLine('[6502 Debug] Killing existing emulator process');
                this.emulatorProcess.kill();
                this.emulatorProcess = undefined;
            }

            const executableDir = path.dirname(executablePath);
            const env = { ...process.env, ...config.env };
            const spawnOptions: child_process.SpawnOptions = {
                detached: false,
                stdio: ['ignore', 'pipe', 'pipe'],  // stdin=ignore, stdout=pipe, stderr=pipe
                cwd: executableDir,
                env
            };

            outputChannel.appendLine(`[6502 Debug] Spawning: ${executablePath} ${args.join(' ')}`);
            outputChannel.appendLine(`[6502 Debug] Environment variables: ${JSON.stringify(config.env)}`);
            this.emulatorProcess = child_process.spawn(executablePath, args, spawnOptions);

            this.emulatorProcess.stdout?.on('data', (data) => {
                outputChannel.appendLine(`[Emulator Host] ${data.toString().trimEnd()}`);
            });
            this.emulatorProcess.stderr?.on('data', (data) => {
                outputChannel.appendLine(`[Emulator Host Error] ${data.toString().trimEnd()}`);
            });
            this.emulatorProcess.on('exit', (code) => {
                outputChannel.appendLine(`[6502 Debug] Emulator host process exited with code ${code}`);
                if (code !== 0 && code !== null) {
                    vscode.window.showErrorMessage(`Emulator host app exited with error code ${code}. Check console output for details.`);
                }
                this.emulatorProcess = undefined;
            });
            return true;
        } catch (error) {
            const errorMsg = `Failed to launch emulator host app: ${error}`;
            outputChannel.appendLine(`[6502 Debug] ${errorMsg}`);
            vscode.window.showErrorMessage(errorMsg);
            return false;
        }
    }

    killEmulatorProcess() {
        if (this.emulatorProcess) {
            outputChannel.appendLine('[6502 Debug] Killing emulator host process (debug session ended)');
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
        
        outputChannel.appendLine(`[6502 Debug] createDebugAdapterDescriptor called for session: ${session.name}`);
        outputChannel.appendLine(`[6502 Debug] __waitingForEmulator: ${session.configuration.__waitingForEmulator}`);

        // Emulator mode: wait for the emulator's TCP debug server, then connect
        if (session.configuration.__waitingForEmulator) {
            const port = session.configuration.__emulatorDebugPort;
            const host = session.configuration.__emulatorDebugHost || DEFAULT_DEBUG_HOST;
            const isAttach = session.configuration.request === 'attach';
            // Attach mode: emulator should already be running, so use a short default timeout.
            // Launch mode: emulator needs time to boot, so use a longer default timeout.
            const defaultTimeout = isAttach ? 5 : 120;
            const timeoutSeconds = session.configuration.startupTimeout || defaultTimeout;
            const timeoutMs = timeoutSeconds * 1000;
            outputChannel.appendLine(`[6502 Debug] Waiting for emulator host TCP server on ${host}:${port} (timeout: ${timeoutSeconds}s, mode: ${isAttach ? 'attach' : 'launch'})...`);

            const isReady = await this.waitForTcpServerListening(host, port, timeoutMs);
            if (!isReady) {
                const hint = isAttach ? ' Is the emulator running with --enableExternalDebug?' : '';
                vscode.window.showErrorMessage(`Emulator host TCP debug server did not respond within ${timeoutSeconds} seconds on ${host}:${port}.${hint}`);
                return undefined;
            }

            outputChannel.appendLine(`[6502 Debug] Emulator host TCP server is ready on ${host}:${port}`);
            return this.createTcpDebugAdapter(host, port);
        }

        // Minimal mode: launch the debug adapter as a child process (STDIO)
        const executablePath = session.configuration.emulatorExecutable;
        outputChannel.appendLine(`[6502 Debug] Launching minimal debug adapter executable: ${executablePath}`);
        return this.createExecutableDebugAdapter(executablePath);
    }

    private async waitForTcpServerListening(host: string, port: number, timeoutMs: number): Promise<boolean> {
        // Wait for the TCP server to start accepting connections
        // Note: This will create one connection that the server accepts but we immediately close
        // The server will handle this gracefully (it expects a DAP initialize message)
        const startTime = Date.now();

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
                    
                    socket.connect(port, host);
                });
                
                // Port is listening - wait a moment for the server to reject the empty connection
                await new Promise(resolve => setTimeout(resolve, 200));
                return true;
            } catch {
                // Port not ready yet, wait and retry
                await new Promise(resolve => setTimeout(resolve, 500));
            }
        }
        
        return false;
    }
    
    private createTcpDebugAdapter(host: string, port: number): vscode.DebugAdapterDescriptor | undefined {
        try {
            const server = new DebugAdapterServer(port, host);
            outputChannel.appendLine(`[6502 Debug] ✓ Created DebugAdapterServer for ${host}:${port}`);
            return server;
        } catch (error) {
            const errorMsg = `[6502 Debug] Error creating TCP debug adapter: ${error}`;
            outputChannel.appendLine(errorMsg);
            vscode.window.showErrorMessage(errorMsg);
            return undefined;
        }
    }
    
    private createExecutableDebugAdapter(executablePath: string): vscode.DebugAdapterDescriptor | undefined {
        try {
            outputChannel.appendLine(`[6502 Debug] ✓ Using debug adapter: ${executablePath}`);
            const debugAdapterExecutable = new DebugAdapterExecutable(executablePath, []);
            outputChannel.appendLine('[6502 Debug] ✓ Created DebugAdapterExecutable, returning to VSCode');
            return debugAdapterExecutable;
        } catch (error) {
            const errorMsg = `[6502 Debug] Error in createDebugAdapterDescriptor: ${error}`;
            outputChannel.appendLine(errorMsg);
            vscode.window.showErrorMessage(errorMsg);
            return undefined;
        }
    }
}

/**
 * Generate a build task for a .asm file in tasks.json
 */
async function generateBuildTask(uri: vscode.Uri): Promise<void> {
    if (!uri?.fsPath.endsWith('.asm')) {
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
            if (!value.match(/^(0x[0-9a-fA-F]+|\$[0-9a-fA-F]+|\d+)$/)) {
                return 'Enter a valid address (e.g., 0xc000, $c000, or 49152)';
            }
            return undefined;
        }
    });

    if (!startAddress) {
        return; // User cancelled
    }

    // Compute cwd: use ${workspaceFolder}-relative path when possible, else absolute
    const fileDir = path.dirname(uri.fsPath);
    const wsRoot = workspaceFolder.uri.fsPath;
    let taskCwd: string;
    if (fileDir === wsRoot) {
        taskCwd = '${workspaceFolder}';
    } else if (fileDir.startsWith(wsRoot + path.sep)) {
        const relDir = path.relative(wsRoot, fileDir).split(path.sep).join('/');
        taskCwd = `\${workspaceFolder}/${relDir}`;
    } else {
        taskCwd = fileDir;
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
            '-Wl', { value: `-Ln,${fileBasename}.lbl`, quoting: 'strong' },
            '-Wl', { value: `--dbgfile,${fileBasename}.dbg`, quoting: 'strong' },
            '-Wl', { value: `-m,${fileBasename}.map`, quoting: 'strong' }
        ],
        options: {
            cwd: taskCwd
        },
        problemMatcher: '$ca65',
        group: {
            kind: 'build',
            isDefault: false
        }
    };

    const tasksJsonPath = path.join(workspaceFolder.uri.fsPath, '.vscode', 'tasks.json');
    const written = await upsertBuildTaskInTasksJson(tasksJsonPath, workspaceFolder, taskLabel, newTask);
    if (!written) { return; }

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
}

/**
 * Reads (or creates) tasks.json, upserts a task by label using jsonc.modify to
 * preserve comments, and writes the file. Prompts before overwriting an
 * existing task with the same label. Returns true if the file was written.
 */
async function upsertBuildTaskInTasksJson(
    tasksJsonPath: string,
    workspaceFolder: vscode.WorkspaceFolder,
    taskLabel: string,
    newTask: Record<string, unknown>
): Promise<boolean> {
    let tasksConfig: any;
    let content: string;
    const fileExists = fs.existsSync(tasksJsonPath);

    try {
        if (fileExists) {
            content = fs.readFileSync(tasksJsonPath, 'utf8');
            tasksConfig = jsonc.parse(content);
        } else {
            tasksConfig = { version: '2.0.0', tasks: [] };
            content = JSON.stringify(tasksConfig, null, 2);
            const vscodeDir = path.join(workspaceFolder.uri.fsPath, '.vscode');
            if (!fs.existsSync(vscodeDir)) {
                fs.mkdirSync(vscodeDir, { recursive: true });
            }
        }

        if (!tasksConfig.tasks) {
            tasksConfig.tasks = [];
        }

        const existingIndex = tasksConfig.tasks.findIndex((t: any) => t.label === taskLabel);
        if (existingIndex >= 0) {
            const overwrite = await vscode.window.showWarningMessage(
                `Task "${taskLabel}" already exists. Overwrite?`,
                'Yes', 'No'
            );
            if (overwrite !== 'Yes') { return false; }
        }

        const jsonPath = existingIndex >= 0
            ? ['tasks', existingIndex]
            : ['tasks', -1];
        const edits = jsonc.modify(content, jsonPath, newTask, {
            formattingOptions: { tabSize: 2, insertSpaces: true }
        });
        content = jsonc.applyEdits(content, edits);

        fs.writeFileSync(tasksJsonPath, content, 'utf8');
        return true;
    } catch (error) {
        vscode.window.showErrorMessage(`Failed to create task: ${error}`);
        outputChannel.appendLine(`[6502 Debug] Error generating task: ${error}`);
        return false;
    }
}

/**
 * Reads (or creates) launch.json, upserts a configuration by name using
 * jsonc.modify to preserve comments, writes the file and opens it in the editor.
 */
async function upsertLaunchConfiguration(
    workspaceFolder: vscode.WorkspaceFolder,
    newConfig: Record<string, unknown>,
    logContext: string
): Promise<void> {
    const configName = newConfig.name as string;
    const launchJsonPath = path.join(workspaceFolder.uri.fsPath, '.vscode', 'launch.json');
    let launchConfig: any;
    let content: string;
    const fileExists = fs.existsSync(launchJsonPath);

    try {
        if (fileExists) {
            content = fs.readFileSync(launchJsonPath, 'utf8');
            launchConfig = jsonc.parse(content);
        } else {
            launchConfig = { version: '0.2.0', configurations: [] };
            content = JSON.stringify(launchConfig, null, 2);
        }

        if (!launchConfig.configurations) {
            launchConfig.configurations = [];
        }

        const existingIndex = launchConfig.configurations.findIndex((c: any) => c.name === configName);
        const jsonPath = existingIndex >= 0
            ? ['configurations', existingIndex]
            : ['configurations', -1];
        const edits = jsonc.modify(content, jsonPath, newConfig, {
            formattingOptions: { tabSize: 2, insertSpaces: true }
        });
        content = jsonc.applyEdits(content, edits);

        fs.writeFileSync(launchJsonPath, content, 'utf8');

        vscode.window.showInformationMessage(
            `Launch configuration "${configName}" created! Press F5 to debug.`
        );

        const doc = await vscode.workspace.openTextDocument(launchJsonPath);
        await vscode.window.showTextDocument(doc);
    } catch (error) {
        vscode.window.showErrorMessage(`Failed to create launch config: ${error}`);
        outputChannel.appendLine(`[6502 Debug] Error generating ${logContext} launch config: ${error}`);
    }
}

/**
 * Picks the best-matching build task label by priority:
 *   1. Exact extension-generated pattern: `Build <fileName> (C64)`
 *   2. Label contains the basename (case-insensitive)
 *   3. Task's command or args reference the filename
 */
function pickSuggestedTask(tasks: any[], fileName: string, fileBasename: string): string | undefined {
    return tasks.find((t: any) => t.label === `Build ${fileName} (C64)`)?.label
        ?? tasks.find((t: any) => t.label.toLowerCase().includes(fileBasename.toLowerCase()))?.label
        ?? tasks.find((t: any) =>
            (t.command && typeof t.command === 'string' && t.command.includes(fileName)) ||
            t.args?.some((arg: string) => arg.includes(fileName))
        )?.label;
}

/**
 * Reads tasks.json and returns the best-matching build task label for the
 * given .asm file, optionally prompting the user. Returns undefined if no
 * task is selected (user cancelled or no tasks exist).
 */
async function findBuildTaskForFile(
    workspaceFolder: vscode.WorkspaceFolder,
    uri: vscode.Uri,
    fileName: string,
    fileBasename: string
): Promise<string | undefined> {
    const tasksJsonPath = path.join(workspaceFolder.uri.fsPath, '.vscode', 'tasks.json');
    let availableTasks: string[] = [];
    let suggestedTask: string | undefined;

    if (fs.existsSync(tasksJsonPath)) {
        try {
            const content = fs.readFileSync(tasksJsonPath, 'utf8');
            const tasksConfig = jsonc.parse(content);
            if (tasksConfig.tasks) {
                availableTasks = tasksConfig.tasks.map((t: any) => t.label);
                suggestedTask = pickSuggestedTask(tasksConfig.tasks, fileName, fileBasename);
            }
        } catch {
            // Ignore parse errors
        }
    }

    if (availableTasks.length === 0) {
        const result = await vscode.window.showWarningMessage(
            `No build tasks found. Would you like to create one first?`,
            'Generate Build Task',
            'Cancel'
        );
        if (result === 'Generate Build Task') {
            await generateBuildTask(uri);
        }
        return undefined;
    }

    if (availableTasks.length === 1) {
        return availableTasks[0];
    }

    if (suggestedTask) {
        return suggestedTask;
    }

    return vscode.window.showQuickPick(availableTasks, {
        placeHolder: 'Select a build task to use as preLaunchTask',
        canPickMany: false
    });
}

/**
 * Generate a launch configuration that uses the build task
 */
async function generateLaunchConfig(
    workspaceFolder: vscode.WorkspaceFolder,
    taskLabel: string,
    fileBasename: string
): Promise<void> {
    await upsertLaunchConfiguration(workspaceFolder, {
        type: 'dotnet6502',
        request: 'launch',
        name: `Debug ${fileBasename}.asm`,
        preLaunchTask: taskLabel,
        system: 'C64',
        stopOnEntry: true,
        stopOnBRK: true
    }, 'launch');
}

/**
 * Command to generate launch config - prompts user to select an existing task
 */
async function generateLaunchConfigCommand(uri: vscode.Uri): Promise<void> {
    if (!uri?.fsPath.endsWith('.asm')) {
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

    const taskLabel = await findBuildTaskForFile(workspaceFolder, uri, fileName, fileBasename);
    if (!taskLabel) { return; }

    await generateLaunchConfig(workspaceFolder, taskLabel, fileBasename);
}

/**
 * Command to generate emulator launch config - prompts user to select an existing task
 */
async function generateEmulatorLaunchConfigCommand(uri: vscode.Uri): Promise<void> {
    if (!uri?.fsPath.endsWith('.asm')) {
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

    const taskLabel = await findBuildTaskForFile(workspaceFolder, uri, fileName, fileBasename);
    if (!taskLabel) { return; }

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
    await upsertLaunchConfiguration(workspaceFolder, {
        type: 'dotnet6502',
        request: 'launch',
        name: `Launch Full Emulator Host with C64 to Source Debug ${fileBasename}.asm`,
        preLaunchTask: taskLabel,
        debugAdapter: 'emulator',
        system: 'C64',
        waitForSystemReady: true,
        loadProgram: true,
        runProgram: true,
        stopOnEntry: false,
        stopOnBRK: true
    }, 'emulator');
}

/**
 * Command to generate emulator launch config for .prg files
 */
async function generatePrgLaunchConfigCommand(uri: vscode.Uri): Promise<void> {
    if (!uri?.fsPath.endsWith('.prg')) {
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
    await upsertLaunchConfiguration(workspaceFolder, {
        type: 'dotnet6502',
        request: 'launch',
        name: `Launch C64 emulator with ${fileBasename}.prg`,
        debugAdapter: 'emulator',
        program: `\${workspaceFolder}/${fileName}`,
        system: 'C64',
        waitForSystemReady: true,
        loadProgram: true,
        runProgram: true,
        stopOnEntry: false,
        stopOnBRK: false
    }, '.prg');
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
                    const hex = addr.toString(16).toUpperCase().padStart(4, '0');
                    lineMap.set(Number.parseInt(lineStr), `$${hex}`);
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

        const addrNum = Number.parseInt(addrRef.replace(/^0x/i, ''), 16);
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
 * Translates source file paths between the local (VS Code) machine and the remote
 * (debug adapter) machine based on user-configured pathMappings.
 *
 * Used only for attach configurations with a non-empty pathMappings array.
 * Longest-prefix matching: when multiple entries apply, the one with the longest
 * remoteRoot (or localRoot) prefix wins.
 */
class RemotePathMapper {
    private readonly byRemote: Array<{ localRoot: string; remoteRoot: string }>;
    private readonly byLocal: Array<{ localRoot: string; remoteRoot: string }>;

    constructor(mappings: Array<{ localRoot: string; remoteRoot: string }>) {
        this.byRemote = [...mappings].sort((a, b) => b.remoteRoot.length - a.remoteRoot.length);
        this.byLocal  = [...mappings].sort((a, b) => b.localRoot.length  - a.localRoot.length);
    }

    hasMapping(): boolean { return this.byRemote.length > 0; }

    /** Translates a remote (adapter) path to a local (VS Code) path. Pass-through if no match. */
    toLocal(remotePath: string): string {
        if (!remotePath) { return remotePath; }
        const norm = remotePath.replace(/\\/g, '/');
        for (const { localRoot, remoteRoot } of this.byRemote) {
            const rr = remoteRoot.replace(/\\/g, '/');
            if (norm.toLowerCase().startsWith(rr.toLowerCase() + '/') ||
                norm.toLowerCase() === rr.toLowerCase()) {
                const suffix = norm.slice(rr.length);
                return localRoot.replace(/\//g, path.sep) + suffix.replace(/\//g, path.sep);
            }
        }
        return remotePath;
    }

    /** Translates a local (VS Code) path to a remote (adapter) path. Pass-through if no match. */
    toRemote(localPath: string): string {
        if (!localPath) { return localPath; }
        const norm = localPath.replace(/\\/g, '/');
        for (const { localRoot, remoteRoot } of this.byLocal) {
            const lr = localRoot.replace(/\\/g, '/');
            if (norm.toLowerCase().startsWith(lr.toLowerCase() + '/') ||
                norm.toLowerCase() === lr.toLowerCase()) {
                const suffix = norm.slice(lr.length);
                return remoteRoot.replace(/\\/g, '/') + suffix.replace(/\\/g, '/');
            }
        }
        return localPath;
    }
}

/** Rewrites source.path inside a DAP message object in-place. Returns the modified object. */
function translateSourcePath(obj: any, translate: (p: string) => string): any {
    if (!obj || typeof obj !== 'object') { return obj; }
    if (obj.source && typeof obj.source === 'object' && typeof obj.source.path === 'string') {
        obj.source.path = translate(obj.source.path);
    }
    return obj;
}

/**
 * Hooks into DAP message traffic to:
 *  1. Trigger static address map fetching on the first `stopped` event.
 *  2. Intercept each `stackTrace` response (sent automatically by VSCode after
 *     every stop) to apply a dynamic decoration on the current stopped line —
 *     which is especially useful inside macro bodies where addresses differ per call.
 *  3. When the session is an attach with pathMappings, perform bidirectional path
 *     translation so VS Code always sees local paths and the adapter always sees
 *     remote paths.
 */
class DotNet6502DebugTrackerFactory implements vscode.DebugAdapterTrackerFactory {
    constructor(private readonly manager: AddressDecorationManager) {}

    createDebugAdapterTracker(session: vscode.DebugSession): vscode.DebugAdapterTracker {
        let pendingStop = false;

        const rawMappings: any[] = (session.configuration.request === 'attach' &&
            Array.isArray(session.configuration.pathMappings))
            ? session.configuration.pathMappings
            : [];
        const mapper = new RemotePathMapper(rawMappings);
        const hasMapper = mapper.hasMapping();

        return {
            // VS Code → adapter: translate local paths to remote paths in requests
            onWillReceiveMessage: hasMapper ? (message: any) => {
                if (message?.type !== 'request') { return; }
                const args = message.arguments;
                if (!args) { return; }
                const cmd: string = message.command;

                if (cmd === 'setBreakpoints' || cmd === 'breakpointLocations' || cmd === 'gotoTargets' || cmd === 'source') {
                    translateSourcePath(args, p => mapper.toRemote(p));
                }
            } : undefined,

            // Adapter → VS Code: translate remote paths to local paths in responses/events
            onDidSendMessage: (message: any) => {
                if (message.type === 'event' && message.event === 'stopped') {
                    this.manager.fetchAndApply(session);
                    pendingStop = true;
                }

                if (hasMapper) {
                    applyResponsePathMappings(message, mapper);
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

/**
 * Translates remote→local paths inside DAP responses/events that carry source
 * references the user-facing VSCode side needs to be able to open.
 */
function applyResponsePathMappings(message: any, mapper: RemotePathMapper): void {
    if (message.type === 'response' && message.command === 'stackTrace' && message.body?.stackFrames) {
        for (const frame of message.body.stackFrames) {
            translateSourcePath(frame, p => mapper.toLocal(p));
        }
    } else if (message.type === 'response' && message.command === 'setBreakpoints' && message.body?.breakpoints) {
        for (const bp of message.body.breakpoints) {
            translateSourcePath(bp, p => mapper.toLocal(p));
        }
    } else if (message.type === 'event' && (message.event === 'loadedSource' || message.event === 'output')) {
        translateSourcePath(message.body, p => mapper.toLocal(p));
    }
}
