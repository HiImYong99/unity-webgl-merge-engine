"""Animal Pop Play 스크린샷 프레임 합성 — raw 실플레이 캡처(1080x2400) → 1080x1920 마케팅 프레임 5장 × 로케일."""
import asyncio, json, os, sys, glob, re, base64
from hchrome import HChrome
ROOT=os.path.dirname(os.path.abspath(__file__)); CAP=json.load(open(f"{ROOT}/captions.json"))
BG=[("#FFB347","#FF7A3D"),("#7C6CFF","#4F8BFF"),("#FF6F91","#FF4D6D"),("#34C98B","#1FA971"),("#4F8BFF","#3061D0")]
FONT={"ko-KR":("Black+Han+Sans","'Black Han Sans'",400),"ja-JP":("M+PLUS+Rounded+1c:wght@800","'M PLUS Rounded 1c'",800),
      "zh-CN":("Noto+Sans+SC:wght@900","'Noto Sans SC'",900),"zh-TW":("Noto+Sans+TC:wght@900","'Noto Sans TC'",900)}
SUB={"ko-KR":"'Apple SD Gothic Neo'","ja-JP":"'Hiragino Sans'","zh-CN":"'PingFang SC'","zh-TW":"'PingFang TC'"}
SPR="http://127.0.0.1:8765/TemplateData/sprites/Animal_%d.webp"
def b64(p): return "data:image/png;base64,"+base64.b64encode(open(p,'rb').read()).decode()
def pick(loc):
    fs=sorted(glob.glob(f"{ROOT}/raw/{loc}/d*_s*.png"))
    assert len(fs)>=6,(loc,len(fs))
    from PIL import Image
    def dim(p):
        im=Image.open(p).convert('L').crop((0,0,300,100)); return sum(im.getdata())/(300*100)<200
    ok=[f for f in fs if not dim(f)]
    return {"hero":ok[int(len(ok)*0.85)],"full":ok[-1],"home":f"{ROOT}/raw/{loc}/z_home.png","over":f"{ROOT}/raw/{loc}/z_gameover.png"}
IOS=os.environ.get("IOS")=="1"
W,H,HEAD,PADT,CW,CH,SC=(1290,2796,640,140,1160,2000,1160/1080) if IOS else (1080,1920,400,80,948,1440,948/1080)
OUTDIR="out_ios" if IOS else "out"
def html(loc,i,img):
    fam=FONT.get(loc,("Nunito:wght@900","'Nunito'",900)); h,s=CAP[loc][i]; c1,c2=BG[i]
    if i==1:
        sizes=[int(x*(1.18 if IOS else 1)) for x in (150,170,190,215,240,270,300,350,390,440,520)]
        rows=[[1,2,3,4],[5,6,7],[8,9],[10,11]]
        body="<div class='chain'>"+"".join("<div class='row'>"+"<span class='arr'>›</span>".join(f"<img src='{SPR%n}' style='width:{sizes[n-1]}px'>" for n in r)+"</div>" for r in rows)+"</div>"
    else:
        crop={0:(130,0),2:(130,0),3:(320,0),4:(360,0)}[i]
        body=f"<div class='card'><img src='{b64(img)}' style='margin-top:-{crop[0]*SC:.0f}px'></div>"
    return f"""<!doctype html><html><head><meta charset=utf-8>
<link href="https://fonts.googleapis.com/css2?family={fam[0]}&family=Nunito:wght@700;900&display=block" rel=stylesheet>
<style>*{{margin:0;box-sizing:border-box}}body{{width:{W}px;height:{H}px;overflow:hidden;background:linear-gradient(165deg,{c1},{c2});font-family:{fam[1]},'Nunito',sans-serif;color:#fff;text-align:center}}
.head{{height:{HEAD}px;padding:{PADT}px 56px 0;display:flex;flex-direction:column;align-items:center;justify-content:center;gap:22px}}
h1{{font-size:{"116" if IOS else "96"}px;line-height:1.08;font-weight:{fam[2]};white-space:nowrap;letter-spacing:-1px;text-shadow:0 6px 0 rgba(0,0,0,.12)}}
p{{font-size:{"48" if IOS else "40"}px;font-weight:700;font-family:{SUB.get(loc,"'Nunito'")},'Nunito',sans-serif;opacity:.95;white-space:nowrap}}
.card{{width:{CW}px;height:{CH}px;margin:20px auto 0;border-radius:64px;overflow:hidden;background:#fdf8f6;box-shadow:0 30px 80px rgba(0,0,0,.28),0 0 0 10px rgba(255,255,255,.35)}}
.card img{{width:{CW}px;display:block}}
.chain{{margin-top:20px;display:flex;flex-direction:column;align-items:center;gap:18px}}.row{{display:flex;align-items:center;justify-content:center;gap:6px}}
.row img{{filter:drop-shadow(0 12px 18px rgba(0,0,0,.25))}}.arr{{font-size:90px;font-weight:900;opacity:.85;font-family:'Nunito'}}
</style></head><body><div class=head><h1 id=h>{h}</h1><p id=s>{s}</p></div>{body}
<script>document.fonts.ready.then(()=>{{for(const [id,min] of [['h',54],['s',26]]){{const e=document.getElementById(id);let f=parseFloat(getComputedStyle(e).fontSize);while(e.offsetWidth>{W-112}&&f>min){{f-=1;e.style.fontSize=f+'px'}}}}document.title='ready'}})</script></body></html>"""
async def main(locs):
    os.makedirs(f"{ROOT}/html",exist_ok=True)
    async with HChrome(port=9399,lang='en-US') as c:
        await c.metrics(W,H,1,mobile=False)
        for loc in locs:
            src=pick(loc); out=f"{ROOT}/{OUTDIR}/{loc}"; os.makedirs(out,exist_ok=True)
            for i,key in enumerate(["hero",None,"full","home","over"]):
                p=f"{ROOT}/html/{loc}_{i}.html"; open(p,'w').write(html(loc,i,src[key] if key else None))
                await c.goto("file://"+p,settle=0.5,flutter=False)
                for _ in range(60):
                    if await c.eval("document.title")=="ready": break
                    await asyncio.sleep(0.25)
                await asyncio.sleep(0.6); await c.screenshot(f"{out}/{i+1}.png")
            print(loc,'done',flush=True)
asyncio.run(main(sys.argv[1:]))
