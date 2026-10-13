# Draws thunderstore/nexus-banner.png (1280x720), the Nexus Mods page's main image: the icon, the
# mod's name and what it does. python3 thunderstore/make_banner.py   (needs Pillow and DejaVu Sans)
import os
from PIL import Image, ImageDraw, ImageFont

here = os.path.dirname(os.path.abspath(__file__))
W, H = 1280, 720
img = Image.new("RGB", (W, H), (28, 36, 30))
d = ImageDraw.Draw(img)
# A frame like the icon's.
d.rounded_rectangle((24, 24, W - 24, H - 24), radius=48, outline=(78, 96, 72), width=6)
icon = Image.open(os.path.join(here, "icon.png")).convert("RGBA").resize((340, 340), Image.LANCZOS)
img.paste(icon, (70, (H - 340) // 2), icon)

font = "/usr/share/fonts/dejavu-sans-fonts/DejaVuSans{}.ttf"
title = ImageFont.truetype(font.format("-Bold"), 66)
line = ImageFont.truetype(font.format(""), 30)
small = ImageFont.truetype(font.format(""), 21)
x = 450
d.text((x, 160), "Moder Warehouse", font=title, fill=(240, 232, 214))
d.text((x, 236), "Plus", font=title, fill=(214, 176, 72))
for i, text in enumerate([
    "Sorts your deposits into the right chests",
    "Withdraw, craft and * wildcard from a sign",
    "Stock and statistics boards",
    "Server-only: players need no mod",
]):
    d.text((x, 350 + i * 48), "• " + text, font=line, fill=(214, 206, 190))
d.text((x, 565), "Fork of Moder Warehouse by Cris Haani · works with ContainerSigns", font=small, fill=(150, 160, 140))
img.save(os.path.join(here, "nexus-banner.png"))
