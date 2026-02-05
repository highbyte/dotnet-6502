import * as vscode from 'vscode';
import * as path from 'path';
import * as fs from 'fs';
import * as net from 'net';
import * as child_process from 'child_process';
import { DebugAdapterExecutable, DebugAdapterServer } from 'vscode';
import { MemoryContentProvider, openMemoryViewer } from './memoryViewer';

export function activate(context: vscode.ExtensionContext) {
    console.log('[6502 Debug] Extension activating...');
    
    // Register memory content provider
    const memoryProvider = new MemoryContentProvider(context);
    context.subscriptions.push(
        vscode.workspace.registerTextDocumentContentProvider('memory', memoryProvider)
    );
    
    // Register debug configuration provider
    context.subscriptions.push(
        vscode.debug.registerDebugConfigurationProvider('dotnet6502', new DebugConfigurationProvider())
    );
    
    // Register debug adapter
    context.subscriptions.push(
        vscode.debug.registerDebugAdapterDescriptorFactory('dotnet6502', new DebugAdapterExecutableFactory())
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
    
    // Register command to view memory
    context.subscriptions.push(
        vscode.commands.registerCommand('dotnet6502.viewMemory', async () => {
            await openMemoryViewer(context, memoryProvider);
        })
    );
    
    console.log('[6502 Debug] Extension activated successfully');
}

export function deactivate() {
    console.log('[6502 Debug] Extension deactivating...');
}

class DebugConfigurationProvider implements vscode.DebugConfigurationProvider {
    private avaloniaProcess: child_process.ChildProcess | undefined;

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
        // If launchAvalonia is true, start the Avalonia Desktop app
        if (config.launchAvalonia) {
            console.log('[6502 Debug] launchAvalonia is true, starting Avalonia Desktop app');
            
            const executablePath = config.avaloniaExecutable;
            if (!executablePath || !fs.existsSync(executablePath)) {
                vscode.window.showErrorMessage(
                    `Avalonia executable not found: ${executablePath}. Please set 'avaloniaExecutable' in your launch configuration.`
                );
                return undefined;
            }

            const debugPort = config.avaloniaDebugPort || 4711;
            const system = config.avaloniaSystem || 'C64';
            const systemVariant = config.avaloniaSystemVariant;
            const waitForReady = config.avaloniaWaitForReady !== false; // Default true
            const loadPrg = config.avaloniaLoadPrg !== false; // Default true
            const runProgram = config.avaloniaRunProgram === true; // Default false
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
                    
                    if (args && Array.isArray(args)) {
                        // Look for -o argument in cl65 task
                        const oIndex = args.indexOf('-o');
                        if (oIndex >= 0 && oIndex + 1 < args.length) {
                            const outputFile = args[oIndex + 1];
                            programPath = path.join(folder.uri.fsPath, outputFile);
                            console.log(`[6502 Debug] Extracted program path from task: ${programPath}`);
                        } else {
                            console.log(`[6502 Debug] Could not find -o argument in task args`);
                        }
                    } else {
                        console.log(`[6502 Debug] Task has no args array`);
                    }
                } else {
                    console.log(`[6502 Debug] Task not found`);
                }
            }

            console.log(`[6502 Debug] Final programPath: ${programPath}, loadPrg: ${loadPrg}, runProgram: ${runProgram}`);

            // For automated Avalonia startup, set program to empty string to prevent auto-detection
            // The debug adapter should attach to the already-running emulator, not launch a new one
            // Avalonia handles loading the program, so the debug adapter shouldn't load it
            config.program = "";  // Empty string prevents auto-detection in debug adapter

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

            console.log(`[6502 Debug] Launching Avalonia: ${executablePath} ${args.join(' ')}`);
            
            try {
                // Kill any existing Avalonia process
                if (this.avaloniaProcess) {
                    console.log('[6502 Debug] Killing existing Avalonia process');
                    this.avaloniaProcess.kill();
                    this.avaloniaProcess = undefined;
                }

                // Launch the Avalonia Desktop app
                const path = require('path');
                const executableDir = path.dirname(executablePath);
                
                const spawnOptions: any = {
                    detached: false,
                    stdio: ['ignore', 'pipe', 'pipe'],  // stdin=ignore, stdout=pipe, stderr=pipe
                    cwd: executableDir
                };
                
                console.log(`[6502 Debug] Spawning: ${executablePath} ${args.join(' ')}`);
                this.avaloniaProcess = child_process.spawn(executablePath, args, spawnOptions);

                this.avaloniaProcess.stdout?.on('data', (data) => {
                    console.log(`[Avalonia] ${data.toString()}`);
                });

                this.avaloniaProcess.stderr?.on('data', (data) => {
                    console.error(`[Avalonia Error] ${data.toString()}`);
                });

                this.avaloniaProcess.on('exit', (code) => {
                    console.log(`[6502 Debug] Avalonia process exited with code ${code}`);
                    if (code !== 0 && code !== null) {
                        vscode.window.showErrorMessage(`Avalonia Desktop app exited with error code ${code}. Check console output for details.`);
                    }
                    this.avaloniaProcess = undefined;
                });

                // Avalonia will start the system, load PRG, and then start TCP server
                // We need to wait for the TCP server to be ready before connecting
                console.log(`[6502 Debug] Avalonia launched, will wait for TCP server on port ${debugPort}`);

                // Don't set debugServer yet - we'll set it after verifying the server is ready
                // Store the port for later use
                config.__avaloniaDebugPort = debugPort;
                config.__waitingForAvalonia = true;

            } catch (error) {
                const errorMsg = `Failed to launch Avalonia Desktop app: ${error}`;
                console.error('[6502 Debug]', errorMsg);
                vscode.window.showErrorMessage(errorMsg);
                return undefined;
            }
        }

        return config;
    }

    dispose() {
        if (this.avaloniaProcess) {
            console.log('[6502 Debug] Disposing: killing Avalonia process');
            this.avaloniaProcess.kill();
            this.avaloniaProcess = undefined;
        }
    }
}

