// host.js 단위 테스트 — @playgama/bridge를 가짜로 바꿔 번들하고, 시나리오마다 새 vm 컨텍스트 +
// 가짜 시계로 돌린다: 초기화·선로드 타임아웃, 저장 병합·멈춘 쓰기 재전송, 광고 결과·침묵 타임아웃,
// Unity 빌드 gzip 해제(fetch 심).   node test/host.test.mjs
import assert from 'node:assert/strict';
import { gzipSync } from 'node:zlib';
import { fileURLToPath } from 'node:url';
import vm from 'node:vm';
import * as esbuild from 'esbuild';

const here = fileURLToPath(new URL('.', import.meta.url));
const MOCK = `
const S = globalThis.__mock;
export const EVENT_NAME = { AUDIO_STATE_CHANGED: 'audio_state_changed', PAUSE_STATE_CHANGED: 'pause_state_changed',
  INTERSTITIAL_STATE_CHANGED: 'interstitial_state_changed', REWARDED_STATE_CHANGED: 'rewarded_state_changed' };
export const INTERSTITIAL_STATE = { LOADING: 'loading', OPENED: 'opened', CLOSED: 'closed', FAILED: 'failed' };
export const REWARDED_STATE = { LOADING: 'loading', OPENED: 'opened', CLOSED: 'closed', FAILED: 'failed', REWARDED: 'rewarded' };
export const PLATFORM_MESSAGE = { GAME_READY: 'game_ready', LEVEL_STARTED: 'level_started', LEVEL_FAILED: 'level_failed', GAMEPLAY_STARTED: 'gameplay_started' };
const subs = {};
export default {
  initialize: () => S.init(),
  platform: {
    id: 'mock', language: 'ko', isPaused: false, isAudioEnabled: true,
    on: (e, f) => ((subs[e] = subs[e] || []).push(f)),
    sendMessage: (m) => { S.messages.push(m); return Promise.resolve(); },
  },
  storage: { get: (k, p) => S.get(k, p), set: (k, v) => S.set(k, v) },
  advertisement: {
    isInterstitialSupported: true, isRewardedSupported: true, interstitialState: 'closed', rewardedState: 'closed',
    on: (e, f) => ((subs[e] = subs[e] || []).push(f)),
    off: (e, f) => { subs[e] = (subs[e] || []).filter((g) => g !== f); },
    showInterstitial: () => S.show('interstitial'),
    showRewarded: () => S.show('rewarded'),
  },
};
S.emit = (e, v) => (subs[e] || []).slice().forEach((f) => f(v));
`;

const bundle = await esbuild.build({
  entryPoints: [here + '../src/host.js'],
  bundle: true, write: false, format: 'iife', target: 'es2020', logLevel: 'silent',
  plugins: [{
    name: 'mock-bridge',
    setup(b) {
      b.onResolve({ filter: /^@playgama\/bridge$/ }, () => ({ path: 'mock', namespace: 'mock' }));
      b.onLoad({ filter: /.*/, namespace: 'mock' }, () => ({ contents: MOCK, loader: 'js' }));
    },
  }],
});
const CODE = bundle.outputFiles[0].text;

// ── 가짜 시계 ──
function clock() {
  let now = 0, seq = 0;
  const timers = new Map();
  return {
    setTimeout(fn, ms = 0) { const id = ++seq; timers.set(id, { at: now + ms, fn }); return id; },
    clearTimeout(id) { timers.delete(id); },
    async advance(ms) {
      const end = now + ms;
      for (;;) {
        await flush();
        const due = [...timers.entries()].filter(([, t]) => t.at <= end).sort((a, b) => a[1].at - b[1].at)[0];
        if (!due) break;
        timers.delete(due[0]);
        now = due[1].at;
        due[1].fn();
      }
      now = end;
      await flush();
    },
  };
}
const flush = () => new Promise((r) => setImmediate(r));
// vm 컨텍스트의 배열은 프로토타입이 달라 deepStrictEqual이 거부한다 → 이 realm으로 복사
const plain = (v) => JSON.parse(JSON.stringify(v));
const deferred = () => { let resolve, reject; const p = new Promise((a, b) => { resolve = a; reject = b; }); return { p, resolve, reject }; };

