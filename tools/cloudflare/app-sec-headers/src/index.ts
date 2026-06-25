/**
 * Cross-origin isolation + cache-control headers for the dotnet-6502 emulator
 * browser apps.
 *
 * Routed on `highbyte.se/dotnet-6502/*` (a single worker so newly deployed
 * emulator apps are covered automatically). It only stamps headers on the
 * emulator *app* paths (`/dotnet-6502/app*`) and passes everything else
 * (e.g. the docs site at `/dotnet-6502/...`) through unchanged.
 *
 * Two header concerns are handled:
 *
 * 1. COOP/COEP — makes the app page `crossOriginIsolated`, required for
 *    `SharedArrayBuffer` and the low-latency AudioWorklet SID path.
 *
 * 2. Cache-Control — forces revalidation of the few *stable-named* entry files
 *    that gate which app version loads. The .NET WebAssembly publish
 *    fingerprints every assembly (e.g. `Foo.ab12cd34.wasm`), so those are safe
 *    to cache long-term — a new version changes their filenames. But the loader
 *    chain that *selects* those fingerprints keeps stable names
 *    (`index.html` -> `main.js` -> `_framework/dotnet.js` ->
 *    `_framework/blazor.boot.json`). GitHub Pages serves them with
 *    `max-age=600..14400`, so within that window a normal reload serves the old
 *    boot manifest from the browser cache -> old fingerprints -> old version,
 *    even after the in-app "Update" button. Marking these `no-cache` makes the
 *    browser revalidate them on every load (304 when unchanged), so a new
 *    deploy is picked up immediately.
 */

// Matches the emulator app deployments: app, app2, app2-test, future app3, ...
// Anchored so only `/dotnet-6502/app...` paths are isolated, not the docs site.
const APP_PATH_PATTERN = /^\/dotnet-6502\/app[^/]*(\/|$)/;

// Stable-named loader/entry files within an app path. Everything else under the
// app path (the fingerprinted `*.wasm`/`*.dll` assemblies, images, etc.) keeps
// its upstream long-lived cache headers.
const NO_CACHE_PATTERN =
	/(\/|\/index\.html|\/main\.js|\/_framework\/dotnet\.js|\/_framework\/dotnet\.boot\.js|\/_framework\/blazor\.boot\.json)$/;

export default {
	async fetch(request: Request): Promise<Response> {
		const upstream = await fetch(request);

		const path = new URL(request.url).pathname;
		if (!APP_PATH_PATTERN.test(path)) {
			// Not an emulator app path (docs, etc.) — leave untouched.
			return upstream;
		}

		// Re-construct the Response to make the headers mutable.
		const response = new Response(upstream.body, upstream);
		response.headers.set("Cross-Origin-Opener-Policy", "same-origin");
		response.headers.set("Cross-Origin-Embedder-Policy", "require-corp");

		if (NO_CACHE_PATTERN.test(path)) {
			// Must revalidate on every load so a new deploy is seen immediately.
			response.headers.set("Cache-Control", "no-cache");
		}

		return response;
	},
};
