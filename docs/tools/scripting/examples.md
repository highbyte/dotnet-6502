# Examples

Example scripts are included in the `scripts/` directory of the source repository.

| Script | Style | Description |
|--------|-------|-------------|
| `example_frameadvance.lua` | Linear loop | Logs CPU state every 60 frames and detects changes to the A register. |
| `example_monitor.lua` | Event hooks | Defines `on_before_frame` and `on_after_frame` hooks to log CPU state and watch the C64 raster line register. |
| `example_emulator_control.lua` | Linear loop + hooks | Demonstrates the emulator control API: queries state, pauses at frame 300, resumes after 3 seconds, and defines all state-change event hooks. |
| `example_border_cycle.lua` | Linear loop | Waits for the C64 system to be running, waits 3 seconds, then cycles the border color through all 16 C64 colors. |
| `example_file_io.lua` | Linear loop | Demonstrates the file I/O API: lists scripts, reads a text file, writes a CSV log, and shows `emu.load()` and `file.read_bytes()` usage. Requires `AllowFileIO: true`; write operations also require `AllowFileWrite: true`. |
| `example_http.lua` | Event hook | Demonstrates the HTTP API in `on_started()`: GET with and without custom headers, `post_json`, `post` with explicit content type, `get_bytes`, `download`, and error handling for unreachable hosts. Requires `AllowHttpRequests: true`. |
| `example_store.lua` | Linear loop + hooks | Demonstrates the key/value store API: persistent run counter, first-run flag, overwrite/verify, listing all keys, saving a CPU snapshot on `on_started`, and writing a frame checkpoint every 60 frames. Requires `AllowStore: true`. |
| `example_tcp_client.lua` | Linear loop | Demonstrates the TCP client API with a per-frame observation/action loop mimicking a Machine Learning / Reinforcement Learning server protocol (length-prefixed binary). Connects to a local TCP server, sends CPU state as an observation each frame, and applies the first byte of the server's response to the C64 border color register. Requires `AllowTcpClient: true`. Desktop only. |
| `example_input_kb.lua` | Event hook | Demonstrates keyboard input injection: presses A, B, C in sequence using `emu.time()` for timing (0.5s hold per key, 0.3s pause between keys). |
| `example_input_joystick.lua` | Event hook | Demonstrates joystick input injection: repeats a timed sequence (left → pause → right → pause → fire → pause) three times on port 1. |
| `example_quit.lua` | Event hook | Demonstrates an automation pipeline: starts the emulator, polls a memory address each frame for a program result, saves to file, then calls `emu.quit()` to exit. Includes a timeout fallback. |

For per-API examples (HTTP, store, TCP, input), see the corresponding sections in [Lua API](lua-api.md).
