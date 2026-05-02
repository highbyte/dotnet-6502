# Configuration

Scripting is configured in `appsettings.json` under the `"Highbyte.DotNet6502.Scripting"` section:

```json
"Highbyte.DotNet6502.Scripting": {
    "Enabled": true,
    "ScriptDirectory": "scripts",
    "MaxExecutionWarningMs": 5,
    "MaxInstructionsPerResume": 1000000,
    "EnableScriptsAtStart": false,
    "AllowFileIO": true,
    "AllowFileWrite": false,
    "AllowHttpRequests": true,
    "AllowStore": true,
    "StoreSubDirectory": ".store",
    "AllowTcpClient": false
}
```

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `Enabled` | bool | `true` | Master switch for the scripting system. |
| `ScriptDirectory` | string | `""` | Directory to load `.lua` files from. Absolute path, or relative to the application working directory. |
| `MaxExecutionWarningMs` | int | `5` | Log a warning if a script hook takes longer than this many milliseconds. Set to `0` to disable. |
| `MaxInstructionsPerResume` | int | `1000000` | Maximum Lua VM instructions per coroutine resume. Protects against runaway scripts. Set to `0` to disable. |
| `EnableScriptsAtStart` | bool | `false` | Whether scripts start enabled when loaded. When `false`, scripts are loaded but must be enabled manually from the Scripts tab. |
| `AllowFileIO` | bool | `true` | Whether the `file` global and `emu.load()` are available to Lua scripts. Set to `false` in environments without filesystem access (e.g. WASM/browser). |
| `AllowFileWrite` | bool | `false` | Whether scripts may write, append, or delete files via the `file` global. Read operations are always permitted when `AllowFileIO` is `true`. |
| `FileBaseDirectory` | string | `null` | Base directory for all file I/O. When `null` or empty, defaults to `ScriptDirectory`. All script-supplied paths are resolved relative to this directory; traversal outside it (e.g. `../`) is blocked. |
| `AllowHttpRequests` | bool | `true` | Whether the `http` global is available to Lua scripts. When `true`, scripts may make outbound HTTP GET and POST requests to arbitrary URLs. Default is `true`. |
| `AllowStore` | bool | `true` | Whether the `store` global is available to Lua scripts. Provides a cross-platform key/value store. On desktop, backed by files in `StoreSubDirectory`. In browser, backed by `localStorage`. Default is `true`. |
| `StoreSubDirectory` | string | `".store"` | Subdirectory within `ScriptDirectory` used for the filesystem store backend (desktop only). Default is `".store"`. |
| `AllowTcpClient` | bool | `false` | Whether the `tcp` global is available to Lua scripts. Desktop only — forced `false` in browser/WASM builds. Default is `false`. |
