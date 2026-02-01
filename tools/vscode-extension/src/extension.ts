import * as vscode from 'vscode';
import * as path from 'path';
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
