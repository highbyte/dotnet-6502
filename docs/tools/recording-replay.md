# Input recording and replay

Avalonia Desktop, Avalonia Browser, and Headless share the same recording service and
`.d6502demo` format. C64 is currently the supported machine. Other systems can add an
optional recording adapter without duplicating the file format, scheduler, or host controls.

A demo contains an initial machine snapshot, resolved keyboard/joystick input, verification
checkpoints every 300 frames, and a final state digest. It does not need the original Lua
script or TCP client to play. Required ROMs must already be configured; opening a demo
does not download dependencies or run embedded scripts.

## Playback and input

Recording and playback are mutually exclusive. Playback restores the demo's initial
snapshot and applies its recorded input. Pauses do not advance the timeline; frame stepping
advances it by the frames actually executed. Restarting restores the initial snapshot.
Natural completion and stopping playback early both leave the machine paused.

During replay, including while paused, live keyboard, gamepad, paste, native text entry,
and Lua/TCP input are blocked. Display and audio controls remain usable. Scripts are disabled
when playback begins and remain disabled afterward; re-enable them explicitly when ready.
Keys/buttons held across replay exit must be released before pressing them again for live input.

During replay, CPU register queries and safe RAM inspection remain available. Reading C64
I/O can alter chip state and is rejected during both recording and playback. Debugger
stepping/writes and program, snapshot, media, cartridge, reset, or machine-configuration
changes are also rejected during an active session.

## Compatibility and limits

Replay requires the same emulator source build, ROM hashes, CPU/model, SID mode, timing,
collision/render implementation settings, and input schema. There is no cross-version
compatibility guarantee. Desktop, Browser, and Headless built from the same sources can
exchange demos; host audio output, display scale, keyboard layout, and pacing are excluded
from the recorded input.

The initial scope is self-contained gameplay starting at a complete frame boundary. A
debugger stop inside a frame must be resumed before recording. Finish pending buffer paste,
disable the BASIC coding assistant, and wait for active IEC disk transfers before recording. External
I/O cartridges and cartridges without snapshot support are rejected. A session that stops
inside a frame or reaches a storage limit fails and cannot be saved as a complete demo.

Packages and their expanded contents are limited to 128 MiB; the initial snapshot is limited
to 32 MiB. Timeline output is streamed with bounded input-run buffers. On divergence,
playback pauses, releases its input, and reports the failing checkpoint frame and
expected/actual digests in the log and the [demo status response](remote-control/tcp-protocol.md#demostate).

The embedded snapshot can contain program code, mounted media, and session data. Saving a
demo is a local operation; review its contents before sharing it.

## Host controls and command references

- **Avalonia Desktop and Browser:** [recording/replay controls, file pickers, and status indicator](../host-apps/avalonia/overview.md#input-recording-and-replay).
- **Headless:** [playback/validation flags and exit codes](../host-apps/headless/overview.md#demo-playback-and-validation), with [command-line examples](../host-apps/headless/overview.md#examples).
- **TCP:** [demo command reference, parameters, responses, and server-side path rules](remote-control/tcp-protocol.md#demorecord).
- **Remote Client:** [concrete recording and playback CLI examples](remote-control/remote-client.md#usage-examples).
