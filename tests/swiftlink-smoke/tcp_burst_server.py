#!/usr/bin/env python3
import argparse
import pathlib
import socket
import sys
import time
from typing import Optional

from _safe_path import safe_path


def wait_for_trigger(trigger_file: Optional[str]) -> None:
    if not trigger_file:
        return

    trigger_path = pathlib.Path(trigger_file)
    while not trigger_path.exists():
        time.sleep(0.05)


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--host", default="127.0.0.1")
    parser.add_argument("--port", type=int, default=0)
    parser.add_argument("--port-file")
    parser.add_argument("--log-file")
    parser.add_argument("--trigger-file")
    parser.add_argument("--start-byte", type=lambda value: int(value, 0), default=0x30)
    parser.add_argument("--count", type=int, default=16)
    args = parser.parse_args()

    payload = bytes((args.start_byte + i) & 0xFF for i in range(args.count))
    log_path = safe_path(args.log_file) if args.log_file else None

    with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as server:
        server.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
        server.bind((args.host, args.port))
        server.listen()

        actual_port = server.getsockname()[1]
        if args.port_file:
            safe_path(args.port_file).write_text(f"{actual_port}\n", encoding="utf-8")

        print(f"listening {args.host}:{actual_port}", flush=True)

        client, address = server.accept()
        print(f"connected {address[0]}:{address[1]}", flush=True)
        with client:
            wait_for_trigger(args.trigger_file)
            client.sendall(payload)
            hex_bytes = payload.hex().upper()
            print(f"sent {hex_bytes}", flush=True)
            if log_path:
                log_path.write_text(f"{hex_bytes}\n", encoding="utf-8")

    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except KeyboardInterrupt:
        sys.exit(0)