function boot({ init = () => Promise.resolve(), get, set, show, local = {}, gz = {}, fetchImpl } = {}) {
  const c = clock();
  const store = new Map(Object.entries(local));
  const listeners = {};
  const target = { addEventListener: (e, f) => ((listeners[e] = listeners[e] || []).push(f)) };
  const mock = {
    messages: [], writes: [], init,
    get: get || ((keys) => Promise.resolve(keys.map(() => null))),
    set: set || ((k, v) => { mock.writes.push([k, v]); return Promise.resolve(); }),
    show: show || (() => {}),
  };
  const sandbox = {
    __mock: mock, console,
    setTimeout: c.setTimeout, clearTimeout: c.clearTimeout,
    Headers, Response, ReadableStream, DecompressionStream, Uint8Array, Promise, Map, Set, JSON, Math, Number, Object, Error, String, Array,
    localStorage: { getItem: (k) => (store.has(k) ? store.get(k) : null), setItem: (k, v) => store.set(k, String(v)) },
    document: { ...target, visibilityState: 'visible' },
    fetch: fetchImpl || (() => Promise.reject(new Error('no fetch'))),
    AP_PG_GZ: gz,
  };
  sandbox.window = sandbox;
  sandbox.addEventListener = target.addEventListener;
  vm.runInNewContext(CODE, sandbox);
  return { host: sandbox.PlaygamaHost, mock, store, clock: c, sandbox };
}

const tests = [];
const test = (name, fn) => tests.push([name, fn]);

test('선로드 → localStorage 채움, 최고점은 큰 쪽 유지', async () => {
  const t = boot({
    local: { animalpop_best: '500', animalpop_bgm: '1' },
    get: () => Promise.resolve(['300', '0', null, '{"d":1}', null]),
  });
  assert.equal(await t.host.ready, true);
  assert.equal(t.store.get('animalpop_best'), '500'); // 로컬이 더 큼 → 유지
  assert.equal(t.store.get('animalpop_bgm'), '0'); // 원격 우선
  assert.equal(t.store.get('animalpop_dc'), '{"d":1}');
  await t.clock.advance(0);
  assert.deepEqual(plain(t.mock.writes), [[['animalpop_best'], ['500']]]); // 로컬에만 있던 큰 값만 올림
});

test('jslib가 로컬 최고점을 낮춰도 브리지로는 안 내려감 + best()는 최댓값', async () => {
  const t = boot({ get: () => Promise.resolve(['900', null, null, null, null]) });
  await t.host.ready;
  t.store.set('animalpop_best', '120'); // Unity PlayerPrefs 값으로 덮임
  t.host.save();
  await t.clock.advance(0);
  assert.deepEqual(t.mock.writes, []);
  assert.equal(t.host.best(50), 900);
});

test('초기화 8초 타임아웃 → 호스트 없이 진행, 저장·광고 안 함', async () => {
  const t = boot({ init: () => new Promise(() => {}) });
  let done = null;
  t.host.ready.then((v) => (done = v));
  await t.clock.advance(7999);
  assert.equal(done, null);
  await t.clock.advance(1);
  assert.equal(done, false);
  t.store.set('animalpop_best', '10');
  t.host.save();
  await t.clock.advance(0);
  assert.deepEqual(t.mock.writes, []);
  assert.equal(await t.host.showRewarded(), 'failed');
  assert.equal(t.host.platformId(), null);
});

test('선로드 실패 → 이번 세션은 브리지에 안 씀 (클라우드 세이브 보호)', async () => {
  const t = boot({ get: () => new Promise(() => {}), local: { animalpop_best: '5' } });
  const r = t.host.ready;
  await t.clock.advance(5000);
  assert.equal(await r, true);
  t.host.save();
  await t.clock.advance(0);
  assert.deepEqual(t.mock.writes, []);
});

test('쓰기 1건씩, 그 사이 변경은 다음 배치로 합침', async () => {
  const pend = [];
  const t = boot({ set: (k, v) => { t.mock.writes.push([k, v]); const d = deferred(); pend.push(d); return d.p; } });
  await t.host.ready;
  await t.clock.advance(0);
  t.store.set('animalpop_best', '1'); t.host.save();
  await t.clock.advance(0);
  t.store.set('animalpop_best', '2'); t.host.save();
  t.store.set('animalpop_bgm', '0'); t.host.save();
  await t.clock.advance(0);
  assert.equal(t.mock.writes.length, 1);
  pend[0].resolve();
  await t.clock.advance(0);
  assert.deepEqual(plain(t.mock.writes[1]), [['animalpop_best', 'animalpop_bgm'], ['2', '0']]);
});

