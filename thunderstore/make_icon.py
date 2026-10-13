# Draws thunderstore/icon.png (256x256): three stacked wooden chests with a sign, and a gold
# plus. python3 thunderstore/make_icon.py   (needs Pillow)
import os
from PIL import Image, ImageDraw

S = 4  # draw at 4x, then shrink for smooth edges
W = 256 * S
img = Image.new("RGBA", (W, W), (0, 0, 0, 0))
d = ImageDraw.Draw(img)
d.rounded_rectangle((8 * S, 8 * S, 248 * S, 248 * S), radius=40 * S, fill=(28, 36, 30, 255), outline=(78, 96, 72, 255), width=4 * S)


def chest(x, y, w, h):
    # Body, lid line, iron bands and a lock.
    d.rounded_rectangle((x * S, y * S, (x + w) * S, (y + h) * S), radius=6 * S, fill=(140, 92, 48, 255), outline=(72, 44, 22, 255), width=4 * S)
    d.line((x * S, (y + h * 0.38) * S, (x + w) * S, (y + h * 0.38) * S), fill=(72, 44, 22, 255), width=4 * S)
    for bx in (x + w * 0.2, x + w * 0.8):
        d.rectangle(((bx - 4) * S, y * S, (bx + 4) * S, (y + h) * S), fill=(96, 100, 108, 255))
    cx = x + w / 2
    d.rounded_rectangle(((cx - 8) * S, (y + h * 0.3) * S, (cx + 8) * S, (y + h * 0.3 + 18) * S), radius=3 * S, fill=(214, 176, 72, 255))


chest(40, 150, 82, 60)
chest(126, 150, 82, 60)
chest(83, 86, 82, 60)
# A sign on a post, left of the stack.
d.rectangle((48 * S, 92 * S, 54 * S, 146 * S), fill=(96, 64, 34, 255))
d.rounded_rectangle((26 * S, 60 * S, 78 * S, 96 * S), radius=4 * S, fill=(196, 160, 104, 255), outline=(96, 64, 34, 255), width=3 * S)
for ly in (72, 84):
    d.line((34 * S, ly * S, 70 * S, ly * S), fill=(80, 56, 32, 255), width=3 * S)
# The plus.
d.ellipse((168 * S, 28 * S, 236 * S, 96 * S), fill=(214, 176, 72, 255), outline=(120, 92, 30, 255), width=4 * S)
d.rectangle((197 * S, 40 * S, 207 * S, 84 * S), fill=(40, 30, 14, 255))
d.rectangle((180 * S, 57 * S, 224 * S, 67 * S), fill=(40, 30, 14, 255))

img.resize((256, 256), Image.LANCZOS).save(os.path.join(os.path.dirname(os.path.abspath(__file__)), "icon.png"))
