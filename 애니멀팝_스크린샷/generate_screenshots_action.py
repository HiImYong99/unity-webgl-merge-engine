import os
import math
from PIL import Image, ImageDraw, ImageFont, ImageChops

texts = [
    ["시간순삭 꿀잼!", "동물 머지 퍼즐"],
    ["같은 동물끼리", "통쾌하게 쾅!"],
    ["통통 튀는", "물리엔진 손맛!"],
    ["아슬아슬 한계도전", "최종 진화는!?"],
    ["나만의 신기록", "최고 기록 도전!"]
]

# (banner_color, stripe_color)
colors = [
    ('#4F8BFF', '#3061D0'),
    ('#FF5E8E', '#D93D6A'),
    ('#FF8E3D', '#D96A1C'),
    ('#A05EFF', '#7C35DF'),
    ('#1FD068', '#149B49')
]

def hex_to_rgb(hx):
    hx = hx.lstrip('#')
    return tuple(int(hx[i:i+2], 16) for i in (0, 2, 4))

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
    if bottom <= top:
        return im
    return im.crop((0, top, w, bottom))

font_main_path = '/tmp/BlackHanSans.ttf'
try:
    # Use larger font since it's full width action text
    font_large = ImageFont.truetype(font_main_path, 130)
    font_small = ImageFont.truetype(font_main_path, 110)
except Exception as e:
    print("Font error:", e)

base_dir = '/Users/yong/Desktop/unity-webgl-merge-engine/애니멀팝_스크린샷'

for i, (txt1, txt2) in enumerate(texts):
    file_name = f'플레이스토어_screen{i+1}.jpg'
    file_path = os.path.join(base_dir, file_name)
    if not os.path.exists(file_path):
        continue
        
    img = Image.open(file_path).convert("RGB")
    img = crop_black_edges(img)
    W, H = img.size
    
    # We will draw directly on `canvas`
    canvas = img.copy().convert("RGBA")
    
    c_main = hex_to_rgb(colors[i][0])
    c_dark = hex_to_rgb(colors[i][1])
    
    bh1 = int(H * 0.18)
    bh2 = int(H * 0.22)
    
    pt_bl = (0, H)
    pt_br = (W, H)
    pt_tl = (0, H - bh1)
    pt_tr = (W, H - bh2)
    poly = [pt_bl, pt_tl, pt_tr, pt_br]
    
    # Mask for the banner
    mask = Image.new('L', (W, H), 0)
    ImageDraw.Draw(mask).polygon(poly, fill=255)
    
    # Banner colored image
    banner = Image.new('RGBA', (W, H), c_main + (255,))
    
    # Draw stripes on the banner image
    draw_banner = ImageDraw.Draw(banner)
    stripe_w = 40
    stripe_gap = 40
    for x in range(-H, W + H, stripe_w + stripe_gap):
        # diagonal lines
        draw_banner.line([(x, 0), (x + H, H)], fill=c_dark + (255,), width=stripe_w)
        
    # Paste banner using mask
    canvas.paste(banner, (0, 0), mask)
    
    # Draw white top edge for the banner
    draw = ImageDraw.Draw(canvas)
    draw.line([pt_tl, pt_tr], fill=(255,255,255,255), width=20)
    
    def draw_thick_text(draw, x, y, text, font, fill_color, outline_color, thickness):
        # shadow / outline
        for dx in range(-thickness, thickness+1):
            for dy in range(-thickness, thickness+1):
                if dx*dx + dy*dy <= thickness*thickness:
                    draw.text((x+dx, y+dy), text, font=font, fill=outline_color)
        # solid drop shadow slightly lower (simulating 3D block)
        shadow_drop = 15
        for dx in range(-thickness//2, thickness//2):
            draw.text((x+dx, y+thickness+shadow_drop), text, font=font, fill=outline_color)
            
        draw.text((x, y), text, font=font, fill=fill_color)

    # Calculate text position (bottom left align)
    # The text is drawn horizontally
    t_x = 60
    t_y1 = H - bh1 + 40
    t_y2 = t_y1 + 140
    
    # If the text is too low, shift it up
    text_total_height = t_y2 + 140 - t_y1
    available_h = H - (H - bh1)
    if available_h < 350:
        # adjust up slightly
        shift = 350 - available_h
        t_y1 -= shift
        t_y2 -= shift
        
    # Draw the text!
    # White text with thick dark outline
    draw_thick_text(draw, t_x, t_y1, txt1, font_small, (255,255,255,255), c_dark+(255,), 18)
    draw_thick_text(draw, t_x, t_y2, txt2, font_large, (255,255,255,255), c_dark+(255,), 22)
    
    # Add comic action lines/sparkles on the right side
    # `#` mark but diagonal
    sp_x = W - 200
    sp_y = H - int(bh2 * 0.6)
    draw.line([(sp_x, sp_y-40), (sp_x+40, sp_y+40)], fill=(255,255,255,255), width=15)
    draw.line([(sp_x+40, sp_y-40), (sp_x, sp_y+40)], fill=(255,255,255,255), width=15)
    
    sp_x2 = W - 100
    sp_y2 = H - int(bh2 * 0.8)
    draw.line([(sp_x2, sp_y2-25), (sp_x2+25, sp_y2+25)], fill=(255,255,255,255), width=10)
    draw.line([(sp_x2+25, sp_y2-25), (sp_x2, sp_y2+25)], fill=(255,255,255,255), width=10)
    
    out_name = os.path.join(base_dir, f'out_action_screen{i+1}.png')
    canvas.convert("RGB").save(out_name, "PNG")
    print(f"Saved {out_name}")
