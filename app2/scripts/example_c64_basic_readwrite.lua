-- example_c64_basic_readwrite.lua
-- Demonstrates writing a BASIC program via c64.print_text() and reading it
-- back with c64.get_basic_source().
--
-- Sequence:
--   1. Wait for the C64 emulator to be running
--   2. Wait for BASIC to initialize
--   3. Type a 2-line BASIC program into the keyboard buffer
--   4. Wait for the C64 to process the input
--   5. Read the program back from memory and log it
--   6. Log whether the retrieved source matches what was written

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

log.info("BASIC ready. Typing BASIC program...")

-- == Step 3: Type a 2-line BASIC program ===============================
--
-- c64.print_text() queues text into the C64 keyboard buffer exactly as if the
-- user typed it. Each line must end with "\n" (mapped to C64 Return key).
-- BASIC tokenizes keywords (print, goto) as each line is confirmed with Return.
-- Note: input must be lowercase - the C64 keyboard uses PETSCII where lowercase
-- letters map to uppercase display characters (the C64's default text mode).

local PROGRAM_LINE1 = "10 print \"hello from lua\""
local PROGRAM_LINE2 = "20 goto 10"

c64.print_text(PROGRAM_LINE1 .. "\n" .. PROGRAM_LINE2 .. "\n")

-- == Step 4: Wait for the C64 to process the input =====================
--
-- The keyboard buffer drains at most one character per frame. The two lines
-- total ~35 characters; 120 frames (~2.4 s at 50 fps PAL) gives ample margin.

log.info("Waiting for keyboard buffer to drain...")

for _ = 1, 120 do
    emu.frameadvance()
end

-- == Step 5: Read the program back from memory =========================

local source = c64.get_basic_source()
log.info("Retrieved BASIC source:")
log.info(source)

-- == Step 6: Check that both lines round-tripped correctly =============

local ok1 = string.find(source, "10") ~= nil and string.find(source, "PRINT") ~= nil  -- tokenizer uppercases keywords
local ok2 = string.find(source, "20") ~= nil and string.find(source, "GOTO") ~= nil   -- tokenizer uppercases keywords

if ok1 and ok2 then
    log.info("Round-trip check PASSED: both lines found in retrieved source.")
else
    log.info("Round-trip check FAILED: one or more lines missing from retrieved source.")
end

if emu.host() == "headless" then
    emu.quit()
end
