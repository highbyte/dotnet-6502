# Desktop Applications

Pre-built desktop applications are available for Windows, Linux, and macOS.

## Download

Download the latest release for your platform from the [Releases](https://github.com/highbyte/dotnet-6502/releases) page.

| Platform | Download |
|----------|----------|
| Windows x64 | `DotNet6502-*-win-x64.zip` |
| Windows ARM64 | `DotNet6502-*-win-arm64.zip` |
| Linux x64 | `DotNet6502-*-linux-x64.zip` |
| Linux ARM64 | `DotNet6502-*-linux-arm64.zip` |
| macOS ARM64 (Apple Silicon) | `DotNet6502-*-osx-arm64.zip` |

---

## Launching the Application

### Windows

1. Extract the `.zip` file to a folder
2. Double-click the `.exe` file to run

#### SmartScreen Warning

Since the application is not code-signed, Windows SmartScreen may show a warning:

> "Windows protected your PC - Microsoft Defender SmartScreen prevented an unrecognized app from starting."

**To proceed:**
1. Click **"More info"**
2. Click **"Run anyway"**

This warning only appears the first time you run the application.

---

### Linux

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

### macOS

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

#### Why can't I double-click to run?

macOS Gatekeeper blocks unsigned/non-notarized applications from running via Finder. Running from Terminal with the `xattr -cr .` command removes the quarantine flag and allows execution.

---

## Verifying Download Integrity (Optional)

Each release includes SHA256 checksum files (`checksums-*.sha256`) to verify your download hasn't been corrupted or tampered with.

### Windows (PowerShell)

```powershell
(Get-FileHash -Algorithm SHA256 DotNet6502-Avalonia-win-x64.zip).Hash.ToLower()
```

### Linux

```sh
sha256sum DotNet6502-Avalonia-linux-x64.zip
```

### macOS

```sh
shasum -a 256 DotNet6502-Avalonia-osx-arm64.zip
```

Compare the output with the corresponding entry in the `checksums-*.sha256` file.

---

## Available Applications

| Application | Description |
|-------------|-------------|
| **Avalonia** | Cross-platform app using Avalonia UI for rendering |
| **SadConsole** | Cross-platform desktop console-style app using SadConsole library |
| **SilkNetNative** | Cross-platform desktop app using Silk.NET + SkiaSharp + shaders for rendering |

---

## Advanced: Console Logging (Avalonia Desktop)

The Avalonia desktop application supports optional console logging for debugging and monitoring. This allows you to view log messages in real-time from a terminal, in addition to the Log Tab in the UI.

### Command Line Parameters

| Parameter | Description |
|-----------|-------------|
| `--console-log` or `-c` | Enable console logging output |
| `--log-level <level>` or `-l <level>` | Set the minimum log level (default: `Information`) |

#### Valid Log Levels

| Level | Description |
|-------|-------------|
| `Trace` | Most verbose - includes all messages |
| `Debug` | Detailed debugging information |
| `Information` | General informational messages (default) |
| `Warning` | Warning messages only |
| `Error` | Error messages only |
| `Critical` | Only critical/fatal errors |

### Usage Examples

```sh
# Enable console logging with default level (Information)
./Highbyte.DotNet6502.App.Avalonia.Desktop --console-log

# Enable console logging with Debug level (more verbose)
./Highbyte.DotNet6502.App.Avalonia.Desktop -c -l Debug

# Enable console logging with Warning level (less verbose)
./Highbyte.DotNet6502.App.Avalonia.Desktop --console-log --log-level Warning

# Show all messages including trace
./Highbyte.DotNet6502.App.Avalonia.Desktop -c -l Trace
```

### Platform-Specific Behavior

#### Windows

On Windows, the application opens a **separate console window** for log output. This is because Windows GUI applications (WinExe) don't have a console attached by default.

- A new console window titled "DotNet6502 Emulator - Log Output" will appear
- Log messages are displayed in this dedicated window
- The console window closes automatically when the application exits

#### macOS / Linux

On macOS and Linux, console logging works **inline in the terminal**:

- Log messages appear directly in the terminal where you launched the application
- No separate window is created
- The terminal prompt returns normally when the application exits

### Example Output

```
Console logging enabled (level: Information)
14:32:15 info: App[0] Starting emulator initialization...
14:32:16 info: C64Setup[0] C64 system initialized
14:32:17 info: MainViewModel[0] Emulator started
14:32:45 warn: C64MenuView[0] Could not load disk image
```
