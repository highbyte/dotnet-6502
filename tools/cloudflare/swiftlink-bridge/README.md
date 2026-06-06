# SwiftLink Cloudflare Bridge

Cloudflare Worker that accepts a browser WebSocket connection and bridges it to one fixed outbound TCP target. The intended consumer is the browser-hosted SwiftLink transport for `dotnet-6502`.

## What it does

- Accepts WebSocket upgrades on one configured path.
- Opens one outbound TCP socket with `connect()` from `cloudflare:sockets`.
- Forwards binary WebSocket frames to TCP as a byte stream.
- Forwards TCP bytes back to the browser as binary WebSocket frames.
- Refuses to act as an open proxy because the target host and port come only from Worker config.
- Supports an optional shared token via `?token=...` or `Authorization: Bearer ...`.

## Local development

The committed `wrangler.jsonc` is set up for local-first testing:

- `TARGET_HOST=127.0.0.1`
- `TARGET_PORT=9001`
- `TARGET_TLS=false`
- `BRIDGE_PATH=/bridge`

Start the repo's TCP echo server in one terminal:

```sh
python3 ../../../tests/swiftlink-smoke/tcp_echo_server.py --host 127.0.0.1 --port 9001
```

Start the Worker in another terminal:

```sh
npm install
npm run dev
```

Then:

1. Open `http://127.0.0.1:8787/`.
2. Connect to the default `ws://127.0.0.1:8787/bridge`.
3. Send hex payload `41`.
4. Confirm the echoed payload comes back as `41`.

For a terminal-only smoke test, with `wrangler dev` still running:

```sh
npm run smoke -- ws://127.0.0.1:8787/bridge 41
```

## Secrets

Do not put shared tokens in `wrangler.jsonc`. Use `.dev.vars` locally:

```sh
cp .dev.vars.example .dev.vars
```

Then set:

```text
SHARED_TOKEN=replace-me
```

## Validation

```sh
npm test
npm run cf-typegen
```

## Deployment notes

- Keep the destination fixed in Worker config. Do not let browsers choose arbitrary hostnames or ports.
- Use `workers.dev` while iterating, then add a zone route or custom domain once the target route is known.
- If a behavior differs between local `wrangler dev --local` and Cloudflare edge execution, use `npm run dev:remote` to compare against a remote preview session.
