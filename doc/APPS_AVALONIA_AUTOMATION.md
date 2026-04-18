<h1 align="center">Avalonia app UI automation and accessibility</h1>

# Overview

The Avalonia desktop and browser apps expose accessibility metadata so that screen readers and UI-automation agents (including AI agents) can identify, describe, and operate every interactive control.

This is implemented by setting attached properties from the `Avalonia.Automation` namespace on controls in `.axaml`:

- `AutomationProperties.AutomationId` — a stable, programmatic selector (flat PascalCase, e.g. `StartButton`, `ScaleSlider`). Maps to the platform-specific identifier (AX `identifier` on macOS, `AutomationId` on Windows UIA).
- `AutomationProperties.Name` — a short human-readable label (e.g. "Start", "Scale"). Maps to AX `title` on macOS / `Name` on UIA. Read aloud by screen readers.

The `AutomationProperties` attached properties live in the default `https://github.com/avaloniaui` XML namespace, so no extra `xmlns` is required.

Source: [`src/apps/Avalonia/Highbyte.DotNet6502.App.Avalonia.Core/Views/`](../src/apps/Avalonia/Highbyte.DotNet6502.App.Avalonia.Core/Views/).

# What Avalonia auto-generates (no attributes needed)

Even without any `AutomationProperties` attributes, Avalonia already surfaces some data via its automation peers:

| Source in XAML                | Auto-mapped to                                |
| ----------------------------- | --------------------------------------------- |
| `Button.Content="Start"`      | AX label / UIA Name = "Start"                 |
| `TabItem.Header="Log"`        | AX label / UIA Name = "Log"                   |
| `Name="MyControl"` / `x:Name` | AX `identifier` / UIA `AutomationId`          |
| `ToolTip.Tip="…"`             | AX `help` / UIA `HelpText`                    |
| `Window.Title="…"`            | AX window title                               |

