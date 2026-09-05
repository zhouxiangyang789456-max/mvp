# -*- coding: utf-8 -*-
"""Blender MCP socket client for driving the Blender MCP addon directly.

Speaks the newline-delimited JSON protocol used by the Blender MCP addon
socket server (default 127.0.0.1:9876). See
D:\\prounity\\mvp\\.codex\\vendor\\blender-mcp\\src\\blender_mcp_addon\\server\\socket_server.py

Usage (CLI):
    python blender_mcp_client.py <capability> <json_payload> [--timeout 60]

Example:
    python blender_mcp_client.py blender.get_objects "{\"type_filter\":\"MESH\"}"

Usage (as module):
    from blender_mcp_client import BlenderMCPClient
    c = BlenderMCPClient()
    r = c.call("blender.get_objects", {})
    c.close()
"""

from __future__ import annotations

import json
import socket
import sys


class BlenderMCPClient:
    def __init__(self, host: str = "127.0.0.1", port: int = 9876, timeout: float = 300.0):
        self.host = host
        self.port = port
        self.timeout = timeout
        self._sock = None

    def _ensure(self) -> socket.socket:
        if self._sock is None:
            self._sock = socket.create_connection((self.host, self.port), timeout=self.timeout)
        return self._sock

    def call(self, capability: str, payload: dict, timeout: float | None = None) -> dict:
        sock = self._ensure()
        sock.settimeout(timeout or self.timeout)
        req = json.dumps({"capability": capability, "payload": payload}, ensure_ascii=False)
        sock.sendall((req + "\n").encode("utf-8"))
        buf = b""
        while b"\n" not in buf:
            chunk = sock.recv(65536)
            if not chunk:
                break
            buf += chunk
        if b"\n" not in buf:
            raise ConnectionError("Blender MCP server closed connection without a response")
        line, _ = buf.split(b"\n", 1)
        if not line.strip():
            raise ConnectionError("Empty response from Blender MCP server")
        return json.loads(line)

    def close(self) -> None:
        if self._sock is not None:
            try:
                self._sock.close()
            except OSError:
                pass
            self._sock = None

    def __enter__(self) -> "BlenderMCPClient":
        return self

    def __exit__(self, *exc) -> None:
        self.close()


def _main() -> int:
    args = [a for a in sys.argv[1:] if not a.startswith("--")]
    if len(args) < 1:
        print("usage: blender_mcp_client.py <capability> [json_payload]", file=sys.stderr)
        return 2
    capability = args[0]
    payload = {}
    if len(args) > 1:
        payload = json.loads(args[1])
    timeout = 300.0
    if "--timeout" in sys.argv:
        timeout = float(sys.argv[sys.argv.index("--timeout") + 1])
    with BlenderMCPClient(timeout=timeout) as c:
        r = c.call(capability, payload)
        print(json.dumps(r, ensure_ascii=False, indent=2))
        return 0 if r.get("ok") else 1


if __name__ == "__main__":
    sys.exit(_main())
