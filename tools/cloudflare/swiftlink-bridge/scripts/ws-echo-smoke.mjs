#!/usr/bin/env node

const [url, hexPayload = "41"] = process.argv.slice(2);

if (!url) {
	console.error("usage: npm run smoke -- <ws-url> [hex-payload]");
	process.exit(1);
}

const normalizedHex = hexPayload.replace(/[^0-9a-f]/gi, "");
if (normalizedHex.length === 0 || normalizedHex.length % 2 !== 0) {
	console.error("hex payload must contain an even number of hex digits");
	process.exit(1);
}

const payload = new Uint8Array(normalizedHex.length / 2);
for (let i = 0; i < normalizedHex.length; i += 2) {
	payload[i / 2] = Number.parseInt(normalizedHex.slice(i, i + 2), 16);
}

const socket = new WebSocket(url);
socket.binaryType = "arraybuffer";

const timeout = setTimeout(() => {
	console.error("smoke timed out waiting for websocket echo");
	socket.close(1011, "timeout");
	process.exit(1);
}, 5000);

socket.addEventListener("open", () => {
	console.log(`open ${url}`);
	socket.send(payload);
	console.log(`sent ${normalizedHex.toUpperCase()}`);
});

socket.addEventListener("message", (event) => {
	const bytes = new Uint8Array(event.data);
	const receivedHex = Array.from(bytes, (value) => value.toString(16).padStart(2, "0")).join("").toUpperCase();
	console.log(`recv ${receivedHex}`);

	if (receivedHex !== normalizedHex.toUpperCase()) {
		console.error(`expected ${normalizedHex.toUpperCase()} but received ${receivedHex}`);
		clearTimeout(timeout);
		socket.close(1011, "mismatch");
		process.exit(1);
	}

	clearTimeout(timeout);
	socket.close(1000, "smoke passed");
	process.exit(0);
});

socket.addEventListener("close", (event) => {
	console.log(`close code=${event.code} reason=${event.reason || "(none)"}`);
});

socket.addEventListener("error", () => {
	clearTimeout(timeout);
	console.error("websocket error");
	process.exit(1);
});
