"""Terrain layers for the rebuild's world-space map renderer (rebuild/Web/App/wwwroot/terrain.js).
Run with Python 3; no dependencies. Writes PNGs to assets/icons/terrain/ and sources to
assets/pixelart/terrain/.

The renderer draws a cell as: ground texture, then mud and snow through world-noise masks, then
water and ice, mountains, and sprites placed on a world grid. Tiles only supply texture, so this
script paints:
  ground/<terrain>_<n>_<palette>   bare ground per terrain, 4 variants, one per palette;
  snow/full_<flavour>_<n>          full snow cover, lit by the same height field as the ground
                                   (flat; rugged for hills; crag for rock, which keeps bare faces);
  sprites/<kind>_<palette>         spruce, shrub, boulder and reed sprites.
Palettes share one pixel grid per layer, so a season is a palette swap. Only the winter palette is
emitted for now; the summer and autumn colours are kept for when those states are drawn.
Every 96px layer is periodic; variants differ only in the faded interior.
"""
from pathlib import Path
import importlib.util
import math
import random
import struct
import zlib

HERE = Path(__file__).resolve().parent
ROOT = HERE.parent
_spec = importlib.util.spec_from_file_location('bt', ROOT / 'scripts/build-terrain.py')
bt = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(bt)
pnoise, SPRUCE, SIZE = bt.pnoise, bt.SPRUCE, bt.SIZE
VARIANTS = 4
TERRAINS = ('forest', 'clearing', 'plain', 'hills', 'rock', 'marsh', 'water', 'deepwater')
AMP = dict(plain=.12, clearing=.08, forest=.1, hills=.4, rock=.45, marsh=.05, water=0, deepwater=0)
FLAVOUR = dict(hills='rugged', rock='crag')
FLAVOUR_AMP = dict(flat=.1, rugged=.4, crag=.45)
SCOUR = dict(flat=None, rugged=-.16, crag=-.02)

FIXED = dict(bt.OUTDOOR)
FIXED.update(ti0='#9db4bc', ti1='#7f9aa6', ti2='#647f8d', ti3='#4f6a7a',
             rw1='#6e5f40', rw2='#c2a96e')
SEASONS = {
    'summer': dict(
        g0='#bcc28c', g1='#a1ad76', g2='#879661', g3='#6e7e4f', g4='#576640', g5='#404d32',
        n0='#6f6d4a', n1='#5a5a3e', n2='#474932', n3='#353a28',
        e0='#9a8763', e1='#7e6b4d', e2='#64533e', e3='#463a2c',
        fl1='#e4e2c8', fl2='#b98aa3', li='#a9a670', sh='#323d2b',
        b0='#86a05c', b1='#637f46', b2='#465d35', rd1='#5f7140', rd2='#97a05e',
        wa0='#8aaeb3', wa1='#65909b', wa2='#4a7682', wa3='#385f6c', wg='#d3e3e2',
        dw0='#3f6a7a', dw1='#305668', dw2='#264759', dw3='#1c3748'),
    'autumn': dict(
        g0='#cfc08f', g1='#b9a674', g2='#9e8a5c', g3='#816f4a', g4='#64563b', g5='#473e2d',
        n0='#766a48', n1='#61563b', n2='#4c442f', n3='#383224',
        e0='#9a8763', e1='#7e6b4d', e2='#64533e', e3='#463a2c',
        fl1='#e6c56c', fl2='#b4522e', li='#bf9a52', sh='#3a3126',
        b0='#e4ac40', b1='#c3772e', b2='#8c4c25', rd1='#8d7a4c', rd2='#c8ad72',
        wa0='#80a0a8', wa1='#5c838e', wa2='#446a76', wa3='#335562',  wg='#c9d8d8',
        dw0='#385e6e', dw1='#2b4d5e', dw2='#224050', dw3='#19323f'),
}
# Winter ground is last autumn's: dead grass and needles, seen where wind scours the snow.
SEASONS['winter'] = dict(SEASONS['autumn'])
OVERLAY_BAND = 12
SYMBOLS = 'ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789'


def color(name, season):
    return SEASONS[season].get(name) or FIXED[name]


def hash01(x, y, salt):
    h = (x * 374761393 + y * 668265263 + salt * 982451653) & 0xffffffff
    h = ((h ^ (h >> 13)) * 1274126177) & 0xffffffff
    return (h ^ (h >> 16)) / 4294967296


