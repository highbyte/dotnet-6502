# SadConsole troubleshooting

## General prerequisites

## Compatibility Matrix

| OS / Architecture | x64 | ARM64 |
|-------------------|-----|-------|
| **Windows**       | ✅ Works | ❌ Not working |
| **macOS**         | ➖ N/A | ✅ Works |
| **Linux**         | ⚠️ Works* | ❌ Not working |

*May require additional packages (see below)

## Notes

### Windows x64

Tested on Windows 11 (x64). No extra configuration.

### Windows ARM64

Tested on Windows 11 (ARM64) running in VM on a M1 Mac. Not working.

Exception below. Not investigated, but maybe a Microsoft.Xna.Framework issue. Missing Windows arm64 native libraries?

```
Exception: The type initializer for 'Keyboard' threw an exception.
Stack trace:    at Microsoft.Xna.Framework.Input.Keyboard.PlatformGetState()
Stack trace:    at Microsoft.Xna.Framework.Input.Keyboard.PlatformGetState()
   at Microsoft.Xna.Framework.Input.Keyboard.GetState()
   at SadConsole.Host.Keyboard.Refresh()
   at SadConsole.Host.Keyboard..ctor()
   at SadConsole.Game..ctor()
   at SadConsole.Game.Create(Builder configuration)
   at Highbyte.DotNet6502.App.SadConsole.SadConsoleHostApp.Run() in C:\Users\highbyte\source\repos\dotnet-6502\src\apps\Highbyte.DotNet6502.App.SadConsole\SadConsoleHostApp.cs:line 163
   at Program.<Main>$(String[] args) in C:\Users\highbyte\source\repos\dotnet-6502\src\apps\Highbyte.DotNet6502.App.SadConsole\Program.cs:line 63
```

Note: The x64 version works on Windows ARM64 through the automatic arm->intel instruction translation that Windows 11 has. Though audio must be disabled.

### Mac ARM64

Tested on MacBook Air M1, MacOS 26. No extra configuration.

### Linux x64

Should work.

#### Linux via WSLg under Windows

Tested on Ubuntu 22.04.5 (x64). No extra configuration.

### Linux ARM64

Tested on Ubuntu 25.10. Not working.

Exception below. Not investigated, but maybe a Microsoft.Xna.Framework issue. Missing Linux ARM64 native libraries?

```
Exception: The type initializer for 'Keyboard' threw an exception.
Stack trace:    at Microsoft.Xna.Framework.Input.Keyboard.PlatformGetState()
   at Microsoft.Xna.Framework.Input.Keyboard.GetState()
   at SadConsole.Host.Keyboard.Refresh()
   at SadConsole.Host.Keyboard..ctor()
   at SadConsole.Game..ctor()
   at SadConsole.Game.Create(Builder configuration)
   at Highbyte.DotNet6502.App.SadConsole.SadConsoleHostApp.Run() in /home/highbyte/source/repos/dotnet-6502/src/apps/Highbyte.DotNet6502.App.SadConsole/SadConsoleHostApp.cs:line 163
   at Program.<Main>$(String[] args) in /home/highbyte/source/repos/dotnet-6502/src/apps/Highbyte.DotNet6502.App.SadConsole/Program.cs:line 63
```

## Known limitations

### Non-US keyboard layouts — punctuation keys

