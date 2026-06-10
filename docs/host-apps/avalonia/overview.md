# Avalonia app

A cross-platform emulator front-end written with [Avalonia UI](https://avaloniaui.net/), shipped as
**two runtimes that share almost all code, including the UI**:

- **[Browser](browser.md)** — runs entirely in the browser via WebAssembly (no install).
- **[Desktop](desktop.md)** — runs natively on Windows, macOS and Linux.

Because the two runtimes share their UI, the system-specific features are documented once per system,
with the small Browser-vs-Desktop differences called out inline:

- [C64 in the Avalonia apps](c64.md)
- [Generic computer in the Avalonia apps](generic.md)

Runtime-specific topics:

- [Desktop automation](automation.md) — UI automation and accessibility.
- [Desktop troubleshooting](troubleshooting.md) — platform-specific notes.
