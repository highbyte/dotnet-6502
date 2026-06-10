# C64 keyboard mapping

The emulator maps your host keyboard onto the C64 keyboard. The mapping is **host-agnostic** —
identical across every app (Avalonia, SilkNet, SadConsole, browser) and across operating systems.
Each host only translates its native key events into a neutral *physical key* abstraction
([W3C `KeyboardEvent.code`](https://www.w3.org/TR/uievents-code/) values); the actual C64 mapping
is defined once, in `C64HostKeyboard`.

Letters `A`–`Z` and digits `0`–`9` map one-to-one. Symbol keys depend on the
[keyboard layout](#keyboard-layouts). The keys below are the ones that are **not** obvious,
because the C64 has keys that no modern keyboard has.

## Special keys

| Host key | C64 key | Notes |
|---|---|---|
| `Esc` | RUN/STOP | |
| `Tab` | CTRL | The C64 CTRL key (not a modifier on its own). |
| `Left Ctrl` | C= | The Commodore (logo) key. |
| `Page Up` | RESTORE | Often used as `RUN/STOP`+`RESTORE` to reset. Wired to the CPU's NMI line, not the key matrix. **Mac:** `Fn`+`↑`. |
| `Home` | CLR/HOME | **Mac:** `Fn`+`←`. |
| `Insert` | INST | Equivalent to `Shift`+`Delete`. **Mac:** no Insert key — use `Shift`+`Delete`. |
| `Delete` / `Backspace` | DEL | |
| `End` | ← | The C64 left-arrow key (top-left of the C64 keyboard). **Mac:** `Fn`+`→`. |
| `Page Down` | ↑ | The C64 up-arrow key. **Mac:** `Fn`+`↓`. |
| `Arrow keys` | CRSR up / down / left / right | |
| `Enter` | RETURN | |
| `F1` `F3` `F5` `F7` | F1 F3 F5 F7 | For F2/F4/F6/F8, hold `Shift` (e.g. `Shift`+`F1` = F2). **Mac:** hold `Fn` or enable standard function keys. |
| `Left Shift` / `Right Shift` | SHIFT | |
| `Space` | SPACE | |

## Keyboard layouts

Symbol keys (`-`, `=`, `[`, `]`, `;`, `'`, `\`, etc.) are **layout-dependent**. Two layouts are
provided:

- **US** — the default, for an ANSI US-QWERTY keyboard.
- **Swedish** — for an ISO Swedish keyboard. For example, the physical `-` key produces C64 `+`,
  the key right of `Ä` produces C64 `'`, and the key left of `Z` produces C64 `<` / `>`.

Any unrecognised layout falls back to **US**.

### How the layout is chosen

The active layout is resolved at C64 startup by `C64InputHandler.ResolveKeyboardLayout()`:

1. **Explicit config setting (wins).** The C64 `KeyboardLayout` config setting (see
   [Setting the layout](#setting-the-layout)). When set to `US` or `Swedish`, that layout is forced
   and detection is skipped.
2. **Auto-detect from the host's keyboard layout.** When the config setting is unset/empty, the
   host is asked for its native keyboard layout identifier — Win32 `GetKeyboardLayoutNameW` on
   Windows (KLID), the Text Input Sources (`TIS*`) API on macOS (e.g. `com.apple.keylayout.Swedish`),
   `navigator.keyboard.getLayoutMap()` in Chromium browsers. The resolved id is mapped to a
   `C64KeyboardLayout` if known.
3. **OS culture fallback.** If the host cannot report a layout (Linux, Safari/Firefox, etc.),
   `CultureInfo.CurrentCulture` is consulted — `sv` → Swedish, others → unknown. This is
   *inaccurate* (the UI culture is not the physical keyboard) but better than nothing.
4. **Default.** Otherwise **US**.

The resolved layout, and which step it came from, is written to the application log at
**Information** level. Held-key resolution (`HostKey → C64Key`) is logged at **Debug** level when
the held-key set changes — enable Debug logging if you need to see exactly which `HostKey` a
physical key produces on your host.

### Setting the layout

The layout can be left as auto-detect (recommended for most users) or pinned:

- **`appsettings.json`** — under each host's C64 section, `InputConfig.KeyboardLayout`:

    ```json
    "InputConfig": {
      // Empty = auto-detect (host keyboard layout → OS culture → US).
      // Set to "US" or "Swedish" to force a layout.
      "KeyboardLayout": ""
    }
    ```

- **In-app config dialog** — Avalonia, SilkNet and Blazor WASM all expose a *Keyboard layout*
  dropdown in the C64 config UI. Selecting **Auto** (empty) writes `null`/empty back; selecting
  `US` or `Swedish` pins the layout. Restart the system after changing.

The setting is read at C64 startup; changing it mid-session has no effect until you stop and
restart the C64 system.

### Swedish layout quick reference

Common C64 character chords on a Swedish ISO keyboard. Letters and digits map one-to-one; this
table covers only the keys that aren't obvious.

| Host key | C64 character | Notes |
|---|---|---|
| `+` (right of `0`) | `+` | |
| `Shift`+`+` | `?` | |
| `´` (right of `+`) | `'` | |
| `Å` (right of `P`) | `]` | Convenience binding. `Alt`+`9` also produces `]`. |
| `Shift`+`Å` | `↑` | C64 up-arrow key. |
| `¨` (right of `Å`) | `@` | |
| `Shift`+`¨` | `↑` (shifted) | |
| `Ö` (right of `L`) | `£` | C64 British pound. |
| `Ä` (right of `Ö`) | `[` | Convenience binding. `Alt`+`8` also produces `[`. |
| `'` (right of `Ä`) | `'` | Apostrophe. |
| `Shift`+`'` | `*` | C64 asterisk. |
| `<` (left of `Z`) | `<` | |
| `Shift`+`<` | `>` | |
| `,` | `,` | |
| `Shift`+`,` | `;` | |
| `.` | `.` | |
| `Shift`+`.` | `:` | |
| `-` (right of `.`) | `-` | |
| `Shift`+`-` | `←` | C64 left-arrow key. |
| `Alt`+`8` / `Alt`+`9` | `[` / `]` | Matches what's printed on the Swedish keyboard via `Option`/`AltGr`. Both `Alt` keys work. |

## Per-host keyboard support

The pipeline relies on each host delivering true **physical** key positions in step ①. Hosts vary
in how well their UI framework supports this; the table summarises the current state.

| Host | Physical key source | Layouts (US / Swedish) | Auto-detect source | Notes |
|------|---------------------|------------------------|--------------------|-------|
| **Avalonia Desktop** | `Avalonia.Input.PhysicalKey` (W3C `code`) | ✅ both | Win32 KLID (Windows) / `TIS*` (macOS) | Fully supported. |
| **Avalonia Browser** | `Avalonia.Input.PhysicalKey` (W3C `code`) | ✅ both | `navigator.keyboard.getLayoutMap()` via `[JSImport]` | Chromium browsers only; other browsers fall through to culture. |
| **SilkNet Native** | Silk.NET / GLFW positional `Key` | ✅ both | Win32 KLID (Windows) / `TIS*` (macOS) | GLFW already delivers positional keys. See [macOS quirk](#macos-keyboard-notes) about `<` / `§`. |
| **Blazor WASM** | `KeyboardEventArgs.Code` (W3C `code`) | ✅ both | `navigator.keyboard.getLayoutMap()` via JS interop | Chromium browsers only. |
| **SadConsole** | MonoGame `Keys` (layout-dependent) | ✅ US / ⚠️ Swedish (mostly — see [troubleshooting](../../host-apps/sadconsole/troubleshooting.md#non-us-keyboard-layouts-punctuation-keys)) | Win32 KLID (Windows) / `TIS*` (macOS) — for the SadConsole-side layout override only | Most Swedish punctuation now works via a SadConsole-specific MonoGame-Keys-to-`HostKey` override. The `§` key remains unrecoverable — MonoGame returns `Keys.None` for it on macOS SDL. |

### Hosts that fully support physical keys

Avalonia (Desktop + Browser), SilkNet and Blazor WASM all expose true W3C-`code`-style physical
keys, so the `US`/`Swedish` layout maps work correctly on any culture / UI language. Prefer one of
these hosts when typing on a non-US keyboard.

### SadConsole — partial Swedish support

MonoGame exposes no physical-key API — `Keys` is layout-dependent (it names keys by the character
they produce on the active OS layout, not by W3C position). On a Swedish OS layout, MonoGame
therefore reports `OemMinus` for the `-` key, `OemQuotes` for the `'` key and `Add` for the `+`
key — all at *different* W3C positions than on US.

`SadConsoleInputHandlerContext` detects the Swedish OS layout (via the same
`KeyboardLayoutDetector` the C64 layout resolver uses) and re-maps those three `Keys` to the
W3C-positional `HostKey` values Avalonia/SilkNet would have produced (`Slash`, `Backslash`,
`Minus`). The shared SV map then handles them correctly, and most Swedish punctuation works.

Remaining gap: the **`§` key (left of `1`)** — MonoGame returns `Keys.None` for it on macOS SDL,
so SadConsole has no way to see that key was pressed. See the
[SadConsole troubleshooting note](../../host-apps/sadconsole/troubleshooting.md#non-us-keyboard-layouts-punctuation-keys)
for the upstream context and a possible future fix via MonoGame's `Window.TextInput` event.

## macOS keyboard notes

The mapping itself is the same on macOS and Windows/Linux — the physical-key abstraction does not
vary by OS. But Mac keyboards make some of the [special keys](#special-keys) harder to reach:

- **No `Insert` key.** macOS keyboards have no Insert key at all — use `Shift`+`Delete` for the
  C64 INST function instead.
- **`Home` / `End` / `Page Up` / `Page Down`** are absent as dedicated keys on most Mac laptops
  and the compact Magic Keyboard; press them with `Fn`+`Arrow` (`Fn`+`←` = Home, `Fn`+`→` = End,
  `Fn`+`↑` = Page Up, `Fn`+`↓` = Page Down). Note that **Page Up = C64 RESTORE**, so a reset is
  `Esc`(RUN/STOP) + `Fn`+`↑`.
- **Function keys** F1–F7 may trigger macOS media/brightness actions by default — hold `Fn`, or
  enable *"Use F1, F2, etc. keys as standard function keys"* in System Settings.
- The `Left Ctrl` key (→ C64 Commodore key) and `Tab` (→ C64 CTRL) work as on any keyboard; the
  Mac `Cmd` and `Option` keys are not mapped.

### macOS ISO-keyboard quirk — `<` and `§` swap (handled automatically)

On a non-US (ISO) Mac keyboard, the two keys left of `1` (`§`) and left of `Z` (`<`) are reported
with **hardware keycodes that are swapped** relative to the W3C `code` convention `HostKey`
follows — the `§` key arrives as `IntlBackslash` and the `<` key as `Backquote`. macOS does this
both natively *and inside a browser*.

`C64InputHandler` corrects this automatically when running on macOS with a non-US C64 layout
(`_swapBackquoteAndIntlBackslash` in `C64InputHandler.cs`), so the keys behave as expected. No
configuration is required.

The macOS check (`IHostInputState.IsRunningOnMacOS`) defaults to `RuntimeInformation.IsOSPlatform(OSPlatform.OSX)`
for native hosts. Browser hosts override it: the .NET runtime reports `OSPlatform.Browser` in
WASM, so they detect the underlying OS from the browser (Blazor WASM via `navigator.platform`,
Avalonia Browser via the same value through `[JSImport]`). **SadConsole also overrides it to
`false`** — MonoGame/SDL on macOS already reports the `<` key as `IntlBackslash` directly,
without the `Backquote`/`IntlBackslash` confusion the other hosts hit, so applying the swap there
would corrupt an already-correct `HostKey`.

## Using the keyboard as a joystick

The host keyboard can also drive a C64 joystick port:

- **Port 1** — `Arrow keys` for direction, `Space` for fire.
- **Port 2** — `W` `A` `S` `D` for direction, `Left Ctrl` for fire.

A connected gamepad maps its D-pad to direction and the `A` button to fire.

## See also

- [SadConsole troubleshooting — non-US keyboard layouts](../../host-apps/sadconsole/troubleshooting.md#non-us-keyboard-layouts-punctuation-keys)