class Canvas:
    """Lighting and noise shared by every layer of one terrain flavour and variant."""

    def __init__(self, amp, variant, band=34):
        S = SIZE
        self.v, self.band = variant, 34       # relief always matches the ground it lights
        # Rugged ground gets per-variant relief; a shared one repeats visibly every source tile.
        n = self.field if amp > .3 else (lambda x, y, c, seed: pnoise(x, y, c, seed))
        hf = [[(n(x, y, 3, 1) * .6 + n(x, y, 6, 2) * .3 + n(x, y, 12, 3) * .1) * amp
               for x in range(S)] for y in range(S)]
        self.shade = [[-(hf[y][(x + 1) % S] - hf[y][x - 1] + hf[(y + 1) % S][x] - hf[y - 1][x]) * 9
                       for x in range(S)] for y in range(S)]
        self.band = band

    def fade(self, x, y):
        t = min(1., max(0., (min(x, y, SIZE - 1 - x, SIZE - 1 - y) - 3) / self.band))
        return t * t * (3 - 2 * t)

    def field(self, x, y, cells, seed):
        f = self.fade(x, y)
        return pnoise(x, y, cells, seed) * (1 - f) + pnoise(x, y, cells, seed + 100 * (self.v + 1)) * f

    @staticmethod
    def ramp(names, value, x, y, salt=0):
        t = hash01(x, y, 91 + salt) * .5 + pnoise(x, y, 48, 6) * .5
        value = max(0., min(len(names) - 1., value))
        lo = math.floor(value)
        return names[min(len(names) - 1, lo + (1 if value - lo > t else 0))]


G = ['g0', 'g1', 'g2', 'g3', 'g4', 'g5']
N = ['n0', 'n1', 'n2', 'n3']
E = ['e0', 'e1', 'e2', 'e3']
R = ['r0', 'r1', 'r2', 'r3', 'r4']
WA = ['wa0', 'wa1', 'wa2', 'wa3']
DW = ['dw0', 'dw1', 'dw2', 'dw3']
SNOW = ['s0', 's1', 's2', 's3', 's4', 's5']


def blank():
    return [[None] * SIZE for _ in range(SIZE)]


def ground(terrain, variant):
    cv, px = Canvas(AMP[terrain], variant), blank()
    for y in range(SIZE):
        for x in range(SIZE):
            u, v = x / SIZE * math.tau, y / SIZE * math.tau
            s = cv.shade[y][x]
            grain = (pnoise(x, y, 19, 7) - .5) * .5
            if terrain in ('plain', 'clearing', 'hills', 'marsh'):
                lit = 1.9 - s * 2.4 + grain - (.4 if terrain == 'clearing' else 0)
                c = cv.ramp(G, lit, x, y)
                if s > -.02 and math.sin(u * 8 + v * 13 + pnoise(x, y, 6, 8) * 9) > .95:
                    c = G[max(0, G.index(c) - 1)]
                tuft = hash01(x, y, 31)
                if tuft < .07 and terrain != 'marsh': c = G[min(5, G.index(c) + 2)]
                elif tuft < .1 and terrain != 'marsh' and y > 0: c = G[max(0, G.index(c) - 1)]
                bloom = (.02 if terrain == 'clearing' else .008) * cv.field(x, y, 5, 32) * 2
                if terrain != 'marsh' and hash01(x, y, 33) < bloom:
                    c = 'fl1' if hash01(x, y, 34) < .6 else 'fl2'
                if terrain == 'hills' and s < -.13 + (pnoise(x, y, 10, 11) - .5) * .3:
                    c = cv.ramp(R, 2.4 - s * 2.6 + (pnoise(x, y, 12, 13) - .5), x, y, 2)
                if terrain == 'marsh':
                    pool = cv.field(x, y, 12, 21) + (pnoise(x, y, 30, 22) - .5) * .12
                    if pool > .6:
                        c = cv.ramp(WA, 1.3 + (pool - .6) * 6 + grain, x, y, 3)
                        if math.sin(u * 3 + v * 11 + pnoise(x, y, 6, 20) * 6) > .96: c = 'wg'
                    elif pool > .55:
                        c = 'e3' if hash01(x, y, 35) < .6 else 'e2'
                    elif hash01(x, y, 36) < .05:
                        c = 'g0'
            elif terrain == 'forest':
                c = cv.ramp(N, 1.5 - s * 2 + grain, x, y)
                moss = cv.field(x, y, 8, 31) + (pnoise(x, y, 28, 37) - .5) * .15
                if moss > .6: c = cv.ramp(G, 3.3 - s * 2 + grain, x, y, 4)
                if hash01(x, y, 38) < .012: c = 'n0'
            elif terrain == 'rock':
                rv = 2.2 - s * 2.6 + (pnoise(x, y, 12, 13) - .5) + (pnoise(x, y, 32, 14) - .5) * .4
                c = cv.ramp(R, rv, x, y)
                hollow = cv.field(x, y, 14, 12)
                if hollow > .6 and s > -.05: c = cv.ramp(G, 2.5 - s * 2 + grain, x, y, 5)
                elif rv < 2 and hash01(x, y, 39) < .05: c = 'li'
            elif terrain == 'water':
                wv = 1.4 + (pnoise(x, y, 6, 15) - .5) * 1.4 + (pnoise(x, y, 24, 16) - .5) * .5
                c = cv.ramp(WA, wv, x, y)
                if math.sin(u * 3 + v * 11 + pnoise(x, y, 6, 20) * 6) > .985 and hash01(x, y, 70) < .7: c = 'wg'
                elif math.sin(u * 5 - v * 7 + pnoise(x, y, 8, 23) * 5) > .985: c = 'wa0'
            elif terrain == 'deepwater':
                dv = 1.5 + (pnoise(x, y, 4, 18) - .5) * 1.4 + (pnoise(x, y, 16, 19) - .5) * .6
                c = cv.ramp(DW, dv, x, y)
                if math.sin(u * 3 + v * 11 + pnoise(x, y, 6, 20) * 6) > .97: c = 'wa2'
            px[y][x] = c
    return px


