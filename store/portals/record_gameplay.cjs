#!/usr/bin/env node
// Records the gameplay part of the portal preview videos from the Playgama
// build: a jar already half full (warm-up drops, not recorded), then up to 45 s
// of dropping, keeping the 16.5 s stretch where the score climbs most (merges
// chaining), as 1080x2160 JPEG frames at an exact 30 fps.
//
// Headless only (no window, no focus). The page clock is virtual (vt.js): Unity's
// main loop runs on requestAnimationFrame + performance.now, so it only advances
// when a frame is captured — frames are exactly 1/30 s apart on any machine.
// Drops sort animals into columns by level (small left, big right) so they meet
// their twins and merge; the next-animal roll is Unity's own RNG, so each run
// plays a different (but equally busy) game.
//
// Usage: node store/portals/record_gameplay.cjs [outDir] [--web DIR]
//   default web root = build/animal-pop-playgama.zip unpacked to a temp dir
//   (the upload itself). Then run make_videos.sh on outDir.
const path = require('path');
const fs = require('fs');
const os = require('os');
const http = require('http');
const { execFileSync } = require('child_process');
const { chromium } = require(process.env.PLAYWRIGHT || '/Users/yong/Desktop/home-it-pick/node_modules/playwright');

const HERE = __dirname;
const ROOT = path.join(HERE, '..', '..');
const args = process.argv.slice(2);
const webArg = args.indexOf('--web');
const positional = args.filter((a, i) => !a.startsWith('--') && (webArg < 0 || i !== webArg + 1));
const OUT = path.resolve(positional[0] || path.join(os.tmpdir(), 'ap-frames'));
const MIME = {
  '.html': 'text/html', '.js': 'text/javascript', '.json': 'application/json', '.css': 'text/css',
  '.png': 'image/png', '.webp': 'image/webp', '.jpg': 'image/jpeg', '.svg': 'image/svg+xml',
  '.woff2': 'font/woff2', '.gz': 'application/octet-stream', '.wasm': 'application/wasm',
};

// 450x900 CSS px = the game's 1:2 portrait canvas, x2.4 → 1080x2160 frames.
const VIEW = { width: 450, height: 900 };
const START = [225, 641];    // landing "Start"
const DROP_Y = 300;          // drops follow the pointer's x
const COL = { 1: 140, 2: 182, 3: 225, 4: 267, 5: 310 };  // by level; bigger → right wall
const REST = [449, 899];     // pointer parks in the corner between drops
const WARMUP = 30;           // unrecorded drops to half-fill the jar
const KEEP_S = 16.5;         // gameplay seconds kept for the video
const RECORD_S = 45;         // recorded, to pick the busiest KEEP_S from
const GAP = [27, 33];        // frames between drops (the game's cooldown is 0.85 s)

let seed = 7;                // jitter only; the animals come from Unity's RNG
const rand = () => ((seed = (seed * 1103515245 + 12345) & 0x7fffffff) / 0x7fffffff);

function serve(root) {
  const srv = http.createServer((req, res) => {
    let p = decodeURIComponent(req.url.split('?')[0]);
    if (p.endsWith('/')) p += 'index.html';
    const f = path.join(root, p);
    if (!f.startsWith(root) || !fs.existsSync(f)) { res.statusCode = 404; return res.end(); }
    res.setHeader('Content-Type', MIME[path.extname(f)] || 'application/octet-stream');
    fs.createReadStream(f).pipe(res);
  });
  return new Promise((r) => srv.listen(0, '127.0.0.1', () => r(srv)));
}

