#!/usr/bin/env python3
"""Build LuxTicker.ico from one vector LW.

Orange #F7931A fills every frame. Letters are paper #F6F2E8. The paths are a
trace of the reference mark: a heavy L, and a W whose middle peak rises above
the outer strokes. The 256 frame is that vector. Every other size is an exact
area scale of that same bitmap, so the letters do not get redrawn.
"""

from __future__ import annotations

import struct
import zlib
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
ICO = ROOT / "src" / "LuxTicker.App" / "Assets" / "LuxTicker.ico"
TOAST = ROOT / "src" / "LuxTicker.App" / "Assets" / "lux-wire-toast.png"
ART = ROOT / "artifacts"
REFERENCE = Path("/cursor/stores/self/lw-icon-reference.png")

ORANGE = (247, 147, 26, 255)
PAPER = (246, 242, 232, 255)
SIZES = (16, 20, 24, 32, 40, 48, 64, 96, 128, 256)
FRAME = 92.0

# Traced from the reference, then shifted 2px left so the margins match.
_SHIFT = -2.0
_W = [
    (55.5, 17.5), (49.5, 48.5), (47.5, 48.5), (45.5, 35.5), (38.5, 35.5),
    (42.5, 58.5), (44.5, 62.5), (44.5, 67.5), (46.5, 71.5), (53.5, 70.5),
    (56.5, 46.5), (57.5, 46.5), (57.5, 43.5), (59.5, 43.5), (63.5, 70.5),
    (70.5, 71.5), (72.5, 62.5), (73.5, 62.5), (73.5, 57.5), (74.5, 57.5),
    (78.5, 35.5), (71.5, 35.5), (69.5, 48.5), (67.5, 48.5), (62.5, 18.5),
    (56.5, 17.5),
]
_L = [
    (17.5, 35.5), (16.5, 36.5), (16.5, 51.5), (17.5, 51.5), (17.5, 55.5),
    (16.5, 55.5), (17.5, 69.5), (16.5, 70.5), (17.5, 71.5), (33.5, 71.5),
    (34.5, 61.5), (26.5, 60.5), (26.5, 35.5), (18.5, 35.5),
]
W_PATH = [(x + _SHIFT, y) for x, y in _W]
L_PATH = [(x + _SHIFT, y) for x, y in _L]

def inside(x: float, y: float, poly: list[tuple[float, float]]) -> bool:
    count = False
    for i, (x1, y1) in enumerate(poly):
        x2, y2 = poly[(i + 1) % len(poly)]
        if (y1 > y) != (y2 > y) and x < (x2 - x1) * (y - y1) / (y2 - y1) + x1:
            count = not count
    return count


def raster_vector(size: int) -> list[list[tuple[int, int, int, int]]]:
    samples = 8
    solid = [[ORANGE for _ in range(size)] for _ in range(size)]
    for y in range(size):
        for x in range(size):
            hit = 0
            for sy in range(samples):
                for sx in range(samples):
                    px = (x + (sx + 0.5) / samples) * FRAME / size
                    py = (y + (sy + 0.5) / samples) * FRAME / size
                    if inside(px, py, L_PATH) or inside(px, py, W_PATH):
                        hit += 1
            if hit >= 0.42 * samples * samples:
                solid[y][x] = PAPER

    def paper(x: int, y: int) -> bool:
        return 0 <= x < size and 0 <= y < size and solid[y][x] == PAPER

    for y in range(size):
        for x in range(size):
            if solid[y][x] == ORANGE and all(paper(x + dx, y + dy) for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1))):
                solid[y][x] = PAPER
    return solid


def scale_exact(src: list[list[tuple[int, int, int, int]]], size: int) -> list[list[tuple[int, int, int, int]]]:
    """Area-average src onto size×size. Integer math so C# can match it."""
    n = len(src)
    if size == n:
        return [list(row) for row in src]
    out: list[list[tuple[int, int, int, int]]] = []
    for y in range(size):
        y_start = y * n
        y_end = (y + 1) * n
        row: list[tuple[int, int, int, int]] = []
        for x in range(size):
            x_start = x * n
            x_end = (x + 1) * n
            paper = 0
            iy0 = y_start // size
            iy1 = (y_end + size - 1) // size
            ix0 = x_start // size
            ix1 = (x_end + size - 1) // size
            for iy in range(iy0, min(n, iy1)):
                top = y_start if y_start > iy * size else iy * size
                bot = y_end if y_end < (iy + 1) * size else (iy + 1) * size
                ov_y = bot - top
                if ov_y <= 0:
                    continue
                for ix in range(ix0, min(n, ix1)):
                    left = x_start if x_start > ix * size else ix * size
                    right = x_end if x_end < (ix + 1) * size else (ix + 1) * size
                    ov_x = right - left
                    if ov_x > 0 and src[iy][ix] == PAPER:
                        paper += ov_x * ov_y
            row.append(PAPER if paper * 2 >= n * n else ORANGE)
        out.append(row)
    return out


