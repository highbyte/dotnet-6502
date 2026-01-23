# VSCode 6502 Debugger Extension

This extension provides debugging support for 6502 machine code programs using the dotnet-6502 emulator.

## Installation

1. Install dependencies:
   ```bash
   npm install
   ```

2. Compile the extension:
   ```bash
   npm run compile
   ```

3. Build the debug adapter:
   ```bash
   cd ..
   dotnet build src/apps/Highbyte.DotNet6502.DebugAdapter
   ```

## Testing

1. **Open the vscode-extension folder in VSCode:**
   ```bash
   cd vscode-extension
   code .
   ```

2. **Press F5** to launch the Extension Development Host
   - This opens a new VSCode window with your extension loaded
   - The `.vscode/launch.json` in the extension folder configures this

3. **In the Extension Development Host window:**
   - Open a folder that contains a .prg file
   - Create a .vscode/launch.json configuration for "6502"
   - Set breakpoints and start debugging

## Architecture

```
VSCode Extension (TypeScript)
    ↓
    Uses DebugAdapterExecutable to launch
    ↓
Debug Adapter (C# Console App)
    ↓
    Controls the 6502 CPU emulator
    ↓
Highbyte.DotNet6502 Library
```

The extension locates the compiled debug adapter executable and launches it. The debug adapter communicates with VSCode using the Debug Adapter Protocol (DAP) via stdin/stdout.
