-- example_emulator_control.lua
-- Demonstrates emu control API: querying state and requesting emulator operations.
--
-- This script uses emu.yield() instead of emu.frameadvance() because it needs
-- to keep ticking while the emulator is paused (to observe state and resume).
--
-- Yield primitives:
--   emu.frameadvance()  -- yield until next emulator frame (frozen while paused)
--   emu.yield()         -- yield until next timer tick (keeps ticking while paused)
--
-- Available functions:
--   emu.state()            -- "running", "paused", or "stopped"
--   emu.framecount()       -- number of emulator frames executed
--   emu.time()             -- wall-clock seconds elapsed since scripts were loaded
--   emu.systems()          -- table of available system names
--   emu.selected_system()  -- currently selected system name
--   emu.selected_variant() -- currently selected variant name
--   emu.start()            -- request start (deferred to after current frame)
--   emu.pause()            -- request pause
--   emu.stop()             -- request stop
--   emu.reset()            -- request stop + restart
--   emu.select(name [, variant]) -- request system selection (emulator must be stopped)

local function list_systems()
    local systems = emu.systems()
    local names = {}
    for _, name in ipairs(systems) do
        table.insert(names, name)
    end
    return table.concat(names, ", ")
end

log.info(string.format(
    "Emulator state: %s | System: %s (%s) | Available: [%s]",
    emu.state(),
    emu.selected_system(),
    emu.selected_variant(),
    list_systems()
))

local paused_at_time = nil

while true do
    local frame = emu.framecount()

    -- Pause at frame 300 (~5 seconds on C64), then resume after 3 seconds
    if frame == 300 and emu.state() == "running" then
        log.info("Frame 300 reached — requesting pause")
        emu.pause()
        paused_at_time = emu.time()
    end

    if paused_at_time ~= nil and emu.time() - paused_at_time >= 3.0 then
        log.info(string.format("Resuming after %.1fs pause", emu.time() - paused_at_time))
        emu.start()
        paused_at_time = nil
    end

    -- Log state every 60 frames (only when running, since framecount doesn't advance while paused)
    if emu.state() == "running" and frame % 60 == 0 then
        log.debug(string.format("Frame %d | state=%s", frame, emu.state()))
    end

    -- Use emu.yield() so this script keeps ticking even while paused,
    -- allowing it to observe the paused state and call emu.start().
    emu.yield()
end
