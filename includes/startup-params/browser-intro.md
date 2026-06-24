The Avalonia Browser app supports URL-driven startup automation. This is the browser counterpart to the Avalonia Desktop app's [CLI arguments](../../host-apps/avalonia/desktop.md#cli-arguments): instead of passing `--system` or `--script`, you encode the request in the page URL query string. The parameters and groups below mirror the desktop reference one-to-one where the platforms overlap.

Parameters are grouped below into **General parameters** (system-agnostic — valid for any system) and one group per **system** (interpreted by that system's plugin; currently only **C64**).

The **Depends on** column lists each parameter's requirements and any parameters it is mutually exclusive with. The **desktop equivalent** is noted per group: the main difference is that the browser fetches its load sources over HTTP (`loadPrgUrl` / `loadD64Url` / `loadCrtUrl` / `basicUrl` / `scriptUrl`), whereas the desktop app reads from the local filesystem (`--loadPrg` / `--loadD64` / `--loadCrt` / `--basicFile` / `--script`) and additionally offers URL variants (`--loadPrgUrl` / `--loadD64Url` / `--loadCrtUrl`).

Query parameter names are case-insensitive. Boolean flags treat an empty value, `1`, `true`, and `yes` as true.

Validation rules are intentionally forgiving: invalid combinations are ignored (logged to the F12 DevTools console) and the normal UI still loads.
