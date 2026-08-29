# ROMs

The Oric Atmos system requires one ROM:

| ROM name | File | Mapping | Size | SHA-1 |
|---|---|---|---|---|
| `basic` | `basic11b.rom` | `$C000`-`$FFFF` | 16,384 bytes | `9451a1a09d8f75944dbd6f91193fc360f1de80ac` |

This is the Atmos BASIC 1.1b firmware. The character patterns used by the ULA live in RAM and do
not require a separate character-generator ROM.

!!! important
    The Oric ROM is copyrighted firmware. The emulator does not grant a licence, and an online
    archive being publicly reachable does not prove that it is authorized to redistribute the
    ROM. Download or use it only if you own an Oric Atmos or otherwise have permission to possess
    and use the firmware.

## Downloading or supplying the ROM

The Avalonia configuration view can fetch `basic11b.rom` from the
[RetroBIOS Oricutron collection](https://abdess.github.io/retrobios/emulators/oricutron/).
It displays an explicit ownership/licence acknowledgement before downloading and validates the
result against the SHA-1 above. The ROM is not bundled with the emulator.

You can instead select a local `.rom` or `.bin` file in the configuration view. Desktop hosts
look in the following default directory:

- macOS/Linux: `~/Documents/Highbyte/DotNet6502/roms/Oric`
- Windows: `%USERPROFILE%\Documents\Highbyte\DotNet6502\roms\Oric`

The desktop default file name is configured as:

```json
"Highbyte.DotNet6502.Oric.Avalonia": {
  "SystemConfig": {
    "ROMs": [
      { "Name": "basic", "File": "basic11b.rom" }
    ]
  }
}
```

The browser host keeps an uploaded or downloaded ROM in browser storage.
