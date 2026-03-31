-- example_store.lua
-- Demonstrates the cross-platform key/value store via the 'store' global.
--
-- Prerequisites: in the "Highbyte.DotNet6502.Scripting" config section, set:
--   "Enabled": true
--   "AllowStore": true
--
-- On desktop, values are stored as files in {ScriptDirectory}/.store/ (created automatically).
-- In browser, values are stored in localStorage under the prefix "dotnet6502.store.".
-- Values are always strings. Keys must be valid filenames (no path separators, no "..").
--
-- API summary:
--   store.get(key)         -> string or nil
--   store.set(key, value)  -> (no return)
--   store.delete(key)      -> (no return)
--   store.exists(key)      -> boolean
--   store.list()           -> table of keys (1-indexed)

-- Guard: exit with a clear message if AllowStore is not enabled in config.
if not store then
    log.error("[store] 'store' global is not registered. Set AllowStore: true in the 'Highbyte.DotNet6502.Scripting' config section.")
    return
end

-- ---- Persistent run counter ------------------------------------------------
-- Each time the emulator starts, increment a counter in the store.
-- This value survives application restarts (desktop) or browser tab closes (browser).
local run_count = tonumber(store.get("run_count") or "0") + 1
store.set("run_count", tostring(run_count))
log.info(string.format("[store] This script has started %d time(s).", run_count))

-- ---- First-run flag --------------------------------------------------------
if not store.exists("first_run_done") then
    log.info("[store] First run detected — storing initial values.")
    store.set("first_run_done", "1")
    store.set("greeting", "Hello from example_store.lua!")
end

local greeting = store.get("greeting")
if greeting then
    log.info("[store] Greeting: " .. greeting)
end

-- ---- Overwrite and verify --------------------------------------------------
store.set("temp_value", "alpha")
store.set("temp_value", "beta")   -- overwrites "alpha"
local v = store.get("temp_value")
log.info("[store] temp_value = " .. (v or "nil") .. " (expected: beta)")

-- ---- List all stored keys --------------------------------------------------
local keys = store.list()
log.info(string.format("[store] %d key(s) in store:", #keys))
for _, k in ipairs(keys) do
    log.info("  " .. k .. " = " .. (store.get(k) or ""))
end

-- ---- Hook: save CPU snapshot on emulator start -----------------------------
function on_started()
    local snapshot = string.format("PC=$%04X A=$%02X X=$%02X Y=$%02X",
        cpu.pc, cpu.a, cpu.x, cpu.y)
    store.set("last_start_cpu", snapshot)
    log.info("[store] Saved CPU snapshot on start: " .. snapshot)
end

-- ---- Coroutine loop: update a live counter every 60 frames ----------------
-- Demonstrates writing to the store at runtime (not just on startup).
local frame_key = "frame_checkpoint"
log.info("[store] Entering frame loop — saving a checkpoint every 60 frames.")

while true do
    emu.frameadvance()
    local frame = emu.framecount()
    if frame % 60 == 0 then
        store.set(frame_key, tostring(frame))
        -- Uncomment to log each checkpoint:
        -- log.debug("[store] checkpoint at frame " .. frame)
    end
end
