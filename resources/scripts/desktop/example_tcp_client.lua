-- example_tcp_client.lua
--
-- Demonstrates the tcp global for connecting to a fake TCP server mimicking a Machine Learning / Reinforcement Learning
-- TCP server. Requires AllowTcpClient: true in appsettings.json scripting configuration.
--
-- Expected server protocol (length-prefixed binary):
--   Client -> Server: 4-byte little-endian length N, then N bytes (observation/state)
--   Server -> Client: 4-byte little-endian length M, then M bytes (action)
--
-- To test without an ML server, run a simple TCP echo server:
--   Linux/macOS:  ncat -l 9000 --keep-open --exec "/bin/cat"
--   Windows:      ncat -l 9000 --keep-open --exec "cmd /c more"
--
-- The example sends the current CPU state as an observation and writes the first byte
-- of the server's response to the C64 border color register (0xD020) as a visual cue.

local HOST = "127.0.0.1"
local PORT = 9000
local CONNECT_TIMEOUT_MS = 3000

-- Helper: encode a little-endian 4-byte length prefix.
-- Returns a 1-indexed Lua table of 4 numbers (0-255), e.g. encode_u32_le(5) → {5, 0, 0, 0}.
--
-- Why a number table instead of a Lua string?
-- In standard Lua, strings are byte arrays and can hold arbitrary binary data.
-- In MoonSharp, strings are .NET System.String (UTF-16), so any byte value > 127
-- would be misinterpreted as a Unicode code point, silently corrupting binary payloads.
-- Using a 1-indexed table of plain numbers (0-255) avoids all encoding issues and is
-- the standard pattern throughout this scripting API for binary data.
--
-- Note: MoonSharp implements Lua 5.2. Lua 5.3 bitwise operators (&, |, >>, <<) are NOT
-- supported. Use integer arithmetic instead:
--   n & 0xFF       → n % 256
--   (n >> 8) & 0xFF → math.floor(n / 256) % 256   (etc.)
local function encode_u32_le(n)
    return {
        n % 256,                          -- byte 0 (least significant)
        math.floor(n / 256) % 256,        -- byte 1
        math.floor(n / 65536) % 256,      -- byte 2
        math.floor(n / 16777216) % 256,   -- byte 3 (most significant)
    }
end

-- Helper: decode a 4-byte little-endian unsigned 32-bit integer from a byte table.
-- 'bytes' is a 1-indexed table of numbers (0-255) as returned by conn:receive(4).
local function decode_u32_le(bytes)
    return bytes[1] + bytes[2] * 256 + bytes[3] * 65536 + bytes[4] * 16777216
end

log.info("Connecting to ML/RL server at " .. HOST .. ":" .. PORT)
local res = tcp.connect(HOST, PORT, CONNECT_TIMEOUT_MS)
if not res.ok then
    log.error("Connection failed: " .. (res.error or "unknown error"))
    return
end
local conn = res.data
log.info("Connected successfully. Starting observation/action loop...")

local frame_count = 0

while true do
    emu.frameadvance()
    frame_count = frame_count + 1

    -- Build observation: 5 bytes of CPU state (PC lo, PC hi, A, X, Y)
    local obs = {
        cpu.pc % 256,
        math.floor(cpu.pc / 256) % 256,
        cpu.a,
        cpu.x,
        cpu.y,
    }

    -- Send 4-byte length prefix.
    -- encode_u32_le(#obs) returns a 1-indexed byte table, e.g. {5, 0, 0, 0} for #obs == 5.
    -- conn:send() accepts either a string or a 1-indexed byte table (numbers 0-255).
    local prefix = encode_u32_le(#obs)
    local sr = conn:send(prefix)
    if not sr.ok then
        log.error("Failed to send length prefix: " .. (sr.error or "?"))
        break
    end

    -- Send observation payload
    sr = conn:send(obs)
    if not sr.ok then
        log.error("Failed to send observation: " .. (sr.error or "?"))
        break
    end

    -- Receive 4-byte response length prefix.
    -- conn:receive(n) reads exactly n bytes and returns { ok=bool, data={...}, error=string|nil }
    -- where data is a 1-indexed byte table (numbers 0-255).
    local lr = conn:receive(4)
    if not lr.ok then
        log.error("Failed to receive response length: " .. (lr.error or "?"))
        break
    end
    local resp_len = decode_u32_le(lr.data)  -- decode the 4 bytes into a plain integer

    if resp_len > 0 then
        -- Receive action payload
        local ar = conn:receive(resp_len)
        if not ar.ok then
            log.error("Failed to receive action: " .. (ar.error or "?"))
            break
        end

        -- Apply action: write first byte to the C64 border color register as a visual cue.
        -- ar.data is a 1-indexed byte table; ar.data[1] is the first byte (0-255).
        -- % 16 keeps the value in the 0-15 range that the border color register accepts.
        mem.write(0xD020, ar.data[1] % 16)
    end

    -- Log progress every 60 frames (~1 second)
    if frame_count % 60 == 0 then
        log.debug("TCP loop: " .. frame_count .. " frames sent/received")
    end
end

conn:close()
log.info("Disconnected from ML/RL server after " .. frame_count .. " frames.")
