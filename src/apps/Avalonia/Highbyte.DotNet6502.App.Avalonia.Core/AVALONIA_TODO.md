Stats panel (Browser):
- Change to it appears directly on the right side of emulator view (column 1).

Avalonia browser app:
- Detect old version loaded from browser cache and let user refresh via button
- May also be implemented in the Blazor WASM app.

Audio:
- Implement via existing NAudio code in AvaDesktop app
- Maybe for future in Avalonia Browser: Investigate audio/synth alternatives that is compatible with WebAssembly. Preferably without having to write browser WebAudio JS API interop code.

Code structure:
- Split Avalonia core render, config, etc function to separate library to be similar with other implementations. Keep UI focused stuff in Avalonia core.