(async () => {
  let web = webArg >= 0 ? path.resolve(args[webArg + 1]) : null;
  if (!web) {
    web = fs.mkdtempSync(path.join(os.tmpdir(), 'ap-web-'));
    execFileSync('unzip', ['-q', path.join(ROOT, 'build', 'animal-pop-playgama.zip'), '-d', web]);
  }
  if (!fs.existsSync(path.join(web, 'playgama_bridge.js'))) throw new Error(`${web} is not the Playgama build`);
  fs.rmSync(OUT, { recursive: true, force: true });
  fs.mkdirSync(OUT, { recursive: true });
  const srv = await serve(web);
  // Metal-backed GL; SwiftShader (the e2e flags) also works but renders slower.
  const browser = await chromium.launch({ headless: true, args: ['--use-angle=metal', '--enable-gpu', '--ignore-gpu-blocklist', '--mute-audio'] });
  try {
    const ctx = await browser.newContext({ viewport: VIEW, deviceScaleFactor: 2.4, locale: 'en-US' });
    await ctx.addInitScript({ path: path.join(HERE, 'vt.js') });
    const page = await ctx.newPage();
    let n = 0;
    const step = async (shoot) => {
      await page.evaluate('__vt.step(1000/30)');
      if (shoot) await page.screenshot({ path: path.join(OUT, `${String(n++).padStart(5, '0')}.jpg`), type: 'jpeg', quality: 93, timeout: 120000 });
    };
    const hud = () => page.evaluate(() => ({
      next: +((/Animal_(\d+)/.exec((document.getElementById('next-img') || {}).src || '') || [])[1] || 0),
      score: document.getElementById('score').innerText,
      drops: +document.getElementById('msb-drop').textContent,
      merges: +document.getElementById('msb-merge').textContent,
      over: document.getElementById('gameover-overlay').classList.contains('visible'),
    }));
    let cur = 0;  // level of the animal in hand (= the previous "next")
    const drop = async (frames, shoot) => {
      const before = await hud();
      const x = (COL[cur] || 225) + Math.round((rand() - 0.5) * 16);
      await page.mouse.move(x, DROP_Y);
      await page.mouse.down();
      await step(shoot);
      await step(shoot);
      await page.mouse.up();
      await page.mouse.move(...REST);
      for (let k = 2; k < frames; k++) await step(shoot);
      cur = before.next;
      const after = await hud();
      return after;
    };

    await page.goto(`http://127.0.0.1:${srv.address().port}/index.html`, { waitUntil: 'load', timeout: 300000 });
    await page.waitForSelector('#landing-overlay.visible', { timeout: 300000 });
    await page.waitForTimeout(1500);
    await page.mouse.click(...START);
    await page.mouse.move(...REST);
    await page.waitForSelector('#game-hud.visible', { timeout: 30000 });
    await page.waitForTimeout(1500);

    await page.evaluate('__vt.start()');
    let st = await hud();
    cur = 0;
    for (let i = 0; i < WARMUP; i++) st = await drop(GAP[0], false);
    for (let i = 0; i < 20; i++) await step(false);  // let the pile settle
    console.log(`warm-up: ${st.drops} drops, ${st.merges} merges, score ${st.score}`);

    const total = Math.round(RECORD_S * 30), keep = Math.round(KEEP_S * 30);
    const marks = [[0, +st.score.replace(/,/g, '')]];  // [frame, score] at each drop
    while (n < total) {
      const gap = GAP[0] + Math.floor(rand() * (GAP[1] - GAP[0] + 1));
      st = await drop(gap, true);
      if (st.over) break;  // the game-over frames are never kept (window ends at the last mark)
      marks.push([n, +st.score.replace(/,/g, '')]);
      process.stdout.write(`\r${n}/${total} frames, drops ${st.drops}, merges ${st.merges}, score ${st.score}   `);
    }
    // busiest window: starts on a drop, the score gain it covers is maximal
    let best = null;
    for (const [f0, s0] of marks) {
      if (f0 + keep > marks[marks.length - 1][0]) break;
      const s1 = marks.filter(([f]) => f <= f0 + keep).pop()[1];
      if (!best || s1 - s0 > best.gain) best = { f0, gain: s1 - s0 };
    }
    if (!best) throw new Error(`only ${marks[marks.length - 1][0]} frames before game over — lower WARMUP`);
    // keep that window, renumbered from 00000 (in order, so no rename clobbers a kept frame)
    for (const f of fs.readdirSync(OUT).sort()) {
      const i = +f.slice(0, 5);
      if (i >= best.f0 && i < best.f0 + keep) fs.renameSync(path.join(OUT, f), path.join(OUT, String(i - best.f0).padStart(5, '0') + '.jpg'));
      else fs.unlinkSync(path.join(OUT, f));
    }
    console.log(`\nkept frames ${best.f0}..${best.f0 + keep - 1} (score +${best.gain}) → ${OUT}`);
  } finally {
    await browser.close();
    srv.close();
  }
})().catch((e) => { console.error(e); process.exit(1); });
