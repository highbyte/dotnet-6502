<h1 align="center">Requirements and local development setup</h1>

# Requirements
- Windows, Linux, or Mac.
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) installed.
- Develop in 
  - VSCode (Windows, Linux, Mac)
  - Visual Studio 2022 (Windows)
  - Or other preferred editor.
- Specifics for Blazor WASM (`Highbyte.DotNet6502.App.WASM`) project
  - Visual Studio 2022: For building the WASM projects, add the component ".NET WebAssembly Build Tools" in Visual Studio Installer.
  - VSCode / command line: For building the WASM projects, install the dotnet workload "wasm-tool", see instruction [here](https://learn.microsoft.com/en-us/aspnet/core/blazor/tooling?view=aspnetcore-8.0&pivots=windows#net-webassembly-build-tools).

# Class diagram overview
See [here](SYSYEM_DIAGRAM.md)

# Tests
The [XUnit](https://xunit.net/) library is used.

Tests are currently focused on the core `Highbyte.DotNet6502` library. Most of its code has been developed with a test-first approach.

Tests may expand to parts of system-specific logic code such as `Highbyte.DotNet6502.Systems.CommodoreC64`.

## Unit tests
To run only unit tests (for the `Highbyte.DotNet6502` library)

```powershell
cd tests/Highbyte.DotNet6502.Tests
dotnet test --filter TestType!=Integration
```

## Functional (integration) test
There is a special XUnit test that is running the test code found here: [Functional 6502 test program](https://github.com/Klaus2m5/6502_65C02_functional_tests/blob/master/6502_functional_test.a65)

The purpose of it is to verify if an emulator (or real computer) executes the 6502 instructions correctly.

Notes on the special functional/integration XUnit test 
- it downloads the [Functional 6502 test program](https://github.com/Klaus2m5/6502_65C02_functional_tests/blob/master/6502_functional_test.a65) from the repo
- it modifies the .asm source code to disable Decimal tests
- it compiles the .asm source code with the AS65 assembler (bundled in the repo above)
- it loads the compiled 6502 binary into this emulator, and runs it.
- this emulator is configured to stop executing when the program reaches a specific "success" address (where the program loops forever).
- it currently requires a Windows machine to run due to the AS65 assembler. There may be a Java-version of it that could possibly be used instead.

To run only the special functional/integration XUnit test:

```powershell
cd tests/Highbyte.DotNet6502.Tests
dotnet test --filter TestType=Integration
```

# Code coverage report locally (`Highbyte.DotNet6502` library)

Install report-generator global tool
```powershell
dotnet tool install -g dotnet-reportgenerator-globaltool
```

Generate and show reports (Windows)
```powershell
./codecov-browser.ps1
./codecov-console.ps1
```

Generate and show reports (Linux)
```shell
chmod +x ./codecov-console.sh
./codecov-console.sh
```


# Using other emulators to verify correct behavior
When in doubt how a specific 6502 instruction actually worked, it was useful to use the monitor in the VICE emulator (that is widely known to be an accurate emulator of C64 and 6502/6510 CPU) as a reference for stepping through machine code programs.

It was also useful to use VICE to see how graphics and audio is working (or not working) compared to the C64 implementation in this emulator, and use as a reference for general correctness.

## VICE monitor
Monitor commands: https://vice-emu.sourceforge.io/vice_12.html

How to load and step through a program in the VICE monitor
```
l "C:\Source\Repos\dotnet-6502\samples\Assembler\Generic\Build\testprogram.prg" 0 1000
d 1000
r PC=1000
z
r
```
