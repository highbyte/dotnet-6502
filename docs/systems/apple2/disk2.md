# Disk II (slot 6)

The emulated Disk II controller card boots and reads standard 16-sector DOS 3.3 disk images
(`.dsk` / `.do`), which is what makes disk-dependent software — including games that load data
during play — runnable. It is **read-only**: the drive always reports write-protected.

This is separate from the file-level `.dsk` support in the Load/Save section, which extracts a
program from a disk catalog and injects it into RAM without any drive involved. Use that for
RAM-resident programs; use the drive for anything that boots or reads the disk while running.

## What is emulated

The Disk II is the architectural opposite of a Commodore 1541: the card has **no CPU and no
command protocol**. It is roughly eight TTL chips plus two small PROMs, and the Apple's own 6502
does all the work in software — stepping the head by toggling stepper phase magnets, spinning the
motor, and polling raw GCR bytes out of a shift register. There is therefore nothing to emulate
at a protocol level; the emulation is of the 16 soft switches, and DOS's RWTS (or a game's own
loader) runs unmodified against it.

| Address | Function |
|---|---|
| `$C0E0`–`$C0E7` | Stepper phase 0–3 off/on — head position in half-tracks |
| `$C0E8` / `$C0E9` | Motor off / on |
| `$C0EA` / `$C0EB` | Select drive 1 / drive 2 (only drive 1 is present) |
| `$C0EC` | Q6L — read the data shift register |
| `$C0ED` | Q6H — with Q7L, the write-protect sense |
| `$C0EE` | Q7L — read mode |
| `$C0EF` | Q7H — write mode (no-op) |

The card's 256-byte P5 boot ROM (`341-0027`, 16-sector) is mapped at `$C600`, but only while a
disk is inserted, so an empty drive lets the Autostart slot scan fall through to BASIC instead of
hanging. On insert, each of the 35 tracks is converted once into the nibble stream a real head
would see: sync gaps, address fields (`D5 AA 96`, 4-and-4 encoded volume/track/sector/checksum,
`DE AA EB`) and data fields (`D5 AA AD`, 342 bytes of 6-and-2 encoded data plus checksum,
`DE AA EB`), with the standard DOS 3.3 2:1 sector interleave.

## Inserting versus booting

These are separate operations, because they are separate things on the real machine.

**Applesoft has no disk commands at all.** `CATALOG`, `BLOAD`, `BRUN` and `BSAVE` are not in the
system ROM — they come from DOS, which is itself software read off a diskette into RAM (~`$9600`)
where it hooks the character-I/O vectors. (`LOAD` and `SAVE` *are* in ROM, but they are
Applesoft's **cassette** commands until DOS redirects them.) So the workflow on real hardware is:
**boot a DOS disk once, then swap diskettes freely** and use them from the DOS prompt. This is the
opposite of a C64, where the 1541 carries its own DOS in ROM inside the drive and `LOAD"$",8`
works on a cold machine with no system disk at all.

- **Insert** puts a diskette in drive 1 and disturbs nothing. Resident DOS picks it up on its next
  access — this is the equivalent of the C64's "Attach .d64 image".
- **Boot** restarts the machine from the disk in the drive, the equivalent of typing `PR#6`. It is
  also how you get DOS in the first place, and what self-booting games need.

One subtlety about booting is worth knowing because it looks like a bug otherwise: **the Autostart
slot scan only runs on a cold start**. A machine already sitting at the BASIC prompt warm-starts
on reset and goes straight back to the prompt without looking at the slots, so booting invalidates
the power-up byte at `$03F4` first — what power-cycling a real Apple does.

- **Avalonia host:** sidebar → **Disk drive**. One button inserts or ejects (its label follows
  the drive's state, like the C64 menu's attach/detach), and *Boot from disk (PR#6)* appears once
  a disk is in.
- **Remote control:** `apple2.insertdisk`, `apple2.bootdisk`, `apple2.ejectdisk`,
  `apple2.diskstatus`.

The `disk2` ROM is **optional** — without it the machine is simply a diskless Apple II Plus, and
only disk booting is unavailable. Add it in the Apple II configuration dialog like the other ROMs.

## Timing model and its known limitation

Each read of the data register delivers the next nibble of the track stream, so the consumer's own
polling paces the data and a reader can never miss a byte regardless of how slowly it collects
them. That robustness is deliberate: the machine has no cycle-accurate bus for a rotational model
to key off.

**Known limitation:** booting the DOS 3.3 System Master takes about 35 emulated seconds, most of
it DOS's *own* one-second motor spin-up wait, entered 31 times. RWTS decides whether
the drive is spinning by comparing successive reads of the data register, and that decision
depends on real read timing this model does not reproduce. Everything loads correctly, just slower
than a real machine (~7 s). Two alternatives were implemented and measured, and both are worse: a
true rotational model (position from elapsed CPU cycles) never completed DOS's sector reads, and a
latch that holds a byte for N cycles never reached the DOS banner at all. Sync-gap sizes were also
swept (20/5, 16/16, 12/12, 10/10, 9/9) with no effect, confirming the cause is the timing model
rather than the track layout. Removing the wait needs a cycle-accurate read path — the natural
companion to sequencer-PROM (LSS) emulation, if copy-protected media is ever supported.

## Not supported

- Writing. The drive reports write-protected, so saving, formatting and copying will fail.
- A second drive; only drive 1 exists.
- ProDOS-ordered `.po` images, and nibble/flux `.nib`/`.woz` images.
- Copy protection that depends on bit-level sequencer behaviour or exact rotational timing. Use a
  cracked release of such titles.

## Verified

The sidebar's **Download & Run programs** list also uses the drive: entries marked as
self-booting are downloaded (unzipping if needed), inserted and booted, rather than injected
into RAM. Curation rule for new entries: verify them in the running emulator first, and prefer
a **cracked** release of a commercial title. Copy protection is not emulated, so an untouched
original typically reads the disk and then sits on a black screen — the archive's plain
`choplifter.dsk` and `bolo.dsk` do exactly that, while the 4am cracks of the same games boot.

Live-verified in the Avalonia desktop host:

- **DOS 3.3 System Master** (`680-0210-A`, 1982) boots to its banner, and `CATALOG` lists the disk
  contents — a live DOS command performing disk I/O through RWTS.
- **Lode Runner** (Brøderbund, 4am crack) boots and plays, and the drive's read counter keeps
  advancing during gameplay as the game streams its levels from disk. Also verified through the
  Download & Run list end to end: download → unzip → insert → boot → playing.
- **Choplifter** (Brøderbund, 4am + san inc crack) and **Bolo** (Synergistic, 4am crack) boot to
  their title screens.

Automated coverage lives in `Disk2NibbleCodecTests`, `Disk2TrackNibblizerTests` and
`Disk2ControllerTests`, plus opt-in integration tests (`Apple2RealRomDisk2BootTests`) that boot a
real DOS 3.3 image. Those skip unless the system and Disk II ROMs are available (in the Apple II
ROM directory under their published file names — what the app's ROM download writes — or via
`DOTNET6502_APPLE2_ROM` / `DOTNET6502_APPLE2_DISK2_ROM`) *and* `DOTNET6502_APPLE2_BOOT_DSK`
points at a bootable DOS 3.3 image:

```sh
DOTNET6502_APPLE2_BOOT_DSK=/path/to/dos33.dsk dotnet test --filter TestType=Integration
```

Disk images deliberately have no default location — like C64 `.d64` images they are user content
kept wherever you keep it, and picked with the file dialog.
