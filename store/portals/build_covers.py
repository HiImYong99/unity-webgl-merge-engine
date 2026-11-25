#!/usr/bin/env python3
"""HTML5 portal (Playgama, CrazyGames) covers for Animal Pop.

Writes next to this file:
  cover-landscape-1920x1080.png    Playgama landscape
  cover-portrait-1080x1920.png     Playgama portrait
  cover-square-800x800.png         Playgama square
  cover-portrait-800x1200.png      CrazyGames 2:3
  video-bg-landscape-1920x1080.png backdrops for the preview videos (no text; make_videos.sh
  video-bg-portrait-1080x1620.png  lays the 1:2 game column over the middle — see VIDEO_BGS)

Portal rule: the English title "Animal Pop" is the only text. No tagline, icons,
store badges or border; the title stays out of the top-left corner.

Look = the store feature graphic: a warm yellow sunburst with the lion (the top
of the merge chain) in the middle and the rest of the chain around it. The art
is the game's own sprites (Assets/_Project/Resources/Animals, 600-1700 px); the
title face is Nunito Black, the game's UI font (playgama/fonts, OFL).

    python3 store/portals/build_covers.py [landscape|portrait|square|portrait23|videobg ...]
"""
import math
import sys
from pathlib import Path

from PIL import Image, ImageDraw, ImageFilter, ImageFont

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[1]
ANIMALS = ROOT / "Assets" / "_Project" / "Resources" / "Animals"
NUNITO = ROOT / "playgama" / "fonts" / "nunito-latin.woff2"

# Animal_<n>: 1 chick, 2 mouse, 3 hedgehog, 4 rabbit, 5 calico cat, 6 shiba,
# 7 sheep, 8 monkey, 9 pig, 10 panda, 11 lion
RAY_A, RAY_B = (255, 205, 38), (255, 222, 92)
EDGE = (246, 150, 20)
OUTLINE = (138, 70, 8)


# ---------------------------------------------------------------- helpers

def sunburst(W, H, cx, cy, rays=22):
    """Alternating rays around (cx, cy), a warm light in the middle, orange edges."""
    S = 2
    big = Image.new("RGB", (W * S, H * S), RAY_A)
    d = ImageDraw.Draw(big)
    R = math.hypot(W, H) * S
    for i in range(rays):
        a0 = 2 * math.pi * i / rays
        a1 = a0 + math.pi / rays
        d.polygon([(cx * S, cy * S),
                   (cx * S + R * math.cos(a0), cy * S + R * math.sin(a0)),
                   (cx * S + R * math.cos(a1), cy * S + R * math.sin(a1))], fill=RAY_B)
    bg = big.resize((W, H), Image.LANCZOS).convert("RGBA")
    s = min(W, H)
    light = Image.new("L", (W, H), 0)
    r = s * 0.42
    ImageDraw.Draw(light).ellipse([cx - r, cy - r, cx + r, cy + r], fill=255)
    bg.alpha_composite(tint(light.filter(ImageFilter.GaussianBlur(s * 0.16)), (255, 250, 225), 0.85))
    # orange toward the corners
    edge = Image.new("L", (W, H), 255)
    ImageDraw.Draw(edge).ellipse([W * -0.1, H * -0.12, W * 1.1, H * 1.12], fill=0)
    bg.alpha_composite(tint(edge.filter(ImageFilter.GaussianBlur(s * 0.14)), EDGE, 0.55))
    return bg


def tint(alpha, color, strength=1.0):
    if strength != 1.0:
        alpha = alpha.point(lambda v: round(v * strength))
    layer = Image.new("RGBA", alpha.size, color + (0,))
    layer.putalpha(alpha)
    return layer


def animal(n, size):
    im = Image.open(ANIMALS / f"Animal_{n}.png").convert("RGBA")
    im = im.crop(im.getbbox())
    s = size / max(im.size)
    return im.resize((max(1, round(im.width * s)), max(1, round(im.height * s))), Image.LANCZOS)


def place(canvas, n, cx, cy, size, angle=0.0, halo=False):
    im = animal(n, size)
    if angle:
        im = im.rotate(angle, resample=Image.BICUBIC, expand=True)
    x, y = round(cx - im.width / 2), round(cy - im.height / 2)
    pad = round(size * 0.25)
    a = Image.new("L", (im.width + pad * 2, im.height + pad * 2), 0)
    a.paste(im.getchannel("A"), (pad, pad))
    if halo:  # white glow behind the star of the chain
        canvas.alpha_composite(tint(a.filter(ImageFilter.GaussianBlur(size * 0.07)), (255, 255, 255), 0.9),
                               (x - pad, y - pad))
    sh = Image.new("L", a.size, 0)
    sh.paste(a, (0, round(size * 0.035)))
    canvas.alpha_composite(tint(sh.filter(ImageFilter.GaussianBlur(size * 0.03)), (120, 60, 0), 0.45),
                           (x - pad, y - pad))
    canvas.alpha_composite(im, (x, y))