test('8초 멈춘 쓰기가 나중에 끝나면 최신 값을 다시 보냄', async () => {
  const pend = [];
  const t = boot({ set: (k, v) => { t.mock.writes.push([k, v]); const d = deferred(); pend.push(d); return d.p; } });
  await t.host.ready;
  t.store.set('animalpop_best', '100'); t.host.save();
  await t.clock.advance(8000); // 타임아웃
  t.store.set('animalpop_best', '200'); t.host.save(); // 새 쓰기 (옛 것은 아직 매달림)
  await t.clock.advance(0);
  assert.deepEqual(t.mock.writes.map((w) => w[1][0]), ['100', '200']);
  pend[1].resolve();
  await t.clock.advance(0);
  pend[0].resolve(); // 옛 쓰기가 늦게 도착 → 200을 다시 보내야 한다
  await t.clock.advance(0);
  assert.deepEqual(t.mock.writes.map((w) => w[1][0]), ['100', '200', '200']);
});

test('보상형: rewarded 받은 경우만 보상, 광고 동안 일시정지 알림', async () => {
  let paused = [];
  const t = boot({ show: () => ['loading', 'opened', 'rewarded', 'closed'].forEach((s, i) => t.sandbox.setTimeout(() => t.mock.emit('rewarded_state_changed', s), 100 * (i + 1))) });
  await t.host.ready;
  t.host.onPause((p) => paused.push(p));
  const r = t.host.showRewarded();
  assert.equal(await t.host.showRewarded(), 'busy'); // 중복 탭
  await t.clock.advance(1000);
  assert.equal(await r, 'rewarded');
  assert.deepEqual(paused, [true, false]);
});

test('보상형: 중간에 닫음 → closed, 실패 → failed', async () => {
  let seq;
  const t = boot({ show: () => seq.forEach((s, i) => t.sandbox.setTimeout(() => t.mock.emit('rewarded_state_changed', s), 100 * (i + 1))) });
  await t.host.ready;
  seq = ['loading', 'opened', 'closed'];
  let r = t.host.showRewarded(); await t.clock.advance(1000);
  assert.equal(await r, 'closed');
  seq = ['loading', 'failed'];
  r = t.host.showRewarded(); await t.clock.advance(1000);
  assert.equal(await r, 'failed');
});

test('보상형: 45초 침묵 → failed (rewarded 뒤 침묵이어도 보상 없음), 늦은 이벤트 무시', async () => {
  const t = boot({ show: () => t.sandbox.setTimeout(() => t.mock.emit('rewarded_state_changed', 'rewarded'), 100) });
  await t.host.ready;
  const paused = [];
  t.host.onPause((p) => paused.push(p));
  const r = t.host.showRewarded();
  await t.clock.advance(100 + 44999);
  let v = null; r.then((x) => (v = x)); await t.clock.advance(0);
  assert.equal(v, null);
  await t.clock.advance(1);
  assert.equal(await r, 'failed');
  assert.deepEqual(paused, [true, false]);
});

test('전면: 30초 침묵이어도 resolve', async () => {
  const t = boot({ show: () => {} });
  await t.host.ready;
  const r = t.host.showInterstitial();
  await t.clock.advance(30000);
  assert.equal(await r, undefined);
});

test('메시지: game_ready는 1회', async () => {
  const t = boot();
  await t.host.ready;
  t.host.gameReady(); t.host.gameReady(); t.host.levelStarted(); t.host.levelFailed();
  assert.deepEqual(plain(t.mock.messages), ['game_ready', 'level_started', 'level_failed']);
});

test('fetch 심: gzip 해제 + 원본 Content-Length, 이미 풀린 응답은 그대로', async () => {
  const raw = new Uint8Array(200000).map((_, i) => (i * 7) % 251);
  const gz = { 'Build/a.data.gz': raw.length, 'Build/a.wasm.gz': raw.length };
  let serveGz = true;
  const t = boot({ gz, fetchImpl: () => Promise.resolve(new Response(serveGz ? gzipSync(raw) : raw)) });
  let res = await t.sandbox.fetch('Build/a.data.gz');
  assert.equal(res.headers.get('Content-Length'), String(raw.length));
  assert.deepEqual(new Uint8Array(await res.arrayBuffer()), raw);
  serveGz = false; // 호스트가 Content-Encoding: gzip으로 이미 풀어서 줌
  res = await t.sandbox.fetch('https://cdn.example/g/Build/a.wasm.gz?v=1');
  assert.equal(res.headers.get('Content-Type'), 'application/wasm');
  assert.deepEqual(new Uint8Array(await res.arrayBuffer()), raw);
});

let failed = 0;
for (const [name, fn] of tests) {
  try {
    await fn();
    console.log('PASS', name);
  } catch (e) {
    failed++;
    console.log('FAIL', name, '\n  ', e.message);
  }
}
console.log(failed ? `\n${failed} FAIL` : '\nALL PASS');
process.exit(failed ? 1 : 0);
