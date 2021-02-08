# dotnet-6502

![.NET](https://github.com/highbyte/dotnet-6502/workflows/.NET/badge.svg)[![SonarCloud Quality Gate](https://sonarcloud.io/api/project_badges/measure?project=highbyte_dotnet-6502&metric=alert_status)](https://sonarcloud.io/dashboard?id=highbyte_dotnet-6502) [![SonarCloud Security Rating](https://sonarcloud.io/api/project_badges/measure?project=highbyte_dotnet-6502&metric=security_rating)](https://sonarcloud.io/dashboard?id=highbyte_dotnet-6502) [![SonarCloud Vulnerabilities](https://sonarcloud.io/api/project_badges/measure?project=highbyte_dotnet-6502&metric=vulnerabilities)](https://sonarcloud.io/project/issues?id=highbyte_dotnet-6502&resolved=false&types=VULNERABILITY) [![SonarCloud Reliability Rating](https://sonarcloud.io/api/project_badges/measure?project=highbyte_dotnet-6502&metric=reliability_rating)](https://sonarcloud.io/dashboard?id=highbyte_dotnet-6502) [![SonarCloud Bugs](https://sonarcloud.io/api/project_badges/measure?project=highbyte_dotnet-6502&metric=bugs)](https://sonarcloud.io/project/issues?id=highbyte_dotnet-6502&resolved=false&types=BUG) [![SonarCloud Coverage](https://sonarcloud.io/api/project_badges/measure?project=highbyte_dotnet-6502&metric=coverage)](https://sonarcloud.io/component_measures?id=highbyte_dotnet-6502&metric=coverage&view=list)

A [6502 CPU](https://en.wikipedia.org/wiki/MOS_Technology_6502) emulator for .NET

What it (currently) does/is
- .NET 5 cross platform library
- Emulation of a 6502 processor
- Supports all official 6502 opcodes
- Can load an assembled 6502 program binary and execute it
- Passes this [Functional 6502 test program](https://github.com/Klaus2m5/6502_65C02_functional_tests)
- **_A programming excerise, that may or may not turn into something more_**

What's (currently) missing
- A monitor
- A way for input/output other than loading files to memory and inspecting memory after execution
- Decimal mode (Binary Coded Decimal) calculcations
- Support for unofficial opcodes

What it isn't (and probably never will be)
- An emulation of an entire computer (such as Apple II or Commodore 64)
- The fastest emulator

Inspiration for this library was a [Youtube-series](https://www.youtube.com/watch?v=qJgsuQoy9bc&list=PLLwK93hM93Z13TRzPx9JqTIn33feefl37) about implementing a 6502 emulator in C++

# How to use from a .NET application
- Use Windows, Linux, or Mac
- [.NET 5 SDK](https://dotnet.microsoft.com/download/dotnet/5.0) installed.
- Add reference to Highbyte.DotNet6502.dll 

Example how to call the library (as of now, API may change)
```c#
  var mem = BinaryLoader.Load(
      "C:\Binaries\MyCompiled6502Program.prg", 
      out ushort loadedAtAddress);
      
  var computerBuilder = new ComputerBuilder();
  computerBuilder
      .WithCPU()
      .WithStartAddress(loadedAtAddress)
      .WithMemory(mem);
      
  var computer = computerBuilder.Build();
  computer.Run();  
```  

# How to develop
- Use Windows, Linux, or Mac.
- [.NET 5 SDK](https://dotnet.microsoft.com/download/dotnet/5.0) installed.
- Develop in VSCode (Windows, Linux, Mac), Visual Studio 2019 (Windows), or other preferred editor.

# Tests
Most of the code has been developed with a test-first approach.

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

## Code coverage report locally

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

## 6502 Resources

### 6502 CPU Emulator in C++ video
- https://www.youtube.com/playlist?list=PLLwK93hM93Z13TRzPx9JqTIn33feefl37

### Reference material
- http://www.obelisk.me.uk/6502/index.html
- https://www.atariarchives.org/alp/appendix_1.php
- http://www.6502.org/tutorials/compare_beyond.html
- https://www.c64-wiki.com/wiki/Reset_(Process)
- https://www.c64-wiki.com/wiki/BRK
- https://sta.c64.org/cbm64mem.html
- http://www.emulator101.com/6502-addressing-modes.html
- https://www.pagetable.com/?p=410

### Test programs
- http://visual6502.org/wiki/index.php?title=6502TestPrograms
- https://github.com/Klaus2m5/6502_65C02_functional_tests/blob/master/6502_functional_test.a65
- http://www.csharp4u.com/2017/01/getting-pretty-hex-dump-of-binary-file.html?m=1

### Assemblers
Was used during develoment to compile actual 6502 source code to a binary, and then run it through the emulator.

- https://marketplace.visualstudio.com/items?itemName=rosc.vs64
- https://nurpax.github.io/c64jasm-browser/
- https://skilldrick.github.io/easy6502/#first-program

### Monitors / Emulators
Was used during development to test how certain instructions worked when in doubt.

#### VICE
Monitor commands: https://vice-emu.sourceforge.io/vice_12.html

How to load and step through a program in the VICE monitor
```
l "C:\Source\Repos\dotnet-6502\.cache\Highbyte.DotNet6502.ConsoleUI\testprogram5.prg" 0 1000
d 1000
r PC=1000
z
r
```
