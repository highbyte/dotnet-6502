-- example_input_joystick.lua
-- Demonstrates joystick input injection.
-- The following sequence is repeated 3 times:
--   left 0.2s → pause 0.2s → right 0.2s → pause 0.2s → fire 0.5s → pause 0.5s
--
-- IMPORTANT: Input functions must not be called from top-level script code.
-- They are only valid once the first on_before_frame() fires (i.e. after the
-- input provider has been wired to the running system).

log.info("Joystick input example started.")
log.info("System: " .. emu.selected_system())

local PORT    = 1   -- joystick port to use (1 or 2)
local REPEATS = 3

local sequence = {
    { action = "left",  hold = 0.2 },
    { action = nil,     hold = 0.2 },
    { action = "right", hold = 0.2 },
    { action = nil,     hold = 0.2 },
    { action = "fire",  hold = 0.5 },
    { action = nil,     hold = 0.5 },
}

local step = 0       -- current position in sequence (0 = not yet started)
local step_start = 0 -- emu.time() when the current step began
local rep = 0        -- current repetition (1-based once started)

local function start_step(now)
    local entry = sequence[step]
    local prefix = "Rep " .. rep .. "/" .. REPEATS .. ": "
    if entry.action then
        log.info(prefix .. "joystick '" .. entry.action .. "' for " .. entry.hold .. "s")
    else
        log.info(prefix .. "pause for " .. entry.hold .. "s")
    end
    step_start = now
end

function on_before_frame()
    local now = emu.time()

    -- First frame: log discovery info and start the sequence
    if step == 0 then
        local actions = input.available_joystick_actions()
        log.info("Available joystick actions: " .. #actions .. " total")
        log.info("Joystick port count: " .. input.joystick_count())
        rep  = 1
        step = 1
        start_step(now)
    end

    if rep > REPEATS then return end

    local entry = sequence[step]

    -- Activate the joystick action for this step (nothing to do during a pause)
    if entry.action then
        input.joystick_set(PORT, entry.action, true)
    end

    -- Advance to the next step once the hold/pause duration has elapsed
    if now - step_start >= entry.hold then
        step = step + 1
        if step > #sequence then
            -- Finished one repetition; start the next
            rep  = rep + 1
            step = 1
            if rep > REPEATS then
                log.info("All " .. REPEATS .. " repetitions complete.")
                return
            end
        end
        start_step(now)
    end
end
