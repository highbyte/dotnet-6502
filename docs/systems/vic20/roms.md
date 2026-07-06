# ROMs

The VIC-20 system requires three ROM files: **Kernal**, **Basic**, and **Character generator (Chargen)**.

!!! important
    You may need a license from Commodore/Cloanto (or own a real VIC-20) to use VIC-20 ROM files.

## Where to get them

The ROM files can be downloaded from [zimmers.net](https://www.zimmers.net/anonftp/pub/cbm/firmware/computers/vic20/):

| ROM | File | Direct link |
|-----|------|-------------|
| Basic | `basic.901486-01.bin` | [download](https://www.zimmers.net/anonftp/pub/cbm/firmware/computers/vic20/basic.901486-01.bin) |
| Kernal | `kernal.901486-07.bin` | [download](https://www.zimmers.net/anonftp/pub/cbm/firmware/computers/vic20/kernal.901486-07.bin) |
| Chargen | `characters.901460-03.bin` | [download](https://www.zimmers.net/anonftp/pub/cbm/firmware/computers/vic20/characters.901460-03.bin) |

The Avalonia Browser and Avalonia Desktop apps also offer an in-app auto-download option (with a license notice).

## Where to put them

By default, desktop hosts look for VIC-20 ROMs in the shared user content directory:

- macOS/Linux: `~/Documents/Highbyte/DotNet6502/roms/VIC20`
- Windows: `%USERPROFILE%\Documents\Highbyte\DotNet6502\roms\VIC20`

The directory and filenames can be changed in app settings. User changes saved by the apps are written to the host-specific `appsettings.user.json` overlay under the OS local application data directory, not beside the shipped executable. A shipped `appsettings.json` can still provide packaged defaults:

```json
"Highbyte.DotNet6502.VIC20.Headless": {
  "SystemConfig": {
    "ROMDirectory": "",
    "ROMs": [
      { "Name": "basic",   "File": "basic.901486-01.bin" },
      { "Name": "kernal",  "File": "kernal.901486-07.bin" },
      { "Name": "chargen", "File": "characters.901460-03.bin" }
    ]
  }
}
```

The browser-based apps store ROM files in browser local storage after upload (or after auto-download).
