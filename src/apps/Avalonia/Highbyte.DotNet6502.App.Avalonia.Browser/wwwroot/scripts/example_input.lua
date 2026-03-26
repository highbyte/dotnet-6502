-- example_input.lua
-- Demonstrates the input API for injecting and reading keyboard and joystick input.
--
-- IMPORTANT: The script's top-level code runs immediately when the script is
-- enabled, before any frames execute. The input provider is only wired once
-- the first emu.frameadvance() or the first on_before_frame() fires.
-- Therefore, input functions should only be called from hooks (on_started,
-- on_before_frame) or after at least one frameadvance/yield.
--
-- See on_before_frame() below for the actual demonstration.

local function join(tbl, sep)
    local result = ""
    for i, v in ipairs(tbl) do
        if i > 1 then result = result .. sep end
        result = result .. tostring(v)
    end
    return result
end

log.info("Input API example started.")
log.info("System: " .. emu.selected_system())
log.info("Waiting for first frame to initialize input provider...")

-- Track how many frames we've injected for finite-duration demos
local frame_counter = 0
local demo_phase = 0  -- 0=not started, 1=typing A, 2=done

function on_before_frame()
    frame_counter = frame_counter + 1

    -- Discovery: only run once, on the first frame
    if frame_counter == 1 then
        local joystick_actions = input.available_joystick_actions()
        if joystick_actions and #joystick_actions > 0 then
            log.info("Available joystick actions: " .. join(joystick_actions, ", "))
        else
            log.info("No joystick actions available")
        end
        log.info("Joystick port count: " .. input.joystick_count())

        local keys = input.available_keys()
        if keys and #keys > 0 then
            local preview = ""
            local limit = math.min(10, #keys)
            for i = 1, limit do
                if i > 1 then preview = preview .. ", " end
                preview = preview .. keys[i]
            end
            if #keys > limit then preview = preview .. " ..." end
            log.info("Available keys (" .. #keys .. " total). First 10: " .. preview)
        else
            log.info("No keys available")
        end
    end

    -- Demo phase 1: inject 'A' for 300 frames (a short typing burst)
    if demo_phase == 0 and frame_counter > 1 then
        log.info("Injecting 'A' for 300 frames...")
        demo_phase = 1
    end

    if demo_phase == 1 then
        input.key_press("a")

        -- After 300 frames, move to done phase
        if frame_counter > 300 then
            demo_phase = 2
            log.info("Stopping injection. Demo complete.")
            log.info("Final frame count: " .. frame_counter)
        end
    end
end