def dib(pixels: list[list[tuple[int, int, int, int]]]) -> bytes:
    height = len(pixels)
    width = len(pixels[0])
    xor_rows = bytearray()
    for y in range(height - 1, -1, -1):
        for r, g, b, a in pixels[y]:
            xor_rows.extend((b, g, r, a))
    mask_stride = ((width + 31) // 32) * 4
    mask = bytearray(mask_stride * height)
    header = struct.pack(
        "<IiiHHIIiiII",
        40,
        width,
        height * 2,
        1,
        32,
        0,
        len(xor_rows) + len(mask),
        0,
        0,
        0,
        0,
    )
    return header + bytes(xor_rows) + mask


def write_ico(path: Path, frames: dict[int, list[list[tuple[int, int, int, int]]]]) -> None:
    blobs = [(size, dib(frames[size])) for size in SIZES]
    out = bytearray(struct.pack("<HHH", 0, 1, len(blobs)))
    offset = 6 + 16 * len(blobs)
    directory = bytearray()
    body = bytearray()
    for size, blob in blobs:
        entry = 0 if size >= 256 else size
        directory += struct.pack("<BBBBHHII", entry, entry, 0, 0, 1, 32, len(blob), offset)
        offset += len(blob)
        body += blob
    path.write_bytes(bytes(out + directory + body))


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


def read_png(path: Path) -> list[list[tuple[int, int, int, int]]]:
    data = path.read_bytes()
    pos = 8
    idat = b""
    width = height = 0
    while pos < len(data):
        length = struct.unpack(">I", data[pos : pos + 4])[0]
        kind = data[pos + 4 : pos + 8]
        chunk = data[pos + 8 : pos + 8 + length]
        pos += 12 + length
        if kind == b"IHDR":
            width, height = struct.unpack(">II", chunk[:8])
        elif kind == b"IDAT":
            idat += chunk
        elif kind == b"IEND":
            break
    raw = zlib.decompress(idat)
    stride = width * 4
    rows: list[bytearray] = []
    prev = bytearray(stride)
    index = 0

    def paeth(a: int, b: int, c: int) -> int:
        estimate = a + b - c
        pa, pb, pc = abs(estimate - a), abs(estimate - b), abs(estimate - c)
        if pa <= pb and pa <= pc:
            return a
        return b if pb <= pc else c

    for _ in range(height):
        filt = raw[index]
        index += 1
        row = bytearray(raw[index : index + stride])
        index += stride
        if filt == 1:
            for x in range(stride):
                row[x] = (row[x] + (row[x - 4] if x >= 4 else 0)) & 255
        elif filt == 2:
            for x in range(stride):
                row[x] = (row[x] + prev[x]) & 255
        elif filt == 3:
            for x in range(stride):
                left = row[x - 4] if x >= 4 else 0
                row[x] = (row[x] + ((left + prev[x]) // 2)) & 255
        elif filt == 4:
            for x in range(stride):
                a = row[x - 4] if x >= 4 else 0
                b = prev[x]
                c = prev[x - 4] if x >= 4 else 0
                row[x] = (row[x] + paeth(a, b, c)) & 255
        elif filt != 0:
            raise SystemExit(f"unsupported png filter {filt}")
        rows.append(row)
        prev = row
    image: list[list[tuple[int, int, int, int]]] = []
    for y in range(height):
        image.append([tuple(rows[y][x * 4 : x * 4 + 4]) for x in range(width)])  # type: ignore[misc]
    return image


def scale(pixels: list[list[tuple[int, int, int, int]]], factor: int) -> list[list[tuple[int, int, int, int]]]:
    out: list[list[tuple[int, int, int, int]]] = []
    for row in pixels:
        for _ in range(factor):
            line: list[tuple[int, int, int, int]] = []
            for pixel in row:
                line.extend([pixel] * factor)
            out.append(line)
    return out


def side_by_side(ours: list[list[tuple[int, int, int, int]]]) -> None:
    if not REFERENCE.exists():
        return
    ref = read_png(REFERENCE)
    tile = 276
    gap = 24
    width = tile * 2 + gap * 3
    height = tile + gap * 2
    bg = (230, 226, 216, 255)
    canvas = [[bg for _ in range(width)] for _ in range(height)]

    def blit(image: list[list[tuple[int, int, int, int]]], left: int) -> None:
        src = len(image)
        for y in range(tile):
            for x in range(tile):
                sy = min(src - 1, y * src // tile)
                sx = min(src - 1, x * src // tile)
                canvas[gap + y][left + x] = image[sy][sx]

    blit(ref, gap)
    blit(ours, gap * 2 + tile)
    write_png(ART / "icon-reference-compare.png", canvas)


def main() -> None:
    master = raster_vector(256)
    frames = {size: master if size == 256 else scale_exact(master, size) for size in SIZES}
    write_ico(ICO, frames)
    write_png(TOAST, frames[256])
    for size in (16, 20, 24, 32, 48, 96, 256):
        write_png(ART / f"icon-{size}.png", frames[size])
    side_by_side(raster_vector(92))
    # A contact sheet at a readable scale, for a quick look.
    shown = ((16, 10), (20, 8), (24, 6), (32, 5), (48, 4), (96, 2))
    gap = 16
    tile = 192
    width = gap + (tile + gap) * len(shown)
    height = gap + 16 + tile + gap
    bg = (230, 226, 216, 255)
    sheet = [[bg for _ in range(width)] for _ in range(height)]
    for index, (size, factor) in enumerate(shown):
        scaled = scale(frames[size], factor)
        left = gap + index * (tile + gap) + (tile - len(scaled[0])) // 2
        top = gap + 16 + (tile - len(scaled)) // 2
        for y, row in enumerate(scaled):
            for x, pixel in enumerate(row):
                sheet[top + y][left + x] = pixel
    write_png(ART / "lux-ticker-icon-preview.png", sheet)
    print(f"ico {ICO} ({ICO.stat().st_size} bytes)")


if __name__ == "__main__":
    main()
