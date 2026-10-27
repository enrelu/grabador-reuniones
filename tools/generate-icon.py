#!/usr/bin/env python3
"""Genera Assets/app.ico (PNG dentro de ICO, Vista+)."""
from __future__ import annotations

import math
import struct
import zlib
from pathlib import Path


def png_rgba(size: int) -> bytes:
    rows = []
    cx = cy = (size - 1) / 2.0
    radius = size / 2.0 - 0.6
    for y in range(size):
        row = bytearray()
        for x in range(size):
            dx, dy = x - cx, y - cy
            r = math.hypot(dx, dy)
            if r <= radius:
                t = r / radius
                # azul → borde más oscuro
                b = int(92 + 40 * (1 - t))
                g = int(61 + 20 * (1 - t))
                red = int(11 + 10 * (1 - t))
                a = 255
                # punto de grabación
                if math.hypot(dx, dy) <= radius * 0.22:
                    red, g, b = 220, 48, 48
                # trazo de onda (aprox. blanco)
                nx = (x / size) * 2 - 1
                wave = math.sin(nx * math.pi * 2.2) * 0.22
                wy = (y / size) - 0.5
                if abs(wy - wave) < (0.07 if size >= 32 else 0.12) and abs(nx) < 0.72 and r < radius * 0.82:
                    red, g, b = 255, 255, 255
            else:
                red = g = b = a = 0
            row += bytes((red, g, b, a))
        rows.append(b"\x00" + bytes(row))
    raw = b"".join(rows)

    def chunk(tag: bytes, data: bytes) -> bytes:
        crc = zlib.crc32(tag + data) & 0xFFFFFFFF
        return struct.pack(">I", len(data)) + tag + data + struct.pack(">I", crc)

    ihdr = struct.pack(">IIBBBBB", size, size, 8, 6, 0, 0, 0)
    return (
        b"\x89PNG\r\n\x1a\n"
        + chunk(b"IHDR", ihdr)
        + chunk(b"IDAT", zlib.compress(raw, 9))
        + chunk(b"IEND", b"")
    )


def ico(pngs: list[tuple[int, bytes]]) -> bytes:
    count = len(pngs)
    offset = 6 + 16 * count
    directory = struct.pack("<HHH", 0, 1, count)
    payload = b""
    for size, data in pngs:
        w = 0 if size >= 256 else size
        directory += struct.pack("<BBBBHHII", w, w, 0, 0, 1, 32, len(data), offset)
        payload += data
        offset += len(data)
    return directory + payload


def main() -> None:
    out = Path(__file__).resolve().parents[1] / "src/GrabadorReuniones/Assets/app.ico"
    out.parent.mkdir(parents=True, exist_ok=True)
    sizes = (16, 32, 48, 256)
    pngs = [(s, png_rgba(s)) for s in sizes]
    out.write_bytes(ico(pngs))
    print(f"Wrote {out} ({out.stat().st_size} bytes)")


if __name__ == "__main__":
    main()
