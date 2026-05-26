## SwiftLink smoke

Local end-to-end smoke test for C64 SwiftLink Phase 1 through the real Headless app.

What it checks:

- Headless host config enables SwiftLink and connects to a local TCP endpoint.
- The smoke runner waits until the SwiftLink TCP connection is established.
- A tiny C64 machine-code program writes one byte to `$DE00`.
- A local echo server returns that byte.
- The C64 program reads the echoed byte back and stores it at `$C100`.
- The smoke runner loads the PRG and starts it through the existing remote-control TCP API, then verifies `$C100 == $41`.

Run locally:

```sh
./tests/swiftlink-smoke/run-local.sh
```

```powershell
./tests/swiftlink-smoke/run-local.ps1
```

Requirements:

- Python 3 available as `python3`, `python`, or `py -3`.
- Working C64 ROM configuration for the Headless app.

This smoke is intentionally separate from the xUnit suites. It starts a real TCP server and a real Headless app process, so it is better treated like `tests/wasm-smoke` than a unit or integration test.

If the smoke fails, the runner keeps its temporary logs and artifacts and prints the temp directory path.
