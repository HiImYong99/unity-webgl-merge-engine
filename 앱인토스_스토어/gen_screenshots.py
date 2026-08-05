#!/usr/bin/env python3
# 앱인토스 세로형 스크린샷 (636x1048). 클린/미니멀: Pretendard 헤드라인 + 게임화면 둥근 카드(하단 블리드).
# 외곽선/컬러밴드/이모지/만화반짝이 X. HTML → Chrome 헤드리스 렌더.
import os, subprocess, tempfile

OUT_DIR = '/Users/yong/Desktop/unity-webgl-merge-engine/앱인토스_스토어'
SRC_DIR = '/Users/yong/Desktop/unity-webgl-merge-engine/애니멀팝_스크린샷'
CHROME = '/Applications/Google Chrome.app/Contents/MacOS/Google Chrome'
W, H = 636, 1048

# 순서: 강한 화면 우선. (src screenN, kicker dot, h1, h2(html, <b>=토스블루 강조), sub, bg카드 포커스 Y%)
PLAN = [
    (4, '#FFB23E', '동물을 합쳐',     '더 크게 <b>키워요</b>',   '같은 동물끼리 만나면 한 단계 진화해요', '60%'),
    (3, '#FF7A9C', '통통 튀는',       '<b>말랑</b> 동물 합치기',  '합칠수록 점수가 쑥쑥 올라요',          '54%'),
    (1, '#2FC36B', '토스에서',         '<b>무료</b>로 즐겨요',     '설치 없이 바로 시작하는 동물 머지',     '50%'),
    (5, '#A07BFF', '최고 기록에',      '<b>도전</b>해 보세요',     '기록을 깰수록 더 짜릿해져요',          '50%'),
    (2, '#5B9BFF', '동물 11종을',      '모두 <b>모아요</b>',       '병아리부터 사자까지 한 마리씩',        '88%'),
]

TPL = '''<!DOCTYPE html><html lang="ko"><head><meta charset="UTF-8"><style>
@font-face{{font-family:Pretendard;font-weight:800;src:url('./fonts/Pretendard-ExtraBold.otf')}}
@font-face{{font-family:Pretendard;font-weight:700;src:url('./fonts/Pretendard-Bold.otf')}}
@font-face{{font-family:Pretendard;font-weight:600;src:url('./fonts/Pretendard-SemiBold.otf')}}
@font-face{{font-family:Pretendard;font-weight:500;src:url('./fonts/Pretendard-Medium.otf')}}
*{{margin:0;padding:0;box-sizing:border-box}}
body{{width:{W}px;height:{H}px;overflow:hidden;font-family:Pretendard,sans-serif}}
.bg{{width:{W}px;height:{H}px;position:relative;overflow:hidden;
  background:linear-gradient(180deg,#F7F8FC 0%,#EDEFF6 100%);}}
.top{{position:absolute;top:0;left:0;right:0;padding:46px 48px 0;text-align:center;z-index:2}}
.kicker{{display:inline-flex;align-items:center;gap:9px;font-weight:600;font-size:20px;color:#9aa1ae;margin-bottom:20px;letter-spacing:-.2px}}
.kdot{{width:11px;height:11px;border-radius:50%;background:{DOT};box-shadow:0 0 0 4px {DOT}22}}
.h{{font-weight:800;font-size:51px;line-height:1.16;letter-spacing:-1.6px;color:#1d2230}}
.h b{{color:#3182F6;font-weight:800}}
.sub{{margin-top:15px;font-weight:500;font-size:21px;color:#8b91a1;letter-spacing:-.4px}}
.card{{position:absolute;left:50%;transform:translateX(-50%);top:356px;bottom:-58px;width:478px;
  border-radius:42px;overflow:hidden;border:1px solid rgba(20,30,60,.06);
  background-image:url('{SRC}');background-size:cover;background-position:center {POS};
  box-shadow:0 36px 72px -26px rgba(40,52,92,.32),0 12px 26px rgba(40,52,92,.10)}}
.card::after{{content:"";position:absolute;inset:0;border-radius:42px;box-shadow:inset 0 2px 6px rgba(255,255,255,.5);pointer-events:none}}
</style></head><body><div class="bg">
<div class="top"><div class="kicker"><span class="kdot"></span>애니멀 팝</div>
<div class="h">{H1}<br>{H2}</div><div class="sub">{SUB}</div></div>
<div class="card"></div></div></body></html>'''

tmp = tempfile.mkdtemp()
for idx, (sn, dot, h1, h2, sub, pos) in enumerate(PLAN, start=1):
    src = os.path.join(SRC_DIR, f'플레이스토어_screen{sn}.jpg')
    html = TPL.format(W=W, H=H, DOT=dot, H1=h1, H2=h2, SUB=sub, SRC='file://' + src, POS=pos)
    hp = os.path.join(tmp, f'ss{idx}.html')
    with open(hp, 'w') as f:
        f.write(html)
    # 폰트를 상대경로(./fonts)로 쓰므로 OUT_DIR 기준 렌더 위해 OUT_DIR에 임시 HTML 배치
    hp_out = os.path.join(OUT_DIR, f'._ss{idx}.html')
    with open(hp_out, 'w') as f:
        f.write(html)
    outpng = os.path.join(OUT_DIR, f'toss_screenshot_{idx}.png')
    subprocess.run([CHROME, '--headless=new', '--disable-gpu', '--hide-scrollbars',
                    '--force-device-scale-factor=1', f'--window-size={W},{H}',
                    '--virtual-time-budget=3500', '--allow-file-access-from-files',
                    f'--screenshot={outpng}', 'file://' + hp_out],
                   capture_output=True)
    os.remove(hp_out)
    print(f'[{idx}] src=screen{sn} -> toss_screenshot_{idx}.png')
print('done')
