-- example_c64_load_d64.lua
-- Waits for BASIC to initialize, then loads a .d64 disk image into the
-- emulated DiskDrive 1541 via c64.load_d64().
--
-- The path supports ~ (home directory) on Mac/Linux and %USERPROFILE% on
-- Windows - expansion is handled automatically by the engine.
--
-- After a successful load the directory listing command is typed automatically.

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

log.info("BASIC ready. Loading disk image...")

-- == Load the disk image ===============================================
--
-- Adjust the path below to point at an actual .d64 file on your system.
-- ~ is expanded to the current user's home directory on Mac/Linux;
-- %USERPROFILE% or %HOME% also work.

local disk_path = "~/Downloads/C64/Games/Montezuma's Revenge - 1103.d64"

local ok, err = pcall(function()
    c64.load_d64(disk_path)
end)

if ok then
    log.info("Disk image loaded successfully: " .. disk_path)
    c64.print_text("load \"$\",8\n")
    c64.print_text("list\n")
    c64.print_text("load \"*\",8,1\n")
    c64.print_text("run\n")
else
    log.error("Failed to load disk image: " .. disk_path)
    log.error("Error: " .. tostring(err))
end
