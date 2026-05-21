-- example_c64_cli_demo.lua
--
-- Demonstrates end-to-end CLI-driven automation for the C64 emulator.
--
-- Launch command (from the app output directory):
--   ./Highbyte.DotNet6502.App.Avalonia.Desktop --script scripts/example_c64_cli_demo.lua
--
-- The --script path is resolved relative to the current working directory.
-- An absolute path also works:
--   ./Highbyte.DotNet6502.App.Avalonia.Desktop --script /path/to/example_c64_cli_demo.lua
--
-- The script selects the C64 system and starts the emulator itself,
-- so no --system or --start flags are needed.
--
-- Sequence:
--   1. Ensure the C64 system is selected and running
--   2. Wait for the BASIC "READY." prompt to appear in screen RAM
--   3. Cycle the border through all 16 C64 colors for ~2 seconds
--   4. Restore the default border color
--   5. Type "HELLO!" at a human-like typing speed

-- == Step 1: Ensure C64 is running =====================================

if emu.selected_system() ~= "C64" then
    log.info("[demo] Selecting C64 system...")
    emu.select("C64")
end

-- Wait for the system selection to be processed (emu.select is deferred)
while emu.selected_system() ~= "C64" do
    emu.yield()
end

-- Validate config before attempting to start
local ok, errors = emu.config_valid()
if not ok then
    log.error("[demo] C64 system config is invalid - cannot start.")
    for _, e in ipairs(errors) do
        log.error("[demo]   " .. e)
    end
    return
end

if emu.state() ~= "running" then
    log.info("[demo] Starting emulator...")
    emu.start()
end

-- Wait until C64 is actually up and running
while emu.state() ~= "running" do
    emu.yield()
end

log.info("[demo] C64 is running. Waiting for BASIC READY prompt...")

-- == Step 2: Wait for BASIC to initialize ==============================
while not c64.basic_started() do
    emu.frameadvance()
end

log.info("[demo] BASIC READY detected. Cycling border color for 2 seconds...")

-- == Step 3: Cycle border color (~2 seconds at C64 PAL 50 fps ~= 100 frames) ==
--
-- C64 border color register: $D020 (bits 3-0, 16 colors 0-15)

local BORDER_REG = 0xD020
local border_start = emu.time()
local color = 0
while emu.time() - border_start < 2.0 do
    mem.write(BORDER_REG, color)
    color = (color + 1) % 16
    emu.frameadvance()
end

-- == Step 4: Restore default border (C64 default: light blue = 14) ====

mem.write(BORDER_REG, 14)
log.info('[demo] Border cycling done. Typing "HELLO!" ...')

-- == Step 5: Type "HELLO!" at human-like speed =========================
--
-- C64 BASIC starts in uppercase mode, so h-o produce uppercase letters on screen.
-- '!' on C64 keyboard = lshift + 1.
--
-- Input injection pattern: input.key_press() must be called every frame to keep
-- a key held (ClearScriptInput fires at the start of each frame). So we loop for
-- hold_frames, pressing the key each iteration, then wait gap_frames with no press.
--
-- Timing at 50 fps (PAL C64):
--   hold_frames = 4  -> ~80 ms per key
--   gap_frames  = 6  -> ~120 ms between keys
--   Total per keystroke ~= 200 ms -> ~5 characters/second

local HOLD_FRAMES = 4
local GAP_FRAMES  = 6

local function type_key(key, with_shift)
    for _ = 1, HOLD_FRAMES do
        input.key_press(key)
        if with_shift then input.key_press("lshift") end
        emu.frameadvance()
    end
    for _ = 1, GAP_FRAMES do
        emu.frameadvance()
    end
end

type_key("h")
type_key("e")
type_key("l")
type_key("l")
type_key("o")
type_key("1", true)   -- lshift + 1 -> '!'

log.info('[demo] Done. "HELLO!" typed.')
