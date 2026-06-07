type RateLimiter = {
	limit(options: { key: string }): Promise<{ success: boolean }>;
};

type ProxyConfig = {
	proxyPath: string;
	allowedOrigins: string[];
	allowLocalhostOrigins: boolean;
	allowedTargetHosts: string[];
	maxRedirects: number;
	maxResponseBytes: number;
	sharedToken: string | null;
};

type ConfigValidationResult =
	| { ok: true; config: ProxyConfig }
	| { ok: false; error: string; proxyPath: string };

type RateLimitResult =
	| { ok: true }
	| { ok: false; retryAfterSeconds: number };

const BURST_WINDOW_SECONDS = 10;
const SUSTAINED_WINDOW_SECONDS = 60;
const CACHE_TTL_BY_STATUS = {
	"200-299": 86400,
	"300-399": 60,
	"404": 60,
	"500-599": 0,
};

export function normalizeProxyPath(value: string | undefined): string {
	if (!value) {
		return "/fetch";
	}

	const trimmed = value.trim();
	if (trimmed.length === 0 || trimmed === "/") {
		return "/fetch";
	}

	const withLeadingSlash = trimmed.startsWith("/") ? trimmed : `/${trimmed}`;
	if (withLeadingSlash.length <= 1) {
		return withLeadingSlash;
	}

	let endIndex = withLeadingSlash.length;
	while (endIndex > 1 && withLeadingSlash[endIndex - 1] === "/") {
		endIndex--;
	}

	return withLeadingSlash.slice(0, endIndex);
}

export function csv(value: string | undefined): string[] {
	if (!value) {
		return [];
	}

	return value
		.split(",")
		.map((entry) => entry.trim().toLowerCase())
		.filter(Boolean);
}

function parsePositiveInteger(value: string | undefined, fallback: number): number {
	if (!value) {
		return fallback;
	}

	const parsed = Number.parseInt(value.trim(), 10);
	if (!Number.isInteger(parsed) || parsed < 1) {
		return fallback;
	}

	return parsed;
}

function isTruthyFlag(value: string | undefined): boolean {
	if (!value) {
		return false;
	}

	switch (value.trim().toLowerCase()) {
		case "1":
		case "true":
		case "yes":
		case "on":
			return true;
		default:
			return false;
	}
}

export function validateConfig(env: Env): ConfigValidationResult {
	const envRecord = env as unknown as Record<string, unknown>;
	const proxyPath = normalizeProxyPath(env.PROXY_PATH);
	const allowedOrigins = csv(typeof envRecord.ALLOWED_ORIGINS === "string" ? envRecord.ALLOWED_ORIGINS : "");
	const allowedTargetHosts = csv(
		typeof envRecord.ALLOWED_TARGET_HOSTS === "string" ? envRecord.ALLOWED_TARGET_HOSTS : "",
	);
	const sharedToken = typeof envRecord.SHARED_TOKEN === "string" ? envRecord.SHARED_TOKEN.trim() : "";
	const maxRedirects = parsePositiveInteger(
		typeof envRecord.MAX_REDIRECTS === "string" ? envRecord.MAX_REDIRECTS : undefined,
		5,
	);
	const maxResponseBytes = parsePositiveInteger(
		typeof envRecord.MAX_RESPONSE_BYTES === "string" ? envRecord.MAX_RESPONSE_BYTES : undefined,
		32 * 1024 * 1024,
	);

	if (allowedOrigins.length === 0) {
		return { ok: false, error: "ALLOWED_ORIGINS must contain at least one origin.", proxyPath };
	}

	if (allowedTargetHosts.length === 0) {
		return { ok: false, error: "ALLOWED_TARGET_HOSTS must contain at least one hostname.", proxyPath };
	}

	return {
		ok: true,
		config: {
			proxyPath,
			allowedOrigins,
			allowLocalhostOrigins: isTruthyFlag(
				typeof envRecord.ALLOW_LOCALHOST_ORIGINS === "string" ? envRecord.ALLOW_LOCALHOST_ORIGINS : undefined,
			),
			allowedTargetHosts,
			maxRedirects,
			maxResponseBytes,
			sharedToken: sharedToken || null,
		},
	};
}

