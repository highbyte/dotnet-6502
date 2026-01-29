# Testing the 6502 Debugger - Quick Guide

## Step 1: Build the Test Program

Run the build script:
```powershell
.\build-test-program.ps1
```

This assembles `test-program.asm` → `test-program.prg` using ACME.

## Step 2: Test in Extension Development Host

Since you already have the Extension Development Host window open:

1. **In the Extension Development Host window**, open **the test folder**:
   - File → Open Folder → select `vscode-extension-test/`
   - **Important**: Don't open `vscode-extension/` - that's for extension development!
   - The test folder has its own `.vscode/launch.json` for debugging 6502 programs

2. **The launch.json should already be there**:
   - `vscode-extension-test/.vscode/launch.json` is pre-configured
   - Or create `.vscode/launch.json` manually with:
   ```json
   {
     "version": "0.2.0",
     "configurations": [
       {
         "type": "6502",
         "request": "launch",
         "name": "Debug test-program.prg",
         "program": "${workspaceFolder}/test-program.prg",
         "stopOnEntry": true
       }
     ]
   }
   ```

3. **Set breakpoints**:
   - Open `test-program.asm` 
   - Remember: Line number = memory address
   - The program loads at `$0600` (address 1536)
   - So set a breakpoint at line 1536 to break at start

4. **Press F5** to start debugging

5. **Expected results**:
   - Debug Console shows "Loaded test-program.prg at $0600"
   - Variables panel shows:
     - Registers: PC=$0600, SP=$FF, A=$00, X=$00, Y=$00
     - Flags: N=0, V=0, B=0, D=0, I=0, Z=0, C=0
   - Call Stack shows disassembled instruction
   - Can step through with F10

## What the Test Program Does

```
$0600: LDA #$01   ; Load 1 into A
$0602: STA $00    ; Store to zero page
$0604: LDX #$05   ; Load 5 into X
$0606: DEX        ; Decrement X (loop)
$0607: BNE $0606  ; Branch if not zero
$0609: LDA $00    ; Load from zero page
$060B: CLC        ; Clear carry
$060C: ADC #$02   ; Add 2
$060E: BRK        ; Break (stop)
```

## Troubleshooting

### "Could not find the 6502 debug adapter executable"
Make sure you've built the debug adapter:
```bash
cd ..
dotnet build src/apps/Highbyte.DotNet6502.DebugAdapter
```

### Breakpoints not working
- Line number must equal memory address in hex
- Line 1536 = $0600
- Try setting breakpoint at line 1536, 1538, 1540, etc.

### Variables not showing
- Make sure you've hit "stopOnEntry" or a breakpoint first
- Variables only populate when execution is paused

## Next Steps

After validating the debugger works:
- Try your own .prg files
- Test with samples from the main repo
- Report any bugs or issues
