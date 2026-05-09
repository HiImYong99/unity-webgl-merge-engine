import os
from PIL import Image, ImageDraw, ImageFont, ImageFilter, ImageChops

texts = [
    "시간순삭! 귀여운 머지 퍼즐",
    "같은 동물을 쾅! 더 크게",
    "아슬아슬! 짜릿한 물리엔진",
    "가득 차기 전에 최종 진화!",
    "최고 점수로 랭킹 1위 달성!"
]

gradients = [
    ('#ff9a9e', '#fecfef'),
    ('#f6d365', '#fda085'),
    ('#84fab0', '#8fd3f4'),
    ('#fccb90', '#d57eeb'),
    ('#e0c3fc', '#8ec5fc')
]

def hex_to_rgb(hx):
    hx = hx.lstrip('#')
    return tuple(int(hx[i:i+2], 16) for i in (0, 2, 4))

def create_gradient(w, h, color1, color2):
    gradient = Image.new('RGB', (1, 2))
    gradient.putpixel((0, 0), hex_to_rgb(color1))
    gradient.putpixel((0, 1), hex_to_rgb(color2))
    return gradient.resize((w, h), Image.Resampling.BICUBIC)

def crop_black_edges(im):
    w, h = im.size
    pixels = im.load()
    top = 0
    bottom = h
    for y in range(h):
        if max(pixels[w//2, y]) > 15 or max(pixels[100, y]) > 15:
            top = y
            break
    for y in range(h-1, -1, -1):
        if max(pixels[w//2, y]) > 15 or max(pixels[100, y]) > 15:
            bottom = y + 1
            break
    # Add a safety check in case the image is somehow very dark
    if bottom <= top:
        return im
    return im.crop((0, top, w, bottom))

def add_corners(im, rad):
    circle = Image.new('L', (rad * 2, rad * 2), 0)
    draw = ImageDraw.Draw(circle)
    draw.ellipse((0, 0, rad * 2 - 1, rad * 2 - 1), fill=255)
    
    alpha = Image.new('L', im.size, 255)
    w, h = im.size
    alpha.paste(circle.crop((0, 0, rad, rad)), (0, 0))
    alpha.paste(circle.crop((rad, 0, rad * 2, rad)), (w - rad, 0))
    alpha.paste(circle.crop((0, rad, rad, rad * 2)), (0, h - rad))
    alpha.paste(circle.crop((rad, rad, rad * 2, rad * 2)), (w - rad, h - rad))
    
    if im.mode == 'RGBA':
        im_alpha = im.split()[3]
        alpha = ImageChops.darker(alpha, im_alpha)
    
    im.putalpha(alpha)
    return im

def draw_shadow(canvas, app_w, app_h, img_x, img_y, rad):
    # Create black rounded rect
    shadow = Image.new('RGBA', (app_w, app_h), (0,0,0, 100))
    shadow = add_corners(shadow, rad)
    # create larger canvas to blur
    blur_pad = 100
    shadow_pad = Image.new('RGBA', (app_w + blur_pad*2, app_h + blur_pad*2), (0,0,0,0))
    shadow_pad.paste(shadow, (blur_pad, blur_pad), shadow)
    # apply heavy gaussian blur
    shadow_pad = shadow_pad.filter(ImageFilter.GaussianBlur(30))
    
    # Check bounds before pasting
    px = img_x - blur_pad
    py = img_y - blur_pad + 25 # +25 for vertical shadow shift
    
    # Paste using alpha compositing
    bg_t = canvas.copy().convert("RGBA")
    temp = Image.new("RGBA", bg_t.size, (0,0,0,0))
    temp.paste(shadow_pad, (px, py), shadow_pad)
    canvas = Image.alpha_composite(bg_t, temp)
    return canvas.convert("RGB")

font_path = '/tmp/Jua.ttf'
try:
    font = ImageFont.truetype(font_path, 80)
except Exception as e:
    print("Font error:", e)

base_dir = '/Users/yong/Desktop/unity-webgl-merge-engine/애니멀팝_스크린샷'

for i, text in enumerate(texts):
    file_name = f'플레이스토어_screen{i+1}.jpg'
    file_path = os.path.join(base_dir, file_name)
    if not os.path.exists(file_path):
        print(f"Skipping {file_path}")
        continue
    
    img = Image.open(file_path).convert("RGB")
    img = crop_black_edges(img)
    orig_W, orig_H = img.size
    
    # Canvas
    W, H = 1080, 2400
    canvas = create_gradient(W, H, gradients[i][0], gradients[i][1])
    
    # Scale app image
    app_w = int(W * 0.72)
    # Calculate app_h preserving aspect ratio of cropped image
    app_h = int(app_w * orig_H / orig_W)
    
    app_img = img.resize((app_w, app_h), Image.Resampling.LANCZOS)
    app_img = app_img.convert("RGBA")
    app_img = add_corners(app_img, 60)
    
    img_x = (W - app_w) // 2
    img_y = H - app_h - 120 # place it closer to the bottom
    
    # Draw shadow
    canvas = draw_shadow(canvas, app_w, app_h, img_x, img_y, 60)
    
    # Paste app
    canvas.paste(app_img, (img_x, img_y), app_img)
    
    # Draw text
    draw = ImageDraw.Draw(canvas)
    
    bbox = draw.textbbox((0,0), text, font=font)
    tw = bbox[2] - bbox[0]
    th = bbox[3] - bbox[1]
    
    x_pos = (W - tw) / 2
    y_pos = min(img_y // 2 - th // 2, 250) - 100
    
    # Simple shadow
    shadow_offset = 6
    draw.text((x_pos+shadow_offset, y_pos+shadow_offset), text, font=font, fill=(0,0,0, 120))
    # Fill text with a very dark grey instead of black for modern look
    draw.text((x_pos, y_pos), text, font=font, fill=(50, 50, 50))
    
    out_name = os.path.join(base_dir, f'out_modern_screen{i+1}.png')
    canvas.convert("RGB").save(out_name, "PNG")
    print(f"Saved {out_name}")
