-- example_monitor.lua
-- Demonstrates the Lua scripting API for dotnet-6502.
--
-- Available globals:
--   cpu.pc, cpu.a, cpu.x, cpu.y, cpu.sp  (numbers, read-only)
--   cpu.carry, cpu.zero, cpu.negative, cpu.overflow, cpu.interrupt_disable, cpu.decimal_mode  (booleans, read-only)
--   mem.read(address)           -> byte value (0-255)
--   mem.write(address, value)   -> writes byte to address
--   log.info(msg), log.debug(msg), log.warn(msg), log.error(msg)
--
-- To enable scripting, set "Enabled": true in appsettings.json under "Highbyte.DotNet6502.Scripting".

local frame_count = 0

-- Called before each emulator frame executes (~50 times/sec for C64)
function on_before_frame()
    frame_count = frame_count + 1

    -- Log CPU state every 60 frames (~once per second)
    if frame_count % 60 == 0 then
        log.info(string.format(
            "Frame %d | PC=$%04X A=$%02X X=$%02X Y=$%02X SP=$%02X | C=%s Z=%s N=%s V=%s",
            frame_count,
            cpu.pc, cpu.a, cpu.x, cpu.y, cpu.sp,
            tostring(cpu.carry), tostring(cpu.zero),
            tostring(cpu.negative), tostring(cpu.overflow)
        ))
    end
end

-- Called after each emulator frame completes
function on_after_frame()
    -- Example: watch a C64 raster line register
    -- The C64 raster line is stored at $D012 (and bit 7 of $D011 for the high bit)
    local raster = mem.read(0xD012)
    if raster > 250 then
        log.debug("Raster near bottom of screen: " .. raster)
    end

    -- Example: patch memory at runtime (uncomment to use)
    -- mem.write(0x0400, 0x01)  -- Write to C64 screen RAM position 0
end
