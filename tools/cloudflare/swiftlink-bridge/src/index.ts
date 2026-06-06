import { connect } from "cloudflare:sockets";

type BridgeConfig = {
	bridgePath: string;
	sharedToken: string | null;
	defaultTargetId: string | null;
	targets: Record<string, BridgeTarget>;
	legacyTarget: BridgeTarget | null;
};

type BridgeTarget = {
	id: string;
	host: string;
	port: number;
	tls: boolean;
};

type ResolvedTarget =
	| { ok: true; target: BridgeTarget }
	| { ok: false; status: number; error: string };

type ConfigValidationResult =
	| { ok: true; config: BridgeConfig }
	| { ok: false; error: string; bridgePath: string };

export function normalizeBridgePath(value: string | undefined): string {
	if (!value) {
		return "/bridge";
	}

	const trimmed = value.trim();
	if (trimmed.length === 0 || trimmed === "/") {
		return "/bridge";
	}

	const withLeadingSlash = trimmed.startsWith("/") ? trimmed : `/${trimmed}`;
	return withLeadingSlash.length > 1
		? withLeadingSlash.replace(/\/+$/, "")
		: withLeadingSlash;
}

export function isTruthyFlag(value: string | undefined): boolean {
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

function isValidTargetId(value: string): boolean {
	return /^[a-z0-9][a-z0-9_-]{0,63}$/i.test(value);
}

function parseTargetDefinition(id: string, value: unknown): BridgeTarget | null {
	if (!value || typeof value !== "object") {
		return null;
	}

	const candidate = value as Record<string, unknown>;
	const host = typeof candidate.host === "string" ? candidate.host.trim() : "";
	const port = Number(candidate.port);
	if (!host || !Number.isInteger(port) || port < 1 || port > 65535) {
		return null;
	}

	return {
		id,
		host,
		port,
		tls: Boolean(candidate.tls),
	};
}

function parseTargetsValue(value: unknown): Record<string, BridgeTarget> | null {
	if (!value || typeof value !== "object" || Array.isArray(value)) {
		return null;
	}

	const targets: Record<string, BridgeTarget> = {};
	for (const [id, targetValue] of Object.entries(value)) {
		if (!isValidTargetId(id)) {
			return null;
		}

		const target = parseTargetDefinition(id, targetValue);
		if (!target) {
			return null;
		}

		targets[id] = target;
	}

	return targets;
}

function parseAllowedTargets(env: Env): Record<string, BridgeTarget> | null {
	const envRecord = env as Record<string, unknown>;
	const directTargets = parseTargetsValue(envRecord.TARGETS);
	if (directTargets) {
		return directTargets;
	}

	const rawTargetsJson = typeof envRecord.TARGETS_JSON === "string" ? envRecord.TARGETS_JSON.trim() : "";
	if (!rawTargetsJson) {
		return null;
	}

	try {
		return parseTargetsValue(JSON.parse(rawTargetsJson));
	} catch {
		return null;
	}
}

export function validateConfig(env: Env): ConfigValidationResult {
	const bridgePath = normalizeBridgePath(env.BRIDGE_PATH);
	const sharedToken = env.SHARED_TOKEN?.trim();
	const targets = parseAllowedTargets(env);
	const envRecord = env as Record<string, unknown>;
	const defaultTargetId =
		typeof envRecord.DEFAULT_TARGET_ID === "string" && envRecord.DEFAULT_TARGET_ID.trim()
			? envRecord.DEFAULT_TARGET_ID.trim()
			: null;

	if (targets && defaultTargetId && !(defaultTargetId in targets)) {
		return { ok: false, error: "DEFAULT_TARGET_ID must reference a configured target.", bridgePath };
	}

	let legacyTarget: BridgeTarget | null = null;
	const targetHost = env.TARGET_HOST?.trim();
	if (targetHost) {
		const rawTargetPort = String(env.TARGET_PORT ?? "").trim();
		const targetPort = Number.parseInt(rawTargetPort, 10);
		if (!Number.isInteger(targetPort) || targetPort < 1 || targetPort > 65535) {
			return { ok: false, error: "TARGET_PORT must be an integer between 1 and 65535.", bridgePath };
		}

		legacyTarget = {
			id: "legacy",
			host: targetHost,
			port: targetPort,
			tls: isTruthyFlag(env.TARGET_TLS),
		};
	}

	if (!targets && !legacyTarget) {
		return { ok: false, error: "Configure TARGETS/TARGETS_JSON or TARGET_HOST/TARGET_PORT.", bridgePath };
	}

	return {
		ok: true,
		config: {
			bridgePath,
			sharedToken: sharedToken ? sharedToken : null,
			defaultTargetId,
			targets: targets ?? {},
			legacyTarget,
		},
	};
}

export function resolveTarget(request: Request, config: BridgeConfig): ResolvedTarget {
	const url = new URL(request.url);
	const requestedTargetId = url.searchParams.get("target")?.trim() ?? "";
	if (requestedTargetId) {
		if (!isValidTargetId(requestedTargetId)) {
			return { ok: false, status: 400, error: "Invalid target id." };
		}

		const target = config.targets[requestedTargetId];
		if (!target) {
			return { ok: false, status: 403, error: "Requested target is not allowed." };
		}

		return { ok: true, target };
	}

	if (config.defaultTargetId) {
		return { ok: true, target: config.targets[config.defaultTargetId] };
	}

	if (config.legacyTarget) {
		return { ok: true, target: config.legacyTarget };
	}

	return { ok: false, status: 500, error: "No target is available for this bridge." };
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

function createHtmlResponse(requestUrl: URL, configResult: ConfigValidationResult): Response {
	const bridgePath = configResult.ok ? configResult.config.bridgePath : configResult.bridgePath;
	const wsProtocol = requestUrl.protocol === "https:" ? "wss:" : "ws:";
	const defaultTargetId = configResult.ok ? configResult.config.defaultTargetId : null;
	const defaultUrl = new URL(`${wsProtocol}//${requestUrl.host}${bridgePath}`);
	if (defaultTargetId) {
		defaultUrl.searchParams.set("target", defaultTargetId);
	}
	const availableTargets = configResult.ok ? Object.keys(configResult.config.targets).sort() : [];
	const html = `<!doctype html>
<html lang="en">
  <head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1" />
    <title>SwiftLink Bridge Probe</title>
    <style>
      :root {
        color-scheme: dark;
        --bg: #10131a;
        --panel: #171b24;
        --panel-edge: #273042;
        --text: #e8edf7;
        --muted: #93a0b8;
        --accent: #7ee0c4;
        --accent-strong: #39c49a;
        --danger: #ff8f8f;
      }
      * { box-sizing: border-box; }
      body {
        margin: 0;
        min-height: 100vh;
        font-family: "IBM Plex Sans", "Segoe UI", sans-serif;
        background:
          radial-gradient(circle at top left, rgba(126, 224, 196, 0.18), transparent 32%),
          linear-gradient(180deg, #0d1016 0%, var(--bg) 100%);
        color: var(--text);
      }
      main {
        max-width: 880px;
        margin: 0 auto;
        padding: 32px 20px 48px;
      }
      .panel {
        background: color-mix(in srgb, var(--panel) 92%, black);
        border: 1px solid var(--panel-edge);
        border-radius: 18px;
        padding: 20px;
        box-shadow: 0 20px 60px rgba(0, 0, 0, 0.35);
      }
      h1 {
        margin: 0 0 8px;
        font-size: clamp(2rem, 5vw, 3rem);
        letter-spacing: -0.04em;
      }
      p, label, small {
        color: var(--muted);
      }
      .grid {
        display: grid;
        gap: 14px;
        grid-template-columns: repeat(auto-fit, minmax(220px, 1fr));
        margin-top: 18px;
      }
      label {
        display: block;
        font-size: 0.9rem;
        margin-bottom: 6px;
      }
      input, textarea, button {
        width: 100%;
        border-radius: 12px;
        border: 1px solid #31405a;
        background: #0f141c;
        color: var(--text);
        padding: 12px 14px;
        font: inherit;
      }
      textarea {
        min-height: 110px;
        resize: vertical;
      }
      button {
        cursor: pointer;
        background: linear-gradient(135deg, var(--accent), var(--accent-strong));
        color: #04281d;
        font-weight: 700;
      }
      button.secondary {
        background: #182131;
        color: var(--text);
      }
      button:disabled {
        opacity: 0.55;
        cursor: not-allowed;
      }
      .actions {
        display: grid;
        gap: 12px;
        grid-template-columns: repeat(auto-fit, minmax(160px, 1fr));
        margin-top: 18px;
      }
      .status {
        margin-top: 16px;
        padding: 12px 14px;
        border-radius: 12px;
        border: 1px solid #2d394e;
        background: #101723;
      }
      .log {
        margin-top: 18px;
        font-family: "IBM Plex Mono", "SFMono-Regular", monospace;
        white-space: pre-wrap;
        background: #0a0e14;
        border: 1px solid #1e2838;
        border-radius: 12px;
        padding: 16px;
        min-height: 220px;
      }
      .warn {
        color: var(--danger);
      }
      .pill {
        display: inline-flex;
        gap: 8px;
        align-items: center;
        border-radius: 999px;
        border: 1px solid #2c3545;
        padding: 8px 12px;
        margin-top: 16px;
        font-size: 0.9rem;
      }
    </style>
  </head>
  <body>
    <main>
      <div class="panel">
        <h1>SwiftLink Bridge Probe</h1>
        <p>Local worker page for exercising the WebSocket endpoint before the browser-hosted emulator is wired in.</p>
        <div class="pill">${configResult.ok ? "Bridge config looks valid." : `<span class="warn">${escapeHtml(
					configResult.error,
				)}</span>`}</div>
        <div class="grid">
          <div>
            <label for="wsUrl">WebSocket URL</label>
            <input id="wsUrl" value="${escapeHtml(defaultUrl.toString())}" />
          </div>
          <div>
            <label for="token">Shared token</label>
            <input id="token" placeholder="Optional token appended as ?token=..." />
          </div>
        </div>
        <div class="grid">
          <div>
            <label for="targetId">Target ID</label>
            <input id="targetId" value="${escapeHtml(defaultTargetId ?? "")}" placeholder="Optional when default target exists" />
            <small>Available targets: ${escapeHtml(availableTargets.join(", ") || "(legacy single-target mode)")}</small>
          </div>
        </div>
        <div class="grid">
          <div>
            <label for="hexPayload">Hex payload</label>
            <textarea id="hexPayload">41</textarea>
            <small>Enter bytes as hex. Whitespace is ignored.</small>
          </div>
          <div>
            <label>Notes</label>
            <p>For the initial smoke, point the Worker at the repo's TCP echo server and send <code>41</code>. The echoed byte should come back as <code>41</code>.</p>
          </div>
        </div>
        <div class="actions">
          <button id="connectButton" type="button">Connect</button>
          <button id="sendButton" type="button" disabled>Send Binary</button>
          <button id="disconnectButton" class="secondary" type="button" disabled>Disconnect</button>
          <button id="clearButton" class="secondary" type="button">Clear Log</button>
        </div>
        <div id="status" class="status">Disconnected</div>
        <div id="log" class="log"></div>
      </div>
    </main>
    <script>
      const statusEl = document.getElementById("status");
      const logEl = document.getElementById("log");
      const connectButton = document.getElementById("connectButton");
      const sendButton = document.getElementById("sendButton");
      const disconnectButton = document.getElementById("disconnectButton");
      const clearButton = document.getElementById("clearButton");
	      const wsUrlInput = document.getElementById("wsUrl");
	      const tokenInput = document.getElementById("token");
	      const targetIdInput = document.getElementById("targetId");
	      const hexPayloadInput = document.getElementById("hexPayload");

      let socket = null;

      function log(line) {
        const prefix = new Date().toISOString().slice(11, 23);
        logEl.textContent += "[" + prefix + "] " + line + "\\n";
        logEl.scrollTop = logEl.scrollHeight;
      }

      function setConnectedState(isConnected, statusText) {
        statusEl.textContent = statusText;
        connectButton.disabled = isConnected;
        sendButton.disabled = !isConnected;
        disconnectButton.disabled = !isConnected;
      }

      function buildUrl() {
	        const rawUrl = wsUrlInput.value.trim();
	        const token = tokenInput.value.trim();
	        const targetId = targetIdInput.value.trim();
	        const url = new URL(rawUrl);
	        if (token) {
	          url.searchParams.set("token", token);
	        } else {
	          url.searchParams.delete("token");
	        }
	        if (targetId) {
	          url.searchParams.set("target", targetId);
	        } else {
	          url.searchParams.delete("target");
	        }
	        return url.toString();
	      }

      function parseHex(text) {
        const normalized = text.replace(/[^0-9a-f]/gi, "");
        if (normalized.length === 0 || normalized.length % 2 !== 0) {
          throw new Error("Hex payload must contain an even number of digits.");
        }
        const bytes = new Uint8Array(normalized.length / 2);
        for (let i = 0; i < normalized.length; i += 2) {
          bytes[i / 2] = Number.parseInt(normalized.slice(i, i + 2), 16);
        }
        return bytes;
      }

      connectButton.addEventListener("click", () => {
        const url = buildUrl();
        socket = new WebSocket(url);
        socket.binaryType = "arraybuffer";
        log("connecting " + url);
        setConnectedState(false, "Connecting...");

        socket.addEventListener("open", () => {
          log("open");
          setConnectedState(true, "Connected");
        });

        socket.addEventListener("message", (event) => {
          if (typeof event.data === "string") {
            log("recv text " + event.data);
            return;
          }
          const view = new Uint8Array(event.data);
          const hex = Array.from(view, (value) => value.toString(16).padStart(2, "0")).join(" ");
          log("recv bytes " + hex.toUpperCase());
        });

        socket.addEventListener("close", (event) => {
          log("close code=" + event.code + " reason=" + (event.reason || "(none)"));
          setConnectedState(false, "Disconnected");
          socket = null;
        });

        socket.addEventListener("error", () => {
          log("error");
          setConnectedState(false, "Error");
        });
      });

      sendButton.addEventListener("click", () => {
        try {
          const bytes = parseHex(hexPayloadInput.value);
          socket.send(bytes);
          const hex = Array.from(bytes, (value) => value.toString(16).padStart(2, "0")).join(" ");
          log("sent bytes " + hex.toUpperCase());
        } catch (error) {
          log("send failed: " + error.message);
        }
      });

      disconnectButton.addEventListener("click", () => {
        socket?.close(1000, "probe complete");
      });

      clearButton.addEventListener("click", () => {
        logEl.textContent = "";
      });
    </script>
  </body>
</html>`;

	return new Response(html, {
		headers: {
			"content-type": "text/html; charset=utf-8",
			"cache-control": "no-store",
		},
	});
}

function createHealthResponse(configResult: ConfigValidationResult): Response {
	const body = configResult.ok
			? {
					ok: true,
					bridgePath: configResult.config.bridgePath,
					defaultTargetId: configResult.config.defaultTargetId,
					targetIds: Object.keys(configResult.config.targets).sort(),
					legacyTarget:
						configResult.config.legacyTarget === null
							? null
							: {
									host: configResult.config.legacyTarget.host,
									port: configResult.config.legacyTarget.port,
									tls: configResult.config.legacyTarget.tls,
								},
					authRequired: configResult.config.sharedToken !== null,
				}
		: {
				ok: false,
				bridgePath: configResult.bridgePath,
				error: configResult.error,
			};

	return Response.json(body, {
		headers: {
			"cache-control": "no-store",
		},
		status: configResult.ok ? 200 : 500,
	});
}

function escapeHtml(value: string): string {
	return value
		.replaceAll("&", "&amp;")
		.replaceAll("<", "&lt;")
		.replaceAll(">", "&gt;")
		.replaceAll('"', "&quot;");
}

async function toUint8Array(data: unknown): Promise<Uint8Array | null> {
	if (data instanceof Uint8Array) {
		return data;
	}

	if (data instanceof ArrayBuffer) {
		return new Uint8Array(data);
	}

	if (ArrayBuffer.isView(data)) {
		return new Uint8Array(data.buffer, data.byteOffset, data.byteLength);
	}

	if (data instanceof Blob) {
		return new Uint8Array(await data.arrayBuffer());
	}

	return null;
}

async function handleSession(
	webSocket: WebSocket,
	config: BridgeConfig,
	target: BridgeTarget,
	sessionId: string,
): Promise<void> {
	webSocket.accept({ allowHalfOpen: true });
	webSocket.binaryType = "arraybuffer";

	let socket: Socket | null = null;
	let writer: WritableStreamDefaultWriter<Uint8Array> | null = null;
	let reader: ReadableStreamDefaultReader<Uint8Array> | null = null;
	let closed = false;
	let outboundWrites = Promise.resolve();
	const pendingFrames: Uint8Array[] = [];
	let wsToTcpBytes = 0;
	let tcpToWsBytes = 0;

	const enqueueWrite = (bytes: Uint8Array) => {
		outboundWrites = outboundWrites
			.then(async () => {
				if (closed) {
					return;
				}

				if (writer === null) {
					pendingFrames.push(bytes);
					return;
				}

				wsToTcpBytes += bytes.byteLength;
				if (wsToTcpBytes <= 64) {
					console.log(
						`[swiftlink-bridge:${sessionId}] ws->tcp ${bytes.byteLength} byte(s) hex=${toHex(bytes)}`,
					);
				}

				await writer.write(bytes);
			})
			.catch(async (error) => {
				console.error(`[swiftlink-bridge:${sessionId}] tcp write failed`, error);
				await closeBoth(1011, "write failed");
			});
	};

	const closeBoth = async (code = 1000, reason = "closed"): Promise<void> => {
		if (closed) {
			return;
		}

		closed = true;

		try {
			if (webSocket.readyState === WebSocket.OPEN || webSocket.readyState === WebSocket.CLOSING) {
				webSocket.close(code, reason);
			}
		} catch (error) {
			console.warn(`[swiftlink-bridge:${sessionId}] websocket close failed`, error);
		}

		try {
			await outboundWrites.catch(() => undefined);
			await writer?.close();
		} catch (error) {
			console.warn(`[swiftlink-bridge:${sessionId}] writer close failed`, error);
		}

		try {
			await socket?.close();
		} catch (error) {
			console.warn(`[swiftlink-bridge:${sessionId}] socket close failed`, error);
		}

		try {
			writer?.releaseLock();
			reader?.releaseLock();
		} catch {
		}
		};

	webSocket.addEventListener("message", (event) => {
		void (async () => {
			const bytes = await toUint8Array(event.data);
			if (bytes === null) {
				console.warn(`[swiftlink-bridge:${sessionId}] non-binary websocket frame rejected`);
				await closeBoth(1003, "binary only");
				return;
			}

			enqueueWrite(bytes);
		})().catch(async (error) => {
			console.error(`[swiftlink-bridge:${sessionId}] websocket message handling failed`, error);
			await closeBoth(1011, "message failed");
		});
	});

	webSocket.addEventListener("close", (event) => {
		console.log(
			`[swiftlink-bridge:${sessionId}] websocket closed code=${event.code} reason=${event.reason || "(none)"}`,
		);
		void closeBoth(1000, "websocket closed");
	});

	webSocket.addEventListener("error", () => {
		if (closed) {
			return;
		}

		console.error(`[swiftlink-bridge:${sessionId}] websocket error`);
		void closeBoth(1011, "websocket error");
	});

	try {
		socket = connect(
			{ hostname: target.host, port: target.port },
			{ secureTransport: target.tls ? "on" : "off" },
		);
		const socketInfo = await socket.opened;
		console.log(
			`[swiftlink-bridge:${sessionId}] tcp connected remote=${socketInfo.remoteAddress ?? "(unknown)"} local=${socketInfo.localAddress ?? "(unknown)"}`,
		);

			writer = socket.writable.getWriter();
			reader = socket.readable.getReader();

			for (const pendingFrame of pendingFrames.splice(0)) {
				enqueueWrite(pendingFrame);
			}

		while (!closed) {
			const { value, done } = await reader.read();
			if (done) {
				break;
				}

				if (value && value.byteLength > 0) {
					tcpToWsBytes += value.byteLength;
					if (tcpToWsBytes <= 64) {
						console.log(
							`[swiftlink-bridge:${sessionId}] tcp->ws ${value.byteLength} byte(s) hex=${toHex(value)}`,
						);
					}
					webSocket.send(value);
				}
			}

			console.log(
				`[swiftlink-bridge:${sessionId}] session complete ws->tcp=${wsToTcpBytes} tcp->ws=${tcpToWsBytes}`,
			);
			await closeBoth(1000, "tcp closed");
		} catch (error) {
		if (closed) {
			return;
		}

		console.error(`[swiftlink-bridge:${sessionId}] session failed`, error);
		await closeBoth(1011, "tcp failed");
	}
}

function toHex(bytes: Uint8Array): string {
	return Array.from(bytes, (value) => value.toString(16).padStart(2, "0")).join(" ");
}

export default {
	async fetch(request, env, ctx): Promise<Response> {
		const url = new URL(request.url);
		const configResult = validateConfig(env);

		if (request.method === "GET" && url.pathname === "/") {
			return createHtmlResponse(url, configResult);
		}

		if (request.method === "GET" && url.pathname === "/healthz") {
			return createHealthResponse(configResult);
		}

		if (!configResult.ok || url.pathname !== configResult.config.bridgePath) {
			return new Response("Not Found", { status: 404 });
		}

		if (request.method !== "GET") {
			return new Response("Method Not Allowed", { status: 405 });
		}

		if (request.headers.get("Upgrade")?.toLowerCase() !== "websocket") {
			return new Response("Expected WebSocket upgrade", { status: 426 });
		}

		if (!isAuthorized(request, configResult.config.sharedToken)) {
			return new Response("Unauthorized", { status: 401 });
		}

		const targetResult = resolveTarget(request, configResult.config);
		if (!targetResult.ok) {
			return new Response(targetResult.error, { status: targetResult.status });
		}

		const pair = new WebSocketPair();
		const [client, server] = Object.values(pair);
		const sessionId = crypto.randomUUID();

		ctx.waitUntil(handleSession(server, configResult.config, targetResult.target, sessionId));

		return new Response(null, {
			status: 101,
			webSocket: client,
		});
	},
} satisfies ExportedHandler<Env>;
