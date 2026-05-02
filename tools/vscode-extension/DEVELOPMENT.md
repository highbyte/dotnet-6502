# Development

How to build, run, and debug the extension and emulator from source.

See also [IMPLEMENTATION.md](IMPLEMENTATION.md) for the extension's internal architecture.

**Clone the repo** (required for all sections below):
```sh
git clone https://github.com/highbyte/dotnet-6502.git
cd dotnet-6502
```

## Installing the VSCode extension from a .vsix package

Build and install the extension locally.

1. **Build the .vsix package**:

   _macOS/Linux_:
   ```sh
   cd tools/vscode-extension
   ./publish.sh
   ```

   _Windows_:
   ```powershell
   cd tools\vscode-extension
   .\publish.ps1
   ```

2. **Install in VS Code** (path printed by the script):
   ```sh
   code --install-extension "tools/vscode-extension/publish/dotnet-6502-debugger-0.1.0.vsix"
   ```

3. **Build the .NET debug adapters** (from repo root):
   ```sh
   dotnet build src/apps/Highbyte.DotNet6502.DebugAdapter
   dotnet build src/apps/Avalonia/Highbyte.DotNet6502.App.Avalonia.Desktop
   ```

To uninstall the VSCode extension:
```sh
code --uninstall-extension highbyte.dotnet-6502-debugger
```

## Running/debugging the extension from source

Use this approach if you want to develop or debug the extension itself.

1. **Install [Node.js](https://nodejs.org/)** v20 or later.

2. **Install extension dependencies** (first time only):
   ```sh
   cd tools/vscode-extension
   npm install
   ```

3. **Compile the extension**:
   ```sh
   npm run compile
   ```

4. **Build the .NET debug adapters** (from repo root):
   ```sh
   dotnet build src/apps/Highbyte.DotNet6502.DebugAdapter
   dotnet build src/apps/Avalonia/Highbyte.DotNet6502.App.Avalonia.Desktop
   ```

5. **Open the vscode-extension folder in VSCode**:
   ```sh
   cd tools/vscode-extension
   code .
   ```

6. **Launch Extension Development Host**:
   - In VSCode (with vscode-extension folder open), press **F5**
   - A new "Extension Development Host" window opens with the extension loaded

## Debugging the emulator C# Code

**Install the required development dependencies**,
see [Development](../../docs/home/development.md).

**Window 1 - Extension Test (6502 Debugging)**:
1. Open `/tools/vscode-extension-test/` folder in VS Code
2. Open your 6502 test program (e.g., `test-program.asm`)
3. Press **F5** to start debugging the 6502 program
4. The debug adapter process will start in the background

**Window 2 - Debugging the C# Code**:
1. Open the repository root folder in a separate VS Code window
2. Set breakpoints in the C# code you want to debug:
   - `src/apps/Highbyte.DotNet6502.DebugAdapter/DebugAdapterLogic.cs` - Debug adapter protocol handling
   - `src/libraries/Highbyte.DotNet6502/CPU.cs` - 6502 CPU emulation
   - Any other emulator code
3. Go to Run & Debug → **"Attach to DotNet6502 VSCode Debug Adapter"**
4. Press **F5** to attach to the running debug adapter

Now when you step through 6502 instructions in Window 1, VS Code will hit your C# breakpoints in Window 2, allowing you to:
- See exactly how each 6502 instruction is executed
- Debug the debug adapter protocol implementation
- Step through the emulator's internal logic
- Inspect memory, CPU state, and other internals

**Tips**:
- Set breakpoints in `HandleNextAsync()` or `HandleContinueAsync()` in `DebugAdapterLogic.cs` to catch every step
- Set breakpoints in `CPU.Execute()` to see each 6502 instruction execution
- Use the Variables panel in Window 2 to inspect the emulator's internal state
