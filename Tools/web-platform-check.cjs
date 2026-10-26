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
  let initCalls = 0, readyCalls = 0, writes = 0;
  const sdkEvents = new Map();
  const sdk = {
    features: { LoadingAPI: { ready() { readyCalls++; calls.push("sdk-ready"); } } },
    on(event, fn) { sdkEvents.set(event, fn); }, off(event) { sdkEvents.delete(event); }
  };
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
  const unity = { SendMessage(receiver, method, value) { calls.push({ receiver, method, value }); if (settings.onMessage) settings.onMessage(method, value, api); } };
  return { root, doc, api, sdk, calls, data, unity, emitSDK(event) { sdkEvents.get(event)?.(); }, get initCalls() { return initCalls; }, get readyCalls() { return readyCalls; }, get writes() { return writes; } };
}
function lastPause(f) { return f.calls.filter(value => value.method === "OnBrowserPause").at(-1)?.value; }
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

  fs.mkdirSync(path.join(__dirname, "../Logs"), { recursive: true });
  fs.writeFileSync(path.join(__dirname, "../Logs/web-platform-check.json"), JSON.stringify({ result: "PASS", checks }));
  console.log(`LUCKY_PICKAXE_WEB_PLATFORM: PASS (${checks} checks)`);
}
run().catch(error => { console.error(error); process.exitCode = 1; });
