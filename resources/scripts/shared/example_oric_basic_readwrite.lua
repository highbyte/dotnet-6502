-- example_oric_basic_readwrite.lua
-- Types a small Oric BASIC program, then reads the tokenized program back as source.

log.info("Waiting for the Oric Atmos emulator to start...")

if emu.selected_system() ~= "Oric" then
    emu.stop()
    while emu.state() ~= "stopped" do emu.yield() end
    emu.select("Oric", "ATMOS48K")
end
while emu.selected_system() ~= "Oric" do emu.yield() end

if emu.state() ~= "running" then emu.start() end
while emu.state() ~= "running" do emu.yield() end

while not oric.basic_started() do
    emu.frameadvance()
end

log.info("Oric BASIC is ready. Typing a two-line program...")
oric.print_text("10 PRINT \"HELLO FROM LUA\"\n20 GOTO 10\n")

-- Text paste feeds at most one character per frame. Allow the Atmos ROM to
-- consume and tokenize both lines before reading the program from memory.
for _ = 1, 120 do
    emu.frameadvance()
end

local source = oric.get_basic_source()
log.info("Retrieved BASIC source:\n" .. source)

local line1 = string.find(source, "10 PRINT") ~= nil
local line2 = string.find(source, "20 GOTO") ~= nil
if line1 and line2 then
    log.info("Round-trip check PASSED.")
else
    log.error("Round-trip check FAILED.")
end

if emu.host() == "headless" then
    emu.quit()
end
