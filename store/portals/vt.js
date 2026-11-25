// Virtual clock for frame-exact capture (injected before the game boots).
// Normal mode runs on the wall clock (through our own timer/rAF queue). After
// __vt.start() time is frozen; __vt.step(ms) advances it, fires due timers and
// one animation frame — so a loaded machine can't drop or stretch frames.
// Math.random is seeded too (reset on start). Unity's own RNG is not — it lives
// in the wasm — so the animal sequence still differs from run to run.
(() => {
  const SEED = 0x12c4e55;
  let seed = SEED;
  Math.random = () => {
    seed = (seed + 0x6d2b79f5) | 0;
    let t = Math.imul(seed ^ (seed >>> 15), 1 | seed);
    t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t;
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };

  const N = {
    raf: window.requestAnimationFrame.bind(window),
    st: window.setTimeout.bind(window),
    ct: window.clearTimeout.bind(window),
    now: performance.now.bind(performance),
    dnow: Date.now.bind(Date),
  };
  const dateBase = N.dnow() - N.now();
  let controlled = false, delta = 0, frozen = 0;
  const vnow = () => (controlled ? frozen : N.now() + delta);
  performance.now = vnow;
  Date.now = () => Math.floor(dateBase + vnow());

  // ---- timers
  let nextId = 1;
  const timers = new Map();
  let nativeTimer = null;
  const call = (fn, args) => {
    try { typeof fn === 'function' ? fn(...args) : (0, eval)(String(fn)); }
    catch (e) { console.error('vt timer', e); }
  };
  function earliest(limit) {
    let best = null;
    for (const [id, t] of timers) {
      if (t.due <= limit && (!best || t.due < best.t.due || (t.due === best.t.due && id < best.id))) best = { id, t };
    }
    return best;
  }
  function pumpReal() {
    nativeTimer = null;
    if (controlled) return;
    const now = vnow();
    let b;
    while ((b = earliest(now))) {
      if (b.t.iv != null) b.t.due += b.t.iv; else timers.delete(b.id);
      call(b.t.fn, b.t.args);
    }
    kick();
  }
  function kick() {
    if (controlled) return;
    let min = Infinity;
    for (const t of timers.values()) min = Math.min(min, t.due);
    if (min === Infinity) return;
    if (nativeTimer != null) N.ct(nativeTimer);
    nativeTimer = N.st(pumpReal, Math.max(0, min - vnow()));
  }
  const add = (fn, ms, args, iv) => {
    const id = nextId++;
    ms = Math.max(0, +ms || 0);
    timers.set(id, { due: vnow() + ms, fn, args, iv: iv ? Math.max(1, ms) : null });
    kick();
    return id;
  };
  window.setTimeout = (fn, ms, ...args) => add(fn, ms, args, false);
  window.setInterval = (fn, ms, ...args) => add(fn, ms, args, true);
  window.clearTimeout = window.clearInterval = (id) => { timers.delete(id); };

  // ---- animation frames
  let rafId = 1, rafPending = false;
  const rafs = new Map();
  function runFrame(t) {
    const cbs = [...rafs.values()];
    rafs.clear();
    for (const cb of cbs) { try { cb(t); } catch (e) { console.error('vt raf', e); } }
  }
  function realFrame() { rafPending = false; if (!controlled) runFrame(vnow()); }
  window.requestAnimationFrame = (cb) => {
    const id = rafId++;
    rafs.set(id, cb);
    if (!controlled && !rafPending) { rafPending = true; N.raf(realFrame); }
    return id;
  };
  window.cancelAnimationFrame = (id) => { rafs.delete(id); };

  // macrotask yield without the nested-setTimeout clamp
  const ch = new MessageChannel();
  const waiters = [];
  ch.port1.onmessage = () => { const w = waiters.shift(); w && w(); };
  const yieldTask = () => new Promise((r) => { waiters.push(r); ch.port2.postMessage(0); });

  window.__vt = {
    start() {
      seed = SEED; // whatever ran in real time before, the recorded part replays the same
      frozen = vnow();
      controlled = true;
      if (nativeTimer != null) { N.ct(nativeTimer); nativeTimer = null; }
      return frozen;
    },
    async step(ms) {
      const target = frozen + ms;
      for (let guard = 0; guard < 5000; guard++) {
        const b = earliest(target);
        if (!b) break;
        frozen = Math.max(frozen, b.t.due);
        if (b.t.iv != null) b.t.due += b.t.iv; else timers.delete(b.id);
        call(b.t.fn, b.t.args);
        await yieldTask();
      }
      frozen = target;
      runFrame(frozen);
      for (let i = 0; i < 3; i++) await yieldTask();
      return frozen;
    },
    stop() {
      delta = frozen - N.now();
      controlled = false;
      kick();
      if (rafs.size && !rafPending) { rafPending = true; N.raf(realFrame); }
    },
  };
})();
