#!/usr/bin/env python3
"""Pull every RT_ICON frame out of LuxTicker.exe and write a contact sheet.

The sheet is the proof that the built exe carries the same scaled LW at
16, 20, 24, 32, 40, 48, 64, 96, 128, and 256.
"""

from __future__ import annotations

import struct
import sys
import zlib
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
EXE = ROOT / "src" / "LuxTicker.App" / "bin" / "Release" / "net8.0-windows10.0.17763.0" / "win-x64" / "publish" / "LuxTicker.exe"
ART = ROOT / "artifacts"
ORANGE = (247, 147, 26, 255)
PAPER = (246, 242, 232, 255)
SIZES = (16, 20, 24, 32, 40, 48, 64, 96, 128, 256)

RT_ICON = 3
RT_GROUP_ICON = 14


def u16(data: bytes, offset: int) -> int:
    return struct.unpack_from("<H", data, offset)[0]


def u32(data: bytes, offset: int) -> int:
    return struct.unpack_from("<I", data, offset)[0]


def rva_to_off(sections: list[tuple[int, int, int]], rva: int) -> int:
    for virt, raw, size in sections:
        span = max(size, 1)
        if virt <= rva < virt + span:
            return raw + (rva - virt)
    raise SystemExit(f"RVA 0x{rva:x} is not in a section")


def pe_resources(data: bytes) -> tuple[int, list[tuple[int, int, int]]]:
    e_lfanew = u32(data, 0x3C)
    if data[e_lfanew:e_lfanew + 4] != b"PE\0\0":
        raise SystemExit("not a PE")
    coff = e_lfanew + 4
    sections_count = u16(data, coff + 2)
    optional_size = u16(data, coff + 16)
    optional = coff + 20
    magic = u16(data, optional)
    # PE32+ data directories start at 112. PE32 (the Setup stub) starts at 96.
    data_dir = 112 if magic == 0x20B else 96 if magic == 0x10B else 0
    if data_dir == 0:
        raise SystemExit(f"expected PE32 or PE32+, got 0x{magic:x}")
    # Resource is data directory index 2.
    resource_rva = u32(data, optional + data_dir + 2 * 8)
    resource_size = u32(data, optional + 112 + 2 * 8 + 4)
    if resource_rva == 0:
        raise SystemExit("exe has no resource directory")
    section_off = optional + optional_size
    sections: list[tuple[int, int, int]] = []
    for i in range(sections_count):
        base = section_off + i * 40
        virt = u32(data, base + 12)
        raw_size = u32(data, base + 16)
        raw = u32(data, base + 20)
        virt_size = u32(data, base + 8)
        sections.append((virt, raw, max(raw_size, virt_size)))
    return rva_to_off(sections, resource_rva), sections


def walk(data: bytes, root: int, sections: list[tuple[int, int, int]], offset: int, want: int) -> list[tuple[int, bytes]]:
    """Return (id, bytes) for every resource of type `want`."""
    found: list[tuple[int, bytes]] = []

    def directory(off: int, depth: int, ident: int) -> None:
        count = u16(data, off + 12) + u16(data, off + 14)
        entry = off + 16
        for _ in range(count):
            name = u32(data, entry)
            child = u32(data, entry + 4)
            entry += 8
            name_id = name & 0xFFFF
            if depth == 0 and name_id != want:
                continue
            next_id = name_id if depth == 1 else ident
            if child & 0x80000000:
                directory(root + (child & 0x7FFFFFFF), depth + 1, next_id)
                continue
            leaf = root + child
            rva = u32(data, leaf)
            size = u32(data, leaf + 4)
            file_off = rva_to_off(sections, rva)
            found.append((next_id, data[file_off:file_off + size]))

    directory(offset, 0, 0)
    return found


def decode_dib(blob: bytes) -> list[list[tuple[int, int, int, int]]]:
    width = struct.unpack_from("<i", blob, 4)[0]
    height = struct.unpack_from("<i", blob, 8)[0] // 2
    offset = 40
    pixels = [[ORANGE for _ in range(width)] for _ in range(height)]
    for y in range(height - 1, -1, -1):
        for x in range(width):
            b, g, r, a = blob[offset:offset + 4]
            pixels[y][x] = (r, g, b, a)
            offset += 4
    return pixels


