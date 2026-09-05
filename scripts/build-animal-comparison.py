"""Build a labeled comparison from rendered PNGs. Requires Pillow; never alters sprites."""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[1]
NEW = ROOT / 'assets/icons/animal_candidates'
EXISTING = ROOT / 'assets/previews/animal-originals'
OUT = ROOT / 'assets/previews/animal-comparison.png'
names = sorted(path.stem for path in NEW.glob('*.png'))

def font(size):
    try:
        return ImageFont.truetype('/System/Library/Fonts/Supplemental/Arial.ttf', size)
    except OSError:
        return ImageFont.load_default(size=size)

rows = (len(names) + 1) // 2
im = Image.new('RGB', (744, 94 + rows * 152 + 34), '#20252c')
draw = ImageDraw.Draw(im)
draw.text((24, 16), 'ANIMAL ICONS  /  NEW vs EXISTING', font=font(24), fill='#ece4d4')
draw.text((24, 49), '16 × 16 sprites shown at 6× • Original files preserved', font=font(15), fill='#b4b9bd')
pretty = {'cavebear': 'Cave bear', 'sabertooth': 'Sabertooth', 'megaloceros': 'Megaloceros'}
for index, name in enumerate(names):
    col, row = divmod(index, rows)
    x, y = 16 + col * 366, 88 + row * 152
    draw.rounded_rectangle((x, y, x+348, y+142), radius=8, fill='#303740')
    draw.text((x+12, y+9), pretty.get(name, name.capitalize()), font=font(18), fill='#ece4d4')
    for slot, (folder, title) in enumerate([(NEW, 'NEW'), (EXISTING, 'EXISTING')]):
        px, py = x+112+slot*118, y+34
        draw.text((px+48, y+11), title, anchor='mt', font=font(12), fill='#b4c9bd' if slot==0 else '#b4b9bd')
        draw.rectangle((px, py, px+95, py+95), fill='#60656b')
        source=Image.open(folder/f'{name}.png').convert('RGBA')
        scale=max(1,min(96//source.width,96//source.height))
        sprite=source.resize((source.width*scale,source.height*scale),Image.Resampling.NEAREST)
        im.paste(sprite,(px+(96-sprite.width)//2,py+(96-sprite.height)//2),sprite)
OUT.parent.mkdir(parents=True,exist_ok=True)
im.save(OUT)
print(OUT)
