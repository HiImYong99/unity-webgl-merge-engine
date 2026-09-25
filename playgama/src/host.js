// Playgama(HTML5 포털) 빌드 전용 호스트 레이어 — build.sh가 esbuild IIFE로 번들해
// index.html <head> 맨 앞에 넣는다. npm @playgama/bridge를 그대로 묶는다(CDN 로드 없음).
//
// 템플릿(Assets/WebGLTemplates/AnimalPop/index.html)은 window.PlaygamaHost가 있으면
// AP_PLATFORM = 'playgama'로 잡고 광고·저장·일시정지·포털 메시지를 여기로 보낸다.
// Toss/Android/iOS 빌드에는 이 파일이 없으므로 그쪽 동작은 그대로다.
//
// 여기서만 하는 일:
//  - Unity 빌드 파일 gzip 해제: 포털 서버가 Content-Encoding을 붙여주는지에 기대지 않는다.
//  - 세이브: 템플릿은 localStorage를 쓴다 → KEYS를 브리지 저장소에서 선로드해 localStorage에
//    채우고(ready), 체크포인트마다 바뀐 키만 브리지로 올린다(save).
//  - 디버그 단축키 차단: 빌드된 C#(GameMgr.Update)이 End=즉시 게임오버, G=3배속을 받는다.
import bridge, {
  EVENT_NAME,
  INTERSTITIAL_STATE,
  PLATFORM_MESSAGE,
  REWARDED_STATE,
} from '@playgama/bridge';
import { gunzipSync } from 'fflate';

// ─────────────────────────────────────────────── Unity 빌드 gzip 해제
// build.sh가 index.html에 { 'Build/<hash>.data.gz': 원본 바이트 수, ... }를 심는다.
// 원본 크기를 Content-Length로 주면 Unity 로더가 버퍼를 한 번에 잡고 진행률도 정확하다.
const GZ_SIZES = window.AP_PG_GZ || {};

