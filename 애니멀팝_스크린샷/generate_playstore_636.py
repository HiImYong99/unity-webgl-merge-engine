import os
from PIL import Image, ImageDraw, ImageFont

OUT_W, OUT_H = 636, 1048

texts = [
    ["시간순삭 꿀잼!", "동물 머지 퍼즐 🐾"],
    ["같은 동물끼리", "합치면 진화! 🐣➡🦁"],
    ["통통 튀는", "물리엔진 손맛! 💥"],
    ["아슬아슬 한계도전", "끝까지 쌓아봐! 🏆"],
    ["나만의 신기록", "최고 기록 도전! ⭐"],
]

colors = [
    ('#4F8BFF', '#2B4FA0'),
    ('#FF5E8E', '#B8305E'),
    ('#FF8E3D', '#B85A15'),
    ('#A05EFF', '#6530B0'),
    ('#1FD068', '#0E7A38'),
]

def hex_to_rgb(hx):
    hx = hx.lstrip('#')
    return tuple(int(hx[i:i+2], 16) for i in (0, 2, 4))

font_path = '/tmp/BlackHanSans.ttf'
font_large = ImageFont.truetype(font_path, 52)
font_small = ImageFont.truetype(font_path, 42)

base_dir = '/Users/yong/Desktop/unity-webgl-merge-engine/애니멀팝_스크린샷'

for i, (line1, line2) in enumerate(texts):
    src = os.path.join(base_dir, f'플레이스토어_screen{i+1}.jpg')
    if not os.path.exists(src):
        print(f"Missing: {src}")
        continue

    img = Image.open(src).convert("RGB")

    # Crop to 9:16 aspect from center, then resize
    sw, sh = img.size
    target_ratio = OUT_W / OUT_H  # ~0.607
    src_ratio = sw / sh
    if src_ratio > target_ratio:
        new_w = int(sh * target_ratio)
        left = (sw - new_w) // 2
        img = img.crop((left, 0, left + new_w, sh))
    else:
        new_h = int(sw / target_ratio)
        top = (sh - new_h) // 2
        img = img.crop((0, top, sw, top + new_h))

    img = img.resize((OUT_W, OUT_H), Image.LANCZOS)

    canvas = img.convert("RGBA")

    c_main = hex_to_rgb(colors[i][0])
    c_dark = hex_to_rgb(colors[i][1])

    # --- Top banner with gradient overlay ---
    banner_h = 180
    overlay = Image.new('RGBA', (OUT_W, banner_h), (0, 0, 0, 0))
    draw_ov = ImageDraw.Draw(overlay)
    for y in range(banner_h):
        alpha = int(220 * (1 - y / banner_h))
        draw_ov.line([(0, y), (OUT_W, y)], fill=c_main + (alpha,))
    canvas.paste(Image.alpha_composite(
        canvas.crop((0, 0, OUT_W, banner_h)).convert('RGBA'), overlay
    ), (0, 0))

    draw = ImageDraw.Draw(canvas)

    # --- Draw text with outline ---
    def draw_outlined(draw, x, y, text, font, fill, outline, thickness=3):
        for dx in range(-thickness, thickness + 1):
            for dy in range(-thickness, thickness + 1):
                if dx * dx + dy * dy <= thickness * thickness:
                    draw.text((x + dx, y + dy), text, font=font, fill=outline)
        draw.text((x, y), text, font=font, fill=fill)

    # Center text horizontally in banner
    bbox1 = draw.textbbox((0, 0), line1, font=font_small)
    bbox2 = draw.textbbox((0, 0), line2, font=font_large)
    w1 = bbox1[2] - bbox1[0]
    w2 = bbox2[2] - bbox2[0]

    y1 = 28
    y2 = y1 + 62

    draw_outlined(draw, (OUT_W - w1) // 2, y1, line1, font_small,
                  (255, 255, 255, 255), c_dark + (255,), 3)
    draw_outlined(draw, (OUT_W - w2) // 2, y2, line2, font_large,
                  (255, 255, 255, 255), c_dark + (255,), 4)

    # --- Bottom bar with app name ---
    bar_h = 50
    bar_overlay = Image.new('RGBA', (OUT_W, bar_h), c_dark + (200,))
    canvas.paste(Image.alpha_composite(
        canvas.crop((0, OUT_H - bar_h, OUT_W, OUT_H)).convert('RGBA'), bar_overlay
    ), (0, OUT_H - bar_h))

    draw = ImageDraw.Draw(canvas)
    app_font = ImageFont.truetype(font_path, 24)
    app_text = "애니멀 팝  Animal Pop"
    bbox_app = draw.textbbox((0, 0), app_text, font=app_font)
    app_w = bbox_app[2] - bbox_app[0]
    draw.text(((OUT_W - app_w) // 2, OUT_H - bar_h + 10), app_text,
              font=app_font, fill=(255, 255, 255, 255))

    out_path = os.path.join(base_dir, f'playstore_636_{i+1}.png')
    canvas.convert("RGB").save(out_path, "PNG")
    print(f"Saved {out_path} ({OUT_W}x{OUT_H})")
