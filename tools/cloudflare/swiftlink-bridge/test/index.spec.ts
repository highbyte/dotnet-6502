import {
	env,
	createExecutionContext,
	waitOnExecutionContext,
	SELF,
} from "cloudflare:test";
import { describe, it, expect } from "vitest";
import worker from "../src";
import { isAuthorized, normalizeBridgePath, validateConfig } from "../src";

describe("swiftlink bridge worker", () => {
	it("normalizes bridge paths", () => {
		expect(normalizeBridgePath(undefined)).toBe("/bridge");
		expect(normalizeBridgePath("bridge")).toBe("/bridge");
		expect(normalizeBridgePath("/compuserve/")).toBe("/compuserve");
	});

	it("validates the configured target port", () => {
		expect(
			validateConfig({
				...env,
				TARGET_HOST: "127.0.0.1",
				TARGET_PORT: "9001",
				BRIDGE_PATH: "/bridge",
				TARGET_TLS: "false",
				SHARED_TOKEN: "",
			}),
		).toMatchObject({
			ok: true,
			config: {
				targetHost: "127.0.0.1",
				targetPort: 9001,
				bridgePath: "/bridge",
				targetTls: false,
			},
		});

		expect(
			validateConfig({
				...env,
				TARGET_HOST: "127.0.0.1",
				TARGET_PORT: "0",
				BRIDGE_PATH: "/bridge",
				TARGET_TLS: "false",
				SHARED_TOKEN: "",
			}),
		).toMatchObject({
			ok: false,
			error: "TARGET_PORT must be an integer between 1 and 65535.",
		});
	});

	it("authorizes using querystring and bearer token", () => {
		expect(isAuthorized(new Request("https://bridge.test/bridge?token=secret"), "secret")).toBe(true);
		expect(
			isAuthorized(
				new Request("https://bridge.test/bridge", {
					headers: { Authorization: "Bearer secret" },
				}),
				"secret",
			),
		).toBe(true);
		expect(isAuthorized(new Request("https://bridge.test/bridge"), "secret")).toBe(false);
	});

	it("serves the local probe page", async () => {
		const response = await SELF.fetch("http://example.com/");
		expect(response.status).toBe(200);
		expect(response.headers.get("content-type")).toContain("text/html");
		expect(await response.text()).toContain("SwiftLink Bridge Probe");
	});

	it("exposes a health document", async () => {
		const response = await SELF.fetch("http://example.com/healthz");
		expect(response.status).toBe(200);
		expect(await response.json()).toMatchObject({
			ok: true,
			bridgePath: "/bridge",
			targetHost: "127.0.0.1",
			targetPort: 9001,
			targetTls: false,
			authRequired: false,
		});
	});

	it("rejects non-upgrade bridge requests", async () => {
		const request = new Request<unknown, IncomingRequestCfProperties>(
			"http://example.com/bridge",
		);
			const ctx = createExecutionContext();
			const response = await worker.fetch(request, env, ctx);
			await waitOnExecutionContext(ctx);
			expect(response.status).toBe(426);
			expect(await response.text()).toBe("Expected WebSocket upgrade");
		});

	it("rejects unauthorized websocket upgrades", async () => {
		const securedEnv = {
			...env,
			SHARED_TOKEN: "secret",
		};
		const request = new Request<unknown, IncomingRequestCfProperties>(
			"http://example.com/bridge",
			{
				headers: { Upgrade: "websocket" },
			},
		);
		const ctx = createExecutionContext();
		const response = await worker.fetch(request, securedEnv, ctx);
		await waitOnExecutionContext(ctx);
		expect(response.status).toBe(401);
		expect(await response.text()).toBe("Unauthorized");
	});

	it("reports invalid bridge config on healthz", async () => {
		const brokenEnv = {
			...env,
			TARGET_PORT: "70000",
		};
			const request = new Request<unknown, IncomingRequestCfProperties>(
				"http://example.com/healthz",
			);
			const ctx = createExecutionContext();
			const response = await worker.fetch(request, brokenEnv, ctx);
			await waitOnExecutionContext(ctx);
			expect(response.status).toBe(500);
			expect(await response.json()).toMatchObject({
				ok: false,
				bridgePath: "/bridge",
				error: "TARGET_PORT must be an integer between 1 and 65535.",
			});
		});
});
