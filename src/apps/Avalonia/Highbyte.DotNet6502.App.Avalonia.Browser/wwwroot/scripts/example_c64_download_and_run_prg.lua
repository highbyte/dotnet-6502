
-- example_c64_download_and_run_prg.lua
-- Waits for the C64 emulator to start, waits 3 seconds, then downloads a PRG file from the network and loads it into emulator memory.
--
-- Uses emu.yield() so the script keeps ticking while the emulator is paused
-- or stopped, allowing it to detect when the emulator starts.
--

log.info("Waiting for C64 emulator to start...")

-- Wait until the emulator is running with the C64 system selected
while emu.state() ~= "running" or emu.selected_system() ~= "C64" do
    emu.yield()
end

log.info("C64 emulator started. Waiting 3 seconds before downloading PRG file...")

-- Wait 3 seconds (wall-clock)
local start_time = emu.time()
while emu.time() - start_time < 3.0 do
    emu.yield()
end

log.info("Now downloading PRG file from network.")

local resp = http.download("https://highbyte.se/dotnet-6502/app/6502binaries/C64/Assembler/smooth_scroller_and_raster.prg", "smooth_scroller_and_raster.prg")
if resp.ok then
    log.info("Saved to sandbox as smooth_scroller_and_raster.prg")

    log.info("Loading PRG file into emulator memory and starting...")
    emu.load("smooth_scroller_and_raster.prg", true)    -- auto-detect load address from 2-byte PRG header and start execution at that address
else
    log.error("Download failed: " .. (resp.error or "HTTP " .. resp.status))
end