export function isAllowedOrigin(origin: string, config: ProxyConfig): boolean {
	if (!origin) {
		return false;
	}

	if (config.allowedOrigins.includes(origin.toLowerCase())) {
		return true;
	}

	return config.allowLocalhostOrigins && /^https?:\/\/(localhost|127\.0\.0\.1)(:\d+)?$/i.test(origin);
}

export function isAllowedTargetHost(hostname: string, config: ProxyConfig): boolean {
	return config.allowedTargetHosts.includes(hostname.toLowerCase());
}

export function isAuthorized(request: Request, sharedToken: string | null): boolean {
	if (!sharedToken) {
		return true;
	}

	const url = new URL(request.url);
	const queryToken = url.searchParams.get("token");
	if (queryToken === sharedToken) {
		return true;
	}

	const authorization = request.headers.get("Authorization");
	if (!authorization?.startsWith("Bearer ")) {
		return false;
	}

	return authorization.slice("Bearer ".length) === sharedToken;
}

export function parseTargetUrl(value: string): URL | null {
	try {
		const url = new URL(value);
		if (url.protocol !== "http:" && url.protocol !== "https:") {
			return null;
		}
		if (url.username || url.password) {
			return null;
		}
		if (url.port && url.port !== "80" && url.port !== "443") {
			return null;
		}
		return url;
	} catch {
		return null;
	}
}

function getClientRateLimitKey(request: Request, targetHost: string): string {
	const ip = request.headers.get("CF-Connecting-IP")?.trim() || "anonymous";
	return `${ip}:${targetHost.toLowerCase()}`;
}

async function applyRateLimits(request: Request, env: Env, targetHost: string): Promise<RateLimitResult> {
	const key = getClientRateLimitKey(request, targetHost);

	if (env.BURST_LIMITER) {
		const burstResult = await env.BURST_LIMITER.limit({ key });
		if (!burstResult.success) {
			return { ok: false, retryAfterSeconds: BURST_WINDOW_SECONDS };
		}
	}

	if (env.SUSTAINED_LIMITER) {
		const sustainedResult = await env.SUSTAINED_LIMITER.limit({ key });
		if (!sustainedResult.success) {
			return { ok: false, retryAfterSeconds: SUSTAINED_WINDOW_SECONDS };
		}
	}

	return { ok: true };
}

function isRedirect(status: number): boolean {
	return status >= 300 && status < 400;
}

function corsHeaders(origin: string): Record<string, string> {
	return {
		"Access-Control-Allow-Origin": origin,
		"Access-Control-Allow-Methods": "GET, HEAD, OPTIONS",
		"Access-Control-Allow-Headers": "Authorization, Content-Type",
		"Access-Control-Expose-Headers": "Content-Length, Content-Type, Content-Disposition, ETag, Last-Modified",
	};
}

function appendVary(response: Response, value: string): void {
	const current = response.headers.get("Vary");
	if (!current) {
		response.headers.set("Vary", value);
		return;
	}

	const existingValues = current
		.split(",")
		.map((entry) => entry.trim().toLowerCase())
		.filter(Boolean);
	if (!existingValues.includes(value.toLowerCase())) {
		response.headers.set("Vary", `${current}, ${value}`);
	}
}

function jsonResponse(data: unknown, status = 200): Response {
	return new Response(JSON.stringify(data, null, 2), {
		status,
		headers: {
			"content-type": "application/json; charset=utf-8",
			"cache-control": "no-store",
		},
	});
}

