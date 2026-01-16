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