SHRUB = ['.aab.', 'aabbc', 'abbcc', '.ccc.']
SHRUB_PAL = dict(a='b0', b='b1', c='b2')
SHRUB_SNOW = ['x.t.x', '.xtx.', 'sssss', '.ooo.']
SHRUB_SNOW_PAL = dict(x='tt', t='r3', s='s0', o='s3')
BOULDER = bt.BOULDER
BOULDER_BARE = dict(bt.BOULDER_PAL, s='r0')
BOULDER_BARE['0'] = 'li'


def snow(flavour, variant):
    """Full snow cover. Rugged and crag flavours leave steep shaded faces bare."""
    cv, px = Canvas(FLAVOUR_AMP[flavour], variant, OVERLAY_BAND), blank()
    for y in range(SIZE):
        for x in range(SIZE):
            u, v = x / SIZE * math.tau, y / SIZE * math.tau
            s = cv.shade[y][x]
            grain = (pnoise(x, y, 19, 7) - .5) * .5
            tone = cv.ramp(SNOW, 1.3 - s * 1.6 + grain, x, y)
            if s > -.02 and math.sin(u * 8 + v * 13 + pnoise(x, y, 6, 8) * 9) > .93: tone = 's0'
            scoured = SCOUR[flavour] is not None and s < SCOUR[flavour] + (pnoise(x, y, 10, 11) - .5) * .3
            if flavour == 'crag': scoured = scoured and pnoise(x, y, 14, 12) < .5
            px[y][x] = None if scoured else tone
    return px


def png(path, names, season):
    rgb = {}
    def get(n):
        if n not in rgb: rgb[n] = b'\0\0\0\0' if n is None else bytes.fromhex(color(n, season)[1:]) + b'\xff'
        return rgb[n]
    raw = b''.join(b'\0' + b''.join(get(n) for n in row) for row in names)
    def chunk(kind, data):
        return struct.pack('!I', len(data)) + kind + data + struct.pack('!I', zlib.crc32(kind + data))
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(b'\x89PNG\r\n\x1a\n' + chunk(b'IHDR', struct.pack('!2I5B', len(names[0]), len(names), 8, 6, 0, 0, 0))
                     + chunk(b'IDAT', zlib.compress(raw, 9)) + chunk(b'IEND', b''))


def pxa(path, names, season):
    used = sorted({n for row in names for n in row if n is not None})
    symbol = {n: SYMBOLS[i] for i, n in enumerate(used)}
    symbol[None] = '.'
    out = '// Generated by scripts/build-terrain-layers.py.\n'
    out += f'SIZE {len(names[0])}x{len(names)}\nPALETTE\n'
    out += ''.join(f'{symbol[n]} = {color(n, season)}\n' for n in used)
    out += 'PIXELS\n' + '\n'.join(''.join(symbol[n] for n in row) for row in names) + '\n'
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(out)


# Sprites keyed by the renderer's kind names. A snowed sprite is its own grid (snow on the boughs,
# twigs over a drift); bare and autumn sprites are one grid with two palettes.
def sprites(snowed):
    spruce_pal = dict(bt.SPRUCE_PAL, s='s0' if snowed else 'tl')
    out = {f'spruce_{k.lower()}': (rows, spruce_pal) for k, rows in SPRUCE.items()}
    out['shrub'] = (SHRUB_SNOW, SHRUB_SNOW_PAL) if snowed else (SHRUB, SHRUB_PAL)
    out['boulder'] = (BOULDER, bt.BOULDER_PAL if snowed else BOULDER_BARE)
    for h in (2, 3, 4):
        lo, hi = ('rw1', 'rw2') if snowed else ('rd1', 'rd2')
        out[f'reed_{h}'] = (['h'] + ['l'] * (h - 1), dict(h=hi, l=lo))
    return {k: [[None if ch == '.' else pal[ch] for ch in row] for row in rows] for k, (rows, pal) in out.items()}


def write(rel, names, palette):
    png(ROOT / 'assets/icons/terrain' / f'{rel}.png', names, palette)
    pxa(ROOT / 'assets/pixelart/terrain' / f'{rel}.pxa', names, palette)


def main():
    palette = 'winter'
    for v in range(VARIANTS):
        n = v + 1
        for t in TERRAINS:
            write(f'ground/{t}_{n}_{palette}', ground(t, v), palette)
        for flavour in ('flat', 'rugged', 'crag'):
            write(f'snow/full_{flavour}_{n}', snow(flavour, v), palette)
    for kind, names in sprites(snowed=True).items():
        write(f'sprites/{kind}_{palette}', names, palette)
    print('wrote', len(list((ROOT / 'assets/icons/terrain').rglob('*.png'))), 'terrain layers')


if __name__ == '__main__':
    main()
