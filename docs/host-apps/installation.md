# Desktop apps installation

Pre-built desktop applications are available for Windows, Linux, and macOS.

## Available applications

The emulator has front-ends written with different technologies, with somewhat similar functionality (but not exact).

| Application | Description | Install via |
|-------------|-------------|-------------|
| **Avalonia** | Cross-platform app using Avalonia UI for rendering. See the [Avalonia Desktop app](avalonia/desktop.md) page. | Package manager or manual download |
| **Terminal (TUI)** | Cross-platform app that runs the emulator inside a real terminal using Terminal.Gui v2. See the [Terminal (TUI) app](terminal/overview.md) page. | Package manager or manual download |
| **Headless** | Cross-platform console app — no UI, driven by CLI / Lua scripts. See the [Headless app](headless/overview.md) page. | Package manager or manual download |
| **Remote client** | Cross-platform CLI that drives a *running* emulator from another process over its TCP remote-control endpoint. See the [Remote control](../tools/remote-control/overview.md) page. | Package manager or manual download |
| **SadConsole** | Cross-platform desktop console-style app using SadConsole library. See the [SadConsole app](sadconsole/overview.md) page. | Manual download only |
| **SilkNetNative** | Cross-platform desktop app using Silk.NET + SkiaSharp + shaders for rendering. See the [SilkNetNative app](silknet-native/overview.md) page. | Manual download only |

---

## Install via package manager