def write_png(path: Path, pixels: list[list[tuple[int, int, int, int]]]) -> None:
    height = len(pixels)
    width = len(pixels[0])
    raw = bytearray()
    for row in pixels:
        raw.append(0)
        for r, g, b, a in row:
            raw.extend((r, g, b, a))

    def chunk(tag: bytes, payload: bytes) -> bytes:
        return struct.pack(">I", len(payload)) + tag + payload + struct.pack(">I", zlib.crc32(tag + payload) & 0xFFFFFFFF)

    ihdr = struct.pack(">IIBBBBB", width, height, 8, 6, 0, 0, 0)
    png = b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", ihdr) + chunk(b"IDAT", zlib.compress(bytes(raw), 9)) + chunk(b"IEND", b"")
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(png)


def nearest(pixels: list[list[tuple[int, int, int, int]]], size: int) -> list[list[tuple[int, int, int, int]]]:
    n = len(pixels)
    out: list[list[tuple[int, int, int, int]]] = []
    for y in range(size):
        sy = min(n - 1, y * n // size)
        out.append([pixels[sy][min(n - 1, x * n // size)] for x in range(size)])
    return out


def digits(text: str) -> list[list[int]]:
    font = {
        "0": ["111", "101", "101", "101", "111"],
        "1": ["010", "110", "010", "010", "111"],
        "2": ["111", "001", "111", "100", "111"],
        "3": ["111", "001", "111", "001", "111"],
        "4": ["101", "101", "111", "001", "001"],
        "5": ["111", "100", "111", "001", "111"],
        "6": ["111", "100", "111", "101", "111"],
        "7": ["111", "001", "001", "001", "001"],
        "8": ["111", "101", "111", "101", "111"],
        "9": ["111", "101", "111", "001", "111"],
    }
    rows = [""] * 5
    for index, ch in enumerate(text):
        glyph = font[ch]
        if index:
            rows = [row + "0" for row in rows]
        rows = [row + glyph[i] for i, row in enumerate(rows)]
    return [[1 if cell == "1" else 0 for cell in row] for row in rows]


def contact(frames: dict[int, list[list[tuple[int, int, int, int]]]]) -> None:
    cell = 96
    gap = 18
    label_h = 16
    width = gap + (cell + gap) * len(SIZES)
    height = gap + label_h + cell + gap
    bg = (230, 226, 216, 255)
    ink = (22, 20, 18, 255)
    canvas = [[bg for _ in range(width)] for _ in range(height)]
    for index, size in enumerate(SIZES):
        shown = nearest(frames[size], cell)
        left = gap + index * (cell + gap)
        top = gap + label_h
        for y, row in enumerate(shown):
            for x, pixel in enumerate(row):
                canvas[top + y][left + x] = pixel
        glyph = digits(str(size))
        scale = 2
        gx = left
        gy = gap
        for y, row in enumerate(glyph):
            for x, on in enumerate(row):
                if not on:
                    continue
                for sy in range(scale):
                    for sx in range(scale):
                        canvas[gy + y * scale + sy][gx + x * scale + sx] = ink
    write_png(ART / "icon-contact-sheet.png", canvas)


def main() -> None:
    exe = Path(sys.argv[1]) if len(sys.argv) > 1 else EXE
    data = exe.read_bytes()
    root, sections = pe_resources(data)
    groups = walk(data, root, sections, root, RT_GROUP_ICON)
    icons = {ident: blob for ident, blob in walk(data, root, sections, root, RT_ICON)}
    if not groups:
        raise SystemExit("exe has no icon group")
    group = groups[0][1]
    count = u16(group, 4)
    frames: dict[int, list[list[tuple[int, int, int, int]]]] = {}
    entry = 6
    for _ in range(count):
        width = group[entry] or 256
        height = group[entry + 1] or 256
        ident = u16(group, entry + 12)
        blob = icons[ident]
        if blob[:8] == b"\x89PNG\r\n\x1a\n":
            raise SystemExit(f"{width}px frame is a PNG, not a DIB")
        frames[width] = decode_dib(blob)
        if width != height:
            raise SystemExit(f"{width}x{height} is not square")
        entry += 14
    missing = [size for size in SIZES if size not in frames]
    if missing:
        raise SystemExit("exe icon is missing " + ", ".join(str(size) for size in missing))
    for size, pixels in frames.items():
        if pixels[0][0] != ORANGE or pixels[0][-1] != ORANGE:
            raise SystemExit(f"{size}px corners are not orange")
        if not any(pixel == PAPER for row in pixels for pixel in row):
            raise SystemExit(f"{size}px has no paper letters")
        write_png(ART / f"exe-icon-{size}.png", pixels)
    contact(frames)
    print(f"extracted {len(frames)} frames from {exe}")
    print("contact " + str(ART / "icon-contact-sheet.png"))


if __name__ == "__main__":
    main()
