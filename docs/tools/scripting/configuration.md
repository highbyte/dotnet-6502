# Configuration

On desktop targets, scripting is configured under the `"Highbyte.DotNet6502.Scripting"` section. The shipped `appsettings.json` contains packaged defaults; user changes are stored in the host-specific `appsettings.user.json` overlay under the OS local application data folder, not beside the shipped executable. On the Avalonia Browser app, the same section is persisted in browser `localStorage` by the settings UI.

When `ScriptDirectory` is empty, desktop hosts use the shared user script directory:

- macOS/Linux: `~/Documents/Highbyte/DotNet6502/scripts`
- Windows: `%USERPROFILE%\Documents\Highbyte\DotNet6502\scripts`

```json
"Highbyte.DotNet6502.Scripting": {
    "Enabled": true,
    "ScriptDirectory": "",
    "MaxExecutionWarningMs": 5,
    "MaxInstructionsPerResume": 1000000,
    "EnableScriptsAtStart": false,
    "AllowFileIO": true,
    "AllowFileWrite": false,
    "AllowHttpRequests": true,
    "AllowStore": true,
    "StoreSubDirectory": ".store",
    "AllowTcpClient": false,
    "AllowUrlScripts": false
}
```

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `Enabled` | bool | `true` | Master switch for the scripting system. |
| `ScriptDirectory` | string | `""` | Optional directory override to load `.lua` files from. When empty, desktop hosts use `~/Documents/Highbyte/DotNet6502/scripts` (or the Windows equivalent). Relative paths are resolved from the application working directory. |
| `MaxExecutionWarningMs` | int | `5` | Log a warning if a script hook takes longer than this many milliseconds. Set to `0` to disable. |
| `MaxInstructionsPerResume` | int | `1000000` | Maximum Lua VM instructions per coroutine resume. Protects against runaway scripts. Set to `0` to disable. |
| `EnableScriptsAtStart` | bool | `false` | Whether scripts start enabled when loaded. When `false`, scripts are loaded but must be enabled manually from the Scripts tab. |
| `AllowFileIO` | bool | `true` | Whether the `file` global and `emu.load()` are available to Lua scripts. Set to `false` in environments without filesystem access (e.g. WASM/browser). |
| `AllowFileWrite` | bool | `false` | Whether scripts may write, append, or delete files via the `file` global. Read operations are always permitted when `AllowFileIO` is `true`. |
| `FileBaseDirectory` | string | `null` | Base directory for all file I/O. When `null` or empty, defaults to the effective script directory. All script-supplied paths are resolved relative to this directory; traversal outside it (e.g. `../`) is blocked. |
| `AllowHttpRequests` | bool | `true` | Whether the `http` global is available to Lua scripts. When `true`, scripts may make outbound HTTP GET and POST requests to arbitrary URLs. Default is `true`. |
| `AllowStore` | bool | `true` | Whether the `store` global is available to Lua scripts. Provides a cross-platform key/value store. On desktop, backed by files in `StoreSubDirectory`. In browser, backed by `localStorage`. Default is `true`. |
| `StoreSubDirectory` | string | `".store"` | Subdirectory within the effective script directory used for the filesystem store backend (desktop only). Default is `".store"`. |
| `AllowTcpClient` | bool | `false` | Whether the `tcp` global is available to Lua scripts. Desktop only — forced `false` in browser/WASM builds. Default is `false`. |
| `AllowUrlScripts` | bool | `false` | Browser-only. When `true`, the Avalonia Browser app honours the `script` and `scriptUrl` URL query parameters at startup. Disabled by default because a crafted link could otherwise execute Lua against the user's emulator session and `localStorage`. Takes effect on the next page load. |
