import asyncio, sys, re, random, json, os
from hchrome import HChrome
LANG=sys.argv[1] if len(sys.argv)>1 else 'en-US'
DROPS=int(sys.argv[2]) if len(sys.argv)>2 else 140
SEED=int(sys.argv[3]) if len(sys.argv)>3 else 7
OUT=f"raw/{LANG}"; os.makedirs(OUT,exist_ok=True)
COL={1:112,2:146,3:180,4:214,5:248}
JS="JSON.stringify({score:(document.getElementById('score')||{}).innerText,next:(document.getElementById('next-img')||{}).src,go:(function(){var e=document.getElementById('gameover-overlay')||document.querySelector('.go-overlay,#go-overlay');return e?getComputedStyle(e).display+'|'+getComputedStyle(e).opacity+'|'+e.className:null})(),drops:(document.getElementById('msb-drop')||{}).innerText,merges:(document.getElementById('msb-merge')||{}).innerText})"
def lvl(src):
    m=re.search(r'(\d+)[^/\d]*\.(png|webp)',src or ''); return int(m.group(1)) if m else None
async def main():
    random.seed(SEED)
    async with HChrome(port=9341+SEED%50,lang=LANG) as c:
        await c.metrics(360,800,3)
        await c.goto("http://127.0.0.1:8765/index.html",settle=6,flutter=False)
        for _ in range(12):
            await c.tap(180,561); await asyncio.sleep(3)
            if lvl(json.loads(await c.eval(JS))['next']): break
        st=json.loads(await c.eval(JS)); print('start',st,flush=True)
        cur=None
        for i in range(DROPS):
            st=json.loads(await c.eval(JS)); nxt=lvl(st['next'])
            x=COL.get(cur,180)+random.randint(-10,10) if cur else 180
            await c.tap(x,300); await asyncio.sleep(1.15)
            cur=nxt
            st=json.loads(await c.eval(JS))
            if i%5==4:
                sc=(st['score'] or '0').replace(',','')
                await c.screenshot(f"{OUT}/d{i+1:03d}_s{sc}.png"); print(i+1,st['score'],st['merges'],st['go'],nxt,flush=True)
            go=(st.get('go') or '||').split('|')
            if len(go)>1 and go[1] and float(go[1])>0.5:
                await asyncio.sleep(1.5); await c.screenshot(f"{OUT}/z_gameover.png"); print('GAMEOVER',st['score'],flush=True); break
        await c.goto("http://127.0.0.1:8765/index.html",settle=7,flutter=False)
        await c.screenshot(f"{OUT}/z_home.png")
asyncio.run(main())
