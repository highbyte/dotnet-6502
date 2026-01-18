# Avalonia desktop app

## General prerequisites


## Compatibility Matrix

| OS / Architecture | x64 | arm64 |
|-------------------|-----|-------|
| **Windows**       | ✅ Works | ✅ Works |
| **macOS**         | ➖ N/A | ✅ Works |
| **Linux**         | ⚠️ Works* | ⚠️ Works* |

*May require additional packages (see below)

## Notes
### Windows x64
Tested on Windows 11 (x64). No extra configuration.

### Windows arm64
Tested on Windows 11 (arm64) running in VM on a M1 Mac. No extra configuration.

If exception below occurs (problem with the OpenAL audio library), try reboot.

```
FileNotFoundException: Could not load from any of the possible library names! Please make sure that the library is installed and in the right place!
```

### Mac arm64
Tested on MacBook Air M1, MacOS 26. No extra configuration.

### Linux x64
Should work.

#### Linux via WSLg under Windows
Tested on Ubuntu 22.04.5 (x64). No extra configuration.

### Linux arm64
Tested on Ubuntu 25.10. Working after workaround for fonts, see below.

#### SkiaSharp fonts issue
With current SkiaSharp 3.119.1 (that Avalonia uses) there is seemingly a font issue on Linux arm64 related to the freetype native library linking.

The error when starting is this:
```
symbol lookup error: /home/highbyte/source/repos/dotnet-6502/src/apps/Avalonia/Highbyte.DotNet6502.App.Avalonia.Desktop/publish/linux-arm64/libSkiaSharp.so: undefined symbol: FT_Get_BDF_Property
```

Workaround: Make sure the freetype library is pre-loadad. Fonts will be cached once the app is started (so the export statement is only needed once)

```bash
export LD_PRELOAD="/lib/aarch64-linux-gnu/libfreetype.so.6 /lib/aarch64-linux-gnu/libuuid.so.1"
```

To undo the fix, clear the fonts cache
```bash
rm -rf ~/.cache/fontconfig
```

More troubleshooting details:
```bash
LD_DEBUG=libs,symbols ./Highbyte.DotNet6502.App.Avalonia.Desktop 2> ld_debug.txt
grep -E "libSkiaSharp|libfreetype|FT_Get_BDF_Property" ld_debug.txt | tail -n 120
```

# Advanced: Console Logging

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
