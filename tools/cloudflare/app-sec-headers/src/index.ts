/**
 * Cross-origin isolation headers for the dotnet-6502 emulator browser apps.
 *
 * Routed on `highbyte.se/dotnet-6502/*` (a single worker so newly deployed
 * emulator apps are covered automatically). It only stamps headers on the
 * emulator *app* paths (`/dotnet-6502/app*`) and passes everything else
 * (e.g. the docs site at `/dotnet-6502/...`) through unchanged.
 *
 * The COOP/COEP pair makes the app page `crossOriginIsolated`, which is
 * required for `SharedArrayBuffer` and the low-latency AudioWorklet SID path.
 */

// Matches the emulator app deployments: app, app2, app2-test, future app3, ...
// Anchored so only `/dotnet-6502/app...` paths are isolated, not the docs site.
const APP_PATH_PATTERN = /^\/dotnet-6502\/app[^/]*(\/|$)/;

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
		return response;
	},
};