Because of this, controls with `Name="…"` already get a stable identifier for free. The codebase keeps existing `Name=` attributes (they're also used by code-behind `FindControl` lookups) and adds explicit `AutomationProperties.AutomationId` alongside — decoupling the automation identity from the code-behind field name.

# Conventions used in this codebase

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

# What is surfaced

A non-exhaustive list of the most useful AutomationIds, grouped by view. All of these are present in the compiled app and queryable via the platform's accessibility API.

## MainView (always visible)

- **System selection**: `SystemSelectionComboBox`, `SystemVariantSelectionComboBox`
- **Emulator control**: `StartButton`, `PauseButton`, `ResetButton`, `StopButton`, `MonitorButton`, `StatsButton`
- **Display/audio**: `ScaleSlider`, `AudioCheckBox`, `AudioVolumeSlider`, `OptionsButton`
- **Status**: `EmulatorStateText`
- **Bottom tab control**: `InformationTabControl` with tabs `InformationTab`, `ConfigStatusTab`, `LogTab`, `ScriptsTab`, `GeneralInfoTab`, `DebugTab`
- **Log tab**: `ClearLogButton`
- **Scripts tab**: `ScriptsBannerRefreshButton`, `ScriptFolderLink`, `AddScriptButton`, `LoadExamplesButton`, `ScriptsRefreshButton`; sort headers `SortByFileNameButton`, `SortByStatusButton`, `SortByYieldButton`, `SortByHooksButton`
- **Script rows (dynamic)**: `ScriptRow.ToggleEnabled.<FileName>`, `ScriptRow.Reload.<FileName>`, `ScriptRow.Edit.<FileName>`, `ScriptRow.Delete.<FileName>`
- **Debug tab**: `ExternalDebugToggleButton`, `ExternalDebugPortInput`, `DebugSoundButton`, `DebugGamepadButton`

## C64MenuView (sidebar)

- **Basic clipboard**: `CopyBasicButton`, `PasteTextButton`, `AiBasicCheckBox`, `AiBasicInfoButton`
- **Collapsible section headers**: `DiskSectionHeader`, `LoadSaveSectionHeader`, `ConfigSectionHeader`
- **Disk section**: `DiskToggleButton`, `DiskInfoButton`, `PreloadedDiskComboBox`, `PreloadedDiskInfoButton`, `DownloadAndRunDiskButton`
- **Load/Save section**: `LoadBasicButton`, `SaveBasicButton`, `LoadBinaryButton`, `AssemblyExampleComboBox`, `LoadAssemblyExampleButton`, `BasicExampleComboBox`, `LoadBasicExampleButton`
- **Config section**: `ActiveJoystickComboBox`, `JoystickKeyboardCheckBox`, `KeyboardJoystickComboBox`, `C64ConfigButton`

## C64ConfigDialog / C64ConfigUserControl

- **Window**: `C64ConfigDialog`
- **ROMs (dynamic per ROM)**: `RomFileTextBox.Kernal`, `RomFileTextBox.Basic`, `RomFileTextBox.Chargen`
- **ROM actions**: `RomDirectoryTextBox`, `ClearRomsButton`, `LoadRomsButton`, `DownloadRomsButton`, `DownloadRomFilesButton`
- **Video**: `RenderProviderComboBox`, `RenderTargetComboBox`
- **Audio**: `SidAudioCheckBox`
- **Input**: `HostJoystickComboBox`, `KeyboardJoystickEnableCheckBox`, `KeyboardJoystickPortComboBox`, `KeyboardMappingsExpander`
- **Network**: `CorsProxyTextBox`, `CorsProxyResetButton`
- **AI assistant**: `AiHelpButton`, `AiBackendComboBox`, `OpenAiApiKeyTextBox`, `OllamaEndpointTextBox`, `OllamaModelNameTextBox`, `OllamaApiKeyTextBox`, `CustomEndpointApiKeyTextBox`, `TestAiBackendButton`
- **Footer**: `CancelButton`, `OkButton`

## EmulatorConfigUserControl (general options)

`DefaultEmulatorComboBox`, `DefaultScaleSlider`, `ShowErrorDialogCheckBox`, `ShowDebugTabCheckBox`, `AudioProfileComboBox`, `StopOnBrkCheckBox`, `StopOnUnknownInstructionCheckBox`, `LuaScriptDirectoryTextBox`, `LuaStorePrefixTextBox`, `CancelButton`, `OkButton`.

## MonitorDialog / MonitorUserControl

- **Window**: `MonitorDialog`
- `OutputScrollViewer`, `CommandTextBox`, `SendCommandButton`, `CloseMonitorButton`, `MonitorStatusScroll`

## ScriptEditorDialog

- **Root**: `ScriptEditorDialog`
- `FileNameBox`, `ContentBox`, `CancelButton`, `SaveButton`

## Debug views

- **DebugSoundUserControl**: `InitAudioButton`, `PlayAudioButton`, `PauseAudioButton`, `StopAudioButton`, `PlaySynthButton`, `StartSynthReleaseButton`, `StopSynthButton`, `SoundTestComboBox`, `PlaySoundButton`, `StopSoundButton`, `CloseButton`
- **DebugGamepadUserControl**: `CloseButton` (remainder is read-only visual status)

## ErrorUserControl

`ErrorMessageTextBox`, `ExceptionDetailsTextBox`, `ShowDetailsButton`, `ContinueButton`, `ExitButton`.

## Other containers

- `EmulatorView` (root) with `EmulatorRenderSurface` on the render ContentPresenter
- `EmulatorPlaceholderView` (root, pre-start logo)
- `StatisticsView` (root)
- `C64InfoView` (root, keyboard mapping reference)

# Keyboard shortcuts (system menu contributions)

Some controls inside nested `UserControl`s do not traverse cleanly to the macOS AX tree (see "known gaps" below — the left-pane `C64MenuView` sections are the most visible example). To keep those operations reachable for agents and keyboard users, the active system's menu ViewModel implements `ISystemMenuContributor` ([`Core/SystemSetup/ISystemMenuContributor.cs`](../src/apps/Avalonia/Highbyte.DotNet6502.App.Avalonia.Core/SystemSetup/ISystemMenuContributor.cs)) and contributes:
- A `NativeMenu` that Avalonia installs on the **macOS system menu bar** (shown under a top-level header for the active system, e.g. `C64`). On macOS, `NativeMenu` items appear in the OS-level menu bar *outside* the app window — which is the desired UX. The macOS Accessibility API also exposes these items with their `Gesture` string, making shortcuts self-describing: an AI agent can discover them at runtime via `peekaboo menu list` without needing any prior documentation.
- A parallel list of `KeyBinding`s applied to the main window on **Windows / Linux**. `NativeMenu` on these platforms would render as in-window chrome, which is not desired, so `KeyBinding`s are used instead. The shortcuts fire regardless of which child control has focus, but they are invisible to accessibility tools — an automation agent needs to know them in advance (e.g. from this document).

`MainViewModel.ActiveMenuContributor` swaps when `SelectedSystemName` changes; `MainView.axaml.cs` applies the new menu / keybindings, and clears the previous one on teardown.

## C64 shortcuts (active when the C64 system is selected)

| Action                           | macOS               | Windows / Linux       |
| -------------------------------- | ------------------- | --------------------- |
| Toggle Disk Drive section        | `⌘⌥⇧D`         | `Ctrl+Alt+Shift+D`    |
| Toggle Load/Save section         | `⌘⌥L`            | `Ctrl+Alt+L`          |
| Toggle Configuration section     | `⌘⌥C`            | `Ctrl+Alt+C`          |
| Active joystick → Port 1         | `⌘⌥1`            | `Ctrl+Alt+1`          |
| Active joystick → Port 2         | `⌘⌥2`            | `Ctrl+Alt+2`          |
| Toggle Joystick KB               | `⌘⌥K`            | `Ctrl+Alt+K`          |
| Keyboard joystick → Port 1       | `⌘⌥⇧1`         | `Ctrl+Alt+Shift+1`    |
| Keyboard joystick → Port 2       | `⌘⌥⇧2`         | `Ctrl+Alt+Shift+2`    |

On macOS, the shortcuts are discoverable by walking the app's menu bar via peekaboo:

```sh
peekaboo menu list --app "DotNet 6502 Emulator"
peekaboo menu click --app "DotNet 6502 Emulator" --path "C64 > Toggle Configuration section"
```

On Windows / Linux, the same shortcuts are dispatched by the main window's key bindings; an automation harness simulates the key combo instead of clicking a menu.

# What is NOT surfaced (known gaps)

1. **Individual `TabItem` controls on macOS** — verified with `peekaboo see` after running the app. The `InformationTabControl` surfaces, but its `TabItem` children (`InformationTab`, `LogTab`, etc.) do not appear as distinct clickable elements in the AX tree, *despite* having explicit `AutomationProperties.AutomationId` + `Name`. The AX tree on macOS reports roles limited to `button`, `group`, `menu`, `other`, `slider` — no `AXTabGroup` / `AXTab`.

   This is most likely an Avalonia `TabItemAutomationPeer` / macOS NSAccessibility bridge limitation, not a bug in this codebase. Worth filing an issue upstream in `avaloniaui/Avalonia`.

   **Workaround**: click the tab by screen coordinates (see the peekaboo section below), or use keyboard navigation (`Ctrl+Tab` / arrow keys when the tab control is focused).

2. **Collapsed/conditional content** only appears in the AX tree when its container is rendered. Examples:
   - `C64MenuView` section contents (`DiskSectionContent`, `LoadSaveSectionContent`, `ConfigSectionContent`) — only visible when the section header is expanded.
   - `Debug` tab contents — hidden until `ShowDebugTab` is enabled in `EmulatorConfig`.
   - Dialog controls (`C64ConfigDialog`, `MonitorDialog`, `ScriptEditorDialog`) — only exist while the dialog is open.

   To automate these, expand/open the container first, then re-query the AX tree.

3. **Dynamic row ids change with data**. `ScriptRow.Reload.<FileName>` depends on the loaded script set. An agent must first query the Scripts tab to discover current rows.

4. **Browser/WebAssembly target**. Automation there goes through the DOM, not Avalonia's AX bridge. Accessibility attributes in `.axaml` propagate only where the Avalonia runtime has a peer — the browser story is not covered in this document.

# Automating on macOS via peekaboo

[peekaboo](https://peekaboo.dev) is a CLI tool for macOS UI automation (screenshots + AX tree traversal + input). These notes are from hands-on verification with the desktop app.

## One-time setup

Grant peekaboo these permissions (System Settings → Privacy & Security):

- **Screen Recording** (required for `see` / `capture`)
- **Accessibility** (required for `click`, `type`, focus management)

Verify with:

```sh
peekaboo permissions
```

## Discovering the AX tree

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

The app's process name under macOS is `DotNet 6502 Emulator` (set via `Application.Name` in `App.axaml`, which Avalonia applies to `NSApplication` at startup). Confirm with:

```sh
peekaboo list
```

## Clicking controls

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

## Gotchas (learned the hard way)

- **Always pass `--window-index 0`** when targeting this app. The Avalonia runtime creates a secondary hidden window, and without a window scope the focus step in `click` times out with `Error: Timeout while waiting for condition`.

- **Don't use `--no-auto-focus` from a terminal.** The terminal emulator (e.g. Ghostty) reclaims focus between commands, so a `--no-auto-focus` click lands on the terminal window at the same screen coordinates — `click` still reports "✅ Click successful" but against the wrong app. Let peekaboo's auto-focus bring the Avalonia window forward.

- **Text-query clicks can hit the wrong element.** `peekaboo click "Log"` may match a `TextBlock` labelled "Log" *inside* the tab content rather than the tab header, because the header is behind the Avalonia TabItem AX gap described above. When targeting tabs specifically, use coordinates.

- **Screenshot coordinates vs. screen coordinates.** The annotated screenshot from `peekaboo see --annotate` is scaled to roughly 0.75× the window-point size. To convert a pixel position in the screenshot to a click coordinate, scale by ~1.33× and add the window's screen offset (`peekaboo list` shows the window Position).

- **AXIdentifier-based clicking isn't directly supported.** peekaboo's `--id` / `--on` flags take the `elem_NN` token from a `see` snapshot, not the `AutomationProperties.AutomationId` string. To select by AutomationId, parse the JSON from `peekaboo see --json` and look for `identifier == "StartButton"` to find the corresponding `elem_NN`, then pass that to `click --on`.

- **Sidebar buttons (C64MenuView) are not reachable via peekaboo `click`.** Controls in the left-hand sidebar — `DownloadAndRunDiskButton`, `PreloadedDiskComboBox`, `LoadBasicButton`, etc. — do not appear in the AX tree that peekaboo enumerates, so neither text-query nor `elem_NN` clicks work. `peekaboo click --coords` also fails because it still attempts AX focus resolution internally when `--app` is given. The reliable fallback is **AppleScript coordinate-click**, which does a raw hit-test outside the AX tree:

  ```applescript
  tell application "DotNet 6502 Emulator" to activate
  delay 0.5
  tell application "System Events"
      tell process "DotNet 6502 Emulator"
          set winPos to position of window 1
          -- Replace (dx, dy) with the button's offset from the window's top-left corner
          click at {(item 1 of winPos) + dx, (item 2 of winPos) + dy}
      end tell
  end tell
  ```

  From a shell script, wrap this in `osascript -e '...'` or `osascript <<'EOF' ... EOF`. Verified working for `DownloadAndRunDiskButton` (offset approx. `x+90, y+585` at default window size). For sidebar actions that have a C64 menu shortcut (toggle sections, set joystick port), prefer `peekaboo menu click` over coordinate hacks — those are more robust to window size changes.

## Worked example: start the emulator, then open the Log tab

```sh
# Snapshot and find Start button
SNAP=$(peekaboo see --app "DotNet 6502 Emulator" --json | jq -r '.snapshot_id')
START=$(peekaboo see --app "DotNet 6502 Emulator" --json \
        | jq -r '.. | objects | select(.identifier == "StartButton") | .id')

# 1. Click Start — emulator boots into C64 BASIC
peekaboo click --on "$START" --snapshot "$SNAP" \
               --app "DotNet 6502 Emulator" --window-index 0

# 2. Click Log tab by coordinates (tab row is ~y=595 at the given window size)
peekaboo click --coords "440,595" --app "DotNet 6502 Emulator" --window-index 0

# Verify
peekaboo see --app "DotNet 6502 Emulator" --annotate /tmp/after.png
```

The JSON path shapes above are illustrative — adjust with your peekaboo version's output schema. `peekaboo list` + `peekaboo see --json` are the two fundamental queries to script anything more involved.

# See also

- [`APPS_AVALONIA.md`](APPS_AVALONIA.md) — general app overview and features
- [Avalonia Accessibility docs](https://docs.avaloniaui.net/docs/concepts/accessibility)
- [NSAccessibility Programming Guide](https://developer.apple.com/documentation/appkit/accessibility_for_appkit)
- [peekaboo](https://peekaboo.dev) — macOS UI automation CLI used for the verification in this doc