**Background.** The C64 keyboard mapping expects each host to report a key by its **physical
position** (W3C `code`, e.g. the key right of `P`). SadConsole runs on MonoGame, whose `Keys`
enum is **layout-dependent** — it names keys by the character they produce on the active OS
layout, not by W3C position. SadConsole's own
[`AsciiKey.cs`](https://github.com/Thraka/SadConsole/blob/master/SadConsole/Input/AsciiKey.cs)
also hardcodes a US-layout shift table with no layout hook. And on macOS SDL specifically,
MonoGame's keymap is incomplete for ISO punctuation — the ISO 102nd key (`<` / `>`) is mapped,
but the `§` key (left of `1`) is not, so MonoGame returns `Keys.None` for it.

This is the upstream limitation tracked in
[SadConsole issue #213 — *Keyboard layout from OS not respected*](https://github.com/Thraka/SadConsole/issues/213)
(closed as won't-fix). The maintainer's summary: *"MonoGame gives me the keys. As far as it is
concerned, that is the key you pressed ... there simply isn't anything that MonoGame does to
address this. If MonoGame adds this in the future, we can come back to it."*

#### US layout

Letters, digits, navigation and US punctuation all work — MonoGame's `Keys` aligns with the W3C
positions on a US layout.

#### Swedish layout — works, with one exception

`SadConsoleInputHandlerContext` detects the Swedish OS layout (via
`KeyboardLayoutDetector` — Win32 `GetKeyboardLayoutNameW` on Windows, the `TIS*` API on macOS) and
remaps the three MonoGame `Keys` whose layout-bound names diverge from W3C position:

| MonoGame `Keys` | Default `HostKey` (US-positional) | Swedish override (W3C-positional) | Swedish physical key |
|---|---|---|---|
| `Add` | `NumpadAdd` | `Minus` | `+` (right of `0`) |
| `OemMinus` | `Minus` | `Slash` | `-` (right of `.`) |
| `OemQuotes` | `Quote` | `Backslash` | `'` (right of `Ä`) |

The shared SV map then handles them correctly. Combined with the existing SV map entries, most
Swedish punctuation works on SadConsole — see the
[Swedish layout quick reference](../systems/c64/keyboard.md#swedish-layout-quick-reference)
in the C64 keyboard doc for the chord table.

Two SadConsole-specific notes:

- SadConsole also **opts out of the macOS `Backquote`/`IntlBackslash` ISO swap** that the other
  hosts apply on macOS. MonoGame/SDL on macOS already reports the `<` key as `IntlBackslash`
  directly, so the swap would corrupt an already-correct `HostKey`.
- The `KeyboardLayout` C64 config setting still applies on top of the SadConsole override. The
  SadConsole override decides *which `HostKey` to emit*; the C64 layout decides *which C64 key
  the `HostKey` resolves to*. Mismatching them (e.g. forcing `KeyboardLayout = "US"` while your
  physical keyboard is Swedish) will not work reliably — leave it on auto-detect.

#### Remaining gap — the `§` key

MonoGame returns `Keys.None` for the Swedish `§` key (left of `1`) on macOS SDL — no override
can recover what MonoGame never delivers. **Possible future improvement:** subscribe to
MonoGame's `Window.TextInput` event from `SadConsoleInputHandlerContext`. `TextInput` fires with
the *layout-correct typed character* (delivered by the OS, not by MonoGame's keymap), so it
would surface `§` (and any other key MonoGame's `Keys` map misses) as a Unicode `char`. We could
synthesize a `HostKey` value from that character each frame and add it to `KeysDown`. This needs
plumbing — `SadConsoleInputHandlerContext` would have to obtain the underlying `Game.Window`
from `SadConsole.Host.Mono.Game`, manage event subscription lifetime, and bridge the per-event
character stream into the per-frame held-key model. Not done yet; tracked here as an idea.

#### Other layouts

Only US and Swedish are wired today. Adding a new layout means:
(1) extending the layout-specific punctuation map in `C64HostKeyboard.cs`,
(2) extending `KeyboardLayoutResolver` so the OS layout id resolves to it,
(3) adding any MonoGame-`Keys` overrides for SadConsole if that layout reports keys at different
W3C positions than US (same pattern as the Swedish table above).

See [Systems / C64 / Keyboard mapping](../systems/c64/keyboard.md) for the user-facing C64
keyboard reference (including the per-host capability matrix), and
`docs/keyboard-layout-mapping-fix.md` for the internal design.
