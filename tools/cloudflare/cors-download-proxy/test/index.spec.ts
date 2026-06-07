import { env, SELF, createExecutionContext, waitOnExecutionContext } from "cloudflare:test";
import { afterEach, describe, expect, it, vi } from "vitest";
import worker, {
	csv,
	isAllowedOrigin,
	isAuthorized,
	normalizeProxyPath,
	parseTargetUrl,
	validateConfig,
} from "../src";

describe("cors download proxy worker", () => {
	afterEach(() => {
		vi.restoreAllMocks();
	});

	it("normalizes proxy paths", () => {
		expect(normalizeProxyPath(undefined)).toBe("/fetch");
		expect(normalizeProxyPath("fetch")).toBe("/fetch");
		expect(normalizeProxyPath("/downloads/")).toBe("/downloads");
	});

	it("parses CSV values to lowercase", () => {
		expect(csv(" A.example.com, B.example.com ,, ")).toEqual(["a.example.com", "b.example.com"]);
	});

	it("validates required config values", () => {
		expect(validateConfig(env)).toMatchObject({
			ok: true,
			config: {
				proxyPath: "/fetch",
				allowedOrigins: ["https://highbyte.se"],
				allowedTargetHosts: ["www.zimmers.net", "csdb.dk", "compunet.live", "highbyte.se"],
			},
		});

		expect(
			validateConfig({
				...env,
				ALLOWED_TARGET_HOSTS: "",
			}),
		).toMatchObject({
			ok: false,
			error: "ALLOWED_TARGET_HOSTS must contain at least one hostname.",
		});
	});

	it("supports explicit and localhost origins", () => {
		const configResult = validateConfig(env);
		expect(configResult.ok).toBe(true);
		if (!configResult.ok) {
			return;
		}

		expect(isAllowedOrigin("https://highbyte.se", configResult.config)).toBe(true);
		expect(isAllowedOrigin("http://localhost:5000", configResult.config)).toBe(true);
		expect(isAllowedOrigin("https://example.com", configResult.config)).toBe(false);
	});

	it("authorizes using querystring and bearer token", () => {
		expect(isAuthorized(new Request("https://proxy.test/fetch?token=secret"), "secret")).toBe(true);
		expect(
			isAuthorized(
				new Request("https://proxy.test/fetch", {
					headers: { Authorization: "Bearer secret" },
				}),
				"secret",
			),
		).toBe(true);
		expect(isAuthorized(new Request("https://proxy.test/fetch"), "secret")).toBe(false);
	});

	it("rejects invalid target URLs including userinfo and non-default ports", () => {
		expect(parseTargetUrl("https://www.zimmers.net/file.bin")?.hostname).toBe("www.zimmers.net");
		expect(parseTargetUrl("ftp://www.zimmers.net/file.bin")).toBeNull();
		expect(parseTargetUrl("https://user:pass@www.zimmers.net/file.bin")).toBeNull();
		expect(parseTargetUrl("https://www.zimmers.net:444/file.bin")).toBeNull();
	});

	it("serves the local probe page", async () => {
		const response = await SELF.fetch("http://example.com/");
		expect(response.status).toBe(200);
		expect(response.headers.get("content-type")).toContain("text/html");
		expect(await response.text()).toContain("Browser Download CORS Proxy");
	});

	it("exposes a health document", async () => {
		const response = await SELF.fetch("http://example.com/healthz");
		expect(response.status).toBe(200);
		expect(await response.json()).toMatchObject({
			ok: true,
			proxyPath: "/fetch",
			allowedTargetHosts: ["www.zimmers.net", "csdb.dk", "compunet.live", "highbyte.se"],
			rateLimits: {
				burst: { limit: 8, periodSeconds: 10 },
				sustained: { limit: 20, periodSeconds: 60 },
			},
		});
	});

	it("rejects requests from disallowed origins", async () => {
		const request = new Request("https://proxy.test/fetch?url=https%3A%2F%2Fwww.zimmers.net%2Ffile.bin", {
			headers: { Origin: "https://evil.example" },
		});
		const ctx = createExecutionContext();
		const response = await worker.fetch(request, env, ctx);
		await waitOnExecutionContext(ctx);
		expect(response.status).toBe(403);
		expect(await response.text()).toBe("Origin not allowed");
	});

	it("rejects requests to disallowed target hosts", async () => {
		const request = new Request("https://proxy.test/fetch?url=https%3A%2F%2Fevil.example%2Ffile.bin", {
			headers: { Origin: "https://highbyte.se" },
		});
		const ctx = createExecutionContext();
		const response = await worker.fetch(request, env, ctx);
		await waitOnExecutionContext(ctx);
		expect(response.status).toBe(403);
		expect(await response.text()).toBe("Target host not allowed");
	});

	it("enforces optional shared-token auth", async () => {
		const request = new Request("https://proxy.test/fetch?url=https%3A%2F%2Fwww.zimmers.net%2Ffile.bin", {
			headers: { Origin: "https://highbyte.se" },
		});
		const ctx = createExecutionContext();
		const response = await worker.fetch(
			request,
			{
				...env,
				SHARED_TOKEN: "secret",
			},
			ctx,
		);
		await waitOnExecutionContext(ctx);
		expect(response.status).toBe(401);
		expect(await response.text()).toBe("Unauthorized");
	});

	it("returns 429 when the burst limiter blocks the request", async () => {
		const request = new Request("https://proxy.test/fetch?url=https%3A%2F%2Fwww.zimmers.net%2Ffile.bin", {
			headers: { Origin: "https://highbyte.se", "CF-Connecting-IP": "203.0.113.10" },
		});
		const ctx = createExecutionContext();
		const response = await worker.fetch(
			request,
			{
				...env,
				BURST_LIMITER: {
					limit: vi.fn(async () => ({ success: false })),
				},
				SUSTAINED_LIMITER: {
					limit: vi.fn(async () => ({ success: true })),
				},
			},
			ctx,
		);
		await waitOnExecutionContext(ctx);
		expect(response.status).toBe(429);
		expect(response.headers.get("Retry-After")).toBe("10");
		expect(await response.text()).toBe("Too Many Requests");
	});

	it("proxies successful upstream responses with CORS headers and cache hints", async () => {
		const fetchSpy = vi.spyOn(globalThis, "fetch").mockResolvedValue(
			new Response("binary-data", {
				status: 200,
				headers: {
					"Content-Type": "application/octet-stream",
					"Content-Length": "11",
				},
			}),
		);
		const request = new Request("https://proxy.test/fetch?url=https%3A%2F%2Fwww.zimmers.net%2Ffile.bin", {
			headers: { Origin: "https://highbyte.se", Accept: "application/octet-stream" },
		});
		const ctx = createExecutionContext();
		const response = await worker.fetch(request, env, ctx);
		await waitOnExecutionContext(ctx);

		expect(response.status).toBe(200);
		expect(response.headers.get("Access-Control-Allow-Origin")).toBe("https://highbyte.se");
		expect(new TextDecoder().decode(await response.arrayBuffer())).toBe("binary-data");
		expect(fetchSpy).toHaveBeenCalledTimes(1);
		expect(fetchSpy.mock.calls[0]?.[1]).toMatchObject({
			cf: {
				cacheEverything: true,
				cacheTtlByStatus: {
					"200-299": 86400,
					"300-399": 60,
					"404": 60,
					"500-599": 0,
				},
			},
		});
	});

	it("blocks redirects to disallowed hosts", async () => {
		vi.spyOn(globalThis, "fetch").mockResolvedValue(
			new Response(null, {
				status: 302,
				headers: { Location: "https://evil.example/file.bin" },
			}),
		);
		const request = new Request("https://proxy.test/fetch?url=https%3A%2F%2Fcsdb.dk%2Frelease%2Fdownload.php%3Fid%3D1", {
			headers: { Origin: "https://highbyte.se" },
		});
		const ctx = createExecutionContext();
		const response = await worker.fetch(request, env, ctx);
		await waitOnExecutionContext(ctx);

		expect(response.status).toBe(403);
		expect(response.headers.get("Access-Control-Allow-Origin")).toBe("https://highbyte.se");
		expect(await response.text()).toBe("Redirect target host not allowed");
	});

	it("rejects responses that exceed the configured size limit", async () => {
		vi.spyOn(globalThis, "fetch").mockResolvedValue(
			new Response("too-big", {
				status: 200,
				headers: {
					"Content-Length": "7",
					"Content-Type": "application/octet-stream",
				},
			}),
		);
		const request = new Request("https://proxy.test/fetch?url=https%3A%2F%2Fwww.zimmers.net%2Ffile.bin", {
			headers: { Origin: "https://highbyte.se" },
		});
		const ctx = createExecutionContext();
		const response = await worker.fetch(
			request,
			{
				...env,
				MAX_RESPONSE_BYTES: "4",
			},
			ctx,
		);
		await waitOnExecutionContext(ctx);

		expect(response.status).toBe(413);
		expect(await response.text()).toBe("Response too large");
	});
});
