"""Art-direction test: redraw the most-seen sprites and HUD icons in one palette.
16x16 grids, hand-authored. Writes PNGs to ./icons/ and a contact sheet."""
import os
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, 'icons')

PAL = {
    'k': '#1b2028',  # outline: cool near-black, shared by everything
    # fire
    'w': '#fff6d0', 'y': '#ffd34d', 'o': '#ff9433', 'r': '#e5502b', 'R': '#9c2a2c',
    # wood
    'L': '#c49460', 'M': '#8d5f37', 'D': '#5a3a22',
    # hide / fur / skin
    'F': '#ece0c6', 'H': '#c3a074', 'h': '#8c6a48', 'd': '#5b4330', 'S': '#e6b18c', 's': '#b67a5c',
    # snow / cool
    'A': '#f4f6f4', 'B': '#cad8df', 'C': '#8ea7b6', 'E': '#567386',
    # water
    'a': '#d4f2f6', 'b': '#72c3dc', 'c': '#3586ad', 'e': '#20557a',
    # stone / charcoal
    't': '#c3c0b8', 'T': '#827f7a', 'q': '#3a3836', 'Q': '#24221f',
    # berry, leaf
    'x': '#ff8f8f', 'X': '#d6364b', 'Z': '#86203a', 'g': '#9cc46a', 'G': '#4c8540',
    # gold (sun, warning)
    'u': '#fff0a8', 'U': '#f5c542', 'V': '#c98a1f',
    # moon
    'm': '#f1ead2', 'n': '#c7bc98', 'N': '#8c8466',
}

def mirror(left, shade=None):
    """Mirror an 8-wide left half; shade swaps colours on the right (light is top-left)."""
    rows = []
    for row in left:
        assert len(row) == 8, (row, len(row))
        right = row[::-1]
        if shade: right = ''.join(shade.get(ch, ch) for ch in right)
        rows.append(row + right)
    return rows

ICONS = {}

ICONS['campfire'] = [
    "................",
    "........R.......",
    ".......Rr.......",
    ".......Rro..R...",
    "......Rroo.rR...",
    "...R..Rroyy.or..",
    "...rR.Rroyyyor..",
    "..Rrorroywwyor..",
    "..Rroooywwwyyor.",
    "..Rrooywwwwyyor.",
    "...Rroywwwwyor..",
    "..kk.Rroyyor.kk.",
    ".kLMkkRrrRkkMDk.",
    "..kLLMMkkMMDDk..",
    ".tkkLLMMMMDDkkT.",
    "ttT.kkkkkkkk.tTT",
]

ICONS['player'] = mirror([
    "........",
    "......kk",
    ".....khh",
    "....khFF",
    "...khFFF",
    "...khFSS",
    "...khFkS",
    "...khFSS",
    "...khFFF",
    "..khHhFF",
    ".khHHhhh",
    ".kHHkhhh",
    ".kSkdddd",
    "..khhhhh",
    "...kQQkk",
    "..kQQQk.",
], shade={'H': 'h', 'h': 'd', 'F': 'H'})

ICONS['shelter'] = mirror([
    "......k.",
    ".......k",
    "......kA",
    ".....kAA",
    ".....kHA",
    "....kHHH",
    "....kHHk",
    "...kHHkQ",
    "...kHHkQ",
    "..kHHHkQ",
    "..kHHkQQ",
    ".kHHHkQQ",
    ".kHHkQQQ",
    "kHHHkQQQ",
    "kkkkkkkk",
    "........",
], shade={'H': 'h', 'A': 'B'})

ICONS['embers'] = [
    "................",
    ".........B......",
    "........B.......",
    "........B.......",
    ".........B......",
    "..........B.....",
    ".........B......",
    "................",
    "................",
    ".....kkkkkk.....",
    "...kkqoqqRqkk...",
    "..kqRqyqqoqRqk..",
    ".kqqoqRwRqqyqqk.",
    ".kqRqqqoqqRqoqk.",
    "kQqqQqqqQqqqQqqk",
    ".kkkkkkkkkkkkkk.",
]

ICONS['hud_fire'] = [
    "........R.......",
    ".......Rr.......",
    ".......Rro..R...",
    "......Rroo.rR...",
    "...R..Rroyy.or..",
    "...rR.Rroyyyor..",
    "..Rrorroywwyor..",
    "..Rroooywwwyyor.",
    ".Rrrooywwwwyyorr",
    ".Rrooywwwwwwyor.",
    ".Rroyywwwwwyyor.",
    "..Rroyywwwyyor..",
    "..Rrooyyyyyoor..",
    "...RRroooooorR..",
    ".....RRrrrrRR...",
    "................",
]

ICONS['hud_temperature'] = [
    "......kkkk......",
    ".....kABBCk.kk..",
    ".....kA..Ck.....",
    ".....kA..Ck.kk..",
    ".....kArrCk.....",
    ".....kArrCk.kk..",
    ".....kArrCk.....",
    ".....kArrCk.kk..",
    ".....kArrCk.....",
    "....kArrrrCk....",
    "...kAxrrrrrCk...",
    "...kxrrrrrrRk...",
    "...krrrrrrRRk...",
    "...kCrrrrRRCk...",
    "....kkCCCCkk....",
    "......kkkk......",
]

ICONS['hud_water'] = mirror([
    "........",
    ".......k",
    "......kb",
    "......kb",
    ".....kbb",
    ".....kab",
    "....kbab",
    "...kbabb",
    "...kbabb",
    "..kbabbb",
    "..kbbbbb",
    "..kbbbbb",
    "..kcbbbb",
    "...kcbbb",
    "....kkcc",
    "......kk",
], shade={'b': 'c', 'a': 'b', 'c': 'e'})

