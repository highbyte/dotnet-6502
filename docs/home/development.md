# Development

Requirements and local development setup.

## Requirements

- Windows, Linux, or Mac.

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) installed.

- *Browser project specifics:* To enable build of the browser projects in the solution (Blazor WASM and Avalonia Browser), install the WebAssembly build tools:
    - In the command prompt, change current directory to where the repo is checked out, and run `dotnet workload restore`.
    - Or if you prefer, in Visual Studio Installer, add the component ".NET 10.0 WebAssembly Build Tools".
    - For more info about the WebAssembly build tools, see the [.NET tooling documentation](https://learn.microsoft.com/en-us/aspnet/core/blazor/tooling?view=aspnetcore-10.0&pivots=windows#net-webassembly-build-tools).

- Develop in:
    - VSCode (Windows, Linux, Mac)
    - Visual Studio 2026 (Windows)
    - Or other preferred editor.

## Tests

The [XUnit](https://xunit.net/) library is used.

Tests are currently focused on the core `Highbyte.DotNet6502` library. Most of its code has been developed with a test-first approach.

Tests may expand to parts of system-specific logic code such as `Highbyte.DotNet6502.Systems.Commodore64`.

The browser apps (Blazor WASM and Avalonia Browser) additionally have a [Playwright](https://playwright.dev/) smoke test under `tests/wasm-smoke/` that publishes the apps with the release Release/AOT flags and verifies the result boots in headless Chromium. See [WASM AOT publish smoke tests](#wasm-aot-publish-smoke-tests) below.

## Debugging

### Debugging the emulator via VS Code extension

You can debug the C# emulator code while debugging a 6502 program using the VS Code extension. See [VSCode debugger extension — Development](../tools/vscode-debugger/development.md).

### Unit tests

To run only unit tests (for the `Highbyte.DotNet6502` library):

```powershell
cd tests/Highbyte.DotNet6502.Tests
dotnet test --filter TestType!=Integration
```

### Functional (integration) test

There is a special XUnit test that runs the test code found here: [Functional 6502 test program](https://github.com/Klaus2m5/6502_65C02_functional_tests/blob/master/6502_functional_test.a65).

The purpose of it is to verify if an emulator (or real computer) executes the 6502 instructions correctly.

Notes on the special functional/integration XUnit test:

- It downloads the [Functional 6502 test program](https://github.com/Klaus2m5/6502_65C02_functional_tests/blob/master/6502_functional_test.a65) from the repo.
- It modifies the `.asm` source code to disable Decimal tests.
- It compiles the `.asm` source code with the AS65 assembler (bundled in the repo above).
- It loads the compiled 6502 binary into this emulator, and runs it.
- This emulator is configured to stop executing when the program reaches a specific "success" address (where the program loops forever).
- It currently requires a Windows machine to run due to the AS65 assembler. There may be a Java-version of it that could possibly be used instead.

To run only the special functional/integration XUnit test:

```powershell
cd tests/Highbyte.DotNet6502.Tests
dotnet test --filter TestType=Integration
```

### WASM AOT publish smoke tests

A [Playwright](https://playwright.dev/) suite under `tests/wasm-smoke/` publishes the Blazor WASM and Avalonia Browser apps with the same Release/AOT flags as the GitHub Pages release workflow, then verifies the published `wwwroot` boots in headless Chromium and that the system plug-ins are discovered. Catches trim/AOT-only regressions (missing trim roots, plug-in assemblies dropped from the bundle, ...) that `dotnet build` / `dotnet test` do not exercise.

Requires a .NET 10 SDK with the `wasm-tools` workload installed (see [Requirements](#requirements)) and [Node.js](https://nodejs.org/) (used to drive Playwright).

A wrapper script publishes and runs the matching spec end-to-end:

```powershell
cd tests/wasm-smoke
./run-local.ps1 blazor             # or: avalonia-browser, all
```

```sh
cd tests/wasm-smoke
./run-local.sh blazor              # or: avalonia-browser, all
```

The same flow runs automatically on pull requests (paths-filtered to WASM-relevant source) via the `.github/workflows/wasm-aot-verify.yml` workflow.

## Code coverage report locally (`Highbyte.DotNet6502` library)

Install report-generator global tool:

```powershell
dotnet tool install -g dotnet-reportgenerator-globaltool
```

Generate and show reports (Windows):

```powershell
./codecov-browser.ps1
./codecov-console.ps1
```

Generate and show reports (Linux):

```sh
chmod +x ./codecov-console.sh
./codecov-console.sh
```

## SonarCloud quality gate (locally)

A helper script wraps the existing SonarCloud CI scan with a local gate, useful both for manual verification before opening a PR and as a quality gate for automations.

The `sonarscan-dotnet.yml` workflow runs on push to `feature/**`. After pushing, the gate script waits for that workflow run to complete on the current commit and queries SonarCloud for issues introduced on this branch (using the "new code period" filter, so pre-existing issues on master are not flagged).

Requirements: [`gh`](https://cli.github.com/) (authenticated), `curl`, `jq`. For private projects, set `SONAR_TOKEN`; anonymous read works for public projects.

Run the gate (Linux / macOS):

```sh
./tools/sonar-check.sh           # default: blocks gate on MAJOR severity and above
./tools/sonar-check.sh CRITICAL  # only Critical and Blocker fail the gate
```

Run the gate (Windows):

```powershell
.\tools\sonar-check.ps1
.\tools\sonar-check.ps1 CRITICAL
```

Valid severity threshold values (the gate fails on issues at that level **or any higher level**):

| Value | Gate fails on |
|---|---|
| `INFO` | Info, Minor, Major, Critical, Blocker (strictest — anything fails) |
| `MINOR` | Minor, Major, Critical, Blocker |
| `MAJOR` *(default)* | Major, Critical, Blocker |
| `CRITICAL` | Critical, Blocker |
| `BLOCKER` | Blocker only (most lenient) |

Any other value (or omission of the argument when calling explicitly) is treated as a validation error.

Exit codes:

- `0` — no blocking issues at the threshold; gate passes.
- `1` — blocking issues found; the script prints each one.
- `2` — preflight error (branch not pushed, workflow run not found, missing tool, etc.).

Set `SONAR_INCLUDE_PREEXISTING=1` to disable the "new code period" filter for a full branch audit. The script header has additional details.

## Workaround / compatibility

- [Avalonia Desktop app troubleshooting](../desktop-apps/avalonia-desktop-troubleshooting.md)
- [SilkNetNative app troubleshooting](../desktop-apps/silknet-native-troubleshooting.md)
- [SadConsole troubleshooting](../desktop-apps/sadconsole-troubleshooting.md)

## Using other emulators to verify correct behavior

When in doubt how a specific 6502 instruction actually worked, it was useful to use the monitor in the VICE emulator (which is widely known to be an accurate emulator of the C64 and 6502/6510 CPU) as a reference for stepping through machine code programs.

It was also useful to use VICE to see how graphics and audio is working (or not working) compared to the C64 implementation in this emulator, and use as a reference for general correctness.

### VICE monitor

Monitor commands: <https://vice-emu.sourceforge.io/vice_12.html>

How to load and step through a program in the VICE monitor:

```
l "C:\Source\Repos\dotnet-6502\samples\Assembler\Generic\Build\testprogram.prg" 0 1000
d 1000
r PC=1000
z
r
```
