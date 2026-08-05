# ROMs

The Apple II system requires two ROM files:

| ROM name | Chip | Purpose |
|---|---|---|
| `apple2` | `341-011`…`341-015` + `341-020` | Applesoft BASIC + Autostart Monitor, mapped at `$D000`–`$FFFF` |
| `chargen` | `341-0036` | Character generator — the 64 glyph bitmaps the rasterizer draws |

!!! important
    Apple II ROMs are copyrighted by Apple. You may need to own a real Apple II to use them.

## Where to get them

The Avalonia app can download both ROMs for you — open the Apple II **Configuration** section in
the sidebar, click **Apple II config…**, then **Download ROMs**. A licence acknowledgement is
shown first. Alternatively, supply the files yourself.

The most actively maintained archive is the Asimov mirror, under
[`emulators/rom_images/`](https://mirrors.apple2.org.za/ftp.apple.asimov.net/emulators/rom_images/)
— note that this is *not* under `images/`, which holds floppy disk images.

- **System ROM** — `apple.rom`, a bare 12 KB `$D000`-`$FFFF` image. (`apple_ii+_rom.zip` and
  `APPLE2_.ROM` hold the same ROM in the 20 KB layout.)
- **Character generator** — entry `3410036.BIN` inside `ROMS.ZIP`, whose index (`ROMS.ZIP.TXT`)
  lists `341-0036` as the "][plus character ROM". No standalone II/II+ character generator dump
  is published at the top level of that directory, so the emulator extracts it from the archive
  by entry name — the archive holds 42 `.bin` files, so matching on extension alone would be
  ambiguous.

Other sources include AppleWin's source tree and archive.org TOSEC firmware sets; MAME's
`apple2p` set definition is the authoritative reference for per-chip file names and SHA-1s.

## Accepted images

The loader accepts either layout and validates it by SHA-1:

| Layout | Size | SHA-1 |
|--------|------|-------|
| Trimmed `$D000`–`$FFFF` | 12,288 bytes | `8c5ca0c39005dfb0898af2c0992f797cc77530c0` |
| Emulator-distribution `$B000`–`$FFFF` | 20,480 bytes | `29a53f3bb158b160433369e8e4a1d7cd5bf68ac6` |

In the larger layout the leading 8 KB is padding and the meaningless `$C000`–`$CFFF` I/O space;
only the last 12 KB is loaded.

Six 2 KB per-chip dumps also circulate — `341-011-D0` through `341-015-F0` (Applesoft) plus
`341-020-F8` (Autostart Monitor). Concatenate them into a single 12 KB image in address order
before use.

## The character generator

The 2513 character generator is a *separate* chip and is **not** part of the system ROM image.
On real hardware it is wired to the video circuitry only — the CPU cannot read it — so the
emulator holds it aside for the rasterizer rather than mapping it into the address space.

| Layout | Size | SHA-1 |
|--------|------|-------|
| `341-0036` dump | 2,048 bytes | `f9d312f128c9557d9d6ac03bfad6c3ddf83e5659` |

Only the leading 512 bytes carry unique data: 64 glyphs of 8 scan lines, 5 dots per line stored
in bits 5–1 with the most significant bit leftmost. The remainder of the file repeats that block
with bit 7 set and then duplicates both halves. The loader takes the leading 512 bytes.

The 64 glyphs are `@ A–Z [ \ ] ^ _` followed by `space ! " # … ?` — uppercase only, which is why
an Apple II Plus cannot display lowercase.

## Where to put them

By default, desktop hosts look for Apple II ROMs in the shared user content directory:

- macOS/Linux: `~/Documents/Highbyte/DotNet6502/roms/Apple2`
- Windows: `%USERPROFILE%\Documents\Highbyte\DotNet6502\roms\Apple2`

The directory and filenames can be changed in app settings. User changes saved by the apps are
written to the host-specific `appsettings.user.json` overlay under the OS local application data
directory, not beside the shipped executable. A shipped `appsettings.json` can still provide
packaged defaults:

```json
"Highbyte.DotNet6502.Apple2.Avalonia": {
  "SystemConfig": {
    "ROMDirectory": "",
    "ROMs": [
      { "Name": "apple2",  "File": "APPLE2.ROM" },
      { "Name": "chargen", "File": "3410036.BIN" }
    ]
  }
}
```

The browser-based apps store ROM files in browser local storage after upload.
