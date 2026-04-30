# Changelog

All notable changes to the **6502 Debugger for dotnet-6502** VSCode extension will be documented here.

## [Unreleased]

## [0.2.1] - 2026-04-30
Fixes remote debugging so it works with source.

## [0.2.0] - 2026-04-24
New setting `debugHost` for attaching to emulator running on other computer. Currently disassembly debug only (without source).

## [0.1.6] - 2026-04-17
Docs link changed.

## [0.1.5] - 2026-04-03
Logo changed.

## [0.1.4] - 2026-03-29
Meta-data changed.

## [0.1.3] - 2026-03-29
Logo changed.

## [0.1.2] - 2026-03-28
No extension change, release workflow updated.

## [0.1.1] - 2026-03-28

### Changed
- First automated release via GitHub workflow
- Tweaks to README.md and extensions .vsix contents.
 
## [0.1.0] - 2026-03-27

### Added
- Initial release of the VSCode extension
- Debug adapter integration for debugging 6502 assembly programs using the dotnet-6502 emulator
- Support for launch (minimal and emulator modes) and attach debug configurations
- Source-level debugging for ca65 `.dbg` files, including multi-component `.dbg` file merging
- Memory viewer panel during debug sessions
- Jump to Line (Set PC) command via editor line number context menu
- Auto-detection of program file from pre-launch task output
- Skip-over-interrupt support when stepping (IRQ/NMI handlers)
- Generate build task and launch config commands via explorer context menu for `.asm` and `.prg` files
- Assembly language configuration for `.asm` files (syntax, breakpoints)
- Dependency checking for cc65 and dotnet-6502 executables in PATH, with install instructions
