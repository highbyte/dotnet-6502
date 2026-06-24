# C64 cartridge support

The C64 emulator includes cartridge-slot support for optional built-in devices and for attaching
`.crt` cartridge image files.

Cartridge support is intentionally incremental. Many C64 cartridges are small ROM boards, but
freezer, fast-load, and banked game cartridges often contain their own control registers, RAM,
banking rules, and NMI / GAME / EXROM line behavior. Unsupported cartridge hardware types fail with
an explicit error instead of silently attaching incorrectly.

## Using cartridges

In the Avalonia C64 UI, use the **Cartridge** section to:

- attach a `.crt` cartridge image;
- detach the current cartridge;
- press the cartridge **Freeze** button when the attached cartridge supports it.

Attaching a `.crt` image resets the C64, matching the practical emulator model of inserting a
cartridge before booting the machine. Detaching also resets the machine so the memory map returns to
normal cleanly.

The Avalonia Desktop and Browser apps can also attach a cartridge during automated startup:

```sh
# Local file, Avalonia Desktop
./Highbyte.DotNet6502.App.Avalonia.Desktop --system C64 --start --loadCrt ~/Downloads/fc3.crt

# URL, Avalonia Desktop
./Highbyte.DotNet6502.App.Avalonia.Desktop --system C64 --start --loadCrtUrl https://example.com/fc3.crt
```

```text
# Avalonia Browser query parameter
?system=C64&start=1&loadCrtUrl=https%3A%2F%2Fexample.com%2Ffc3.crt
```

Unlike PRG, BASIC, and D64 startup flows, cartridge startup does **not** require
`waitForSystemReady`. A cartridge is attached as a reset / boot-time device, not loaded after the
BASIC prompt is ready.

## Supported `.crt` hardware types

The emulator currently supports these `.crt` hardware type IDs:

| CRT type | Cartridge | Implemented behavior | Main limitations |
|---:|---|---|---|
| `0` | Generic / normal cartridge | Fixed ROM cartridge using the CRT header GAME / EXROM lines. Supports 8K, 16K, and Ultimax-style shapes when the CHIP data maps cleanly to the selected ROM windows. | Banked or I/O-driven cartridges must use their specific CRT hardware type; generic images are bank 0 only. |
| `1` | Action Replay | Action Replay 4.2/5/6 style cartridge with four 8K ROM banks, 8K RAM, I/O controlled banking / mode register, cartridge RAM export, and Freeze support. | Disk fast-loader usefulness is limited by the emulator's current basic 1541 support. Very timing-sensitive freezer functions may expose remaining VIC-II/CIA timing inaccuracies. |
| `3` | Final Cartridge III | Standard four-bank Final Cartridge III and 16-bank FC3+ style images. Supports ROML / ROMH banking, I/O mirrors, GAME / EXROM control, hidden register behavior, NMI line handling, and Freeze support. | The menu and freezer are usable, but some display edge cases can still reveal non-cycle-exact VIC-II raster timing. |
| `5` | Ocean | Banked Ocean game cartridges with up to 64 8K banks. Smaller images use 16K mode; 512K / 64-bank images use 8K ROML-only mode. Bank register is handled through IO1. | Requires a power-of-two bank count. Only ROM CHIP packets are supported. |
| `6` | Expert Cartridge | Trilogic Expert saved-RAM image support with 8K RAM, ON / PRG / OFF style mapping behavior, NMI acknowledge handling, and Freeze support. | Treated as a saved 8K RAM image; persistence back to the `.crt` file is not implemented. |
| `10` | Epyx FastLoad | 8K Epyx FastLoad ROM with capacitor-style ROM timeout behavior: ROML / IO1 reads enable ROML for 512 CPU cycles, IO2 exposes the final ROM page. | The cartridge boots, but the fast-load benefit is limited until the emulator has more complete 1541 drive emulation. |
| `19` | Magic Desk | Banked 8K ROML cartridge with a write-only IO1 bank / disable register. Supports up to 128 ROM banks. | ROM CHIP packets only; no cartridge RAM or nonstandard Magic Desk variants. |

Images with other hardware type IDs currently report an unsupported CRT hardware error.

## Built-in cartridge-style devices

The emulator also models SwiftLink as a cartridge-slot device rather than as a loose I/O hack.

SwiftLink is configured separately from `.crt` images and is documented in
[SwiftLink support](swiftlink.md). It provides ACIA-style serial I/O at `$DE00` or `$DF00`, optional
IRQ/NMI routing, and host-side raw TCP or Hayes modem transport modes.

## General limitations

- The `.crt` parser supports ROM CHIP packets for ROM cartridges and the Expert 8K RAM image form.
- Writing modified cartridge RAM or bank state back to a `.crt` file is not implemented.
- The Freeze button is only available for cartridge implementations that expose a freezer function.
- Fast-loader cartridges can install and run their own code, but the emulator's disk-drive model is
  still a simplified `.d64` loader, not a cycle-exact 1541.
- Some freezer cartridges stress VIC-II, CIA, NMI, and memory-mapping behavior much harder than
  normal games. Several such core issues have been fixed while adding cartridge support, but this is
  still not a cycle-exact C64.
