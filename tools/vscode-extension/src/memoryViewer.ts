import * as vscode from 'vscode';

export class MemoryContentProvider implements vscode.TextDocumentContentProvider {
    private _onDidChange = new vscode.EventEmitter<vscode.Uri>();
    readonly onDidChange = this._onDidChange.event;

    constructor(private context: vscode.ExtensionContext) {}

    async provideTextDocumentContent(uri: vscode.Uri): Promise<string> {
        // Parse URI: memory:///0xC000-0xC0FF (range format in path for title display)
        const rangeMatch = uri.path.match(/^\/?0x([0-9A-Fa-f]+)-0x([0-9A-Fa-f]+)$/);
        
        let address: number;
        let length: number;
        
        if (rangeMatch) {
            // Range format: 0xC000-0xC0FF
            address = parseInt(rangeMatch[1], 16);
            const endAddr = parseInt(rangeMatch[2], 16);
            length = endAddr - address + 1;
        } else {
            // Legacy format: 0xc000/256
            const legacyMatch = uri.path.match(/^\/?([^\/]+)(?:\/([0-9]+))?$/);
            
            if (!legacyMatch) {
                return `Invalid memory URI format. Expected: memory:///0xC000-0xC0FF\nReceived path: ${uri.path}`;
            }

            const addressStr = legacyMatch[1];
            const lengthStr = legacyMatch[2] || '256';
            
            // Parse address
            if (addressStr.startsWith('0x')) {
                address = parseInt(addressStr.substring(2), 16);
            } else if (addressStr.startsWith('$')) {
                address = parseInt(addressStr.substring(1), 16);
            } else {
                address = parseInt(addressStr);
            }

            length = parseInt(lengthStr);
        }

        // Get active debug session
        const session = vscode.debug.activeDebugSession;
        if (!session || session.type !== 'dotnet6502') {
            return 'No active 6502 debug session';
        }

        try {
            // Send custom request to debug adapter
            const response = await session.customRequest('getMemoryDump', {
                address: address,
                length: length
            });

            if (response.success) {
                return response.content;
            } else {
                return `Error: ${response.message}`;
            }
        } catch (error) {
            return `Error fetching memory: ${error}`;
        }
    }

    update(uri: vscode.Uri) {
        this._onDidChange.fire(uri);
    }
}

export async function openMemoryViewer(context: vscode.ExtensionContext, provider: MemoryContentProvider, address?: string) {
    // Get active debug session
    const session = vscode.debug.activeDebugSession;
    if (!session || session.type !== 'dotnet6502') {
        vscode.window.showWarningMessage('No active 6502 debug session');
        return;
    }

    // Prompt for address if not provided
    if (!address) {
        address = await vscode.window.showInputBox({
            prompt: 'Enter memory address',
            placeHolder: '0x0000 or $0000 or 0',
            value: '0x0000'
        });
        
        if (!address) {
            return; // User cancelled
        }
    }

    // Parse start address
    let startAddr: number;
    if (address.startsWith('0x')) {
        startAddr = parseInt(address.substring(2), 16);
    } else if (address.startsWith('$')) {
        startAddr = parseInt(address.substring(1), 16);
    } else {
        startAddr = parseInt(address);
    }
    
    // Calculate default end address (256 bytes from start)
    const defaultEndAddr = Math.min(startAddr + 0xFF, 0xFFFF);
    const defaultEndStr = `0x${defaultEndAddr.toString(16).toUpperCase().padStart(4, '0')}`;
    
    // Prompt for end address
    const endAddressStr = await vscode.window.showInputBox({
        prompt: 'Enter end address',
        placeHolder: defaultEndStr,
        value: defaultEndStr
    });

    if (!endAddressStr) {
        return; // User cancelled
    }

    // Parse end address
    let endAddr: number;
    if (endAddressStr.startsWith('0x')) {
        endAddr = parseInt(endAddressStr.substring(2), 16);
    } else if (endAddressStr.startsWith('$')) {
        endAddr = parseInt(endAddressStr.substring(1), 16);
    } else {
        endAddr = parseInt(endAddressStr);
    }
    
    // Validate range
    if (endAddr < startAddr) {
        vscode.window.showErrorMessage('End address must be >= start address');
        return;
    }
    
    const length = endAddr - startAddr + 1;
    const rangeTitle = `0x${startAddr.toString(16).toUpperCase().padStart(4, '0')}-0x${endAddr.toString(16).toUpperCase().padStart(4, '0')}`;
    
    // Open memory document with range as path for proper title display
    const uri = vscode.Uri.parse(`memory:///${rangeTitle}`);
    const doc = await vscode.workspace.openTextDocument(uri);
    await vscode.window.showTextDocument(doc, { preview: false });
}