ICONS['hud_food'] = [  # roast haunch on a bone
    "................",
    "..........kk....",
    ".........kttk...",
    "........kttTk...",
    ".......kkTTk....",
    "....kkkkMk......",
    "...kLLMMMDk.....",
    "..kLooLMMMDk....",
    "..kLoLLMMMDk....",
    ".kLLLMMMMDDk....",
    ".kLLMMMMDDDk....",
    ".kMMMMMDDDk.....",
    "..kMMDDDDk......",
    "...kkDDkk.......",
    ".....kk.........",
    "................",
]

ICONS['hud_energy'] = [
    "................",
    "........kkkk....",
    ".......kuUVk....",
    "......kuUVk.....",
    ".....kuUVk......",
    "....kuUVkkkk....",
    "...kuUUUUUVk....",
    "...kkkkkuUVk....",
    "......kuUVk.....",
    ".....kuUVk......",
    "....kuUVk.......",
    "....kUVk........",
    "...kUVk.........",
    "...kVk..........",
    "...kk...........",
    "................",
]

ICONS['hud_foraging'] = [  # woven basket with a find in it
    "................",
    "................",
    ".....kkkkkk.....",
    "....kM....Mk....",
    "...kM.kXkG.Mk...",
    "..kkkkXxXkGkkk..",
    ".kDDDDDDDDDDDDk.",
    ".kLMLMLMLMLMLMk.",
    "..kMLMLMLMLMLk..",
    "..kLMLMLMLMLMk..",
    "..kMLMLMLMLMDk..",
    "...kLMLMLMLDk...",
    "...kMLMLMLMDk...",
    "....kDDDDDDk....",
    ".....kkkkkk.....",
    "................",
]

ICONS['hud_backpack'] = mirror([
    "........",
    "......kk",
    ".....kMk",
    "....kkkk",
    "...kFFHH",
    "..kFHHHH",
    "..kHHHHH",
    "..khhhht",
    "..kHHHHH",
    ".kHHHHHH",
    ".kHHHHHH",
    ".kHHHHHH",
    ".khHHHHH",
    "..khhhhh",
    "...kkkkk",
    "........",
], shade={'H': 'h', 'F': 'H', 'h': 'd', 'M': 'D'})

ICONS['hud_warning'] = mirror([
    "........",
    ".......k",
    "......ku",
    "......ku",
    ".....kuU",
    ".....kuk",
    "....kuUk",
    "....kUUk",
    "...kuUUk",
    "...kUUUk",
    "..kuUUUU",
    "..kUUUUk",
    ".kuUUUUk",
    ".kUUUUUU",
    ".kkkkkkk",
    "........",
], shade={'U': 'V', 'u': 'U'})

ICONS['hud_beasts'] = [  # wolf print: four toes, one pad, light on the dark HUD
    "................",
    "....kk....kk....",
    "...kttk..ktTk...",
    "...kttk..ktTk...",
    "....kk....kk....",
    ".kk...kkkk...kk.",
    "kttk.kttTTk.ktTk",
    "kttk.ktTTTk.kTTk",
    ".kk..kkkkkk..kk.",
    ".....kttttk.....",
    "....kttttTTk....",
    "...kttttTTTTk...",
    "...kttTTTTTTk...",
    "...kTTTkkTTTk...",
    "....kkk..kkk....",
    "................",
]

ICONS['hud_sun'] = [
    "................",
    ".......kk.......",
    "...k...kU...k...",
    "...kk..kk..kk...",
    "....kkkkkkkk....",
    "....kuuuuUUk....",
    "..kkkuuuUUUVkk..",
    ".kUkuuuUUUUVkUk.",
    "..kkuuUUUUUVkk..",
    "....kuUUUUVVk...",
    "....kUUUUVVVk...",
    "....kkVVVVkkk...",
    "...kk..kk..kk...",
    "...k...kU...k...",
    ".......kk.......",
    "................",
]

ICONS['hud_moon'] = [
    "................",
    ".......kkkk.....",
    ".....kkmmmk.....",
    "....kmmmmk......",
    "...kmmmmk.......",
    "...kmmnk........",
    "..kmmmnk........",
    "..kmmmnk...k....",
    "..kmmnnk..kAk...",
    "..kmmnnnk..k....",
    "...kmnnnnk......",
    "...knnnnNNkkkk..",
    "....kNnnnNNNNk..",
    ".....kkNNNNkk...",
    ".......kkkk.....",
    "................",
]

def hexrgba(h): return tuple(int(h[i:i + 2], 16) for i in (1, 3, 5)) + (255,)

def render(name, rows):
    assert len(rows) == 16, (name, len(rows))
    im = Image.new('RGBA', (16, 16), (0, 0, 0, 0))
    for y, row in enumerate(rows):
        assert len(row) == 16, (name, y, row, len(row))
        for x, ch in enumerate(row):
            if ch != '.': im.putpixel((x, y), hexrgba(PAL[ch]))
    return im

if __name__ == '__main__':
    os.makedirs(OUT, exist_ok=True)
    ims = {n: render(n, r) for n, r in ICONS.items()}
    for n, im in ims.items(): im.save(os.path.join(OUT, n + '.png'))
    s, cols = 6, 8
    names = list(ims)
    rows_n = (len(names) + cols - 1) // cols
    sheet = Image.new('RGBA', (cols * 18 * s, rows_n * 18 * s), (31, 36, 40, 255))
    for i, n in enumerate(names):
        big = ims[n].resize((16 * s, 16 * s), Image.NEAREST)
        sheet.alpha_composite(big, ((i % cols) * 18 * s + s, (i // cols) * 18 * s + s))
    sheet.save(os.path.join(HERE, 'icons_sheet.png'))
    print(len(names), 'icons')
