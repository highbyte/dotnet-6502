#!/usr/bin/env python3
import argparse
import pathlib
import socket
import sys


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--host", default="127.0.0.1")
    parser.add_argument("--port", type=int, default=0)
    parser.add_argument("--port-file")
    parser.add_argument("--log-file")
    args = parser.parse_args()

    log_path = pathlib.Path(args.log_file) if args.log_file else None

    with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as server:
        server.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
        server.bind((args.host, args.port))
        server.listen()

        actual_port = server.getsockname()[1]
        if args.port_file:
            pathlib.Path(args.port_file).write_text(f"{actual_port}\n", encoding="utf-8")

        print(f"listening {args.host}:{actual_port}", flush=True)

        while True:
            client, address = server.accept()
            print(f"connected {address[0]}:{address[1]}", flush=True)
            with client:
                while True:
                    data = client.recv(4096)
                    if not data:
                        break
                    hex_bytes = data.hex().upper()
                    print(f"recv {hex_bytes}", flush=True)
                    if log_path:
                        with log_path.open("a", encoding="utf-8") as log_file:
                            log_file.write(f"{hex_bytes}\n")
                    client.sendall(data)


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except KeyboardInterrupt:
        sys.exit(0)
