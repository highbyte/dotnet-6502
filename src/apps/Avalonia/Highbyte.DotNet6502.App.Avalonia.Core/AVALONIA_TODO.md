Logging:
- Fix Avalonia logs. Currently not ending up in log bridge?

AvaloniaCommandTarget
- Make rendering via C64 Commands Provider work with AvaloniaCommandTarget. Correctly map C64 screen codes to the font used.

Code structure:
- Split Avalonia core render, config, etc function to separate library to be similar with other implementations. Keep UI focused stuff in Avalonia core.


