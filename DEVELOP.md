<h1 align="center">How to develop</h1>

# Requirements
- Windows, Linux, or Mac.
- [.NET 7 SDK](https://dotnet.microsoft.com/download/dotnet/7.0) installed.
- Develop in 
  - VSCode (Windows, Linux, Mac)
  - Visual Studio 2022 (Windows)
  - Or other preferred editor.
- Specifics for Blazor WASM  (WebAssembly) projects
  - Visual Studio 2022: For building the WASM projects, add the component ".NET WebAssembly Build Tools" in Visual Studio Installer.
  - VSCode / command line: For building the WASM projects, install the dotnet workload "wasm-tool", see instruction [here](https://learn.microsoft.com/en-us/aspnet/core/blazor/tooling?view=aspnetcore-7.0&pivots=windows#net-webassembly-build-tools).

# Class diagram overview
See [here](SYSYEM_DIAGRAM.md)

# Tests
Most of the ```Highbyte.DotNet6502``` library code has been developed with a test-first approach.

The [XUnit](https://xunit.net/) library is used.

## Unit tests
To run only unit tests:

```powershell
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
dotnet test --filter TestType=Integration
```

# Code coverage report locally

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