function createHomeResponse(requestUrl: URL, configResult: ConfigValidationResult): Response {
	const proxyPath = configResult.ok ? configResult.config.proxyPath : configResult.proxyPath;
	const exampleTarget =
		"https://www.zimmers.net/anonftp/pub/cbm/firmware/computers/c64/kernal.901227-03.bin";
	const exampleProxyUrl = new URL(proxyPath, requestUrl);
	exampleProxyUrl.searchParams.set("url", exampleTarget);

	const html = `<!doctype html>
<html lang="en">
  <head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1" />
    <title>Browser Download CORS Proxy</title>
    <style>
      :root {
        color-scheme: dark;
        --bg: #0f1420;
        --panel: #171f31;
        --edge: #2c3954;
        --text: #e8eefc;
        --muted: #9fb0d3;
        --accent: #81d4ff;
      }
      * { box-sizing: border-box; }
      body {
        margin: 0;
        min-height: 100vh;
        font-family: "IBM Plex Sans", "Segoe UI", sans-serif;
        background:
          radial-gradient(circle at top, rgba(129, 212, 255, 0.12), transparent 30%),
          linear-gradient(180deg, #0a0f18 0%, var(--bg) 100%);
        color: var(--text);
        display: grid;
        place-items: center;
        padding: 24px;
      }
      main {
        width: min(760px, 100%);
        background: rgba(23, 31, 49, 0.9);
        border: 1px solid var(--edge);
        border-radius: 16px;
        padding: 24px;
        box-shadow: 0 18px 60px rgba(0, 0, 0, 0.35);
      }
      h1 { margin-top: 0; }
      p, li { color: var(--muted); line-height: 1.5; }
      code, pre {
        font-family: "IBM Plex Mono", "Cascadia Code", monospace;
      }
      pre {
        margin: 0;
        padding: 14px;
        overflow: auto;
        background: rgba(7, 11, 18, 0.9);
        border-radius: 12px;
        border: 1px solid rgba(129, 212, 255, 0.15);
      }
      a { color: var(--accent); }
    </style>
  </head>
  <body>
    <main>
      <h1>Browser Download CORS Proxy</h1>
      <p>Dedicated Cloudflare Worker for browser-hosted dotnet-6502 downloads.</p>
      <ul>
        <li>Allowed methods: <code>GET</code>, <code>HEAD</code>, <code>OPTIONS</code></li>
        <li>Path: <code>${proxyPath}</code></li>
        <li>Health: <a href="/healthz">/healthz</a></li>
      </ul>
      <pre>${exampleProxyUrl.toString()}</pre>
    </main>
  </body>
</html>`;

	return new Response(html, {
		status: 200,
		headers: {
			"content-type": "text/html; charset=utf-8",
			"cache-control": "no-store",
		},
	});
}

function buildRequestToUpstream(target: URL, method: string, acceptHeader: string | null): Request {
	return new Request(target.toString(), {
		method,
		redirect: "manual",
		headers: acceptHeader ? { Accept: acceptHeader } : undefined,
	});
}

async function fetchUpstream(
	request: Request,
	target: URL,
	config: ProxyConfig,
): Promise<Response> {
	let currentTarget = target;
	let upstream: Response | null = null;

	for (let i = 0; i <= config.maxRedirects; i++) {
		upstream = await fetch(buildRequestToUpstream(currentTarget, request.method, request.headers.get("Accept")), {
			cf: request.method === "GET" || request.method === "HEAD"
				? {
					cacheEverything: true,
					cacheTtlByStatus: CACHE_TTL_BY_STATUS,
				}
				: undefined,
		});

		if (!isRedirect(upstream.status)) {
			break;
		}

		const location = upstream.headers.get("Location");
		if (!location) {
			break;
		}

		const nextTarget = new URL(location, currentTarget);
		if (!isAllowedTargetHost(nextTarget.hostname, config)) {
			return new Response("Redirect target host not allowed", { status: 403 });
		}

		currentTarget = nextTarget;
	}

	if (!upstream) {
		return new Response("Proxy fetch failed", { status: 502 });
	}

	return upstream;
}

function enforceResponseSize(upstream: Response, maxResponseBytes: number): Response {
	const contentLength = upstream.headers.get("Content-Length");
	if (contentLength) {
		const parsed = Number.parseInt(contentLength, 10);
		if (Number.isInteger(parsed) && parsed > maxResponseBytes) {
			return new Response("Response too large", { status: 413 });
		}
	}

	if (!upstream.body) {
		return upstream;
	}

	let bytesRead = 0;
	const reader = upstream.body.getReader();
	const body = new ReadableStream<Uint8Array>({
		async pull(controller) {
			const result = await reader.read();
			if (result.done) {
				controller.close();
				return;
			}

			bytesRead += result.value.byteLength;
			if (bytesRead > maxResponseBytes) {
				controller.error(new Error("Response too large"));
				await reader.cancel("Response too large");
				return;
			}

			controller.enqueue(result.value);
		},
		async cancel(reason) {
			await reader.cancel(reason);
		},
	});

	return new Response(body, upstream);
}

