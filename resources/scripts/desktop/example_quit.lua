-- example_quit.lua
-- Automation pipeline example for use when launching the Avalonia Desktop app
-- from the command line (e.g. via --script).
--
-- Flow:
--   1. Start the C64 emulator.
--   2. Wait for BASIC to be ready.
--   3. Log instructions telling the user to POKE 49152,255 to signal completion.
--   4. Poll address 49152 (0xC000) each frame; quit when value 255 is detected.
--   5. Quit automatically after a 2-minute timeout if no signal is received.

local QUIT_ADDR    = 0xC000   -- address to POKE 255 to signal completion
local QUIT_VALUE   = 255
local TIMEOUT_SECS = 120.0

-- Wait until the C64 system is selected (may be set via --system C64 CLI arg)
while emu.selected_system() ~= "C64" do
    emu.yield()
end

-- Start the C64 emulator
log.info("Starting C64 emulator...")
emu.start()

-- Wait until the emulator is actually running
while emu.state() ~= "running" do
    emu.yield()
end

-- Wait until BASIC has completed its initialization
log.info("Waiting for BASIC to initialize...")
while not c64.basic_started() do
    emu.frameadvance()
end

log.info("BASIC is ready.")
log.info("Quit emulator by poking value 255 to address 49152")

local start_time = emu.time()

while true do
    emu.frameadvance()

    if mem.read(QUIT_ADDR) == QUIT_VALUE then
        log.info("Quit signal received (POKE 49152,255). Quitting application...")
        emu.quit()
        break
    end

    if emu.time() - start_time > TIMEOUT_SECS then
        log.info("Timeout reached. Quitting application...")
        emu.quit()
        break
    end
end