def font(size):
    f = ImageFont.truetype(str(NUNITO), size)
    f.set_variation_by_axes([1000])  # Black
    return f


def title_layer(cap_px, two_line):
    """'Animal Pop' in Nunito Black: white, thick brown outline, soft drop shadow."""
    SS = 2
    bb = font(200).getbbox("A")
    size = round(200 * cap_px / (bb[3] - bb[1])) * SS
    f = font(size)
    stroke = round(size * 0.1)
    lines = ["Animal", "Pop"] if two_line else ["Animal Pop"]
    lead = round(size * 0.92)
    widths = [f.getlength(t) for t in lines]
    asc, desc = f.getmetrics()
    w = round(max(widths)) + stroke * 4
    h = lead * (len(lines) - 1) + asc + desc + stroke * 4
    out_m = Image.new("L", (w, h), 0)
    fill_m = Image.new("L", (w, h), 0)
    for i, (t, tw) in enumerate(zip(lines, widths)):
        pos = (stroke * 2 + (max(widths) - tw) / 2, stroke * 2 + i * lead)
        ImageDraw.Draw(out_m).text(pos, t, font=f, fill=255, stroke_width=stroke, stroke_fill=255)
        ImageDraw.Draw(fill_m).text(pos, t, font=f, fill=255)
    body = tint(out_m, OUTLINE)
    # white face with a faint warm cream toward the bottom of each line
    face = Image.new("RGBA", (w, h), (255, 255, 255, 255))
    grad = Image.new("L", (1, h))
    for y in range(h):
        grad.putpixel((0, y), round(40 * ((y % lead) / lead) ** 2) if lead else 0)
    face.alpha_composite(tint(grad.resize((w, h)), (255, 214, 120)))
    face.putalpha(fill_m)
    body.alpha_composite(face)
    body = body.crop(body.getbbox())
    body = body.resize((round(body.width / SS), round(body.height / SS)), Image.LANCZOS)
    P = round(cap_px * 0.5)
    out = Image.new("RGBA", (body.width + P * 2, body.height + P * 2), (0, 0, 0, 0))
    a = Image.new("L", out.size, 0)
    a.paste(body.getchannel("A"), (P, P + round(cap_px * 0.09)))
    out.alpha_composite(tint(a.filter(ImageFilter.GaussianBlur(cap_px * 0.06)), (110, 50, 0), 0.6))
    out.alpha_composite(body, (P, P))
    return out, (P, P, P + body.width, P + body.height)


# ---------------------------------------------------------------- layouts

# animals: (n, cx, cy, size, degrees) — cx of W, cy and size of H. Drawn in order.
LAYOUTS = {
    "landscape": dict(
        size=(1920, 1080), sun=(0.5, 0.42),
        animals=[(1, 0.07, 0.86, 0.15, -10), (2, 0.94, 0.84, 0.15, 8),
                 (3, 0.28, 0.09, 0.14, -8), (4, 0.72, 0.09, 0.14, 8),
                 (5, 0.10, 0.22, 0.20, 10), (6, 0.90, 0.22, 0.21, -8),
                 (8, 0.16, 0.58, 0.26, -6), (7, 0.84, 0.58, 0.26, 6),
                 (10, 0.31, 0.38, 0.30, -5), (9, 0.69, 0.38, 0.30, 5),
                 (11, 0.5, 0.40, 0.52, 0)],
        title=dict(cap=0.13, two_line=False, cx=0.5, bottom=0.95, max_w=0.72),
    ),
    "portrait": dict(
        size=(1080, 1920), sun=(0.5, 0.36),
        animals=[(1, 0.12, 0.08, 0.075, -10), (4, 0.88, 0.07, 0.075, 8),
                 (3, 0.10, 0.62, 0.075, -8), (2, 0.90, 0.62, 0.075, 8),
                 (6, 0.20, 0.18, 0.11, 8), (5, 0.80, 0.18, 0.11, -8),
                 (8, 0.18, 0.49, 0.13, -6), (7, 0.82, 0.49, 0.13, 6),
                 (10, 0.26, 0.30, 0.14, -5), (9, 0.74, 0.30, 0.14, 5),
                 (11, 0.5, 0.38, 0.27, 0)],
        title=dict(cap=0.085, two_line=True, cx=0.5, bottom=0.90, max_w=0.86),
    ),
    "portrait23": dict(
        size=(800, 1200), sun=(0.5, 0.36),
        animals=[(1, 0.10, 0.08, 0.09, -10), (4, 0.90, 0.07, 0.09, 8),
                 (3, 0.08, 0.62, 0.09, -8), (2, 0.92, 0.62, 0.09, 8),
                 (6, 0.20, 0.19, 0.13, 8), (5, 0.80, 0.19, 0.13, -8),
                 (8, 0.17, 0.50, 0.15, -6), (7, 0.83, 0.50, 0.15, 6),
                 (10, 0.25, 0.32, 0.16, -5), (9, 0.75, 0.32, 0.16, 5),
                 (11, 0.5, 0.38, 0.31, 0)],
        title=dict(cap=0.095, two_line=True, cx=0.5, bottom=0.93, max_w=0.86),
    ),
    "square": dict(
        size=(800, 800), sun=(0.5, 0.40),
        animals=[(2, 0.08, 0.60, 0.13, -8), (1, 0.92, 0.60, 0.13, 8),
                 (6, 0.12, 0.12, 0.18, 8), (5, 0.88, 0.12, 0.18, -8),
                 (10, 0.20, 0.38, 0.25, -5), (9, 0.80, 0.38, 0.25, 5),
                 (11, 0.5, 0.39, 0.48, 0)],
        title=dict(cap=0.115, two_line=False, cx=0.5, bottom=0.95, max_w=0.86),
    ),
}

