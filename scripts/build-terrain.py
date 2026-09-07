"""Author 96px terrain sources and PNGs. Run with Python 3; no dependencies.

A tile covers approximately 100m. Trees have 4–9m crowns, rocks are small
outcrops, and relief is ground shading rather than an oversized mountain icon.
All variants share a periodic edge field and edge-crossing details. Only the
interior changes, so any variant can neighbor any other without cut-off objects.
"""
from pathlib import Path
import math
import random
import struct
import zlib

ROOT = Path(__file__).resolve().parents[1]
SIZE = 96
VARIANTS = 8
TERRAINS = ('forest', 'clearing', 'plain', 'hills', 'water', 'marsh', 'rock', 'mountain', 'deepwater', 'cavefloor')
PALETTE = [
    '#c3d2d9', '#cbd9df', '#d2dfe3', '#d9e4e7', '#e0e9eb', '#e7eeed', '#f1f4ef',
    '#263c36', '#355046', '#446254', '#5b7564', '#80958a',
    '#778589', '#8e9a9c', '#a6b1b2', '#bac3c1', '#627278',
    '#a3c0ca', '#afcbd3', '#bfd7dd', '#cee2e5', '#deedef',
    '#305b70', '#37677b', '#3d7082', '#467b8b', '#548897',
    '#7d8170', '#969883', '#b0af98', '#c2c4b2',
]
PALETTE += [
    '#303b3d', '#455355', '#59696c', '#6d7d7f', '#879697', '#a2aeab',
    '#353934', '#44473f', '#515248', '#5e5f51', '#6b6c5b', '#7c7c68',
    '#222b2c', '#10191b', '#00000000',
]
RGB = [bytes.fromhex(c[1:]) + (b'\xff' if len(c) == 7 else b'') for c in PALETTE]
SYMBOLS = 'ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789'


def png(path, pixels):
    h, w = len(pixels), len(pixels[0])
    def chunk(kind, data):
        return struct.pack('!I', len(data)) + kind + data + struct.pack('!I', zlib.crc32(kind + data))
    raw = b''.join(b'\0' + b''.join(RGB[c] for c in row) for row in pixels)
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(b'\x89PNG\r\n\x1a\n' + chunk(b'IHDR', struct.pack('!2I5B', w, h, 8, 6, 0, 0, 0))
                     + chunk(b'IDAT', zlib.compress(raw, 9)) + chunk(b'IEND', b''))


