# Headless app — installation

Pre-built binaries are available for Windows, Linux, and macOS.

See [APPS_HEADLESS.md](APPS_HEADLESS.md) for an overview of features, CLI arguments, and Lua scripting.

## Install via Package Manager

**Prerequisites:** Install [Homebrew](https://brew.sh/) (macOS/Linux) or [Scoop](https://scoop.sh/) (Windows) if you don't have them already.

### macOS (Homebrew)

```bash
brew tap highbyte/dotnet-6502
brew install --formula dotnet-6502-headless
```

### Linux (Homebrew)

```bash
brew tap highbyte/dotnet-6502
brew install --formula dotnet-6502-headless
```

### Windows (Scoop)

```powershell
scoop bucket add dotnet-6502 https://github.com/highbyte/scoop-dotnet-6502
scoop install dotnet-6502-headless
```

### Launching

> [!NOTE]
> To run the **C64** system, ROM files (Kernal, Basic, Chargen) must be downloaded separately and placed in the directory configured in `appsettings.json`. See [C64 ROM files](APPS_HEADLESS.md#c64-rom-files) for details.

After installing, run the headless app from a terminal:

```
dotnet-6502-headless --system C64 --start --script scripts/example_c64_basic_readwrite.lua
```

### Updating

```bash
# macOS / Linux
brew update && brew upgrade dotnet-6502-headless
```

```powershell
# Windows
scoop update
scoop update dotnet-6502-headless
```

### Uninstalling

```bash
# macOS / Linux
brew uninstall dotnet-6502-headless
brew untap highbyte/dotnet-6502
```

```powershell
# Windows
scoop uninstall dotnet-6502-headless
scoop bucket rm dotnet-6502
```

---

## Install via manual download

Download the latest release for your platform from the [Releases](https://github.com/highbyte/dotnet-6502/releases) page under Assets.

| Platform | Download |
|----------|----------|
| Windows x64 | `DotNet6502-Headless-win-x64.zip` |
| Windows ARM64 | `DotNet6502-Headless-win-arm64.zip` |
| Linux x64 | `DotNet6502-Headless-linux-x64.zip` |
| Linux ARM64 | `DotNet6502-Headless-linux-arm64.zip` |
| macOS ARM64 (Apple Silicon) | `DotNet6502-Headless-osx-arm64.zip` |

### Windows

1. Extract the `.zip` file to a folder
2. Open a terminal in that folder and run:
   ```
   Highbyte.DotNet6502.App.Headless.exe --system C64 --start
   ```

#### SmartScreen Warning

Since the application is not code-signed, Windows SmartScreen may show a warning the first time you run it:

> "Windows protected your PC - Microsoft Defender SmartScreen prevented an unrecognized app from starting."

**To proceed:**
1. Click **"More info"**
2. Click **"Run anyway"**

---

### Linux

1. Extract the `.zip` file:
   ```sh
   unzip DotNet6502-Headless-linux-x64.zip -d dotnet6502-headless
   cd dotnet6502-headless
   ```

2. Run the app:
   ```sh
   ./Highbyte.DotNet6502.App.Headless --system C64 --start
   ```

---

### macOS

> **Note:** The macOS build is not notarized with Apple.

1. Extract the `.zip` file

2. Open Terminal and navigate to the extracted folder:
   ```sh
   cd /path/to/extracted/folder
   ```

3. Remove the quarantine attribute:
   ```sh
   xattr -cr .
   ```

4. Run the app:
   ```sh
   ./Highbyte.DotNet6502.App.Headless --system C64 --start
   ```

---

### Verifying Download Integrity (Optional)

Each release includes SHA256 checksum files (`checksums-*.sha256`) to verify your download.

#### Windows (PowerShell)

```powershell
(Get-FileHash -Algorithm SHA256 DotNet6502-Headless-win-x64.zip).Hash.ToLower()
```

#### Linux

```sh
sha256sum DotNet6502-Headless-linux-x64.zip
```

#### macOS

```sh
shasum -a 256 DotNet6502-Headless-osx-arm64.zip
```

Compare the output with the corresponding entry in the `checksums-*.sha256` file.
