# Desktop apps - installation

Pre-built desktop applications are available for Windows, Linux, and macOS.

## Available applications

The emulator has front-ends written with different technologies, and have somewhat similar functionality (but not exact).

| Frontend Application | Description |
|----------------------|-------------|
| **Avalonia** | Cross-platform app using Avalonia UI for rendering. See details [here](APPS_AVALONIA.md). |
| **SadConsole** | Cross-platform desktop console-style app using SadConsole library. See details [here](APPS_SADCONSOLE.md). |
| **SilkNetNative** | Cross-platform desktop app using Silk.NET + SkiaSharp + shaders for rendering. See details [here](APPS_SILKNET_NATIVE.md). |

## Install via Package Manager

The **Avalonia** desktop app can be installed via package managers for a simpler experience.

**Prerequisites:** Install [Homebrew](https://brew.sh/) (macOS/Linux) or [Scoop](https://scoop.sh/) (Windows) if you don't have them already.

### macOS (Homebrew)

```bash
brew tap highbyte/dotnet-6502
brew install --cask dotnet-6502
```

### Linux (Homebrew)

```bash
brew tap highbyte/dotnet-6502
brew install --formula dotnet-6502
```

### Windows (Scoop)

```powershell
scoop bucket add dotnet-6502 https://github.com/highbyte/scoop-dotnet-6502
scoop install dotnet-6502
```

### Launching

After installing via a package manager, run the emulator from a terminal:

```sh
dotnet-6502
```

On macOS, the app is also installed to `/Applications` and can be launched from Launchpad, Spotlight, or Finder like any other Mac app.

On Windows (Scoop), a Start Menu shortcut **DotNet 6502 Emulator** is also created.

### Updating

```bash
# macOS
brew update && brew upgrade --cask dotnet-6502

# Linux
brew update && brew upgrade --formula dotnet-6502
```

```powershell
# Windows
scoop update
scoop update dotnet-6502
```

### Uninstalling

```bash
# macOS
brew uninstall --cask dotnet-6502
brew untap highbyte/dotnet-6502

# Linux
brew uninstall --formula dotnet-6502
brew untap highbyte/dotnet-6502
```

```powershell
# Windows
scoop uninstall dotnet-6502
scoop bucket rm dotnet-6502
```

---

## Install via manual download

Download the latest release for your platform from the [Releases](https://github.com/highbyte/dotnet-6502/releases) page under Assets.

| Platform | Download |
|----------|----------|
| Windows x64 | `DotNet6502-*-win-x64.zip` |
| Windows ARM64 | `DotNet6502-*-win-arm64.zip` |
| Linux x64 | `DotNet6502-*-linux-x64.zip` |
| Linux ARM64 | `DotNet6502-*-linux-arm64.zip` |
| macOS ARM64 (Apple Silicon) | `DotNet6502-*-osx-arm64.zip` |

### Launching the Application

#### Windows

1. Extract the `.zip` file to a folder
2. Double-click the `.exe` file to run

##### SmartScreen Warning

Since the application is not code-signed, Windows SmartScreen may show a warning:

> "Windows protected your PC - Microsoft Defender SmartScreen prevented an unrecognized app from starting."

**To proceed:**
1. Click **"More info"**
2. Click **"Run anyway"**

This warning only appears the first time you run the application.

---

#### Linux

1. Extract the `.zip` file:
   ```sh
   unzip DotNet6502-Avalonia-linux-x64.zip -d dotnet6502
   cd dotnet6502
   ```

2. Run the application:
   ```sh
   ./Highbyte.DotNet6502.App.Avalonia.Desktop
   ```

No security warnings are typically shown on Linux.

---

#### macOS

> **Note:** The macOS build is not notarized with Apple. It must be run from Terminal.

1. Extract the `.zip` file

2. Open Terminal and navigate to the extracted folder:
   ```sh
   cd /path/to/extracted/folder
   ```

3. Remove the quarantine attribute:
   ```sh
   xattr -cr .
   ```

4. Run the application:
   ```sh
   ./Highbyte.DotNet6502.App.Avalonia.Desktop
   ```

##### Why can't I double-click to run?

macOS Gatekeeper blocks unsigned/non-notarized applications from running via Finder. Running from Terminal with the `xattr -cr .` command removes the quarantine flag and allows execution.

---

### Verifying Download Integrity (Optional)

Each release includes SHA256 checksum files (`checksums-*.sha256`) to verify your download hasn't been corrupted or tampered with.

#### Windows (PowerShell)

```powershell
(Get-FileHash -Algorithm SHA256 DotNet6502-Avalonia-win-x64.zip).Hash.ToLower()
```

#### Linux

```sh
sha256sum DotNet6502-Avalonia-linux-x64.zip
```

#### macOS

```sh
shasum -a 256 DotNet6502-Avalonia-osx-arm64.zip
```

Compare the output with the corresponding entry in the `checksums-*.sha256` file.

---

## Prerequisites, compatibility, and troubleshooting
[Avalonia desktop app](APPS_AVALONIA_TROUBLESHOOT.md)

[Silk.NET desktop app](APPS_SILKNET_NATIVE_TROUBLESHOOT.md)

[SadConsole desktop app](APPS_SADCONSOLE_TROUBLESHOOT.md)
