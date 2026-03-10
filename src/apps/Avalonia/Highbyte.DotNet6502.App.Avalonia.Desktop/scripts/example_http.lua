-- example_http.lua
-- Demonstrates outbound HTTP operations via the 'http' global.
--
-- Prerequisites: in the "Highbyte.DotNet6502.Scripting" config section, set:
--   "Enabled": true
--   "AllowHttpRequests": true
--
-- HTTP calls are async: the emulator keeps running while the request is in-flight,
-- and the script resumes automatically when the response arrives.

-- Called once when the emulator system starts (or is reset).
function on_started()
    -- ── GET: fetch JSON from a public API ──────────────────────────────────
    log.info("Fetching JSON from httpbin.org ...")
    local resp = http.get("https://httpbin.org/get")
    if resp.ok then
        -- resp.body is the full response body as a string
        log.info("GET ok (HTTP " .. resp.status .. "), body length: " .. #resp.body)
    else
        log.error("GET failed (HTTP " .. resp.status .. "): " .. (resp.error or "unknown error"))
    end

    -- ── GET with custom headers ────────────────────────────────────────────
    local resp2 = http.get("https://httpbin.org/get", {
        ["X-Script-Name"] = "example_http.lua",
        ["Accept"]        = "application/json"
    })
    if resp2.ok then
        log.info("GET with headers ok (HTTP " .. resp2.status .. ")")
    end

    -- ── POST JSON ──────────────────────────────────────────────────────────
    local payload = '{"emulator":"dotnet-6502","frame":' .. emu.framecount() .. '}'
    local resp3 = http.post_json("https://httpbin.org/post", payload)
    if resp3.ok then
        log.info("POST JSON ok (HTTP " .. resp3.status .. ")")
    else
        log.error("POST JSON failed: " .. (resp3.error or "?"))
    end

    -- ── POST with explicit content type ────────────────────────────────────
    local resp4 = http.post("https://httpbin.org/post", "hello=world", "application/x-www-form-urlencoded")
    if resp4.ok then
        log.info("POST form ok (HTTP " .. resp4.status .. ")")
    end

    -- ── GET bytes: download binary data and load into emulator memory ──────
    -- Example: fetch a small binary from the network and place it at 0xC000.
    
    local resp5 = http.get_bytes("https://highbyte.se/dotnet-6502/app/6502binaries/C64/Assembler/smooth_scroller_and_raster.prg")
    if resp5.ok then
        log.info("Downloaded " .. #resp5.body .. " bytes")
        -- Read the first two bytes as a little-endian load address (typical for C64 PRG files)
        local load_addr = resp5.body[1] + resp5.body[2] * 256
        log.info(string.format("PRG load address: $%04X", load_addr))
        -- Load the rest of the bytes into emulator memory starting at the load address 
        for i = 3, #resp5.body do
            mem.write(load_addr + i - 3, resp5.body[i])
        end
    else
        log.error("Binary download failed: " .. (resp5.error or "?"))
    end

    -- ── Error handling: bad URL / unreachable host ─────────────────────────
    local bad = http.get("http://this-host-does-not-exist.invalid/")
    if not bad.ok then
        log.warn("Expected failure — status: " .. bad.status .. ", error: " .. (bad.error or "?"))
    end
end
