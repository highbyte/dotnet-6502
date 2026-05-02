# dotnet-6502

<p align="center">
  <img src="assets/screenshots/AvaloniaDesktop_C64_Basic.png" width="60%" alt="DotNet 6502 emulator running C64 Basic" />
</p>

A [6502 CPU](https://en.wikipedia.org/wiki/MOS_Technology_6502) emulator for .NET — cross-platform libraries and applications for executing 6502 machine code, and emulating specific computer systems (such as the Commodore 64) in different UI contexts.

!!! note
    This is mainly a programming exercise that may or may not turn into something more. See [Limitations](home/limitations.md).

## Where to start

- **Try it now** — [Avalonia Browser app](web-apps/avalonia-browser.md) (runs in a browser, no install)
- **Run on desktop** — [Desktop apps installation](desktop-apps/installation.md) (Windows, macOS, Linux)
- **Use the library** — [`Highbyte.DotNet6502`](libraries/core/dotnet6502.md) for embedding 6502 code execution in your own .NET app

## Web apps

| [Avalonia Browser app](web-apps/avalonia-browser.md) | [Blazor Web Assembly app](web-apps/blazor-wasm.md) |
| ---------------------------------------------------- | -------------------------------------------------- |
| <img src="assets/screenshots/AvaloniaBrowser_C64_Montezuma.png" title="Avalonia Browser app, C64 Montezuma's Revenge" /> | <a href="https://highbyte.se/dotnet-6502/app" target="_blank"><img src="assets/screenshots/BlazorWASM_C64_LastNinja.png" title="Blazor Web Assembly app, C64 Last Ninja" /></a> |

## Desktop apps

| [Avalonia Desktop](desktop-apps/avalonia-desktop.md) | [SadConsole](desktop-apps/sadconsole.md) | [SilkNetNative](desktop-apps/silknet-native.md) |
| ---------------------------------------------------- | ---------------------------------------- | ----------------------------------------------- |
| <img src="assets/screenshots/AvaloniaDesktop_C64_Basic.png" title="Avalonia Desktop app, C64 Basic" /> | <img src="assets/screenshots/SadConsole_C64_Basic.png" title="SadConsole desktop app, C64 Basic" /> | <img src="assets/screenshots/SilkNetNative_C64_BubbleBobble.png" title="SilkNetNative app, C64 Bubble Bobble" /> |

See [Desktop apps installation](desktop-apps/installation.md) for download links and instructions for Windows, Linux, and macOS.

## Headless app

[Headless app](desktop-apps/headless.md) — runs the emulator without any UI, rendering, audio, or user input. Controlled entirely via CLI arguments and Lua scripts. Useful for automation, scripting, and CI workflows.

## Featured tools

| [VS Code debugger extension](tools/vscode-debugger/debugging.md) | [Lua scripting](tools/scripting/overview.md) | [Remote control](tools/remote-control/overview.md) |
| ---------------------------------------------------------------- | -------------------------------------------- | -------------------------------------------------- |
| <img src="assets/screenshots/VSCode_source_debug.png" title="VS Code source debug" /> | <img src="assets/screenshots/AvaloniaDesktop_C64_Scripting.png" title="Lua scripts in Avalonia Desktop app" /> | <img src="assets/screenshots/AvaloniaDesktop_RemoteControl.png" title="Avalonia Desktop app with active TCP remote control client" /> |

## Other features

| [Run 6502 machine code in your own .NET apps](libraries/core/dotnet6502.md) | [Machine code monitor](libraries/core/dotnet6502-monitor.md) | [C64 Basic AI code completion](systems/c64/code-completion.md) |
| --------------------------------------------------------------------------- | ------------------------------------------------------------ | -------------------------------------------------------------- |
| <img src="assets/screenshots/Code_integration.png" title="Code integration" /> | <img src="assets/screenshots/SilkNetNative_Monitor.png" title="SilkNetNative app, C64 monitor" /> | <img src="assets/screenshots/BlazorWASM_C64_Basic_AI.png" title="C64 Basic AI code completion" /> |

For full library reference, see [Libraries](libraries/overview.md).
