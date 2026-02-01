import * as vscode from 'vscode';
import * as path from 'path';
import * as fs from 'fs';
import { DebugAdapterExecutable } from 'vscode';
import { Ca65TaskProvider } from './ca65TaskProvider';

let taskProvider: vscode.Disposable | undefined;

export function activate(context: vscode.ExtensionContext) {
    console.log('[6502 Debug] Extension activating...');
    
    // Register debug configuration provider
    context.subscriptions.push(
        vscode.debug.registerDebugConfigurationProvider('dotnet6502', new DebugConfigurationProvider())
    );
    
    // Register debug adapter
    context.subscriptions.push(
        vscode.debug.registerDebugAdapterDescriptorFactory('dotnet6502', new DebugAdapterExecutableFactory())
    );
    
    // Register task provider
    const ca65TaskProvider = new Ca65TaskProvider();
    taskProvider = vscode.tasks.registerTaskProvider(Ca65TaskProvider.Type, ca65TaskProvider);
    context.subscriptions.push(taskProvider);
    
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
    
    // No file watcher needed - single "build current file" task doesn't change
    
    console.log('[6502 Debug] Extension activated successfully');
}

export function deactivate() {
    console.log('[6502 Debug] Extension deactivating...');
    if (taskProvider) {
        taskProvider.dispose();
    }
}

class DebugConfigurationProvider implements vscode.DebugConfigurationProvider {
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
}

class DebugAdapterExecutableFactory implements vscode.DebugAdapterDescriptorFactory {
    createDebugAdapterDescriptor(
        session: vscode.DebugSession,
        executable: vscode.DebugAdapterExecutable | undefined
    ): vscode.ProviderResult<vscode.DebugAdapterDescriptor> {
        
        console.log('[6502 Debug] createDebugAdapterDescriptor called for session:', session.name);
        console.log('[6502 Debug] Session configuration:', JSON.stringify(session.configuration, null, 2));
        
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