**Avalonia**, **Terminal (TUI)**, **Headless**, and the **Remote client** can be installed via package managers — Homebrew on macOS/Linux, Scoop on Windows. (SadConsole and SilkNetNative are [manual download](#install-via-manual-download) only.)

**Prerequisites:** Install [Homebrew](https://brew.sh/) (macOS/Linux) or [Scoop](https://scoop.sh/) (Windows) if you don't have them already.

!!! note "Homebrew tap trust (macOS/Linux)"
    The install commands use the **fully-qualified** `highbyte/dotnet-6502/<package>` name. This installs and trusts only that single package, as required by Homebrew 6.0's [tap-trust](https://docs.brew.sh/Tap-Trust) model — so no separate `brew tap` step is needed. (The same command also works on older Homebrew.)

    **Upgrading from a pre-6.0 install?** If you previously added the tap on an older Homebrew (`brew tap highbyte/dotnet-6502`), that tap is no longer trusted after you upgrade to Homebrew 6.0, so short-name commands like `brew upgrade --formula dotnet-6502` may now be blocked. Trust it once — either the specific package, e.g. `brew trust --formula highbyte/dotnet-6502/dotnet-6502-terminal` (use `--cask` for the macOS Avalonia GUI app), or the whole tap with `brew trust highbyte/dotnet-6502`.

Find your OS below, then your application's row.

### macOS (Homebrew)

| Application | Install | Update | Remove |
|-------------|---------|--------|--------|
| **Avalonia** (GUI) | `brew install --cask highbyte/dotnet-6502/dotnet-6502` | `brew update && brew upgrade --cask dotnet-6502` | `brew uninstall --cask dotnet-6502` |
| **Terminal (TUI)** | `brew install --formula highbyte/dotnet-6502/dotnet-6502-terminal` | `brew update && brew upgrade dotnet-6502-terminal` | `brew uninstall dotnet-6502-terminal` |
| **Headless** | `brew install --formula highbyte/dotnet-6502/dotnet-6502-headless` | `brew update && brew upgrade dotnet-6502-headless` | `brew uninstall dotnet-6502-headless` |
| **Remote client** | `brew install --formula highbyte/dotnet-6502/dotnet-6502-remote` | `brew update && brew upgrade dotnet-6502-remote` | `brew uninstall dotnet-6502-remote` |

### Linux / Ubuntu (Homebrew)

| Application | Install | Update | Remove |
|-------------|---------|--------|--------|
| **Avalonia** | `brew install --formula highbyte/dotnet-6502/dotnet-6502` | `brew update && brew upgrade --formula dotnet-6502` | `brew uninstall --formula dotnet-6502` |
| **Terminal (TUI)** | `brew install --formula highbyte/dotnet-6502/dotnet-6502-terminal` | `brew update && brew upgrade dotnet-6502-terminal` | `brew uninstall dotnet-6502-terminal` |
| **Headless** | `brew install --formula highbyte/dotnet-6502/dotnet-6502-headless` | `brew update && brew upgrade dotnet-6502-headless` | `brew uninstall dotnet-6502-headless` |
| **Remote client** | `brew install --formula highbyte/dotnet-6502/dotnet-6502-remote` | `brew update && brew upgrade dotnet-6502-remote` | `brew uninstall dotnet-6502-remote` |

### Windows (Scoop)

Register the bucket once, then install/update/remove any application:

```powershell
scoop bucket add dotnet-6502 https://github.com/highbyte/scoop-dotnet-6502
```

| Application | Install | Update | Remove |
|-------------|---------|--------|--------|
| **Avalonia** | `scoop install dotnet-6502` | `scoop update; scoop update dotnet-6502` | `scoop uninstall dotnet-6502` |
| **Terminal (TUI)** | `scoop install dotnet-6502-terminal` | `scoop update; scoop update dotnet-6502-terminal` | `scoop uninstall dotnet-6502-terminal` |
| **Headless** | `scoop install dotnet-6502-headless` | `scoop update; scoop update dotnet-6502-headless` | `scoop uninstall dotnet-6502-headless` |
| **Remote client** | `scoop install dotnet-6502-remote` | `scoop update; scoop update dotnet-6502-remote` | `scoop uninstall dotnet-6502-remote` |

!!! note "Removing the tap / bucket"
    The `highbyte/dotnet-6502` Homebrew tap and the `dotnet-6502` Scoop bucket are shared by all three packages. Only remove them once you've uninstalled **all** dotnet-6502 packages:
    `brew untap highbyte/dotnet-6502` (macOS/Linux) or `scoop bucket rm dotnet-6502` (Windows).

### After installing — launch & notes

| Application | Run command | Notes |
|-------------|-------------|-------|
| **Avalonia** | `dotnet-6502` | On macOS also launchable from Launchpad / Spotlight / Finder (installed to `/Applications`); on Windows from the **DotNet 6502 Emulator** Start Menu shortcut. |
| **Terminal (TUI)** | `dotnet-6502-terminal` | Needs a terminal with **Unicode** + **24-bit true color** (on Windows use Windows Terminal, not legacy `conhost`). C64 / VIC-20 ROMs required — auto-downloadable from the in-app Config dialog. See [Terminal requirements](terminal/overview.md#terminal-requirements). |
| **Headless** | `dotnet-6502-headless --system C64 --start --script scripts/example_c64_basic_readwrite.lua` | CLI / Lua automation. C64 ROMs must be available in the default user ROM directory or a configured override. |
| **Remote client** | `dotnet-6502-remote emu.state` | Drives a *separate* running Avalonia Desktop / Headless emulator over TCP — start that emulator with remote control enabled first. See [Remote control](../tools/remote-control/overview.md); run `dotnet-6502-remote --help` for all commands. |

ROM details: [Systems / C64 / ROMs](../systems/c64/roms.md), [Systems / VIC-20 / ROMs](../systems/vic20/roms.md).

---

## Staying up to date

The package-manager `Update` commands above always work and are the manual way to update. In addition, the package-manager builds (Homebrew / Scoop) can **detect when a newer release is available** and surface it inside the app, and can hand the actual upgrade back to the package manager for you.

- **How detection works** — an app installed via Homebrew or Scoop knows which package manager installed it (via an `install-channel` marker written at install time) and compares its own version against the latest GitHub release. Manual-download and development builds report *not managed* and do no update check. Detection is skipped in CI and can be turned off (see below); it never blocks or delays startup.
- **The command shown is the manual command** — when an update is available the app shows the exact `brew upgrade …` / `scoop update …` command from the tables above. Running that yourself is always equivalent to letting the app do it.
- **Delegated update** — where the app offers an in-app "update now" action (Avalonia Desktop, and the console hosts' `--update` flag) it simply runs that same package-manager command for you.

The update surface differs per app:

| Application | Update surface |
|-------------|----------------|
| **Avalonia** (GUI) | Automatic startup check with an in-window update banner and an About dialog offering a one-click **Update now**. |
| **Terminal (TUI)** | Automatic startup check surfaced as a notice in the **Logs** pane. |
| **Headless** | Automatic startup check surfaced via console logging. |
| **Remote client** | No automatic check by design (keeps stdout script-friendly); update flags only. |

All four also accept the `--version` / `--check-update` / `--update` command-line flags (see each app's CLI reference — for Avalonia Desktop and Headless these are in the [General parameters](avalonia/desktop.md#cli-arguments); for the Remote Client in its [Global options](../tools/remote-control/remote-client.md#global-options)).

!!! note "Disabling the automatic check"
    The automatic (startup) update check can be disabled per app — set `UpdateCheckEnabled` to `false` (Avalonia Desktop: the *Check for updates on startup* option / `appsettings.json`; Terminal and Headless: the top-level `UpdateCheckEnabled` key in `appsettings.json`). It is also suppressed for any app when the `DOTNET6502_NO_UPDATE_CHECK` environment variable is set (to anything other than `0`/`false`) or when `CI` is set. The explicit `--check-update` / `--update` flags ignore these and always run.

---

## Install via manual download

Download the latest release for your platform from the [Releases](https://github.com/highbyte/dotnet-6502/releases) page under Assets. Every application ships as its own zip; substitute `<platform>` with one of `win-x64`, `win-arm64`, `linux-x64`, `linux-arm64`, `osx-arm64` (macOS is Apple Silicon / ARM64 only).

| Application | Zip file | Run after extracting |
|-------------|----------|----------------------|
| **Avalonia** | `DotNet6502-Avalonia-<platform>.zip` | `Highbyte.DotNet6502.App.Avalonia.Desktop` |
| **Terminal (TUI)** | `DotNet6502-Terminal-<platform>.zip` | `Highbyte.DotNet6502.App.Terminal` |
| **Headless** | `DotNet6502-Headless-<platform>.zip` | `Highbyte.DotNet6502.App.Headless` |
| **Remote client** | `DotNet6502-RemoteClient-<platform>.zip` | `Highbyte.DotNet6502.App.RemoteClient` |
| **SadConsole** | `DotNet6502-SadConsole-<platform>.zip` | `Highbyte.DotNet6502.App.SadConsole` |
| **SilkNetNative** | `DotNet6502-SilkNetNative-<platform>.zip` | `Highbyte.DotNet6502.App.SilkNetNative` |

On Windows, append `.exe` to the run command (e.g. `Highbyte.DotNet6502.App.Avalonia.Desktop.exe`).

### Launching the application

#### Windows

1. Extract the `.zip` file to a directory.
2. Double-click the `.exe` file to run.

##### SmartScreen warning

Since the application is not code-signed, Windows SmartScreen may show a warning:

> "Windows protected your PC - Microsoft Defender SmartScreen prevented an unrecognized app from starting."

**To proceed:**

1. Click **"More info"**
2. Click **"Run anyway"**

This warning only appears the first time you run the application.

#### Linux

1. Extract the `.zip` file:

   ```sh
   unzip DotNet6502-Avalonia-linux-x64.zip -d dotnet6502
   cd dotnet6502
   ```

2. Run the application (substitute the binary name from the table above):

   ```sh
   ./Highbyte.DotNet6502.App.Avalonia.Desktop
   ```

No security warnings are typically shown on Linux.

#### macOS

!!! note
    The macOS build is not notarized with Apple. It must be run from Terminal.

1. Extract the `.zip` file.

2. Open Terminal and navigate to the extracted directory:

   ```sh
   cd /path/to/extracted/directory
   ```

3. Remove the quarantine attribute:

   ```sh
   xattr -cr .
   ```

4. Run the application (substitute the binary name from the table above):

   ```sh
   ./Highbyte.DotNet6502.App.Avalonia.Desktop
   ```

##### Why can't I double-click to run?

macOS Gatekeeper blocks unsigned/non-notarized applications from running via Finder. Running from Terminal with the `xattr -cr .` command removes the quarantine flag and allows execution.

### Verifying download integrity (optional)

Each release includes SHA256 checksum files (`checksums-*.sha256`) to verify your download hasn't been corrupted or tampered with. Replace `<zip>` with your downloaded file name.

| OS | Command |
|----|---------|
| Windows (PowerShell) | `(Get-FileHash -Algorithm SHA256 <zip>).Hash.ToLower()` |
| Linux | `sha256sum <zip>` |
| macOS | `shasum -a 256 <zip>` |

Compare the output with the corresponding entry in the `checksums-*.sha256` file.

---

## Prerequisites, compatibility, and troubleshooting

- [Avalonia Desktop app troubleshooting](avalonia/troubleshooting.md)
- [SilkNetNative app troubleshooting](silknet-native/troubleshooting.md)
- [SadConsole troubleshooting](sadconsole/troubleshooting.md)
