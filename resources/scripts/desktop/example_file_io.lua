-- example_file_io.lua
-- Demonstrates the file I/O API available to Lua scripts.
--
-- All paths are relative to the configured base directory (ScriptDirectory by default).
-- Paths that attempt to escape the base directory (e.g. "../") are blocked and return nil.
--
-- Read operations are always allowed:
--   file.read(name)          -> string (text content) or nil
--   file.read_bytes(name)    -> table of integers 0-255 (1-indexed) or nil
--   file.exists(name)        -> boolean
--   file.list([pattern])     -> table of filenames (1-indexed), pattern defaults to "*"
--
-- Write operations require AllowFileWrite: true in the "Highbyte.DotNet6502.Scripting" config section:
--   file.write(name, text)   -> writes/overwrites a text file
--   file.append(name, text)  -> appends text to a file (creates if not exists)
--   file.delete(name)        -> deletes a file (no-op if not found)
--
-- Binary loading into emulator memory:
--   emu.load(name)           -> loads binary file; reads 2-byte little-endian PRG load address from header
--   emu.load(name, address)  -> loads raw binary file at the given address (no header parsing)

-- ---- List files in the script directory ----
log.info("Files in script directory:")
local files = file.list("*.lua")
for i, name in ipairs(files) do
    log.info("  " .. i .. ": " .. name)
end

-- ---- Read a text file ----
local readme = "README.txt"
if file.exists(readme) then
    local content = file.read(readme)
    log.info("README.txt: " .. (content or "(empty)"))
else
    log.info("README.txt not found (skipping read)")
end

-- ---- Write a log file (requires AllowFileWrite: true) ----
-- If AllowFileWrite is false, file.write() raises a runtime error and this script is auto-disabled.
local log_file = "frame_log.csv"
file.write(log_file, "frame,pc,a,x,y\n")
log.info("Logging CPU state every 60 frames to " .. log_file)

-- ---- Binary load example (commented out — requires a PRG file in the scripts directory) ----
-- emu.load("my_program.prg")           -- auto-detects load address from 2-byte PRG header
-- emu.load("raw_data.bin", 0xC000)     -- loads raw binary at address $C000 (no header)

-- ---- Read binary bytes ----
-- local bytes = file.read_bytes("patch.bin")
-- if bytes then
--     log.info("patch.bin: " .. #bytes .. " bytes")
--     -- Example: apply the first 16 bytes as a patch at $8000
--     for i = 1, math.min(16, #bytes) do
--         mem.write(0x8000 + i - 1, bytes[i])
--     end
-- end

-- ---- Coroutine loop: append a CSV row every 60 frames ----
while true do
    emu.frameadvance()
    local frame = emu.framecount()
    if frame % 60 == 0 then
        -- log.debug(string.format("Frame %d: PC=$%04X A=$%02X X=$%02X Y=$%02X",
        --     frame, cpu.pc, cpu.a, cpu.x, cpu.y))
        local row = string.format("%d,$%04X,$%02X,$%02X,$%02X\n",
            frame, cpu.pc, cpu.a, cpu.x, cpu.y)
        file.append(log_file, row)
    end
end
