"""Headless variant of the skill's Chrome launcher (no window = no focus steal). Unity WebGL renders via SwiftShader."""
import asyncio, json, os, subprocess, urllib.request, websockets
import cdp
class HChrome(cdp.Chrome):
    def __init__(self, port=9341, lang='en-US'):
        super().__init__(port=port); self.lang=lang
    async def __aenter__(self):
        os.makedirs(self.profile, exist_ok=True)
        self.proc = subprocess.Popen([cdp.CHROME,"--headless=new",f"--remote-debugging-port={self.port}",f"--user-data-dir={self.profile}",
            "--no-first-run","--no-default-browser-check","--disable-extensions","--mute-audio",
            "--use-angle=swiftshader","--enable-unsafe-swiftshader","--ignore-gpu-blocklist",
            f"--lang={self.lang}",f"--accept-lang={self.lang}","--hide-scrollbars","--window-size=440,920","about:blank"],
            stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
        url=None
        for _ in range(80):
            try:
                with urllib.request.urlopen(f"http://localhost:{self.port}/json") as r: data=json.load(r)
                pages=[t for t in data if t.get("type")=="page" and t.get("webSocketDebuggerUrl")]
                if pages: url=pages[0]["webSocketDebuggerUrl"]; break
            except Exception: pass
            await asyncio.sleep(0.25)
        if not url: raise RuntimeError("no devtools target")
        self.ws=await websockets.connect(url,max_size=256*1024*1024)
        self._reader=asyncio.create_task(self._read_loop())
        await self.send("Page.enable"); await self.send("Runtime.enable")
        return self