OUTPUTS = {
    "landscape": "cover-landscape-1920x1080.png",
    "portrait": "cover-portrait-1080x1920.png",
    "square": "cover-square-800x800.png",
    "portrait23": "cover-portrait-800x1200.png",
}


def build(kind):
    L = LAYOUTS[kind]
    W, H = L["size"]
    c = sunburst(W, H, L["sun"][0] * W, L["sun"][1] * H)
    for n, cx, cy, s, ang in L["animals"]:
        place(c, n, cx * W, cy * H, s * H, ang, halo=(n == 11))
    T = L["title"]
    cap = round(T["cap"] * H)
    while True:
        t, (bx0, by0, bx1, by1) = title_layer(cap, T["two_line"])
        if bx1 - bx0 <= T["max_w"] * W:
            break
        cap -= 2
    tx = round(T["cx"] * W - (bx0 + bx1) / 2)
    ty = round(T["bottom"] * H - by1)
    c.alpha_composite(t, (tx, ty))

    out = c.convert("RGB")
    x0, y0 = tx + bx0, ty + by0
    assert not (x0 < W * 0.25 and y0 < H * 0.25), f"{kind}: title in the top-left"
    assert out.size == (W, H)
    out.save(HERE / OUTPUTS[kind], optimize=True)
    print(f"{OUTPUTS[kind]:33} {W}x{H}  title {x0},{y0}..{tx + bx1},{ty + by1}")


# ---------------------------------------------------------------- video backdrop

VIDEO_BGS = {
    # name: (size, game column x0..x1 that make_videos.sh covers, animals (n, cx, cy, size, deg))
    "video-bg-landscape-1920x1080.png": ((1920, 1080), (690, 1230), [
        (1, 560, 930, 150, -10), (2, 360, 780, 170, 6), (3, 150, 600, 190, -8),
        (4, 470, 470, 200, 8), (5, 220, 290, 220, -6), (6, 500, 120, 200, 6),
        (7, 1400, 930, 230, 6), (8, 1640, 760, 250, -6), (9, 1440, 520, 260, 5),
        (10, 1720, 300, 270, -5), (11, 1450, 120, 300, 0)]),
    "video-bg-portrait-1080x1620.png": ((1080, 1620), (135, 945), [
        (1, 70, 260, 120, -10), (5, 60, 700, 150, 8), (8, 70, 1180, 160, -6),
        (3, 1010, 420, 130, 8), (9, 1015, 900, 160, -5), (11, 1010, 1420, 190, 0)]),
}


def build_video_bgs():
    """No title: the game column covers the middle; the chain peeks out on the sides."""
    for name, ((W, H), (x0, x1), animals) in VIDEO_BGS.items():
        c = sunburst(W, H, W / 2, H * 0.45)
        for n, cx, cy, s, ang in animals:
            place(c, n, cx, cy, s, ang)
        m = Image.new("L", (W, H), 0)
        ImageDraw.Draw(m).rectangle((x0, 0, x1, H), fill=255)
        c.alpha_composite(tint(m.filter(ImageFilter.GaussianBlur(24)), (110, 50, 0), 0.55))
        c.convert("RGB").save(HERE / name, optimize=True)
        print(f"{name:33} {W}x{H}  column x {x0}..{x1}")


def main():
    for k in (sys.argv[1:] or [*LAYOUTS, "videobg"]):
        build_video_bgs() if k == "videobg" else build(k)


if __name__ == "__main__":
    main()
