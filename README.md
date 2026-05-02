<p align="center">
  <img src="resources/images/logo.png" width="5%" height="5%" title="DotNet 6502 logo">
</p>
<h2 align="center">
  A <a href="https://en.wikipedia.org/wiki/MOS_Technology_6502">6502 CPU</a> emulator for .NET
</h2>

[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4)](https://highbyte.github.io/dotnet-6502/docs/home/development/)
[![language](https://img.shields.io/badge/language-C%23-239120)](https://highbyte.github.io/dotnet-6502/docs/home/development/)
[![OS](https://img.shields.io/badge/OS-windows%2C%20macOS%2C%20linux-0078D4)](https://highbyte.github.io/dotnet-6502/docs/desktop-apps/installation/)
[![WebAssembly](https://img.shields.io/badge/WebAssembly-654FF0?logo=webassembly&logoColor=fff)](https://highbyte.github.io/dotnet-6502/docs/web-apps/overview/)
[![SonarCloud Quality Gate](https://sonarcloud.io/api/project_badges/measure?project=highbyte_dotnet-6502&metric=alert_status)](https://sonarcloud.io/dashboard?id=highbyte_dotnet-6502)
[![SonarCloud Security Rating](https://sonarcloud.io/api/project_badges/measure?project=highbyte_dotnet-6502&metric=security_rating)](https://sonarcloud.io/dashboard?id=highbyte_dotnet-6502)
[![SonarCloud Vulnerabilities](https://sonarcloud.io/api/project_badges/measure?project=highbyte_dotnet-6502&metric=vulnerabilities)](https://sonarcloud.io/project/issues?id=highbyte_dotnet-6502&resolved=false&types=VULNERABILITY)
[![SonarCloud Reliability Rating](https://sonarcloud.io/api/project_badges/measure?project=highbyte_dotnet-6502&metric=reliability_rating)](https://sonarcloud.io/dashboard?id=highbyte_dotnet-6502)
[![SonarCloud Bugs](https://sonarcloud.io/api/project_badges/measure?project=highbyte_dotnet-6502&metric=bugs)](https://sonarcloud.io/project/issues?id=highbyte_dotnet-6502&resolved=false&types=BUG)
[![SonarCloud Coverage](https://sonarcloud.io/api/project_badges/measure?project=highbyte_dotnet-6502&metric=coverage)](https://sonarcloud.io/component_measures?id=highbyte_dotnet-6502&metric=coverage&view=list)
[![.NET](https://github.com/highbyte/dotnet-6502/actions/workflows/dotnet.yml/badge.svg)](https://github.com/highbyte/dotnet-6502/actions/workflows/dotnet.yml)
[![CodeQL](https://github.com/highbyte/dotnet-6502/actions/workflows/codeql-analysis.yml/badge.svg)](https://github.com/highbyte/dotnet-6502/actions/workflows/codeql-analysis.yml)
[![NuGet Version](https://img.shields.io/nuget/v/Highbyte.DotNet6502)](https://www.nuget.org/packages/Highbyte.DotNet6502/)
[![GitHub Release](https://img.shields.io/github/v/release/highbyte/dotnet-6502?include_prereleases)](#)
[![GitHub Release Date](https://img.shields.io/github/release-date-pre/highbyte/dotnet-6502)](#)
[![GitHub last commit](https://img.shields.io/github/last-commit/highbyte/dotnet-6502)](#)
[![GitHub License](https://img.shields.io/github/license/highbyte/dotnet-6502)](LICENSE)

# Overview

.NET cross-platform libraries and applications for executing 6502 CPU machine code, and emulating specific computer systems (such as Commodore 64) in different UI contexts — browser, desktop, and headless.

> [!IMPORTANT]
> This is mainly a programming exercise that may or may not turn into something more. See [Limitations](#limitations) below.

## 📖 Documentation

Full documentation lives at **<https://highbyte.github.io/dotnet-6502/docs/>** — apps, libraries, tools, systems, and development guides.

## Try it in your browser

| [Avalonia WebAssembly](https://highbyte.se/dotnet-6502/app2) | [Blazor WebAssembly](https://highbyte.se/dotnet-6502/app) |
| ------------------------------------------------------------ | --------------------------------------------------------- |
| <a href="https://highbyte.se/dotnet-6502/app2" target="_blank"><img src="doc/Screenshots/AvaloniaBrowser_C64_Montezuma.png" title="Avalonia WebAssembly app, C64 Montezuma's Revenge"/></a> | <a href="https://highbyte.se/dotnet-6502/app" target="_blank"><img src="doc/Screenshots/BlazorWASM_C64_LastNinja.png" title="Blazor WebAssembly app, C64 Last Ninja"/></a> |

## Other apps and features

- **Desktop apps** for Windows, Linux, and macOS — Avalonia, SadConsole, and SilkNet variants. See [Desktop apps](https://highbyte.github.io/dotnet-6502/docs/desktop-apps/installation/).
- **Headless app** for automation, scripting, and CI workflows — no UI, controlled via CLI and Lua. See [Headless](https://highbyte.github.io/dotnet-6502/docs/desktop-apps/headless/).
- **VS Code debugger extension** for source and disassembly debugging of 6502 code. See [VSCode debugger](https://highbyte.github.io/dotnet-6502/docs/tools/vscode-debugger/debugging/).
- **Lua scripting** for driving the emulator — selecting systems, controlling emulation, reading/writing memory, injecting input. See [Scripting](https://highbyte.github.io/dotnet-6502/docs/tools/scripting/overview/).
- **TCP remote control** lets external processes inspect and drive a running emulator over a newline-delimited JSON protocol. See [Remote control](https://highbyte.github.io/dotnet-6502/docs/tools/remote-control/overview/).
- **C64 Basic AI code completion** in the Blazor browser app. See [AI code completion](https://highbyte.github.io/dotnet-6502/docs/systems/c64/code-completion/).

## Libraries

Published as NuGet packages under `Highbyte.DotNet6502.*` — a core CPU library, system emulation libraries (Commodore 64, Generic), and per-host I/O implementations (Avalonia, Blazor, SadConsole, SilkNet, etc.). See [Libraries](https://highbyte.github.io/dotnet-6502/docs/libraries/) for the full catalog and architecture.

# Limitations

> [!IMPORTANT]
> - Correct emulation of all aspects of computers such as the Commodore 64 is not likely.
> - Not the fastest emulator.
> - A real Commodore 64 uses the *6510* CPU; for the purpose of this emulator the 6502 is treated as equivalent (same instruction set).
> - Code coverage is currently limited to the core `Highbyte.DotNet6502` library.

For the full list of missing features and constraints, see [Limitations](https://highbyte.github.io/dotnet-6502/docs/home/limitations/).

# Development & references

- [Development guide](https://highbyte.github.io/dotnet-6502/docs/home/development/)
- [References & inspiration](https://highbyte.github.io/dotnet-6502/docs/home/references/)

# Credits

- [Kristoffer Strube](https://github.com/KristofferStrube) for the original Blazor WASM async interop code for [WebAudio](https://github.com/KristofferStrube/Blazor.WebAudio), [DOM](https://github.com/KristofferStrube/Blazor.DOM), and [IDL](https://github.com/KristofferStrube/Blazor.WebIDL) that was the basis for a synchronous implementation in this repo. Copyright notice [here](src/libraries/Highbyte.DotNet6502.Impl.AspNet/JSInterop/JSInterop_OriginalLicense.MD).
