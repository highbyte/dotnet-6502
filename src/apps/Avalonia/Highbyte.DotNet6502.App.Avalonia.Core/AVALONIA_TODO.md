Safari notice:
- Proceed Button goes to site root, not root of the app.
- Non-safari browsers on iOS gets the Safari notice. Is this expected?

Fix unit test failure (SonarScan):
  [xUnit.net 00:00:02.29]     Highbyte.DotNet6502.Tests.Instructions.TXA_test.TXA_Clears_Zero_Flag_If_Result_Is_Not_Zero [FAIL]
  Failed Highbyte.DotNet6502.Tests.Instructions.TXA_test.TXA_Clears_Zero_Flag_If_Result_Is_Not_Zero [< 1 ms]
  Error Message:
   System.Exception : Instruction list in InstructionList.GetAllInstructions() is not up to date. It must include all Instruction implementations.
  Stack Trace:
     at Highbyte.DotNet6502.InstructionList.GetAllInstructions() in /github/workspace/src/libraries/Highbyte.DotNet6502/InstructionList.cs:line 122

Multiple GH pages at same time
- Get error from one of them: Canceling since a higher priority waiting request for pages build and deployment @ gh-pages exists

Directory.Packages.props
- Implement it in the src root so all projects can use it, not just those in Avalonia folder.

Control C64 joystick with Controller
- Is there built-in support in Avalonia for both desktop and browser?

Audio:
- Implement via existing NAudio code in AvaDesktop app
- Maybe for future in Avalonia Browser: Investigate audio/synth alternatives that is compatible with WebAssembly. Preferably without having to write browser WebAudio JS API interop code.


