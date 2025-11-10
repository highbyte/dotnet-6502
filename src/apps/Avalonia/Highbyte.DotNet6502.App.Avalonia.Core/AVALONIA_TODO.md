Control C64 joystick with Controller
- Is there built-in support in Avalonia for both desktop and browser?

Audio:
- Implement via existing NAudio code in AvaDesktop app
- Maybe for future in Avalonia Browser: Investigate audio/synth alternatives that is compatible with WebAssembly. Preferably without having to write browser WebAudio JS API interop code.

Code structure:
- Split Avalonia core render, config, etc function to separate library to be similar with other implementations. Keep UI focused stuff in Avalonia core.

