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

By default `appsettings.json` expects them in `%HOME%/Downloads/C64` (i.e. `~/Downloads/C64` on macOS/Linux, `%USERPROFILE%\Downloads\C64` on Windows). The directory and filenames can be changed in `appsettings.json`:

```json
"Highbyte.DotNet6502.C64.Headless": {
  "SystemConfig": {
    "ROMDirectory": "%HOME%/Downloads/C64",
    "ROMs": [
      { "Name": "basic",   "File": "basic.901226-01.bin" },
      { "Name": "kernal",  "File": "kernal.901227-03.bin" },
      { "Name": "chargen", "File": "characters.901225-01.bin" }
    ]
  }
}
```

The browser-based apps store ROM files in browser local storage after upload (or after auto-download).
