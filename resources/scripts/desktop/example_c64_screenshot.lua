-- example_c64_screenshot.lua
-- Starts the C64, waits for BASIC to be ready, takes a PNG screenshot,
-- then stops the emulator.
--
-- Works with both the Avalonia Desktop and Headless host apps.
--
-- Requirements in the "Highbyte.DotNet6502.Scripting" config section:
--   AllowFileIO: true       (required for file operations)
--   AllowFileWrite: true    (required for emu.screenshot)
--
-- The screenshot is saved relative to the scripting base directory
-- (FileBaseDirectory, or ScriptDirectory when not set):
--   <base>/screenshots/c64_basic_ready.png
--
-- Launch with Avalonia Desktop (from app output directory):
--   ./Highbyte.DotNet6502.App.Avalonia.Desktop --script scripts/example_c64_screenshot.lua
--
-- Launch with Headless app (from repo root or app output directory):
--   ./Highbyte.DotNet6502.App.Headless --script scripts/example_c64_screenshot.lua
--
-- Available screenshot function:
--   emu.screenshot(filename)          -> saves as PNG (determined by .png extension)
--   emu.screenshot(filename, quality) -> saves as JPEG (determined by .jpg/.jpeg extension),
--                                        quality 1-100 (default 90)

-- ── Step 1: Ensure C64 is selected and running ───────────────────────────────

if emu.selected_system() ~= "C64" then
    log.info("[screenshot] Selecting C64 system...")
    emu.select("C64")
end

if emu.state() ~= "running" then
    log.info("[screenshot] Starting emulator...")
    emu.start()
end

-- Wait until C64 is actually up and running
while emu.state() ~= "running" or emu.selected_system() ~= "C64" do
    emu.yield()
end

log.info("[screenshot] C64 is running. Waiting for BASIC to start...")

-- ── Step 2: Wait for BASIC to initialize ─────────────────────────────────────
--
-- c64.basic_started() checks the TXTAB pointer ($002B-$002C) to detect
-- whether the BASIC interpreter has initialized and the READY. prompt is up.

while not c64.basic_started() do
    emu.frameadvance()
end

-- Wait a few more frames to ensure the READY. prompt is fully rendered and stable
emu.frameadvance()
emu.frameadvance()
emu.frameadvance()
emu.frameadvance()
emu.frameadvance()

-- Wait 1 second after BASIC is ready
-- local start = emu.time()
-- while emu.time() - start < 1.0 do
--     emu.frameadvance()
-- end

log.info("[screenshot] BASIC ready. Taking screenshot...")

-- ── Step 3: Take the screenshot ───────────────────────────────────────────────
--
-- The file path is relative to the scripting base directory.
-- The screenshots/ subdirectory is created automatically if it does not exist.

local filename = "screenshots/c64_basic_ready.png"
emu.screenshot(filename)
log.info("[screenshot] Saved: " .. filename)

-- ── Step 4: Stop the emulator ────────────────────────────────────────────────
--
-- In headless mode the process keeps running until stopped; request a clean
-- shutdown so the app can exit. In the Avalonia app this is optional — remove
-- or comment out the lines below if you want the emulator to keep running.

log.info("[screenshot] Done. Stopping emulator.")
emu.stop()