def make(terrain, variant):
    fields = {}
    for cells in (4, 8, 16):
        field_rng = random.Random(331 + cells)
        fields[cells] = [[field_rng.uniform(-1, 1) for _ in range(cells)] for _ in range(cells)]

    def noise(x, y, cells):
        px, py = x / SIZE * cells, y / SIZE * cells
        ix, iy = math.floor(px), math.floor(py)
        fx, fy = px-ix, py-iy
        fx, fy = fx*fx*(3-2*fx), fy*fy*(3-2*fy)
        grid = fields[cells]
        a = grid[iy % cells][ix % cells]*(1-fx) + grid[iy % cells][(ix+1) % cells]*fx
        b = grid[(iy+1) % cells][ix % cells]*(1-fx) + grid[(iy+1) % cells][(ix+1) % cells]*fx
        return a*(1-fy) + b*fy
    pixels = []
    for y in range(SIZE):
        row = []
        for x in range(SIZE):
            u, v = x / SIZE * math.tau, y / SIZE * math.tau
            # Periodic background: no directional light-to-dark bands at tile joins.
            common = noise(x, y, 4)*.8 + noise(x, y, 8)*.35 + noise(x, y, 16)*.15
            fade = min(1., max(0., (min(x, y, SIZE-1-x, SIZE-1-y) - 7) / 16))
            detail = noise(x+variant*17, y+variant*31, 4) * 1.5 * fade
            n = common + detail
            if terrain == 'deepwater':
                c = 24 + round(n)
                if math.sin(v * 13 + math.sin(u * 2)) > .985: c = 26
            elif terrain == 'water':
                c = 19 + round(n)
                if abs(noise(x, y, 8) + detail*.15) < .013: c = 21
            elif terrain == 'cavefloor':
                # Broad dry-earth patches: the same quantized field as outdoor art,
                # without its snow palette, grain filter, or per-pixel speckling.
                c = 38 + max(0, min(3, round(1 + n * 1.25)))
                if abs(noise(x, y, 4) + detail * .12) < .012 and noise(x, y, 8) < -.15:
                    c = 37
            elif terrain == 'mountain':
                ridge = common * 1.8 + detail
                c = 32 + max(0, min(4, round(1.5 + ridge * 1.4)))
                if ridge < -.85: c = 31
                # Small snow remnants on the highest shoulders, not a white base.
                if ridge > 1.85 and n > .8: c = 3 if ridge < 2.05 else 4
            elif terrain in ('hills', 'rock'):
                ridge = common * 1.8 + detail
                c = max(0, min(6, round(3.8 + n + ridge)))
            elif terrain == 'marsh':
                c = 2 + round(n)
                if n < -.3: c = 17 + max(0, round((n + 1) * 2))
            else:
                c = max(1, min(6, 4 + round(n)))
            row.append(c)
        pixels.append(row)

    def dot(x, y, c):
        pixels[y % SIZE][x % SIZE] = c

    def ellipse(x, y, rx, ry, c):
        for dy in range(-ry, ry+1):
            for dx in range(-rx, rx+1):
                if dx*dx/(rx*rx) + dy*dy/(ry*ry) <= 1: dot(x+dx, y+dy, c)

    def tree(x, y, r):
        ellipse(x+2, y+3, r+1, max(1, r//2), 1)
        dot(x, y+2, 16)
        dot(x, y+3, 16)
        # Upright pine silhouette, matching the character's oblique map view:
        # pointed tip, widening tiers of boughs, and a visible trunk below.
        top = y-2*r+1
        for tier in range(r):
            for dy in range(3):
                width = min(tier+1, dy+max(0, tier-1))
                for dx in range(-width, width+1):
                    dot(x+dx, top+tier*2+dy, 9 if dx < 0 else 8 if dx == 0 else 7)
            # Snow rests on the upper-left shoulder of each branch tier.
            for dx in range(-max(0, tier-1), 1):
                dot(x+dx, top+tier*2, 5)
        dot(x, top, 6)

    def stone(x, y, r):
        bare = terrain in ('mountain', 'cavefloor')
        shadow, body, top, glint, seam = (31, 33, 35, 36, 32) if bare else (1, 12, 14, 5, 16)
        if terrain == 'cavefloor': shadow, body, top, glint, seam = 37, 32, 34, 35, 31
        ellipse(x+1, y+2, r+1, max(1, r//2), shadow)
        ellipse(x, y, r, max(1, r-1), body)
        ellipse(x-1, y-1, max(1, r-1), max(1, r-2), top)
        for dx in range(-r+1, 1): dot(x+dx, y-r+1, glint)
        dot(x+r-1, y, seam)

    def reed(x, y, r):
        dot(x, y, 27)
        dot(x, y-1, 28)
        dot(x-1, y-2, 29)
        dot(x+1, y-2, 27)
        dot(x+1, y-3, 28)

    # Jittered cells prevent clumps from overlapping into oversized symbols.
    # Border cells use the same RNG across variants and wrap onto opposite edges.
    for gy in range(12):
        for gx in range(12):
            edge = gx in (0, 11) or gy in (0, 11)
            local = random.Random(937 + gy*613 + gx*103 + TERRAINS.index(terrain)*2111 + (0 if edge else variant*7919))
            x, y = gx*8 + local.randrange(2, 6), gy*8 + local.randrange(2, 6)
            roll = local.random()
            density = .60 if terrain == 'forest' else .065 if terrain == 'clearing' else .025 if terrain == 'hills' else 0
            if roll < density:
                tree(x, y, local.randrange(2, 5))
            elif terrain in ('rock', 'mountain', 'hills') and roll < (.50 if terrain == 'rock' else .22):
                stone(x, y, local.randrange(2, 5))
            elif terrain == 'cavefloor' and roll < .075:
                stone(x, y, local.randrange(1, 3))
            elif terrain == 'marsh' and roll < .45:
                reed(x, y, 1)
            elif terrain not in ('water', 'deepwater', 'cavefloor', 'mountain') and roll > .82:
                dot(x, y, 2)
                dot(x+1, y, 3)
                dot(x-1, y-1, 5)
    return pixels


def variant_index(x, y):
    # Same signed 32-bit hash as TileRenderer and PixelArtCli.
    def signed(n):
        n &= 0xffffffff
        return n if n < 0x80000000 else n - 0x100000000
    h = signed(x*73856093 ^ y*19349663)
    h = signed(h ^ (h >> 13))
    h = signed(h * 1274126177)
    h = signed(h ^ (h >> 16))
    return (h & 0xffffffff) % VARIANTS


def write_source(stem, pixels):
    source = '// Generated by scripts/build-terrain.py; native terrain pixel art.\n'
    source += f'SIZE {len(pixels[0])}x{len(pixels)}\nPALETTE\n'
    source += ''.join(f'{symbol} = {color}\n' for symbol, color in zip(SYMBOLS, PALETTE))
    source += 'PIXELS\n' + '\n'.join(''.join(SYMBOLS[c] for c in row) for row in pixels) + '\n'
    (ROOT / 'assets/pixelart' / f'{stem}.pxa').write_text(source)
    png(ROOT / 'assets/icons' / f'{stem}.png', pixels)


def entrance():
    # Facing outdoors (down). The roof/brow is above the boundary, in the cave.
    pixels = [[45] * 48 for _ in range(24)]
    def dot(x, y, c):
        if 0 <= x < 48 and 0 <= y < 24: pixels[y][x] = c
    def ellipse(x, y, rx, ry, c):
        for dy in range(-ry, ry+1):
            for dx in range(-rx, rx+1):
                if dx*dx/(rx*rx) + dy*dy/(ry*ry) <= 1: dot(x+dx, y+dy, c)
    # Same little outcrops and upper-left highlights as the terrain stone stamp.
    for x, y, r in [(6, 16, 5), (8, 10, 5), (13, 6, 5), (20, 4, 6),
                    (28, 5, 6), (35, 7, 5), (40, 12, 5), (41, 18, 4)]:
        ellipse(x+1, y+2, r+1, max(1, r-1), 31)
        ellipse(x, y, r, max(1, r-1), 33)
        ellipse(x-1, y-1, r-1, max(1, r-2), 35)
        for dx in range(-r+1, 1): dot(x+dx, y-r+1, 36)
        dot(x+r-1, y, 32)
    # Stepped recess under the brow, with unlit upper rock and an open threshold.
    for y in range(8, 21):
        inset = [11, 9, 8, 6, 5, 4, 4, 3, 3, 3, 3, 3, 3][y-8]
        for x in range(8+inset, 40-inset): dot(x, y, 44 if y < 15 else 43 if y < 18 else 37)
    for x, y in [(4, 22), (12, 22), (35, 22), (43, 21)]:
        dot(x, y, 33); dot(x+1, y, 34); dot(x, y-1, 35)
    return pixels


def main():
    import sys
    selected = sys.argv[1:] or list(TERRAINS) + ['entrance']
    for terrain in selected:
        if terrain == 'entrance':
            write_source('cave_entrance', entrance())
            continue
        if terrain not in TERRAINS: raise ValueError(f'Unknown terrain: {terrain}')
        for v in range(VARIANTS):
            write_source(f'{terrain}_tile{v+1 if v else ""}', make(terrain, v))
    print('Authored: ' + ', '.join(selected))


if __name__ == '__main__':
    main()
