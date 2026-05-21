-- example_input_kb.lua
-- Demonstrates keyboard input injection.
-- Presses A, B, C in sequence: each key held for 0.5 seconds,
-- with a 0.3-second pause between keys.
--
-- IMPORTANT: Input functions must not be called from top-level script code.
-- They are only valid once the first on_before_frame() fires (i.e. after the
-- input provider has been wired to the running system).

log.info("Keyboard input example started.")
log.info("System: " .. emu.selected_system())

local sequence = {
    { key = "a", hold = 0.5 },
    { key = nil, hold = 0.3 },
    { key = "b", hold = 0.5 },
    { key = nil, hold = 0.3 },
    { key = "c", hold = 0.5 },
}

local step = 0       -- current position in sequence (0 = not yet started)
local step_start = 0 -- emu.time() when the current step began

local function start_step(now)
    local entry = sequence[step]
    if entry.key then
        log.info("Pressing '" .. entry.key .. "' for " .. entry.hold .. "s")
    else
        log.info("Pause for " .. entry.hold .. "s")
    end
    step_start = now
end

function on_before_frame()
    local now = emu.time()

    -- First frame: log available keys and start the sequence
    if step == 0 then
        local keys = input.available_keys()
        log.info("Available keys: " .. #keys .. " total")
        step = 1
        start_step(now)
    end

    if step > #sequence then return end

    local entry = sequence[step]

    -- Hold the key for this step (nothing to do during a pause)
    if entry.key then
        input.key_press(entry.key)
    end

    -- Advance to the next step once the hold/pause duration has elapsed
    if now - step_start >= entry.hold then
        step = step + 1
        if step <= #sequence then
            start_step(now)
        else
            log.info("Sequence complete.")
        end
    end
end
