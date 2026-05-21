
-- example_c64_download_and_run_prg.lua
-- Waits for the C64 emulator to start, waits for BASIC to initialize, then downloads a PRG file from the network and loads it into emulator memory.
--
-- Uses emu.yield() so the script keeps ticking while the emulator is paused
-- or stopped, allowing it to detect when the emulator starts.
--

log.info("Waiting for C64 emulator to start...")

if emu.selected_system() ~= "C64" then
    emu.select("C64")
end
while emu.selected_system() ~= "C64" do
    emu.yield()
end

if emu.state() ~= "running" then
    emu.start()
end
while emu.state() ~= "running" do
    emu.yield()
end

log.info("C64 emulator started. Waiting for BASIC to initialize...")

-- Wait until BASIC has completed its initialization
while not c64.basic_started() do
    emu.frameadvance()
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
