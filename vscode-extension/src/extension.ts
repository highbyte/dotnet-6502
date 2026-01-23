import * as vscode from 'vscode';
import * as path from 'path';
import { DebugAdapterExecutable } from 'vscode';

export function activate(context: vscode.ExtensionContext) {
    context.subscriptions.push(
        vscode.debug.registerDebugAdapterDescriptorFactory('6502', new DebugAdapterExecutableFactory())
    );
}

export function deactivate() {}

class DebugAdapterExecutableFactory implements vscode.DebugAdapterDescriptorFactory {
    createDebugAdapterDescriptor(
        session: vscode.DebugSession,
        executable: vscode.DebugAdapterExecutable | undefined
    ): vscode.ProviderResult<vscode.DebugAdapterDescriptor> {
        
        // Find the debug adapter executable
        // Look for it in the workspace or in a known location
        const workspaceFolder = vscode.workspace.workspaceFolders?.[0];
        
        // Try to find the compiled debug adapter
        // First check if it's in the workspace (development mode)
        let adapterPath: string;
        
        if (workspaceFolder) {
            // Development: look in the project structure
            const projectPath = path.join(workspaceFolder.uri.fsPath, 'src', 'apps', 'Highbyte.DotNet6502.DebugAdapter', 'bin', 'Debug', 'net10.0', 'Highbyte.DotNet6502.DebugAdapter.exe');
            if (require('fs').existsSync(projectPath)) {
                adapterPath = projectPath;
            } else {
                // Try release build
                const releasePath = path.join(workspaceFolder.uri.fsPath, 'src', 'apps', 'Highbyte.DotNet6502.DebugAdapter', 'bin', 'Release', 'net10.0', 'Highbyte.DotNet6502.DebugAdapter.exe');
                if (require('fs').existsSync(releasePath)) {
                    adapterPath = releasePath;
                } else {
                    vscode.window.showErrorMessage('Could not find the 6502 debug adapter executable. Please build the project first.');
                    return undefined;
                }
            }
        } else {
            vscode.window.showErrorMessage('No workspace folder found. Please open the dotnet-6502 project.');
            return undefined;
        }

        return new DebugAdapterExecutable(adapterPath, []);
    }
}
