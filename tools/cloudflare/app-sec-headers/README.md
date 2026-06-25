# dotnet-6502 app cross-origin isolation headers

Cloudflare Worker that adds cross-origin isolation headers to the dotnet-6502
emulator browser apps served from GitHub Pages behind Cloudflare.

## What it does

- Routed on `highbyte.se/dotnet-6502/*` (one worker for all emulator apps).
- Stamps these headers on emulator **app** paths only (`/dotnet-6502/app*`):
  - `Cross-Origin-Opener-Policy: same-origin`
  - `Cross-Origin-Embedder-Policy: require-corp`
- Passes every other `/dotnet-6502/*` request (the docs site, etc.) through
  unchanged.

## Why

`SharedArrayBuffer` and the low-latency AudioWorklet SID path are only available
when `window.crossOriginIsolated === true`, which requires the COOP/COEP pair on
the document. GitHub Pages cannot set arbitrary response headers, so they are
injected at the Cloudflare edge.

The headers are scoped to `/dotnet-6502/app*` rather than all of
`/dotnet-6502/*` so that `require-corp` (which blocks non-CORP/CORS cross-origin
subresources) cannot break the docs site.

## App path convention

New emulator apps are covered automatically as long as they are deployed under
an `app*` path (e.g. `app`, `app2`, `app2-test`, a future `app3`). An app
deployed under a different prefix needs a one-line change to `APP_PATH_PATTERN`
in `src/index.ts`.

## Routing note

The `highbyte.se/dotnet-6502/*` route previously pointed at *no worker* — a
deliberate carve-out so the site-wide `highbyte_se_sec_headers` worker
(`highbyte.se/*`, homepage CSP/security headers) does not run on the emulator
apps. Deploying this worker repoints that same route at this worker; the
homepage worker still does not run on `/dotnet-6502/*` (the more specific route
wins).

## Deploy

```sh
npm install
npx wrangler deploy
```

Routine re-deploys just work, since the `highbyte.se/dotnet-6502/*` route is already
assigned to this worker.

### First-time route takeover

The `highbyte.se/dotnet-6502/*` route previously existed assigned to *no worker* (the
carve-out described above). Wrangler refuses to claim a route that is assigned to a
different worker (or to none), so the very first deploy fails with:

```
Can't deploy routes that are assigned to another worker.
  "null" is already assigned to routes:
    - highbyte.se/dotnet-6502/*
```

The worker script still uploads; only the route assignment is rejected. Reassign the
existing route to this worker once (then re-deploys are clean). Either:

- In the dashboard: Workers & Pages → Routes → reassign `highbyte.se/dotnet-6502/*` to
  `dotnet6502-app-sec-headers`, or
- Via the API (find the route id with `GET /zones/{zone}/workers/routes`, then):

```sh
curl -X PUT "https://api.cloudflare.com/client/v4/zones/{zone}/workers/routes/{route_id}" \
  -H "Authorization: Bearer $CLOUDFLARE_API_TOKEN" \
  -H "Content-Type: application/json" \
  --data '{"pattern":"highbyte.se/dotnet-6502/*","script":"dotnet6502-app-sec-headers"}'
```

## Verify

```sh
curl -sI https://highbyte.se/dotnet-6502/app2/ | grep -i cross-origin
```

Both `cross-origin-opener-policy` and `cross-origin-embedder-policy` should be
present. In the live app's devtools console:

```js
crossOriginIsolated        // true
typeof SharedArrayBuffer   // "function"
```

A docs page must be unaffected:

```sh
curl -sI https://highbyte.se/dotnet-6502/ | grep -i cross-origin   # no output
```
