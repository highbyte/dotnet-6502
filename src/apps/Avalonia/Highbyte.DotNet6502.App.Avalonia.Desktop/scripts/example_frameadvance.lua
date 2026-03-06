-- example_frameadvance.lua
-- Demonstrates BizHawk-style linear loop scripting using emu.frameadvance().
--
-- Instead of implementing on_before_frame() / on_after_frame() callbacks,
-- on_start() runs once as a coroutine. emu.frameadvance() yields back to the
-- host and resumes on the next frame, making the script read like a sequential
-- program rather than an event handler.
--
-- emu.frameadvance()  -- yield until next frame
-- emu.framecount()    -- current frame number (1-based)

local prev_a = nil

function on_start()
    log.info("Script started. Watching A register for changes...")

    while true do
        local frame = emu.framecount()
        local a = cpu.a

        -- Log CPU state every 60 frames (~once per second on C64)
        if frame % 60 == 0 then
            log.info(string.format(
                "Frame %d | PC=$%04X A=$%02X X=$%02X Y=$%02X SP=$%02X",
                frame, cpu.pc, a, cpu.x, cpu.y, cpu.sp
            ))
        end

        -- Detect changes to the A register
        if prev_a ~= nil and a ~= prev_a then
            log.debug(string.format(
                "Frame %d: A changed $%02X -> $%02X",
                frame, prev_a, a
            ))
        end
        prev_a = a

        emu.frameadvance()  -- suspend here; resume next frame
    end
end
