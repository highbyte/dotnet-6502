# Overview of tools

Things you reach for to *do more* with the emulator beyond just running it. Each tool is an integration point — you don't need any of them to just play, but each one unlocks a workflow.

| Tool | Use when you want to… | Where it works |
|------|-----------------------|----------------|
| [VSCode debugger extension](vscode-debugger/debugging.md) | Source-level debug your 6502 assembly programs from VS Code (breakpoints, stepping, watch, disassembly). | Avalonia Desktop, Headless |
| [Lua scripting](scripting/overview.md) | Automate emulator interaction, inject input, read/write memory, define event hooks. Great for testing, demos, AI/RL agents. | Avalonia Desktop, Avalonia Browser (sandboxed), Headless |
| [Remote control](remote-control/overview.md) | Drive a running emulator from an external process via a TCP/JSON protocol — without embedding a script. | Avalonia Desktop, Headless |

For app-specific launch flags (autostart a system, load a `.prg`, enable scripting / debug / remote control), see the **CLI arguments** section on the [Avalonia Desktop](../desktop-apps/avalonia-desktop.md#cli-arguments) and [Headless](../desktop-apps/headless.md#cli-arguments) pages.

## Which tool when?

- **"I want to write 6502 assembly and step through it."** — [VSCode debugger extension](vscode-debugger/debugging.md).
- **"I want a script that exercises the emulator deterministically every time it runs."** — [Lua scripting](scripting/overview.md). Scripts live with the emulator process and can drive input, read state, and use HTTP/TCP/file APIs.
- **"I want my own external process (in any language) to query and control the emulator."** — [Remote control](remote-control/overview.md). Speaks newline-delimited JSON over TCP from anywhere.
- **"I want to launch the emulator with specific options once."** — see CLI arguments on the [Avalonia Desktop](../desktop-apps/avalonia-desktop.md#cli-arguments) or [Headless](../desktop-apps/headless.md#cli-arguments) page.
- **"I want to debug from a different machine than the emulator runs on."** — [VSCode debugger / Remote debugging](vscode-debugger/remote-debugging.md).

## Lua scripting vs Remote control — when to choose which

Both can drive the emulator. The split:

| | Lua scripting | Remote control |
|---|---|---|
| Where the code runs | Inside the emulator process | In a separate process |
| Frame-accurate timing | Yes (`emu.frameadvance()`) | No (commands queued for next frame boundary) |
| Language | Lua only | Any language with a TCP socket (and JSON) |
| State carrying | Persistent in the script | Persistent across requests via the open TCP connection |
| Best for | Per-frame deterministic automation, demos, replays | Tooling, AI agents, integration with non-.NET systems |

These are complementary — you can run a Lua script while also serving the remote-control endpoint, and external clients can interleave their own commands with frame-by-frame logic the script handles.

For VS Code-extension developer docs (extension build/debug, internals), see [VSCode debugger extension / Development](vscode-debugger/development.md).
