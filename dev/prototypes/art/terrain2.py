"""Art-direction test: paint the prototype valley as one continuous image.
32 px per tile, shown at 2x. Light from the top-left. Small fixed palette."""
import json, sys
import numpy as np
from PIL import Image

M = json.load(open(sys.argv[1]))
W, H, TER = M['W'], M['H'], M['terrain']
P = 32
IW, IH = W * P, H * P
rng = np.random.default_rng(7)

def hexc(s): return tuple(int(s[i:i+2], 16) for i in (1, 3, 5))
C = {k: hexc(v) for k, v in dict(
    s0='#f4f6f4', s1='#e3eaec', s2='#ccd7dd', s3='#b0c0ca', s4='#8fa3b2', s5='#6c8193',
    i0='#cfe4ea', i1='#aecfda', i2='#89b3c4', i3='#5f8ea3', i4='#3f6f86',
    r0='#b6b8b5', r1='#8a8d8d', r2='#646a6e', r3='#454b52', r4='#2b3037',
    o='#132420', d='#1d362e', m='#2b4c40', l='#3f6a56', h='#5d8a6f', t='#3b2b20',
    w0='#9a8a63', w1='#6e5f40', w2='#c2a96e', mud='#8d9897',
).items()}

# ---- smooth periodic-free value noise
def vnoise(scale, seed):
    g = np.random.default_rng(seed).random((IH // scale + 3, IW // scale + 3))
    ys, xs = np.mgrid[0:IH, 0:IW] / scale
    x0, y0 = xs.astype(int), ys.astype(int)
    fx, fy = xs - x0, ys - y0
    fx, fy = fx * fx * (3 - 2 * fx), fy * fy * (3 - 2 * fy)
    a, b, c, d = g[y0, x0], g[y0, x0 + 1], g[y0 + 1, x0], g[y0 + 1, x0 + 1]
    return a + (b - a) * fx + (c - a) * fy + (a - b - c + d) * fx * fy

# ---- organic borders: sample terrain through a displaced lookup
dx = (vnoise(9, 1) - .5) * 14 + (vnoise(3, 2) - .5) * 4
dy = (vnoise(9, 3) - .5) * 14 + (vnoise(3, 4) - .5) * 4
ys, xs = np.mgrid[0:IH, 0:IW]
tx = np.clip(((xs + dx) // P).astype(int), 0, W - 1)
ty = np.clip(((ys + dy) // P).astype(int), 0, H - 1)
names = ['plain', 'clearing', 'forest', 'hills', 'rock', 'mountain', 'water', 'marsh']
grid = np.array([[names.index(t) for t in row] for row in TER])
tp = grid[ty, tx]                       # terrain per pixel
# river: one continuous channel through each row's water tile, not tile blobs
water = np.zeros((IH, IW), bool)
centers = []
for y in range(H):
    xs_w = [x for x in range(W) if TER[y][x] == 'water']
    if xs_w: centers.append((xs_w[0] * P + P / 2, y * P + P / 2))
wob = (vnoise(6, 40) - .5) * 8
for (x0, y0), (x1, y1) in zip(centers, centers[1:]):
    for s in np.linspace(0, 1, 160):
        cx, cy = x0 + (x1 - x0) * s, y0 + (y1 - y0) * s
        iy = int(min(cy, IH - 1)); cx += wob[iy, int(cx)]
        r = 6 + 2 * vnoise(12, 41)[iy, int(cx)]
        y_lo, y_hi = int(max(cy - r, 0)), int(min(cy + r + 1, IH))
        x_lo, x_hi = int(max(cx - r, 0)), int(min(cx + r + 1, IW))
        yy, xx = np.mgrid[y_lo:y_hi, x_lo:x_hi]
        water[y_lo:y_hi, x_lo:x_hi] |= (yy - cy) ** 2 + (xx - cx) ** 2 <= r * r
tp = np.where((tp == 6) & ~water, 7, tp)   # leftover water tiles read as marsh
tp = np.where(water, 6, tp)

# ---- heightfield -> slope shading for relief everywhere
def box(a, k):
    for ax in (0, 1):
        p = np.pad(a, [(k + 1, k) if i == ax else (0, 0) for i in (0, 1)], mode='edge')
        c = np.cumsum(p, axis=ax)
        n = c.shape[ax]
        a = (np.take(c, range(2 * k + 1, n), axis=ax) - np.take(c, range(0, n - 2 * k - 1), axis=ax)) / (2 * k + 1)
    return a
def blur(a, k): return box(box(a, k), k)
elev = np.array([0, 0, .05, .55, .3, 1.4, -.15, -.05])[tp]
e = blur(elev, 9)
amp = blur(np.array([.10, .06, .1, .5, .35, 1.0, 0, .05])[tp], 9)
ridge = vnoise(22, 5) * .6 + vnoise(11, 7) * .4
mtn = blur((tp == 5).astype(float), 9)
ridge = ridge * (1 - mtn) + (1 - np.abs(vnoise(30, 8) - .5) * 2) * .7 * mtn + vnoise(9, 9) * .3 * mtn
e = e + (ridge - .5) * amp + (vnoise(5, 6) - .5) * amp * .15
gy, gx = np.gradient(e)
shade = -(gx + gy) * 18                 # light from top-left: + is lit

img = np.zeros((IH, IW, 3), np.uint8)
bayer = np.array([[0, 8, 2, 10], [12, 4, 14, 6], [3, 11, 1, 9], [15, 7, 13, 5]]) / 16
dither = np.random.default_rng(3).random((IH, IW)) * .5 + vnoise(2, 30) * .5

def ramp(keys, v):
    """v in 0..len-1, ordered dither between steps"""
    v = np.clip(v, 0, len(keys) - 1)
    lo = np.floor(v).astype(int); f = v - lo
    idx = np.clip(lo + (f > dither), 0, len(keys) - 1)
    pal = np.array([C[k] for k in keys])
    return pal[idx]

snow = ['s0', 's1', 's2', 's3', 's4', 's5']
grain = (vnoise(5, 8) - .5) * .5
streak = np.sin((xs * .55 + ys * .9) + vnoise(14, 9) * 9) > .93   # wind ripples
base_snow = 1.3 - shade * 1.6 + grain
img[:] = ramp(snow, base_snow)
img[streak & (shade > -.02) & np.isin(tp, [0, 1, 3])] = C['s0']

rockish = np.isin(tp, [4, 5])
rv = 2.2 - shade * 2.6 + (vnoise(8, 10) - .5) * 1.0 + (vnoise(3, 17) - .5) * .4
rock = ramp(['r0', 'r1', 'r2', 'r3', 'r4'], rv)
# snow lies on the lit, flatter faces of rock
snowcap = (shade > .03 + (vnoise(10, 11) - .5) * .3) | ((tp == 4) & (vnoise(7, 12) > .45))
mask = rockish & ~snowcap
img[mask] = rock[mask]

iv = 1.2 + (vnoise(16, 13) - .5) * 1.6 + (vnoise(4, 14) - .5) * .5
ice = ramp(['i0', 'i1', 'i2', 'i3'], iv)
img[water] = ice[water]
crack = water & (np.abs(vnoise(10, 15) - .5) < .012)
img[crack] = C['i4']
def shifted(a, sy, sx):
    return np.roll(np.roll(a, sy, 0), sx, 1)
land = ~water
rim = water & (shifted(land, 1, 0) | shifted(land, 0, 1) | shifted(land, -1, 0) | shifted(land, 0, -1))
img[rim] = C['s4']
rim2 = water & ~rim & (shifted(land, 2, 0) | shifted(land, 0, 2))
img[rim2] = C['i2']

wet = (tp == 7) & (vnoise(5, 16) > .58)
img[wet] = C['mud']

# ---------------------------------------------------------------- sprites
SPRUCE_L = [
    ".....o.....",
    "....oso....",
    "....sld....",
    "...osmdo...",
    "...ssldd...",
    "..olmmmdo..",
    "..sssldd...",
    ".olmmmmddo.",
    ".sssslmddd.",
    "olmmmmmdddo",
    "sssslmmmddd",
    ".oddddddoo.",
    "....oto....",
    ".....t.....",
]
SPRUCE_M = [
    "....o....",
    "...oso...",
    "...sld...",
    "..osmdo..",
    "..sslmd..",
    ".olmmddo.",
    ".ssslmdd.",
    "olmmmdddo",
    ".oddddoo.",
    "....t....",
]
SPRUCE_S = [
    "...o...",
    "..oso..",
    "..sld..",
    ".osmdo.",
    ".sslmd.",
    "olmmddo",
    ".odddo.",
    "...t...",
]
BOULDER = [
    "..sss...",
    ".ssss0o.",
    "s1112oo.",
    "o12233o.",
    ".o3344o.",
    "..oooo..",
]
SPAL = dict(o=C['o'], s=C['s0'], l=C['h'], m=C['m'], d=C['d'], t=C['t'])
BPAL = {'s': C['s0'], '0': C['s2'], '1': C['r0'], '2': C['r1'], '3': C['r2'], '4': C['r3'], 'o': C['r4']}

objs = []
def scatter(n_per_tile, kinds, where):
    for ty_ in range(H):
        for tx_ in range(W):
            for _ in range(rng.poisson(n_per_tile)):
                x = int(tx_ * P + rng.random() * P); y = int(ty_ * P + rng.random() * P)
                if where(tp[y, x], x, y):
                    objs.append((y, x, kinds[rng.integers(len(kinds))]))
dens = vnoise(7, 20)
scatter(26, 'LLMMS', lambda t, x, y: t == 2 and dens[y, x] > .22)
scatter(3, 'MSS', lambda t, x, y: t == 1 and dens[y, x] > .55)
scatter(.7, 'SM', lambda t, x, y: t in (0, 3) and dens[y, x] > .7)
scatter(5, 'B', lambda t, x, y: t == 4)
scatter(1.5, 'B', lambda t, x, y: t in (3, 5) and shade[y, x] < .05)
scatter(30, 'R', lambda t, x, y: t == 7 and not wet[y, x])
objs.sort()

def blit(spr, pal, cx, fy, shadow=True):
    h_, w_ = len(spr), len(spr[0])
    x0, y0 = cx - w_ // 2, fy - h_ + 1
    if shadow:
        for yy in range(2):
            for xx in range(w_ // 2 + 2):
                X, Y = cx + xx - 1, fy + yy
                if 0 <= X < IW and 0 <= Y < IH and not rockish[Y, X]:
                    img[Y, X] = C['s4'] if yy == 0 else C['s3']
    for j, row in enumerate(spr):
        for i, ch in enumerate(row):
            if ch == '.': continue
            X, Y = x0 + i, y0 + j
            if 0 <= X < IW and 0 <= Y < IH:
                img[Y, X] = pal[ch]

for fy, x, k in objs:
    if k == 'L': blit(SPRUCE_L, SPAL, x, fy)
    elif k == 'M': blit(SPRUCE_M, SPAL, x, fy)
    elif k == 'S': blit(SPRUCE_S, SPAL, x, fy)
    elif k == 'B': blit(BOULDER, BPAL, x, fy, shadow=False)
    elif k == 'R':
        hgt = 2 + rng.integers(3)
        for j in range(hgt):
            if fy - j >= 0: img[fy - j, x] = C['w1'] if j < hgt - 1 else C['w2']

out = Image.fromarray(img).resize((IW * 2, IH * 2), Image.NEAREST)
out.save(sys.argv[2])
print('ok', out.size)
