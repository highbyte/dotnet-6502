# ROMs

The C64 system requires three ROM files: **Kernal**, **Basic**, and **Character generator (Chargen)**.

!!! important
    You may need a license from Commodore/Cloanto (or own a real C64) to use C64 ROM files.

## Where to get them

The ROM files can be downloaded from [zimmers.net](https://www.zimmers.net/anonftp/pub/cbm/firmware/computers/c64/):

| ROM | File | Direct link |
|-----|------|-------------|
| Kernal | `kernal.901227-03.bin` | [download](https://www.zimmers.net/anonftp/pub/cbm/firmware/computers/c64/kernal.901227-03.bin) |
| Basic | `basic.901226-01.bin` | [download](https://www.zimmers.net/anonftp/pub/cbm/firmware/computers/c64/basic.901226-01.bin) |
| Chargen | `characters.901225-01.bin` | [download](https://www.zimmers.net/anonftp/pub/cbm/firmware/computers/c64/characters.901225-01.bin) |

The Avalonia Browser, Avalonia Desktop, and SadConsole apps also offer an in-app auto-download option (with a license notice).

## Where to put them

By default, desktop hosts look for C64 ROMs in the shared user content folder:

- macOS/Linux: `~/Documents/Highbyte/DotNet6502/roms/C64`
- Windows: `%USERPROFILE%\Documents\Highbyte\DotNet6502\roms\C64`

The directory and filenames can be changed in app settings. User changes saved by the apps are written to the host-specific `appsettings.user.json` overlay under the OS local application data folder, not beside the shipped executable. A shipped `appsettings.json` can still provide packaged defaults:

```json
"Highbyte.DotNet6502.C64.Headless": {
  "SystemConfig": {
    "ROMDirectory": "",
    "ROMs": [
      { "Name": "basic",   "File": "basic.901226-01.bin" },
      { "Name": "kernal",  "File": "kernal.901227-03.bin" },
      { "Name": "chargen", "File": "characters.901225-01.bin" }
    ]
  }
}
```

The browser-based apps store ROM files in browser local storage after upload (or after auto-download).
