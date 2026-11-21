import json, sys
from PIL import Image, ImageDraw, ImageFont
J = __import__('os').path.dirname(__import__('os').path.abspath(__file__)) + '/'
ICONS = J + '../../../assets/icons/'
M = json.load(open(J + 'map.json'))
TER = M['terrain']
TS = 64
X0, Y0, CW, CH = 5, 5, 15, 9          # crop in tiles
VAR = dict(forest=8, plain=8, clearing=8, rock=8, hills=8, mountain=8, water=8, marsh=8)

def h(x, y):
    v = (x * 374761393 + y * 668265263) & 0xffffffff
    v = ((v ^ (v >> 13)) * 1274126177) & 0xffffffff
    return v ^ (v >> 16)

old = Image.new('RGB', (CW * TS, CH * TS))
for j in range(CH):
    for i in range(CW):
        t = TER[Y0 + j][X0 + i]
        n = h(X0 + i, Y0 + j) % VAR[t] + 1
        im = Image.open(f'{ICONS}{t}_tile{"" if n == 1 else n}.png').convert('RGB').resize((TS, TS), Image.NEAREST)
        old.paste(im, (i * TS, j * TS))

full = Image.open(J + 'new_map.png')            # already 64 px per tile
new = full.crop((X0 * TS, Y0 * TS, (X0 + CW) * TS, (Y0 + CH) * TS))

def sprites(img):
    def put(name, tx, ty, s=3):
        sp = Image.open(ICONS + name).convert('RGBA')
        sp = sp.resize((sp.width * s, sp.height * s), Image.NEAREST)
        img.paste(sp, ((tx - X0) * TS + (TS - sp.width) // 2, (ty - Y0) * TS + (TS - sp.height) // 2), sp)
    put('fire.png', M['CAMP']['x'], M['CAMP']['y'])
    put('player.png', M['BIRCH']['x'], M['BIRCH']['y'])
    put('animals/wolf.png', 13, 6)
    return img

sprites(old); sprites(new)
gap = 24
out = Image.new('RGB', (old.width, old.height * 2 + gap * 3), (24, 26, 30))
out.paste(old, (0, gap)); out.paste(new, (0, old.height + gap * 3))
d = ImageDraw.Draw(out)
d.text((8, 5), 'CURRENT - 96px tiles, per-tile', fill=(220, 220, 220))
d.text((8, old.height + gap * 2 + 5), 'TEST - 32px art at 2x, painted as one continuous map', fill=(220, 220, 220))
out.save(J + 'compare.png')
print(out.size)