class DebugAdapterExecutableFactory implements vscode.DebugAdapterDescriptorFactory {
    async createDebugAdapterDescriptor(
        session: vscode.DebugSession,
        executable: vscode.DebugAdapterExecutable | undefined
    ): Promise<vscode.DebugAdapterDescriptor | undefined> {
        
        console.log('[6502 Debug] createDebugAdapterDescriptor called for session:', session.name);
        console.log('[6502 Debug] Session configuration:', JSON.stringify(session.configuration, null, 2));
        console.log('[6502 Debug] __waitingForAvalonia:', session.configuration.__waitingForAvalonia);
        console.log('[6502 Debug] debugServer:', session.configuration.debugServer);
        
        // If waiting for Avalonia to start, wait for TCP server to be listening
        if (session.configuration.__waitingForAvalonia) {
            const port = session.configuration.__avaloniaDebugPort;
            console.log(`[6502 Debug] Waiting for Avalonia TCP server on port ${port}...`);
            
            const isReady = await this.waitForTcpServerListening(port, 30000);
            if (!isReady) {
                vscode.window.showErrorMessage(`Avalonia TCP debug server did not start within 30 seconds on port ${port}`);
                return undefined;
            }
            
            console.log(`[6502 Debug] Avalonia TCP server is ready on port ${port}`);
            return this.createTcpDebugAdapter(port);
        }
        
        // Check if this is a TCP connection (attach mode or launch with debugServer)
        const debugServerPort = session.configuration.debugServer;
        if (debugServerPort) {
            console.log(`[6502 Debug] Using TCP connection to port ${debugServerPort}`);
            return this.createTcpDebugAdapter(debugServerPort);
        }
        
        // Otherwise, launch the debug adapter as a child process
        console.log('[6502 Debug] No TCP connection detected, launching debug adapter executable');
        return this.createExecutableDebugAdapter();
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
    
    private createExecutableDebugAdapter(): vscode.DebugAdapterDescriptor | undefined {
        try {
            // Find the debug adapter executable
            // Look for the debug adapter executable
            // It's built in the main repo, not in the workspace folder
            let adapterPath: string | undefined;
            
            // Determine the correct executable name based on the OS
            const executableName = process.platform === 'win32' 
                ? 'Highbyte.DotNet6502.DebugAdapter.exe' 
                : 'Highbyte.DotNet6502.DebugAdapter';

            // Try multiple possible locations
            const possiblePaths = [
                // Development: relative to workspace (when vscode-extension-test is open)
                path.join(__dirname, '..', '..', '..', 'src', 'apps', 'Highbyte.DotNet6502.DebugAdapter', 'bin', 'Debug', 'net10.0', executableName),
                // If workspace is vscode-extension folder
                path.join(__dirname, '..', '..', '..', '..', 'src', 'apps', 'Highbyte.DotNet6502.DebugAdapter', 'bin', 'Debug', 'net10.0', executableName),
                // Release build
                path.join(__dirname, '..', '..', '..', 'src', 'apps', 'Highbyte.DotNet6502.DebugAdapter', 'bin', 'Release', 'net10.0', executableName),
                path.join(__dirname, '..', '..', '..', '..', 'src', 'apps', 'Highbyte.DotNet6502.DebugAdapter', 'bin', 'Release', 'net10.0', executableName),
            ];
            
            console.log('[6502 Debug] Extension __dirname:', __dirname);
            console.log('[6502 Debug] Testing paths for debug adapter:');
            for (const testPath of possiblePaths) {
                const exists = require('fs').existsSync(testPath);
                console.log(`  ${exists ? '✓' : '✗'} ${testPath}`);
                if (exists) {
                    adapterPath = testPath;
                    break;
                }
            }
            
            if (!adapterPath) {
                const msg = 'Could not find the 6502 debug adapter executable. Please build: dotnet build src/apps/Highbyte.DotNet6502.DebugAdapter';
                console.error('[6502 Debug]', msg);
                vscode.window.showErrorMessage(msg);
                return undefined;
            }

            console.log('[6502 Debug] ✓ Using debug adapter:', adapterPath);
            
            const debugAdapterExecutable = new DebugAdapterExecutable(adapterPath, []);
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

    try {
        if (fs.existsSync(tasksJsonPath)) {
            // Read existing tasks.json
            const content = fs.readFileSync(tasksJsonPath, 'utf8');
            // Remove comments for parsing
            const jsonContent = content.replace(/\/\/.*/g, '').replace(/\/\*[\s\S]*?\*\//g, '');
            tasksConfig = JSON.parse(jsonContent);
        } else {
            // Create new tasks.json structure
            tasksConfig = {
                version: '2.0.0',
                tasks: []
            };
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
                tasksConfig.tasks[existingIndex] = newTask;
            } else {
                return;
            }
        } else {
            // Add new task
            tasksConfig.tasks.push(newTask);
        }

        // Write tasks.json with proper formatting
        const jsonString = JSON.stringify(tasksConfig, null, 2);
        fs.writeFileSync(tasksJsonPath, jsonString, 'utf8');

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

    try {
        if (fs.existsSync(launchJsonPath)) {
            const content = fs.readFileSync(launchJsonPath, 'utf8');
            const jsonContent = content.replace(/\/\/.*/g, '').replace(/\/\*[\s\S]*?\*\//g, '');
            launchConfig = JSON.parse(jsonContent);
        } else {
            launchConfig = {
                version: '0.2.0',
                configurations: []
            };
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
            launchConfig.configurations[existingIndex] = newConfig;
        } else {
            launchConfig.configurations.push(newConfig);
        }

        const jsonString = JSON.stringify(launchConfig, null, 2);
        fs.writeFileSync(launchJsonPath, jsonString, 'utf8');

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
            const jsonContent = content.replace(/\/\/.*/g, '').replace(/\/\*[\s\S]*?\*\//g, '');
            const tasksConfig = JSON.parse(jsonContent);
            
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
                
                // Priority 3: Args contain filename
                if (!suggestedTask) {
                    suggestedTask = tasksConfig.tasks.find((t: any) => 
                        t.args && t.args.some((arg: string) => arg.includes(fileName))
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
