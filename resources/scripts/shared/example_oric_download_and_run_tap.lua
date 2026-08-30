-- example_oric_download_and_run_tap.lua
-- Downloads the repository's Oric Hello World TAP sample as bytes and loads it
-- directly into BASIC memory. Requires AllowHttpRequests: true.

log.info("Waiting for the Oric Atmos emulator to start...")

if emu.selected_system() ~= "Oric" then
    emu.stop()
    while emu.state() ~= "stopped" do emu.yield() end
    emu.select("Oric", "ATMOS48K")
end
while emu.selected_system() ~= "Oric" do emu.yield() end

if emu.state() ~= "running" then emu.start() end
while emu.state() ~= "running" do emu.yield() end
while not oric.basic_started() do emu.frameadvance() end

-- Pin the repository asset to the commit that introduced it so this example
-- keeps working if branches are renamed or sample paths change later.
local url = "https://raw.githubusercontent.com/highbyte/dotnet-6502/63d63faf280f85a132e61cbef66f8330abe0ca0c/samples/Basic/Oric/Text/Build/HelloWorld.tap"
log.info("Downloading the Oric Hello World TAP sample...")
local response = http.get_bytes(url)

if response.ok then
    local file = oric.load_tap(response.body, 1, false)
    log.info(string.format(
        "Loaded %s at $%04X-$%04X (%s)",
        file.name, file.start, file["end"], file.type))
    oric.print_text("RUN\n")
else
    log.error("Download failed: " .. (response.error or "HTTP " .. response.status))
end
