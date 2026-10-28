"use strict";
// Run with Node.js; no packages or live platform account are needed for these bridge checks.
const fs = require("node:fs"), path = require("node:path"), vm = require("node:vm"), assert = require("node:assert/strict");
const source = fs.readFileSync(path.join(__dirname, "../Assets/WebGLTemplates/LuckyPickaxe/platform.js"), "utf8");
let checks = 0;
function check(condition, label) { checks++; assert.ok(condition, label); }
function emitter(target = {}) {
  const handlers = new Map();
  target.addEventListener = (event, fn) => { if (!handlers.has(event)) handlers.set(event, new Set()); handlers.get(event).add(fn); };
  target.removeEventListener = (event, fn) => handlers.get(event)?.delete(fn);
  target.emit = event => { for (const fn of handlers.get(event) || []) fn(); };
  return target;
}
function fixture(settings = {}) {
  const calls = [], data = new Map(), root = emitter();
  const doc = emitter({ hidden: false, createElement: () => ({}) });
  let initCalls = 0, readyCalls = 0, writes = 0, languageReads = 0;
  const ads = [];
  const sdkEvents = new Map();
  const sdk = {
    environment: { i18n: { get lang() { languageReads++; calls.push("sdk-language"); return settings.language === undefined ? "ru" : settings.language; } } },
    features: { LoadingAPI: { ready() { readyCalls++; calls.push("sdk-ready"); } } },
    on(event, fn) { sdkEvents.set(event, fn); }, off(event) { sdkEvents.delete(event); }
  };
  if (!settings.noRewarded) sdk.adv = { showRewardedVideo({ callbacks }) {
    ads.push(callbacks);
    if (settings.adThrows) throw new Error("Video unavailable");
    if (settings.adSynchronous) { callbacks.onRewarded(); callbacks.onClose(); }
  } };
  Object.assign(root, {
    document: doc, location: { hostname: settings.host || "games.example", search: settings.search || "" },
    console: { warn() {}, error() {} },
    localStorage: {
      getItem(key) { if (settings.storageBlocked) throw new Error("Storage denied"); return data.get(key) || null; },
      setItem(key, value) { if (settings.storageBlocked) throw new Error("Storage denied"); writes++; data.set(key, value); }
    },
    YaGames: { init() { initCalls++; return settings.sdkPromise || (settings.sdkFailure ? Promise.reject(new Error("SDK failed")) : Promise.resolve(sdk)); } }
  });
  doc.head = { appendChild(script) { calls.push(script.src); queueMicrotask(() => script.onerror()); } };
  if (settings.missingScript) delete root.YaGames;
  const context = vm.createContext({ window: root, URLSearchParams, Promise }); vm.runInContext(source, context);
  const api = root.LuckyPickaxe;
  const unity = { SendMessage(receiver, method, value) {
    calls.push({ receiver, method, value });
    if (method === "OnBrowserLanguage" && !settings.manualLanguage) api.confirmLanguage(value);
    if (settings.onMessage) settings.onMessage(method, value, api);
  } };
  return { root, doc, api, sdk, calls, data, unity, ads, emitSDK(event) { sdkEvents.get(event)?.(); }, get initCalls() { return initCalls; }, get readyCalls() { return readyCalls; }, get writes() { return writes; }, get languageReads() { return languageReads; } };
}
function lastPause(f) { return f.calls.filter(value => value.method === "OnBrowserPause").at(-1)?.value; }
function videoEvents(f) { return f.calls.filter(value => value.method === "OnRewardedVideo").map(value => value.value); }
async function start(f) {
  await f.api.init({ onReady() { f.calls.push("ui-ready"); } });
  f.api.attachReceiver("Mine Loot Demo"); f.api.attachUnity(f.unity); f.api.markReady();
}
async function run() {
  let f = fixture();
  await f.api.init({ onReady() { f.calls.push("ui-ready"); } });
  check(f.readyCalls === 0, "SDK initialization alone cannot report Game Ready");
  f.api.markReady(); check(f.readyCalls === 0, "Unity's first frame also waits for an attached instance");
  f.api.attachReceiver("Mine Loot Demo"); f.api.attachUnity(f.unity);
  check(f.readyCalls === 1 && f.api.status().ready && lastPause(f) === "0", "Ready is reported only when SDK, Unity and UI are all ready");
  check(f.calls.indexOf("ui-ready") < f.calls.indexOf("sdk-ready"), "Our loading cover disappears before LoadingAPI.ready");
  f.api.markReady(); f.api.attachUnity(f.unity); await f.api.init();
  check(f.readyCalls === 1 && f.initCalls === 1, "Repeated callbacks never initialize or report ready twice");
  f.emitSDK("game_api_pause"); check(lastPause(f) === "1" && f.api.status().paused, "SDK pause reaches Unity");
  f.root.emit("blur"); f.emitSDK("game_api_resume");
  check(lastPause(f) === "1", "SDK resume cannot clear a browser focus pause");
  f.doc.hidden = true; f.doc.emit("visibilitychange"); f.root.emit("focus");
  check(lastPause(f) === "1", "Focus cannot clear hidden document state");
  f.doc.hidden = false; f.doc.emit("visibilitychange");
  check(lastPause(f) === "0", "Game resumes after the last pause reason clears");
  f.emitSDK("game_api_pause"); f.root.emit("blur"); f.root.emit("focus");
  check(lastPause(f) === "1", "Returning focus cannot clear an SDK pause");
  f.emitSDK("game_api_resume"); check(lastPause(f) === "0", "Final SDK resume restores gameplay");

  let resolveSDK;
  const lateSDK = new Promise(resolve => { resolveSDK = resolve; });
  f = fixture({ sdkPromise: lateSDK }); const loading = f.api.init();
  f.api.attachReceiver("Mine Loot Demo"); f.api.markReady(); f.api.attachUnity(f.unity);
  check(!f.api.status().ready && lastPause(f) === "1", "A slow SDK holds the loaded game paused");
  resolveSDK(f.sdk); await loading;
  check(f.readyCalls === 1 && lastPause(f) === "0", "Late SDK initialization completes the waiting launch once");

  f = fixture(); await f.api.init(); f.emitSDK("game_api_pause");
  f.api.attachReceiver("Mine Loot Demo"); f.api.markReady(); f.api.attachUnity(f.unity);
  check(f.readyCalls === 1 && lastPause(f) === "1", "Pause before Unity attaches is retained through Game Ready");
  f.emitSDK("game_api_resume"); check(lastPause(f) === "0", "Startup platform pause requires its actual resume");
  const beforeDispose = f.calls.length; f.api.dispose(); f.root.emit("blur"); f.emitSDK("game_api_pause");
  check(f.calls.length === beforeDispose, "Bridge disposal removes browser and SDK listeners");

  f = fixture(); await start(f);
  const save = JSON.stringify({ version: 11, tickets: 0, rollPending: true, pendingTypes: [1, 2, 4, 1, 2], note: "Кирка удачи" });
  check(f.api.writeProgress("MineLoot.Progress.v2", save) && f.writes === 1, "A paid draw journal is one synchronous storage transaction");
  check(f.api.readProgress("MineLoot.Progress.v2") === save, "Journal preserves the complete paid draw and Cyrillic JSON");
  const restored = fixture(); restored.root.localStorage.getItem = key => f.data.get(key) || null;
  check(restored.api.readProgress("MineLoot.Progress.v2") === save, "A new page reads the last journal before Unity loads");
  check(f.api.readProgress("Other.Game") === null, "Journal uses a separate stable game key");
  f = fixture({ onMessage(method, value, api) { if (method === "OnBrowserSave") api.writeProgress("MineLoot.Progress.v2", save); } });
  await start(f); f.root.emit("pagehide");
  check(f.api.readProgress("MineLoot.Progress.v2") === save && lastPause(f) === "1", "Page hide flushes the controller before freezing departure state");
  f.root.emit("pageshow"); check(lastPause(f) === "0", "Back-forward page restoration resumes the same instance");
  f = fixture({ storageBlocked: true }); await start(f);
  check(!f.api.writeProgress("MineLoot.Progress.v2", save) && f.api.readProgress("MineLoot.Progress.v2") === null && f.api.status().ready, "Blocked synchronous storage leaves the PlayerPrefs fallback and launch active");

  f = fixture({ host: "localhost", search: "?local=1" }); await start(f);
  check(f.api.status().local && f.initCalls === 0 && f.readyCalls === 0, "Explicit loopback demo runs without claiming an SDK connection");
  f = fixture({ host: "games.example", search: "?local=1" }); await start(f);
  check(!f.api.status().local && f.initCalls === 1, "Production hosts cannot bypass the SDK through a query parameter");
  for (const settings of [{ sdkFailure: true }, { missingScript: true }]) {
    f = fixture(settings); f.api.attachReceiver("Mine Loot Demo"); f.api.attachUnity(f.unity); f.api.markReady();
    let rejected = false; try { await f.api.init(); } catch { rejected = true; }
    check(rejected && f.api.status().failed && !f.api.status().ready && f.readyCalls === 0 && lastPause(f) === "1", "Missing or rejected SDK leaves a failed launch paused without a false ready signal");
  }

  f = fixture();
  check(!f.api.showRewarded("before-ready") && f.ads.length === 0, "Video cannot open before Game Ready");
  await start(f);
  check(f.calls.some(value => value.method === "OnRewardedAvailable" && value.value === "1"), "Available SDK video capability reaches Unity");
  check(!f.api.showRewarded("") && !f.api.showRewarded("invalid:id"), "Empty and malformed video request IDs never reach the SDK");
  check(f.api.showRewarded("first") && f.ads.length === 1 && f.api.status().rewardedPending && lastPause(f) === "1", "An explicit request opens one video and pauses before onOpen");
  check(!f.api.showRewarded("second") && f.ads.length === 1, "A concurrent request cannot open another video");
  f.ads[0].onOpen(); f.emitSDK("game_api_pause"); f.emitSDK("game_api_resume");
  check(lastPause(f) === "1", "SDK resume cannot release the active video request pause");
  f.ads[0].onRewarded(); f.ads[0].onRewarded();
  check(JSON.stringify(videoEvents(f)) === JSON.stringify(["first:rewarded"]) && lastPause(f) === "1", "Only onRewarded grants once and the video remains paused until close");
  f.ads[0].onClose(); f.ads[0].onError(); f.ads[0].onRewarded();
  check(JSON.stringify(videoEvents(f)) === JSON.stringify(["first:rewarded", "first:closed"]) && lastPause(f) === "0", "Close finishes once and all late callbacks are ignored");
  const firstAd = f.ads[0];
  check(f.api.showRewarded("cancelled"), "A completed request permits a fresh explicit request");
  firstAd.onRewarded(); firstAd.onClose();
  check(videoEvents(f).length === 2 && f.api.status().rewardedPending, "Callbacks from a previous request cannot affect the new video");
  f.ads[1].onClose();
  check(videoEvents(f).at(-1) === "cancelled:closed" && !videoEvents(f).includes("cancelled:rewarded") && !f.api.status().paused, "Closing an unfinished video sends no reward and releases its pause");
  f.api.showRewarded("failed"); f.ads[2].onError(); f.ads[2].onClose(); f.ads[2].onRewarded();
  check(videoEvents(f).at(-1) === "failed:error" && !videoEvents(f).includes("failed:rewarded") && !f.api.status().paused, "SDK failure preserves the unpaid result and late callbacks cannot pay it");
  f.api.showRewarded("hidden"); f.doc.hidden = true; f.doc.emit("visibilitychange"); f.ads[3].onClose();
  check(lastPause(f) === "1", "Video completion cannot resume a hidden page");
  f.doc.hidden = false; f.doc.emit("visibilitychange");
  check(lastPause(f) === "0", "Visibility return clears the remaining independent pause");
  f.api.showRewarded("sdk-paused"); f.emitSDK("game_api_pause"); f.ads[4].onClose();
  check(lastPause(f) === "1", "Closing a video cannot clear an outstanding SDK pause");
  f.emitSDK("game_api_resume");
  check(lastPause(f) === "0", "Only the actual SDK resume clears its pause after video close");
  f.root.emit("blur");
  check(!f.api.showRewarded("unfocused") && f.ads.length === 5, "A background page cannot start a video");
  f.root.emit("focus"); f.api.showRewarded("dispose"); const beforeAdDispose = f.calls.length; f.api.dispose();
  f.ads[5].onOpen(); f.ads[5].onRewarded(); f.ads[5].onClose();
  check(f.calls.length === beforeAdDispose, "Disposal invalidates active video callbacks");

  f = fixture({ noRewarded: true }); await start(f);
  check(!f.api.showRewarded("unsupported") && f.calls.some(value => value.method === "OnRewardedAvailable" && value.value === "0") && !f.api.status().paused, "Missing video support is reported without pausing or inventing a reward");
  f = fixture({ host: "localhost", search: "?local=1" }); await start(f);
  check(!f.api.showRewarded("local-demo") && !f.api.status().paused, "Local demo mode never simulates a paid platform reward");
  f = fixture({ adThrows: true }); await start(f);
  check(f.api.showRewarded("throws") && JSON.stringify(videoEvents(f)) === JSON.stringify(["throws:error"]) && !f.api.status().paused, "Synchronous SDK exceptions complete as an unpaid error without a stuck pause");
  const callbackStates = [];
  f = fixture({ adSynchronous: true, onMessage(method, value, api) {
    if (method !== "OnRewardedVideo") return;
    callbackStates.push(api.status().paused);
    if (value.endsWith(":rewarded")) api.writeProgress("MineLoot.Progress.v2", '{"coins":1280,"offlineCoins":0}');
  } }); await start(f); f.api.showRewarded("instant");
  check(JSON.stringify(videoEvents(f)) === JSON.stringify(["instant:rewarded", "instant:closed"]) && callbackStates.every(Boolean) && !f.api.status().paused, "Even synchronous SDK callbacks commit before gameplay resumes");
  check(f.api.readProgress("MineLoot.Progress.v2") === '{"coins":1280,"offlineCoins":0}', "The rewarded wallet and consumed crate can be journaled before close");

  f = fixture({ manualLanguage: true });
  await f.api.init({ onLanguage(value) { f.calls.push("language-applied:" + value); } });
  f.api.attachReceiver("Mine Loot Demo"); f.api.attachUnity(f.unity); f.api.markReady();
  check(f.languageReads === 1 && !f.api.status().ready && lastPause(f) === "1", "SDK language is read at startup and Game Ready waits for Unity to apply it");
  f.api.confirmLanguage("en");
  check(f.readyCalls === 0 && !f.api.status().languageApplied, "An unsupported or mismatched language acknowledgement cannot release loading");
  f.api.confirmLanguage("ru");
  check(f.readyCalls === 1 && f.api.status().languageApplied && lastPause(f) === "0" && f.calls.indexOf("sdk-language") < f.calls.indexOf("sdk-ready") && f.calls.indexOf("language-applied:ru") < f.calls.indexOf("sdk-ready"), "The selected translation is applied before the only Game Ready and unpause");
  f.api.confirmLanguage("ru"); f.api.markReady();
  check(f.readyCalls === 1 && f.calls.filter(value => value.method === "OnBrowserLanguage").length === 1, "Repeated language and readiness callbacks do not reload the UI or repeat Game Ready");
  for (const [requested, normalized] of [["ru-RU", "ru"], ["en", "en"], ["kk", "kk"], ["tr", "tr"], [" EN_us ", "en"], [null, "ru"], ["<script>", "ru"]]) {
    f = fixture({ language: requested }); await start(f);
    check(f.languageReads === 1 && f.api.status().requestedLanguage === normalized && f.api.status().language === "ru" && f.api.status().languageApplied && f.readyCalls === 1, "The single declared Russian translation is selected safely for SDK language " + requested);
  }
  f = fixture({ language: "en" }); delete f.sdk.environment; await start(f);
  check(f.api.status().language === "ru" && f.readyCalls === 1, "Missing optional SDK language data uses the declared Russian translation without blocking startup");
  f = fixture({ host: "localhost", search: "?local=1" }); await start(f);
  check(f.languageReads === 0 && f.api.status().language === "ru" && f.api.status().languageApplied, "Native-like local preview applies Russian without claiming to read the SDK");

  fs.mkdirSync(path.join(__dirname, "../Logs"), { recursive: true });
  fs.writeFileSync(path.join(__dirname, "../Logs/web-platform-check.json"), JSON.stringify({ result: "PASS", checks }));
  console.log(`LUCKY_PICKAXE_WEB_PLATFORM: PASS (${checks} checks)`);
}
run().catch(error => { console.error(error); process.exitCode = 1; });
