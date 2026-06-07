# WebSocket-to-TCP Bridge

Cloudflare Worker that accepts a browser WebSocket connection and bridges it to one allowlisted outbound TCP target selected by logical target id. The intended consumer in this repo is the browser-hosted SwiftLink transport for `dotnet-6502`, but the Worker itself is a generic byte-stream bridge.

## What it does

- Accepts WebSocket upgrades on one configured path.
- Opens one outbound TCP socket with `connect()` from `cloudflare:sockets`.
- Forwards binary WebSocket frames to TCP as a byte stream.
- Forwards TCP bytes back to the browser as binary WebSocket frames.
- Refuses to act as an open proxy because the target comes only from Worker config.
- Supports an optional shared token via `?token=...` or `Authorization: Bearer ...`.
- Supports logical target selection via `?target=...`, mapped server-side to an allowlisted `host` / `port` / `tls` triple.

## Local development

The committed `wrangler.jsonc` is set up for local-first testing:

- `DEFAULT_TARGET_ID=local-echo`
- `TARGETS.compunet-reborn.host=vme.compunet.live`
- `TARGETS.compunet-reborn.port=6400`
- `TARGETS.compunet-reborn.tls=false`
- `TARGETS.local-echo.host=127.0.0.1`
- `TARGETS.local-echo.port=9001`
- `TARGETS.local-echo.tls=false`
- `BRIDGE_PATH=/bridge`
- deployed Worker name: `ws-tcp-bridge`

### Run locally against the echo smoke target

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
2. Connect to the default `ws://127.0.0.1:8787/bridge?target=local-echo`.
3. Send hex payload `41`.
4. Confirm the echoed payload comes back as `41`.

For a terminal-only smoke test, with `wrangler dev` still running:

```sh
npm run smoke -- 'ws://127.0.0.1:8787/bridge?target=local-echo' 41
```

### Run locally against Compunet Reborn

The checked-in local config already includes:

- `TARGETS.compunet-reborn.host=vme.compunet.live`
- `TARGETS.compunet-reborn.port=6400`
- `TARGETS.compunet-reborn.tls=false`

So the default `wrangler dev --local` command is enough:

```sh
npm run dev
```

Then use:

```text
ws://127.0.0.1:8787/bridge?target=compunet-reborn
```

For the Avalonia Browser C64 config, the local override settings are:

- `Bridge URL`: `ws://127.0.0.1:8787/bridge`
- `Target ID`: `compunet-reborn`
- `Transport mode`: `HayesModem`
- `Interrupt line`: `NMI`

## Target selection

The browser client should send a logical target id, not a raw host and port.

Example Worker config:

```jsonc
"vars": {
  "DEFAULT_TARGET_ID": "compunet-reborn",
  "TARGETS": {
    "compunet-reborn": {
      "host": "vme.compunet.live",
      "port": 6400,
      "tls": false
    },
    "local-echo": {
      "host": "127.0.0.1",
      "port": 9001,
      "tls": false
    }
  },
  "BRIDGE_PATH": "/bridge"
}
```

Then connect to:

```text
ws://127.0.0.1:8787/bridge?target=compunet-reborn
```

For the Avalonia Browser C64 config in this repo, the target combo box is prepopulated with
`compunet-reborn` and `local-echo`, matching the local Worker defaults in `wrangler.jsonc`.

The Worker also keeps a legacy fallback mode:

```text
TARGET_HOST
TARGET_PORT
TARGET_TLS
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

## Deploy to Cloudflare

The checked-in Wrangler config is already set up to deploy this Worker as:

```text
https://ws-tcp-bridge.highbyte.workers.dev/
```

with the bridge endpoint:

```text
wss://ws-tcp-bridge.highbyte.workers.dev/bridge
```

Deploy from this directory:

```sh
npx wrangler login
npx wrangler deploy
```

After deploy, verify:

```sh
curl https://ws-tcp-bridge.highbyte.workers.dev/healthz
```

The deployed health document should include:

- `defaultTargetId: "local-echo"`
- `targetIds: ["compunet-reborn", "local-echo"]`

For the Avalonia Browser C64 config, the deployed-default settings are:

- `Bridge URL`: `wss://ws-tcp-bridge.highbyte.workers.dev/bridge`
- `Target ID`: `compunet-reborn`
- `Transport mode`: `HayesModem`
- `Interrupt line`: `NMI`

## Deployment notes

- Keep the destination allowlisted in Worker config. Do not let browsers choose arbitrary hostnames or ports.
- Use `workers.dev` while iterating, then add a zone route or custom domain once the target route is known.
- If a behavior differs between local `wrangler dev --local` and Cloudflare edge execution, use `npm run dev:remote` to compare against a remote preview session.
