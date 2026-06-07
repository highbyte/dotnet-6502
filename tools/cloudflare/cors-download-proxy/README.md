# Browser Download CORS Proxy

Cloudflare Worker for browser-hosted `dotnet-6502` downloads.

## What it does

- Accepts `GET`, `HEAD`, and `OPTIONS` on one configured path.
- Allows requests only from configured browser origins.
- Allows downloads only from configured target hosts.
- Follows redirects manually and re-validates each hop.
- Supports an optional shared token via `?token=...` or `Authorization: Bearer ...`.
- Applies first-version rate limiting inside the Worker.
- Exposes a simple health document at `/healthz`.

## Local development

```sh
npm install
npm run dev
```

If the repo's `swiftlink-bridge` Worker is also running locally, prefer a
different port for this Worker to avoid Wrangler's default `8787` conflict:

```sh
npm run dev -- --ip 127.0.0.1 --port 8788
```

Open:

```text
http://127.0.0.1:8787/
```

Example proxied request:

```text
http://127.0.0.1:8787/fetch?url=https%3A%2F%2Fwww.zimmers.net%2Fanonftp%2Fpub%2Fcbm%2Ffirmware%2Fcomputers%2Fc64%2Fkernal.901227-03.bin
```

When using port `8788`, the browser-app proxy override becomes:

```text
http://127.0.0.1:8788/fetch?url=
```

## Validation

```sh
npm test
npm run cf-typegen
```
