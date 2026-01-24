import * as vscode from 'vscode';
import * as path from 'path';
import { DebugAdapterExecutable } from 'vscode';

export function activate(context: vscode.ExtensionContext) {
    console.log('[6502 Debug] Extension activating...');
    context.subscriptions.push(
        vscode.debug.registerDebugAdapterDescriptorFactory('dotnet6502', new DebugAdapterExecutableFactory())
    );
    console.log('[6502 Debug] Extension activated successfully');
}

export function deactivate() {
    console.log('[6502 Debug] Extension deactivating...');
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
            
            // Try multiple possible locations
            const possiblePaths = [
                // Development: relative to workspace (when vscode-extension-test is open)
                path.join(__dirname, '..', '..', 'src', 'apps', 'Highbyte.DotNet6502.DebugAdapter', 'bin', 'Debug', 'net10.0', 'Highbyte.DotNet6502.DebugAdapter.exe'),
                // If workspace is vscode-extension folder
                path.join(__dirname, '..', '..', '..', 'src', 'apps', 'Highbyte.DotNet6502.DebugAdapter', 'bin', 'Debug', 'net10.0', 'Highbyte.DotNet6502.DebugAdapter.exe'),
                // Release build
                path.join(__dirname, '..', '..', 'src', 'apps', 'Highbyte.DotNet6502.DebugAdapter', 'bin', 'Release', 'net10.0', 'Highbyte.DotNet6502.DebugAdapter.exe'),
                path.join(__dirname, '..', '..', '..', 'src', 'apps', 'Highbyte.DotNet6502.DebugAdapter', 'bin', 'Release', 'net10.0', 'Highbyte.DotNet6502.DebugAdapter.exe'),
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
