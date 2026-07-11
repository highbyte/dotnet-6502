# Avalonia Desktop app automation

UI automation and accessibility for the Avalonia desktop and browser apps.

## Overview

The Avalonia desktop app exposes accessibility metadata so that screen readers and UI-automation agents (including AI agents) can identify, describe, and operate every interactive control.

This is implemented by setting attached properties from the `Avalonia.Automation` namespace on controls in `.axaml`:

- `AutomationProperties.AutomationId` — a stable, programmatic selector (flat PascalCase, e.g. `StartButton`, `ScaleSlider`). Maps to the platform-specific identifier (AX `identifier` on macOS, `AutomationId` on Windows UIA).
- `AutomationProperties.Name` — a short human-readable label (e.g. "Start", "Scale"). Maps to AX `title` on macOS / `Name` on UIA. Read aloud by screen readers.

The `AutomationProperties` attached properties live in the default `https://github.com/avaloniaui` XML namespace, so no extra `xmlns` is required.

Source: [`src/apps/Avalonia/Highbyte.DotNet6502.App.Avalonia.Core/Views/`](https://github.com/highbyte/dotnet-6502/tree/master/src/apps/Avalonia/Highbyte.DotNet6502.App.Avalonia.Core/Views/).

## What Avalonia auto-generates (no attributes needed)

Even without any `AutomationProperties` attributes, Avalonia already surfaces some data via its automation peers:

| Source in XAML                | Auto-mapped to                                |
| ----------------------------- | --------------------------------------------- |
| `Button.Content="Start"`      | AX label / UIA Name = "Start"                 |
| `TabItem.Header="Log"`        | AX label / UIA Name = "Log"                   |
| `Name="MyControl"` / `x:Name` | AX `identifier` / UIA `AutomationId`          |
| `ToolTip.Tip="…"`             | AX `help` / UIA `HelpText`                    |
| `Window.Title="…"`            | AX window title                               |

Because of this, controls with `Name="…"` already get a stable identifier for free. The codebase keeps existing `Name=` attributes (they're also used by code-behind `FindControl` lookups) and adds explicit `AutomationProperties.AutomationId` alongside — decoupling the automation identity from the code-behind field name.

## Conventions used in this codebase

1. **Flat PascalCase ids** matching existing `Name=` style: `StartButton`, `SystemSelectionComboBox`, `ScaleSlider`, `LogTab`, `OkButton`.
2. **Per-row dynamic ids** for `ItemsControl` templates — bound to an identifying field. Pattern:
   ```xml
   <Button AutomationProperties.AutomationId="{Binding FileName, StringFormat='ScriptRow.Reload.{0}'}"
           AutomationProperties.Name="Reload script"
           Content="↻" />
   ```
   Use `.` as separator (e.g. `ScriptRow.Reload.snake.lua`, `RomFileTextBox.Kernal`).
   **Escape leading `{0}`** in a `StringFormat` with `{}` to prevent the XAML parser from interpreting it as a markup extension:
   ```xml
   StringFormat='{}{0} ROM file name'
   ```
3. **Icon-only buttons** (`↻`, `+`, `i`, pencil, trash) need *both* `AutomationId` and a descriptive `Name`, because the `Content` is a glyph that doesn't describe the action.
4. **Text-only buttons** (`Content="Start"`) — `Content` already becomes the AX label, so only `AutomationId` is strictly needed. `Name` is added when `Content` is bound or ambiguous.
5. **Decorative elements** (`TextBlock` labels, `Border`, `Image`, layout `Grid`) do not get AutomationProperties unless an agent needs to read their text (e.g. status strings).
6. **Container-level AutomationId** on root `UserControl`/`Window` elements (`StatisticsView`, `C64InfoView`, `EmulatorPlaceholderView`, dialog windows) so agents can locate and scope to a view.

## What is surfaced

A non-exhaustive list of the most useful AutomationIds, grouped by view. All of these are present in the compiled app and queryable via the platform's accessibility API.

### MainView (always visible)

- **System selection**: `SystemSelectionComboBox`, `SystemVariantSelectionComboBox`
- **Emulator control**: `StartButton`, `PauseButton`, `ResetButton`, `StopButton`, `MonitorButton`, `StatsButton`
- **Display/audio/settings**: `ScaleSlider`, `AudioCheckBox`, `AudioVolumeSlider`, settings button `OptionsButton`
- **Snapshot section (collapsible, common)**: header `SnapshotSectionHeader`, content `SnapshotSectionContent`; buttons `SaveSnapshotButton`, `LoadSnapshotButton`, directory link `SnapshotFolderLink` (visible only when the section is expanded — collapsed by default; toggle it with the `Emulator` menu / `⌘⌥⇧S` shortcut)
- **Status**: `EmulatorStateText`
- **Bottom tab control**: `InformationTabControl` with tabs `InformationTab`, `ConfigStatusTab`, `LogTab`, `ScriptsTab`, `GeneralInfoTab`, `DebugAndRemotingTab`
- **Log tab**: `ClearLogButton`
- **Scripts tab**: `ScriptsBannerRefreshButton`, script directory link `ScriptFolderLink`, `AddScriptButton`, `LoadExamplesButton`, `ScriptsRefreshButton`; sort headers `SortByFileNameButton`, `SortByStatusButton`, `SortByYieldButton`, `SortByHooksButton`
- **Script rows (dynamic)**: `ScriptRow.ToggleEnabled.<FileName>`, `ScriptRow.Reload.<FileName>`, `ScriptRow.Edit.<FileName>`, `ScriptRow.Delete.<FileName>`
- **Debug tab**: `ExternalDebugToggleButton`, `ExternalDebugPortInput`, `DebugSoundButton`, `DebugGamepadButton`

### C64MenuView (sidebar)

- **Basic clipboard**: `CopyBasicButton`, `PasteTextButton`, `AiBasicCheckBox`, `AiBasicInfoButton`
- **Collapsible section headers**: `DiskSectionHeader`, `LoadSaveSectionHeader`, `ConfigSectionHeader`
- **Disk section**: `DiskToggleButton`, `DiskInfoButton`, `PreloadedDiskComboBox`, `PreloadedDiskInfoButton`, `DownloadAndRunDiskButton`
- **Load/Save section**: `LoadBasicButton`, `SaveBasicButton`, `LoadBinaryButton`, `AssemblyExampleComboBox`, `LoadAssemblyExampleButton`, `BasicExampleComboBox`, `LoadBasicExampleButton`
- **Config section**: `ActiveJoystickComboBox`, `JoystickKeyboardCheckBox`, `KeyboardJoystickComboBox`, `C64ConfigButton`

### C64ConfigDialog / C64ConfigUserControl

- **Window**: `C64ConfigDialog`
- **ROMs (dynamic per ROM)**: `RomFileTextBox.Kernal`, `RomFileTextBox.Basic`, `RomFileTextBox.Chargen`
- **ROM actions**: `RomDirectoryTextBox`, `ClearRomsButton`, `LoadRomsButton`, `DownloadRomsButton`, `DownloadRomFilesButton`
- **Video**: `RenderProviderComboBox`, `RenderTargetComboBox`
- **Audio**: `SidAudioCheckBox`
- **Input**: `HostJoystickComboBox`, `KeyboardJoystickEnableCheckBox`, `KeyboardJoystickPortComboBox`, `KeyboardMappingsExpander`
- **Network**: `CorsProxyTextBox`, `CorsProxyResetButton`
- **AI assistant**: `AiHelpButton`, `AiBackendComboBox`, `OpenAiApiKeyTextBox`, `OllamaEndpointTextBox`, `OllamaModelNameTextBox`, `OllamaApiKeyTextBox`, `CustomEndpointApiKeyTextBox`, `TestAiBackendButton`
- **Footer**: `CancelButton`, `OkButton`

### EmulatorConfigUserControl (general settings)

`DefaultEmulatorComboBox`, `DefaultScaleSlider`, `ShowErrorDialogCheckBox`, `ShowDebugToolsCheckBox`, `AudioProfileComboBox`, `StopOnBrkCheckBox`, `StopOnUnknownInstructionCheckBox`, `SnapshotDirectoryTextBox`, `LuaScriptDirectoryTextBox`, `LuaStorePrefixTextBox`, `CancelButton`, `OkButton`.

### MonitorDialog / MonitorUserControl

- **Window**: `MonitorDialog`
- `OutputScrollViewer`, `CommandTextBox`, `SendCommandButton`, `CloseMonitorButton`, `MonitorStatusScroll`

### ScriptEditorDialog

- **Root**: `ScriptEditorDialog`
- `FileNameBox`, `ContentBox`, `CancelButton`, `SaveButton`

### Debug views

- **DebugSoundUserControl**: `InitAudioButton`, `PlayAudioButton`, `PauseAudioButton`, `StopAudioButton`, `PlaySynthButton`, `StartSynthReleaseButton`, `StopSynthButton`, `SoundTestComboBox`, `PlaySoundButton`, `StopSoundButton`, `CloseButton`
- **DebugGamepadUserControl**: `CloseButton` (remainder is read-only visual status)

### ErrorUserControl

`ErrorMessageTextBox`, `ExceptionDetailsTextBox`, `ShowDetailsButton`, `ContinueButton`, `ExitButton`.

### Other containers

- `EmulatorView` (root) with `EmulatorRenderSurface` on the render ContentPresenter
- `EmulatorPlaceholderView` (root, pre-start logo)
- `StatisticsView` (root)
- `C64InfoView` (root, keyboard mapping reference)

## Keyboard shortcuts

The app exposes these layers of shortcuts:

1. **General tab-navigation shortcuts** — always active, independent of which emulator system is running.
2. **Common (non-system-specific) shortcuts** — always active, for cross-system functions such as toggling the Snapshot sidebar section.
3. **System-specific shortcuts** — active only when a particular system is selected (e.g. C64). The active system's menu ViewModel implements `ISystemMenuContributor` ([`Core/SystemSetup/ISystemMenuContributor.cs`](https://github.com/highbyte/dotnet-6502/blob/master/src/apps/Avalonia/Highbyte.DotNet6502.App.Avalonia.Core/SystemSetup/ISystemMenuContributor.cs)).

On macOS all layers appear in the OS-level **system menu bar** (outside the app window) — general (tab) shortcuts under a `View` top-level menu, common shortcuts under an `Emulator` top-level menu, and system-specific shortcuts under the system name (e.g. `C64`). The macOS Accessibility API exposes these with their `Gesture` string, so an AI agent can discover them at runtime without prior documentation:

```sh
peekaboo menu list --app "DotNet 6502 Emulator"
```

On **Windows / Linux**, `NativeMenu` would render as in-window chrome (not desired), so `KeyBinding`s registered on the main window are used instead. They fire regardless of which child control has focus, but are invisible to accessibility tools — an agent needs to know them from this document.

### Tab navigation shortcuts (always active)

These shortcuts jump directly to a named tab regardless of tab order — reordering tabs in code does **not** break automation scripts.

| Tab             | macOS    | Windows / Linux    |
| --------------- | -------- | ------------------ |
| Information     | `⌘⌥I` | `Ctrl+Alt+I`       |
| Config status   | `⌘⌥C` | `Ctrl+Alt+C`       |
| Log             | `⌘⌥L` | `Ctrl+Alt+L`       |
| Scripts         | `⌘⌥S` | `Ctrl+Alt+S`       |
| General info    | `⌘⌥G` | `Ctrl+Alt+G`       |
| Debug           | `⌘⌥D` | `Ctrl+Alt+D`       |

On macOS, click via the menu bar instead of counting arrow-key presses:

```sh
peekaboo menu click --app "DotNet 6502 Emulator" --path "DotNet 6502 Emulator > View > Log"
```

### Common shortcuts (always active — `Emulator` menu)

Cross-system functions, available regardless of which system is selected.

| Action                   | macOS      | Windows / Linux       |
| ------------------------ | ---------- | --------------------- |
| Toggle Snapshot section  | `⌘⌥⇧S`  | `Ctrl+Alt+Shift+S`    |

On macOS, click via the menu bar:

```sh
peekaboo menu click --app "DotNet 6502 Emulator" --path "DotNet 6502 Emulator > Emulator > Toggle Snapshot section"
```

### C64 shortcuts (active when the C64 system is selected)

| Action                           | macOS               | Windows / Linux       |
| -------------------------------- | ------------------- | --------------------- |
| Toggle Disk Drive section        | `⌘⌥⇧D`         | `Ctrl+Alt+Shift+D`    |
| Toggle Load/Save section         | `⌘⌥⇧L`         | `Ctrl+Alt+Shift+L`    |
| Toggle Configuration section     | `⌘⌥⇧C`         | `Ctrl+Alt+Shift+C`    |
| Active joystick → Port 1         | `⌘⌥1`            | `Ctrl+Alt+1`          |
| Active joystick → Port 2         | `⌘⌥2`            | `Ctrl+Alt+2`          |
| Toggle Joystick KB               | `⌘⌥K`            | `Ctrl+Alt+K`          |
| Keyboard joystick → Port 1       | `⌘⌥⇧1`         | `Ctrl+Alt+Shift+1`    |
| Keyboard joystick → Port 2       | `⌘⌥⇧2`         | `Ctrl+Alt+Shift+2`    |

On macOS, click via the menu bar:

```sh
peekaboo menu click --app "DotNet 6502 Emulator" --path "DotNet 6502 Emulator > C64 > Toggle Configuration section"
```

## What is NOT surfaced (known gaps)

1. **Individual `TabItem` controls via `peekaboo see`** — `see` truncates the tree by depth (see "`see` truncates the tree" note above), so it surfaces `InformationTabControl` but not its `TabItem` children as distinct elements. This is a `see`-depth artifact, **not** a genuine AX-bridge limitation: with Accessibility granted and a full-depth enumeration (`peekaboo inspect-ui` or `osascript` `entire contents`), the tab control appears as **`AXTabGroup`** and each tab as an **`AXRadioButton`** carrying its `AutomationId` (`InformationTab`, `ConfigStatusTab`, `LogTab`, `ScriptsTab`, `DebugAndRemotingTab`).

   **Preferred approach for tab switching**: the named menu **keyboard shortcuts** (`⌘⌥I/C/L/S/D`, see "Tab navigation shortcuts" above) — order-independent and the most robust. Keyboard arrow navigation also works if you need it: give the `InformationTabControl` focus, then press the right-arrow key once per tab step:

   ```sh
   # Capture the AX tree and find InformationTabControl's elem_NN
   peekaboo see --app "DotNet 6502 Emulator" --json | jq '.. | objects | select(.identifier == "InformationTabControl") | .id'
   # → e.g. "elem_49"

   # Focus the tab control
   peekaboo click --on elem_49 --app "DotNet 6502 Emulator" --window-index 0

   # Navigate right to reach the target tab (count depends on which tab is currently active)
   # Tab order: Information → ConfigStatus → Log → Scripts → GeneralInfo → Debug
   peekaboo press right   # repeat as needed
   ```

   The number of right-arrow presses depends on the **currently active tab**, not a fixed offset. If "Information" is active, pressing right twice reaches "Log". If another tab is already active, adjust accordingly.

   **Avoid** clicking by screen coordinates for tabs: coordinates are window-size-dependent and scale across display densities. **Avoid** `peekaboo click "Log"`: text-query matching is global and can hit an element with the same label in another app or inside the tab's content area (e.g. Ghostty's "Log Out" menu).

2. **Collapsed/conditional content** only appears in the AX tree when its container is rendered. Examples:
    - `MainView` common `SnapshotSectionContent` (`SaveSnapshotButton`, `LoadSnapshotButton`) — collapsed by default; expand via the `Emulator` menu / `⌘⌥⇧S` shortcut or by clicking `SnapshotSectionHeader`.
    - `C64MenuView` section contents (`DiskSectionContent`, `LoadSaveSectionContent`, `ConfigSectionContent`) — only visible when the section header is expanded.
    - Some tools in `Debug & Remoting` tab contents — hidden until `ShowDebugTools` is enabled in `EmulatorConfig`.
    - Dialog controls (`C64ConfigDialog`, `MonitorDialog`, `ScriptEditorDialog`) — only exist while the dialog is open.

   To automate these, expand/open the container first, then re-query the AX tree.

3. **Dynamic row ids change with data**. `ScriptRow.Reload.<FileName>` depends on the loaded script set. An agent must first query the Scripts tab to discover current rows.

4. **Browser/WebAssembly target**. Automation there goes through the DOM, not Avalonia's AX bridge. Accessibility attributes in `.axaml` propagate only where the Avalonia runtime has a peer — the browser story is not covered in this document.

## Automating on macOS via peekaboo

[peekaboo](https://peekaboo.dev) is a CLI tool for macOS UI automation (screenshots + AX tree traversal + input). These notes are from hands-on verification with the desktop app.

> **This section targets peekaboo 3.8.0.** All commands, flags, output shapes, and behaviors below are written for and verified against that version.

### One-time setup

Grant peekaboo these permissions (System Settings → Privacy & Security):

- **Screen Recording** (required for `see` / `capture`)
- **Accessibility** (required for `click`, `type`, focus management)

Verify with:

```sh
peekaboo permissions
```

> **Accessibility is mandatory, and its absence is easy to misdiagnose.** If the **Accessibility** permission is missing, AX enumeration of the Avalonia window effectively hangs: `peekaboo see` returns only the top-level container groups (`MainContentGrid`, `CenterPanel`, `EmulatorDisplayGrid`) plus the menu bar and never the interactive controls, and a direct `osascript` "count of UI elements of window 1" times out. Screen Recording alone is **not** enough — `see` will still capture a screenshot, so the failure looks like "controls missing from the tree" rather than "permission missing". If any control you expect is absent, re-check `peekaboo permissions` first.

### Discovering the AX tree

```sh
# Launch the app first (from the repo root)
dotnet run --project src/apps/Avalonia/Highbyte.DotNet6502.App.Avalonia.Desktop

# In another terminal, capture the current AX state
peekaboo see --app "DotNet 6502 Emulator"

# With an annotated screenshot showing clickable element IDs (elem_NN)
peekaboo see --app "DotNet 6502 Emulator" --annotate /tmp/ax.png

# Full JSON dump for scripting
peekaboo see --app "DotNet 6502 Emulator" --json > /tmp/ax.json
```

> **`see` truncates the tree by depth — use `inspect-ui` to reach the controls.** `peekaboo see` only descends to the top-level container groups and does **not** list the sidebar buttons, comboboxes, or tabs even with Accessibility granted (it also enumerates the full ~96-item Apple menu bar, which dominates the snapshot). Use `inspect-ui` with raised limits to get the full control tree with each `AutomationProperties.AutomationId` reported as `identifier`:
>
> ```sh
> peekaboo inspect-ui --app-target "DotNet 6502 Emulator" \
>   --max-depth 60 --max-elements 5000 --max-children 500 --json
> ```
>
> The `Snapshot ID:` is embedded in the returned `data.content[0].text` (parse it with a regex, and use a lenient JSON parser — the `debug_logs` field contains raw control characters). The `elem_NN` ids from this snapshot are usable with `click --on … --snapshot …`. Only currently-enabled controls are reported actionable (e.g. `Pause`/`Reset`/`Stop` show `[not actionable]` while the emulator is Uninitialized).

The app's process name under macOS is `DotNet 6502 Emulator` (set via `Application.Name` in `App.axaml`, which Avalonia applies to `NSApplication` at startup). Confirm with:

```sh
peekaboo list
```

### Clicking controls

peekaboo's `click` command has several selection modes:

```sh
# 1. By the short "elem_NN" id from the latest `see` snapshot:
peekaboo click --on elem_24 --app "DotNet 6502 Emulator" --window-index 0

# 2. By a specific snapshot id (useful if you captured earlier):
peekaboo click --on elem_24 --snapshot <UUID-from-see> --app "DotNet 6502 Emulator" --window-index 0

# 3. By text query (matches Name/title/label):
peekaboo click "Start" --app "DotNet 6502 Emulator" --window-index 0

# 4. By absolute screen coordinates:
peekaboo click --coords "440,595" --app "DotNet 6502 Emulator" --window-index 0
```

> **For this app, use mode 1/2 (element id).** Source the `elem_NN` from `inspect-ui` (mode 1's "latest `see` snapshot" won't contain most controls — `see` truncates). **Avoid mode 3 (text query)** — it matches globally and can hit a same-named element in another app or inside tab content (e.g. Ghostty's "Log Out"). **Avoid mode 4 (coordinates)** — brittle across window moves/resizes/display density; the controls have a proper AX surface, so there is no need for coordinate clicking.

### Gotchas (learned the hard way)

- **Always pass `--window-index 0`** when targeting this app. The Avalonia runtime creates a secondary hidden window, and without a window scope the focus step in `click` times out with `Error: Timeout while waiting for condition`.

- **Don't use `--no-auto-focus` from a terminal.** The terminal emulator (e.g. Ghostty) reclaims focus between commands, so a `--no-auto-focus` click lands on the terminal window at the same screen coordinates — `click` still reports "✅ Click successful" but against the wrong app. Let peekaboo's auto-focus bring the Avalonia window forward.

- **Text-query clicks are unreliable for tabs — and can hit other apps.** `peekaboo click "Log"` searches globally across all visible AX elements. It can match a label *inside the tab content*, a menu item in another app (e.g. Ghostty's "Log Out" item), or any other element named "Log" that happens to be on screen. For tab navigation, always use the keyboard approach described in "What is NOT surfaced" item 1 above.

- **Screenshot coordinates vs. screen coordinates.** The annotated screenshot from `peekaboo see --annotate` is scaled to roughly 0.75× the window-point size. To convert a pixel position in the screenshot to a click coordinate, scale by ~1.33× and add the window's screen offset (`peekaboo list` shows the window Position).

- **AXIdentifier-based clicking isn't directly supported.** peekaboo's `--id` / `--on` flags take the `elem_NN` token from a snapshot, not the `AutomationProperties.AutomationId` string. To select by AutomationId, parse the JSON from `peekaboo inspect-ui … --json` (not `see` — `see` truncates and won't contain most controls), look for `identifier == "StartButton"` to find the corresponding `elem_NN` and the `Snapshot ID:`, then pass both to `click --on … --snapshot …`.

- **Sidebar buttons are reachable coordinate-free — do NOT use coordinate clicking.** The left-hand sidebar controls (`StartButton`, `StopButton`, `DownloadAndRunDiskButton`, `PreloadedDiskComboBox`, `LoadBasicButton`, etc.) are absent from `peekaboo see` (depth truncation). Do not fall back to screen-coordinate clicking — it is brittle (breaks on window move/resize/density), and this app has a proper AX surface once Accessibility is granted. Two verified coordinate-free methods:

  1. **peekaboo via `inspect-ui`** — enumerate with `inspect-ui` (not `see`), find the `elem_NN` for the target `identifier`, then click by element id:

     ```sh
     peekaboo inspect-ui --app-target "DotNet 6502 Emulator" \
       --max-depth 60 --max-elements 5000 --max-children 500 --json > /tmp/insp.json
     # parse Snapshot ID + the elem_NN whose identifier == "StartButton", then:
     peekaboo click --on <elem_NN> --snapshot <snapshotId> --app "DotNet 6502 Emulator" --window-index 0
     ```

  2. **System Events `AXPress` by `AXIdentifier`** — snapshot the tree first, then match on `AXIdentifier`:

     ```applescript
     tell application "System Events"
         tell process "DotNet 6502 Emulator"
             set allEls to entire contents of window 1
             repeat with e in allEls
                 try
                     if (value of attribute "AXIdentifier" of e) is "StartButton" then
                         perform action "AXPress" of e
                     end if
                 end try
             end repeat
         end tell
     end tell
     ```

  Both were verified to boot the C64 (Start → Running) and stop it (Stop → Uninitialized). For sidebar section toggles / joystick ports, the C64 **menu keyboard shortcuts** remain the simplest option (see the shortcuts tables above).

- **osascript: snapshot the tree before iterating.** Use `set allEls to entire contents of window 1` then `repeat with e in allEls`. The inline form `repeat with e in (entire contents of window 1)` silently fails to resolve elements and returns zero matches — this can masquerade as an "intermittent" or "missing" AX tree. Also, `items` is an AppleScript reserved word; don't use it as a variable name. Read state with `value of attribute "AXValue"`, enabled state with `AXEnabled`, id with `value of attribute "AXIdentifier"`.

### Worked example: start the emulator, then open the Log tab

```sh
# Snapshot the full control tree and find the Start button (see does NOT surface it — use inspect-ui)
peekaboo app switch --to "DotNet 6502 Emulator"
peekaboo inspect-ui --app-target "DotNet 6502 Emulator" \
  --max-depth 60 --max-elements 5000 --max-children 500 --json > /tmp/insp.json

read SNAP START < <(python3 -c "
import json,re
d=json.load(open('/tmp/insp.json'), strict=False)   # strict=False: debug_logs hold raw control chars
t=d['data']['content'][0]['text']
print(re.search(r'Snapshot ID: (\S+)', t).group(1),
      re.search(r'(elem_\d+) - .*identifier: StartButton', t).group(1))
")

# 1. Click Start — emulator boots into C64 BASIC
peekaboo click --on "$START" --snapshot "$SNAP" --app "DotNet 6502 Emulator" --window-index 0

# 2. Switch to the Log tab via its keyboard shortcut (order-independent, most robust)
peekaboo hotkey --keys "cmd,option,l" --app "DotNet 6502 Emulator"

# Verify (screenshot needs Screen Recording; or read EmulatorStateText via inspect-ui/AXValue)
peekaboo image --app "DotNet 6502 Emulator" --window-index 0 --path /tmp/after.png
```

The JSON path shapes above match peekaboo 3.8.0 output. `peekaboo list` + `peekaboo inspect-ui --json` are the two fundamental queries to script anything more involved (`see` is fine for a screenshot but truncates the control tree).

## See also

- [Avalonia Desktop app](desktop.md) — general app overview
- [Avalonia Accessibility docs](https://docs.avaloniaui.net/docs/concepts/accessibility)
- [NSAccessibility Programming Guide](https://developer.apple.com/documentation/appkit/accessibility_for_appkit)
- [peekaboo](https://peekaboo.dev) — macOS UI automation CLI used for the verification in this doc
