import { env, SELF, createExecutionContext, waitOnExecutionContext } from "cloudflare:test";
import { afterEach, describe, expect, it, vi } from "vitest";
import worker, {
	csv,
	isAllowedOrigin,
	isAllowedTargetHost,
	isAllowedTargetUrl,
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

	it("matches exact target hosts without allowing their subdomains", () => {
		const config = { allowedTargetHosts: ["csdb.dk", "archive.org"] } as never;

		expect(isAllowedTargetHost("csdb.dk", config)).toBe(true);
		expect(isAllowedTargetHost("CSDB.DK", config)).toBe(true);
		// An exact entry stays exact. Adding a wildcard elsewhere must not widen this one.
		expect(isAllowedTargetHost("files.csdb.dk", config)).toBe(false);
	});

	it("matches subdomains only for wildcard entries", () => {
		const config = { allowedTargetHosts: ["archive.org", "*.archive.org"] } as never;

		// The per-request node hosts archive.org redirects downloads to.
		expect(isAllowedTargetHost("dn711007.ca.archive.org", config)).toBe(true);
		expect(isAllowedTargetHost("ia801234.us.archive.org", config)).toBe(true);
		expect(isAllowedTargetHost("archive.org", config)).toBe(true); // from the exact entry

		// The wildcard alone does not cover the bare domain, which is why both are listed.
		expect(isAllowedTargetHost("archive.org", { allowedTargetHosts: ["*.archive.org"] } as never)).toBe(false);
	});

	it("does not let a wildcard match a lookalike domain", () => {
		const config = { allowedTargetHosts: ["*.archive.org"] } as never;

		// The whole point of comparing against ".archive.org" rather than "archive.org":
		// anyone can register these.
		expect(isAllowedTargetHost("evilarchive.org", config)).toBe(false);
		expect(isAllowedTargetHost("notarchive.org", config)).toBe(false);
		// And a suffix match must not be fooled by the domain appearing earlier in the name.
		expect(isAllowedTargetHost("archive.org.evil.com", config)).toBe(false);
	});

	it("restricts path-prefixed targets to the configured repository", () => {
		const config = {
			allowedTargetHosts: [],
			allowedTargetPathPrefixes: [
				{
					hostname: "raw.githubusercontent.com",
					pathname: "/Oric-Software-Development-Kit/Oric-Software",
				},
				{
					hostname: "raw.githubusercontent.com",
					pathname: "/Abdess/retrobios",
				},
			],
		} as never;

		expect(
			isAllowedTargetUrl(
				new URL(
					"https://raw.githubusercontent.com/Oric-Software-Development-Kit/Oric-Software/master/users/chema/Oricium/RELEASE/Oricium12.tap",
				),
				config,
			),
		).toBe(true);
		expect(
			isAllowedTargetUrl(
				new URL("https://raw.githubusercontent.com/Oric-Software-Development-Kit/Oric-Software"),
				config,
			),
		).toBe(true);
		expect(
			isAllowedTargetUrl(
				new URL("https://raw.githubusercontent.com/Abdess/retrobios/main/bios/Oric/Oric/basic11b.rom"),
				config,
			),
		).toBe(true);
		expect(
			isAllowedTargetUrl(
				new URL("https://raw.githubusercontent.com/Abdess/retrobios-lookalike/main/bios/Oric/Oric/basic11b.rom"),
				config,
			),
		).toBe(false);
		expect(
			isAllowedTargetUrl(
				new URL("https://raw.githubusercontent.com/Oric-Software-Development-Kit/Other-Repo/file.tap"),
				config,
			),
		).toBe(false);
		expect(
			isAllowedTargetUrl(
				new URL("https://raw.githubusercontent.com/Oric-Software-Development-Kit/Oric-Software-Evil/file.tap"),
				config,
			),
		).toBe(false);
		expect(
			isAllowedTargetUrl(
				new URL(
					"https://raw.githubusercontent.com/Oric-Software-Development-Kit/Oric-Software%2f..%2fOther-Repo/file.tap",
				),
				config,
			),
		).toBe(false);
	});

	it("rejects wildcards too broad to be an allowlist", () => {
		const base = {
			ALLOWED_ORIGINS: "https://highbyte.se",
			PROXY_PATH: "/fetch",
		};

		for (const hosts of ["*", "*.com", "csdb.dk,*"]) {
			const result = validateConfig({ ...base, ALLOWED_TARGET_HOSTS: hosts } as never);
			expect(result.ok, `expected '${hosts}' to be rejected`).toBe(false);
		}

		expect(validateConfig({ ...base, ALLOWED_TARGET_HOSTS: "*.archive.org" } as never).ok).toBe(true);
	});

	it("validates required config values", () => {
		expect(validateConfig(env)).toMatchObject({
			ok: true,
			config: {
				proxyPath: "/fetch",
				allowedOrigins: ["https://highbyte.se"],
				allowedTargetHosts: [
					"www.zimmers.net",
					"csdb.dk",
					"compunet.live",
					"highbyte.se",
					"mirrors.apple2.org.za",
					"archive.org",
					"*.archive.org",
					"cdn.oric.org",
				],
				allowedTargetPathPrefixes: [
					{
						hostname: "raw.githubusercontent.com",
						pathname: "/Oric-Software-Development-Kit/Oric-Software",
					},
					{
						hostname: "raw.githubusercontent.com",
						pathname: "/Abdess/retrobios",
					},
				],
			},
		});

		expect(
			validateConfig({
				...env,
				ALLOWED_TARGET_HOSTS: "",
				ALLOWED_TARGET_PATH_PREFIXES: "",
			}),
		).toMatchObject({
			ok: false,
			error: "At least one target must be configured in ALLOWED_TARGET_HOSTS or ALLOWED_TARGET_PATH_PREFIXES.",
		});

		expect(
			validateConfig({
				...env,
				ALLOWED_TARGET_PATH_PREFIXES: "https://raw.githubusercontent.com/owner/repo",
			}),
		).toMatchObject({
			ok: false,
			error: expect.stringContaining("ALLOWED_TARGET_PATH_PREFIXES entry"),
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
			allowedTargetHosts: [
				"www.zimmers.net",
				"csdb.dk",
				"compunet.live",
				"highbyte.se",
				"mirrors.apple2.org.za",
				"archive.org",
				"*.archive.org",
				"cdn.oric.org",
			],
			allowedTargetPathPrefixes: [
				"raw.githubusercontent.com/Oric-Software-Development-Kit/Oric-Software",
				"raw.githubusercontent.com/Abdess/retrobios",
			],
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
		expect(await response.text()).toBe("Target not allowed");
	});

	it("allows requests within an allowed target path prefix", async () => {
		const fetchSpy = vi.spyOn(globalThis, "fetch").mockResolvedValue(new Response("tap-bytes", { status: 200 }));
		const request = new Request(
			"https://proxy.test/fetch?url=https%3A%2F%2Fraw.githubusercontent.com%2FOric-Software-Development-Kit%2FOric-Software%2Fmaster%2Fusers%2Fchema%2FOricium%2FRELEASE%2FOricium12.tap",
			{ headers: { Origin: "https://highbyte.se" } },
		);
		const ctx = createExecutionContext();
		const response = await worker.fetch(request, env, ctx);
		await waitOnExecutionContext(ctx);

		expect(response.status).toBe(200);
		expect(await response.text()).toBe("tap-bytes");
		expect(fetchSpy).toHaveBeenCalledTimes(1);
	});

	it("allows the configured Oric Atmos ROM repository", async () => {
		const fetchSpy = vi.spyOn(globalThis, "fetch").mockResolvedValue(new Response("rom-bytes", { status: 200 }));
		const request = new Request(
			"https://proxy.test/fetch?url=https%3A%2F%2Fraw.githubusercontent.com%2FAbdess%2Fretrobios%2Fmain%2Fbios%2FOric%2FOric%2Fbasic11b.rom",
			{ headers: { Origin: "https://highbyte.se" } },
		);
		const ctx = createExecutionContext();
		const response = await worker.fetch(request, env, ctx);
		await waitOnExecutionContext(ctx);

		expect(response.status).toBe(200);
		expect(await response.text()).toBe("rom-bytes");
		expect(fetchSpy).toHaveBeenCalledTimes(1);
	});

	it("rejects requests outside an allowed target path prefix", async () => {
		const request = new Request(
			"https://proxy.test/fetch?url=https%3A%2F%2Fraw.githubusercontent.com%2Fother-owner%2Fother-repo%2Fmain%2Ffile.tap",
			{ headers: { Origin: "https://highbyte.se" } },
		);
		const ctx = createExecutionContext();
		const response = await worker.fetch(request, env, ctx);
		await waitOnExecutionContext(ctx);
		expect(response.status).toBe(403);
		expect(await response.text()).toBe("Target not allowed");
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
		expect(await response.text()).toBe("Redirect target not allowed");
	});

	it("blocks redirects outside an allowed target path prefix", async () => {
		vi.spyOn(globalThis, "fetch").mockResolvedValue(
			new Response(null, {
				status: 302,
				headers: { Location: "https://raw.githubusercontent.com/other-owner/other-repo/main/file.tap" },
			}),
		);
		const request = new Request(
			"https://proxy.test/fetch?url=https%3A%2F%2Fraw.githubusercontent.com%2FOric-Software-Development-Kit%2FOric-Software%2Fmaster%2Fusers%2Fchema%2FOricium%2FRELEASE%2FOricium12.tap",
			{ headers: { Origin: "https://highbyte.se" } },
		);
		const ctx = createExecutionContext();
		const response = await worker.fetch(request, env, ctx);
		await waitOnExecutionContext(ctx);

		expect(response.status).toBe(403);
		expect(response.headers.get("Access-Control-Allow-Origin")).toBe("https://highbyte.se");
		expect(await response.text()).toBe("Redirect target not allowed");
	});

	it("follows a redirect to a wildcard-allowed subdomain", async () => {
		// archive.org 302s every download to a per-request node host, so the wildcard has to hold
		// on the redirect hop and not just on the first request.
		vi.spyOn(globalThis, "fetch")
			.mockResolvedValueOnce(
				new Response(null, {
					status: 302,
					headers: { Location: "https://dn711007.ca.archive.org/0/items/Visicalc_1.27/Visicalc_1.27.dsk" },
				}),
			)
			.mockResolvedValueOnce(new Response("disk-bytes", { status: 200 }));

		const request = new Request(
			"https://proxy.test/fetch?url=https%3A%2F%2Farchive.org%2Fdownload%2FVisicalc_1.27%2FVisicalc_1.27.dsk",
			{ headers: { Origin: "https://highbyte.se" } },
		);
		const ctx = createExecutionContext();
		const response = await worker.fetch(request, env, ctx);
		await waitOnExecutionContext(ctx);

		expect(response.status).toBe(200);
		expect(await response.text()).toBe("disk-bytes");
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