function applyCorsToResponse(upstream: Response, origin: string): Response {
	const response = new Response(upstream.body, upstream);
	for (const [key, value] of Object.entries(corsHeaders(origin))) {
		response.headers.set(key, value);
	}
	appendVary(response, "Origin");
	response.headers.set("X-Robots-Tag", "noindex, nofollow");
	return response;
}

function createHealthResponse(configResult: ConfigValidationResult): Response {
	if (!configResult.ok) {
		return jsonResponse(
			{
				ok: false,
				proxyPath: configResult.proxyPath,
				error: configResult.error,
			},
			500,
		);
	}

	return jsonResponse({
		ok: true,
		proxyPath: configResult.config.proxyPath,
		allowedOrigins: configResult.config.allowedOrigins,
		allowLocalhostOrigins: configResult.config.allowLocalhostOrigins,
		allowedTargetHosts: configResult.config.allowedTargetHosts,
		maxRedirects: configResult.config.maxRedirects,
		maxResponseBytes: configResult.config.maxResponseBytes,
		authRequired: Boolean(configResult.config.sharedToken),
		rateLimits: {
			burst: { limit: 8, periodSeconds: BURST_WINDOW_SECONDS },
			sustained: { limit: 20, periodSeconds: SUSTAINED_WINDOW_SECONDS },
		},
	});
}

export default {
	async fetch(request, env): Promise<Response> {
		const configResult = validateConfig(env);
		const requestUrl = new URL(request.url);

		if (requestUrl.pathname === "/") {
			return createHomeResponse(requestUrl, configResult);
		}

		if (requestUrl.pathname === "/healthz") {
			return createHealthResponse(configResult);
		}

		if (!configResult.ok) {
			return new Response(configResult.error, { status: 500 });
		}

		const config = configResult.config;
		if (requestUrl.pathname !== config.proxyPath) {
			return new Response("Not found", { status: 404 });
		}

		const origin = request.headers.get("Origin") ?? "";
		if (!isAllowedOrigin(origin, config)) {
			return new Response("Origin not allowed", { status: 403 });
		}

		if (request.method === "OPTIONS") {
			return new Response(null, {
				status: 204,
				headers: corsHeaders(origin),
			});
		}

		if (request.method !== "GET" && request.method !== "HEAD") {
			return new Response("Method not allowed", {
				status: 405,
				headers: corsHeaders(origin),
			});
		}

		if (!isAuthorized(request, config.sharedToken)) {
			return new Response("Unauthorized", {
				status: 401,
				headers: corsHeaders(origin),
			});
		}

		const rawTarget = requestUrl.searchParams.get("url");
		if (!rawTarget) {
			return new Response("Missing url parameter", {
				status: 400,
				headers: corsHeaders(origin),
			});
		}

		const target = parseTargetUrl(rawTarget);
		if (!target) {
			return new Response("Invalid target URL", {
				status: 400,
				headers: corsHeaders(origin),
			});
		}

		if (!isAllowedTargetHost(target.hostname, config)) {
			return new Response("Target host not allowed", {
				status: 403,
				headers: corsHeaders(origin),
			});
		}

		const rateLimitResult = await applyRateLimits(request, env, target.hostname);
		if (!rateLimitResult.ok) {
			const response = new Response("Too Many Requests", {
				status: 429,
				headers: corsHeaders(origin),
			});
			response.headers.set("Retry-After", String(rateLimitResult.retryAfterSeconds));
			return response;
		}

		const upstream = await fetchUpstream(request, target, config);
		if (upstream.status === 403 && (await upstream.clone().text()) === "Redirect target host not allowed") {
			return applyCorsToResponse(upstream, origin);
		}

		const boundedResponse = enforceResponseSize(upstream, config.maxResponseBytes);
		return applyCorsToResponse(boundedResponse, origin);
	},
} satisfies ExportedHandler<Env>;
