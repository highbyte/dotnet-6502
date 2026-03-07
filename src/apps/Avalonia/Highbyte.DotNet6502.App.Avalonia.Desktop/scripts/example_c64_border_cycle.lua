-- example_border_cycle.lua
-- Waits for the C64 emulator to start, waits 3 seconds, then cycles the
-- border color once per frame through the 16 C64 colors (0-15).
--
-- Uses emu.yield() so the script keeps ticking while the emulator is paused
-- or stopped, allowing it to detect when the emulator starts.
--
-- C64 border color register: $D020 (bits 0-3 select one of 16 colors)

log.info("Waiting for C64 emulator to start...")

-- Wait until the emulator is running with the C64 system selected
while emu.state() ~= "running" or emu.selected_system() ~= "C64" do
    emu.yield()
end

log.info("C64 emulator started. Waiting 3 seconds before cycling border color...")

-- Wait 3 seconds (wall-clock)
local start_time = emu.time()
while emu.time() - start_time < 3.0 do
    emu.yield()
end

log.info("Now cycling border color every frame.")

local color = 0
while true do
    mem.write(0xD020, color)
    color = (color + 1) % 16
    emu.frameadvance()
end