function gzEntry(input) {
  const url = typeof input === 'string' ? input : input && input.url;
  if (!url) return null;
  const path = url.split(/[?#]/)[0];
  for (const name of Object.keys(GZ_SIZES)) {
    if (path === name || path.endsWith('/' + name)) return name;
  }
  return null;
}

function gunzippedResponse(res, name) {
  const type = name.endsWith('.wasm.gz') ? 'application/wasm' : 'application/octet-stream';
  const headers = new Headers({ 'Content-Type': type, 'Content-Length': String(GZ_SIZES[name]) });
  const reader = res.body.getReader();
  return reader.read().then((first) => {
    const head = first.value || new Uint8Array(0);
    // 호스트가 .gz에 Content-Encoding: gzip을 붙이면 브라우저가 이미 풀어서 준다 → 그대로 통과
    const gzipped = head.length >= 2 && head[0] === 0x1f && head[1] === 0x8b;
    const stream = new ReadableStream({
      start(c) {
        if (head.length) c.enqueue(head);
        if (first.done) c.close();
      },
      async pull(c) {
        const { done, value } = await reader.read();
        if (done) c.close();
        else c.enqueue(value);
      },
      cancel(reason) {
        return reader.cancel(reason);
      },
    });
    if (!gzipped) return new Response(stream, { status: 200, headers });
    if (typeof DecompressionStream === 'function') {
      return new Response(stream.pipeThrough(new DecompressionStream('gzip')), { status: 200, headers });
    }
    // DecompressionStream 없음(iOS 16.3 이하 등) → 통째로 받아 JS로 해제
    return new Response(stream)
      .arrayBuffer()
      .then((buf) => new Response(gunzipSync(new Uint8Array(buf)), { status: 200, headers }));
  });
}

const nativeFetch = window.fetch.bind(window);
window.fetch = function (input, init) {
  const name = gzEntry(input);
  if (!name) return nativeFetch(input, init);
  return nativeFetch(input, init).then((res) => (res.ok && res.body ? gunzippedResponse(res, name) : res));
};

// ─────────────────────────────────────────────────────────── 부팅
// 이 시간을 넘기면 호스트 없이 진행한다(광고 없음, 세이브는 브라우저 localStorage만).
const INIT_TIMEOUT_MS = 8000;
const PRELOAD_TIMEOUT_MS = 5000;

function within(promise, ms) {
  return Promise.race([
    promise,
    new Promise((_, reject) => setTimeout(() => reject(new Error('timeout')), ms)),
  ]);
}

// 템플릿이 localStorage에 쓰는 진행 데이터 중 포털에서 의미 있는 것만.
// 템플릿에 저장 키를 추가하면 여기에도 추가한다(안 하면 브리지에 안 올라간다).
const KEYS = [
  'animalpop_best', // 최고 점수
  'animalpop_bgm', // 배경음 '1'/'0'
  'animalpop_sfx', // 효과음 '1'/'0'
  'animalpop_dc', // 데일리 챌린지 진행
  'animalpop_streak', // 출석 스트릭
];
// 줄어들면 안 되는 값: 빌드된 jslib(_ShowHtmlLanding)가 Unity PlayerPrefs(IndexedDB)의 최고 점수로
// localStorage를 덮는다 → IndexedDB만 지워진 기기에선 더 낮은 값이 올 수 있다.
const MAX_KEYS = new Set(['animalpop_best']);

let hostUp = false; // bridge.initialize() 완료
let storageUp = false; // 선로드 성공 = 이번 세션은 브리지에 저장해도 된다
const sent = new Map(); // key → 브리지에 있다고 알고 있는 값

function localGet(key) {
  try {
    return localStorage.getItem(key);
  } catch {
    return null;
  }
}

function localSet(key, value) {
  try {
    localStorage.setItem(key, value);
  } catch {
    /* 저장소 차단 — 이번 세션 메모리로만 */
  }
}

function num(v) {
  const n = parseInt(v, 10);
  return Number.isFinite(n) ? n : 0;
}

function hydrate(values) {
  KEYS.forEach((key, i) => {
    let remote = values ? values[i] : null;
    if (remote === null || remote === undefined || remote === '') return;
    if (typeof remote !== 'string') remote = JSON.stringify(remote);
    sent.set(key, remote);
    if (MAX_KEYS.has(key) && num(localGet(key)) >= num(remote)) return;
    localSet(key, remote);
  });
}

const ready = (async () => {
  try {
    await within(bridge.initialize(), INIT_TIMEOUT_MS);
    hostUp = true;
  } catch {
    return false;
  }
  watchHost();
  try {
    // false = JSON.parse 안 함: 템플릿이 쓴 문자열 그대로
    hydrate(await within(bridge.storage.get(KEYS, false), PRELOAD_TIMEOUT_MS));
    storageUp = true;
  } catch {
    // 못 읽은 세이브를 '없음'으로 보고 올리면 클라우드 세이브를 덮는다 → 이번 세션은 로컬만
    sent.clear();
  }
  save(); // 로컬에만 있던 값(첫 실행 등)을 올린다
  return true;
})();

// ─────────────────────────────────────────────────────────── 저장
// 한 번에 쓰기 1건. 그 사이 바뀐 값은 다음 배치로 합친다.
const WRITE_TIMEOUT_MS = 8000;
let flushing = false;
let pending = false;

function current(key) {
  const v = localGet(key);
  if (!MAX_KEYS.has(key)) return v;
  const s = sent.get(key);
  return s !== undefined && num(s) > num(v) ? s : v;
}

async function flush() {
  if (flushing) return;
  flushing = true;
  try {
    while (pending) {
      pending = false;
      const keys = [];
      const values = [];
      KEYS.forEach((k) => {
        const v = current(k);
        if (v !== null && v !== sent.get(k)) {
          keys.push(k);
          values.push(v);
        }
      });
      if (keys.length === 0) break;
      let write;
      try {
        write = bridge.storage.set(keys, values);
        await within(write, WRITE_TIMEOUT_MS);
        keys.forEach((k, i) => sent.set(k, values[i]));
      } catch (e) {
        if (e?.message !== 'timeout') break; // 다음 save()에서 다시 시도
        // 멈춘 쓰기가 나중에 끝나면 옛 값이 최신을 덮을 수 있다 → 끝나는 순간 최신 값을 다시 보낸다
        const resend = () => {
          keys.forEach((k) => sent.delete(k));
          save();
        };
        Promise.resolve(write).then(resend, resend);
        break;
      }
    }
  } finally {
    flushing = false;
  }
}

function save() {
  if (!storageUp) return;
  pending = true;
  void flush();
}

document.addEventListener('visibilitychange', () => {
  if (document.visibilityState === 'hidden') save();
});
window.addEventListener('pagehide', save);

// ───────────────────────────────────────────── 호스트 일시정지·음소거
// Unity는 터치(mousedown/touchstart)와 사운드 재생 때마다 AudioContext를 resume한다 →
// suspend로는 소리를 못 막는다. Unity가 연결하는 destination 앞에 마스터 게인을 끼워 끈다.
let masterGain = null;
const NativeAudioContext = window.AudioContext || window.webkitAudioContext;
if (NativeAudioContext) {
  const HostAudioContext = function (...args) {
    const ac = new NativeAudioContext(...args);
    if (!masterGain) {
      // 첫 컨텍스트 = Unity(WEBAudio). 채널마다 gain.connect(audioContext.destination) 한다
      masterGain = ac.createGain();
      masterGain.connect(ac.destination);
      Object.defineProperty(ac, 'destination', { value: masterGain });
      applyHost();
    }
    return ac;
  };
  HostAudioContext.prototype = NativeAudioContext.prototype;
  window.AudioContext = HostAudioContext;
  if (window.webkitAudioContext) window.webkitAudioContext = HostAudioContext;
}

let hostPaused = false; // 포털이 게임을 멈춤(탭 전환·포털 UI)
let hostAudio = true; // 포털 음소거 버튼 등
let adRunning = false; // 우리가 띄운 광고 진행 중
let pauseListener = null;
let pausedNotified = false;

function applyHost() {
  if (masterGain) masterGain.gain.value = hostPaused || !hostAudio || adRunning ? 0 : 1;
  const paused = hostPaused || adRunning;
  if (paused === pausedNotified) return;
  pausedNotified = paused;
  try {
    pauseListener?.(paused);
  } catch {
    /* 템플릿 쪽 오류는 삼킨다 */
  }
}

function readHost() {
  try {
    hostPaused = bridge.platform.isPaused === true;
    hostAudio = bridge.platform.isAudioEnabled !== false;
  } catch {
    /* 호스트 상태 없음 = 재생 */
  }
  applyHost();
}

function watchHost() {
  try {
    bridge.platform.on(EVENT_NAME.AUDIO_STATE_CHANGED, readHost);
    bridge.platform.on(EVENT_NAME.PAUSE_STATE_CHANGED, readHost);
  } catch {
    /* 이벤트 없는 호스트 */
  }
  readHost();
}

// ─────────────────────────────────────────────────────────── 광고
// SDK가 아직 다른 광고 상태에 있음(침묵 타임아웃 뒤 opened에 머문 경우 포함 — 그 세션은 광고 없이 진행)
function adBusy() {
  try {
    const a = bridge.advertisement;
    return (
      a.interstitialState === INTERSTITIAL_STATE.LOADING ||
      a.interstitialState === INTERSTITIAL_STATE.OPENED ||
      a.rewardedState === REWARDED_STATE.LOADING ||
      a.rewardedState === REWARDED_STATE.OPENED ||
      a.rewardedState === REWARDED_STATE.REWARDED
    );
  } catch {
    return true;
  }
}

function adSupported(kind) {
  if (!hostUp) return false;
  try {
    return kind === 'interstitial'
      ? bridge.advertisement.isInterstitialSupported === true
      : bridge.advertisement.isRewardedSupported === true;
  } catch {
    return false;
  }
}

// 호스트가 광고 도중 closed/failed 없이 침묵해도 게임이 멈춘 채 남지 않게.
// 상태 이벤트마다 시계를 다시 거니 실제 광고 길이가 아니라 '침묵'을 제한한다.
const AD_SILENCE_MS = { interstitial: 30000, rewarded: 45000 };

function runAd(event, show, terminal, silenceMs) {
  return new Promise((resolve) => {
    const states = [];
    let done = false;
    let timer = null;
    const finish = (timedOut) => {
      if (done) return;
      done = true;
      clearTimeout(timer);
      try {
        bridge.advertisement.off(event, onState);
      } catch {
        /* 구독 전 */
      }
      adRunning = false;
      applyHost(); // 게임 재개가 결과 처리보다 먼저
      resolve({ states, timedOut });
    };
    const arm = () => {
      clearTimeout(timer);
      timer = setTimeout(() => finish(true), silenceMs);
    };
    const onState = (state) => {
      if (done) return;
      states.push(state);
      if (terminal.includes(state)) finish(false);
      else arm();
    };
    adRunning = true;
    applyHost();
    try {
      bridge.advertisement.on(event, onState);
      arm();
      show();
    } catch {
      finish(true);
    }
  });
}

// ─────────────────────────────────────────────────────── 입력 가드
// 방향키·스페이스가 포털 페이지를 스크롤하지 않게. End/G는 빌드된 C#의 디버그 단축키라
// Unity에 닿기 전에 막는다(캡처 단계 — Unity 리스너보다 먼저 등록된다).
const SCROLL_KEYS = new Set([' ', 'ArrowUp', 'ArrowDown', 'ArrowLeft', 'ArrowRight', 'PageUp', 'PageDown']);
function guardKeys(e) {
  if (e.key === 'End' || e.code === 'End' || e.code === 'KeyG') {
    e.stopImmediatePropagation();
    e.preventDefault();
    return;
  }
  if (e.type === 'keydown' && SCROLL_KEYS.has(e.key)) e.preventDefault();
}
window.addEventListener('keydown', guardKeys, true);
window.addEventListener('keyup', guardKeys, true);

// ───────────────────────────────────────────────────────── 표면
let gameReadySent = false;

function message(name) {
  if (!hostUp) return;
  try {
    void bridge.platform.sendMessage(name).catch(() => {});
  } catch {
    /* 메시지 없는 호스트 */
  }
}

window.PlaygamaHost = {
  /// 브리지 초기화 + 세이브 선로드가 끝나면(또는 타임아웃) resolve. 템플릿은 이 뒤에 랜딩을 연다.
  ready,

  platformId() {
    try {
      return hostUp ? bridge.platform.id : null;
    } catch {
      return null;
    }
  },

  /// 포털 언어(ISO 639-1). 템플릿은 지원 언어일 때만 채택한다 — 국가 코드를 주는 호스트도 있다.
  language() {
    if (!hostUp) return null;
    try {
      const l = bridge.platform.language;
      return typeof l === 'string' && l ? l.toLowerCase() : null;
    } catch {
      return null;
    }
  },

  /// 최고 점수 = 넘겨받은 값(Unity PlayerPrefs)·localStorage·브리지 중 최댓값.
  best(n) {
    return Math.max(num(n), num(localGet('animalpop_best')), num(sent.get('animalpop_best')));
  },

  save,

  /// fn(paused) — 포털이 게임을 멈출 때(탭 전환·포털 UI)와 우리 광고가 떠 있는 동안 true.
  /// 소리는 여기서 마스터 게인으로 끈다 — 템플릿은 Unity 메인 루프만 멈추면 된다.
  onPause(fn) {
    pauseListener = fn;
    if (pausedNotified) fn(true);
  },

  /// 전면 광고. 끝나면(닫힘·실패·침묵·미지원) resolve — 게임은 어느 경우든 이어간다.
  showInterstitial() {
    if (adRunning || !adSupported('interstitial') || adBusy()) return Promise.resolve();
    return runAd(
      EVENT_NAME.INTERSTITIAL_STATE_CHANGED,
      () => bridge.advertisement.showInterstitial(),
      [INTERSTITIAL_STATE.CLOSED, INTERSTITIAL_STATE.FAILED],
      AD_SILENCE_MS.interstitial,
    ).then(() => undefined);
  },

  /// 보상형 광고 → 'rewarded' | 'closed'(중간에 닫음) | 'failed'(미지원·실패·침묵) | 'busy'(이미 광고 중 — 중복 탭).
  /// 보상은 호스트가 rewarded 상태를 보냈을 때만.
  showRewarded() {
    if (adRunning) return Promise.resolve('busy');
    if (!adSupported('rewarded') || adBusy()) return Promise.resolve('failed');
    return runAd(
      EVENT_NAME.REWARDED_STATE_CHANGED,
      () => bridge.advertisement.showRewarded(),
      [REWARDED_STATE.CLOSED, REWARDED_STATE.FAILED],
      AD_SILENCE_MS.rewarded,
    ).then(({ states, timedOut }) => {
      if (timedOut) return 'failed';
      if (states.includes(REWARDED_STATE.REWARDED)) return 'rewarded';
      return states[states.length - 1] === REWARDED_STATE.CLOSED ? 'closed' : 'failed';
    });
  },

  /// 랜딩이 처음 열릴 때 1회 — 포털은 이 뒤부터 로딩 완료로 보고 광고 타이머를 건다.
  gameReady() {
    if (gameReadySent) return;
    gameReadySent = true;
    message(PLATFORM_MESSAGE.GAME_READY);
  },
  levelStarted: () => message(PLATFORM_MESSAGE.LEVEL_STARTED),
  levelFailed: () => message(PLATFORM_MESSAGE.LEVEL_FAILED),
  gameplayResumed: () => message(PLATFORM_MESSAGE.GAMEPLAY_STARTED),
};
