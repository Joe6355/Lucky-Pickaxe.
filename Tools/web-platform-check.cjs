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
  target.emit = (event, detail) => { for (const fn of handlers.get(event) || []) fn(detail); };
  return target;
}
function fixture(settings = {}) {
  const calls = [], data = new Map(), root = emitter();
  let clock=Date.now();const FixtureDate=class extends Date {static now(){return clock;}};
  const doc = emitter({ hidden: false, createElement: () => ({}) });
  let initCalls = 0, readyCalls = 0, writes = 0, languageReads = 0;
  const ads = [], gameplayCalls = [];
  const sdkEvents = new Map();
  const sdk = {
    environment: { i18n: { get lang() { languageReads++; calls.push("sdk-language"); return settings.language === undefined ? "ru" : settings.language; } } },
    features: { LoadingAPI: { ready() { readyCalls++; calls.push("sdk-ready"); } } },
    on(event, fn) { sdkEvents.set(event, fn); }, off(event) { sdkEvents.delete(event); }
  };
  if (!settings.noGameplay) sdk.features.GameplayAPI = {
    start() { gameplayCalls.push("start"); calls.push("gameplay-start"); if (settings.gameplayThrows) throw new Error("Gameplay unavailable"); if (settings.onGameplay) settings.onGameplay("start"); },
    stop() { gameplayCalls.push("stop"); calls.push("gameplay-stop"); if (settings.gameplayThrows) throw new Error("Gameplay unavailable"); if (settings.onGameplay) settings.onGameplay("stop"); }
  };
  if (!settings.noRewarded) sdk.adv = { showRewardedVideo({ callbacks }) {
    calls.push("sdk-video");
    ads.push(callbacks);
    if (settings.adThrows) throw new Error("Video unavailable");
    if (settings.adSynchronous) { callbacks.onRewarded(); callbacks.onClose(); }
  } };
  const authRequests=[];let playerAuthorized=!!settings.authorized,playerCalls=0;
  if(settings.auth) {
    sdk.getPlayer=()=>{playerCalls++;if(settings.playerFails) return Promise.reject(new Error("Player unavailable"));return Promise.resolve({isAuthorized:()=>playerAuthorized,getName:()=>settings.playerName||"Тестовый шахтёр"});};
    sdk.auth={openAuthDialog(){if(settings.authThrows) throw new Error("Dialog unavailable");return new Promise((resolve,reject)=>authRequests.push({resolve(){playerAuthorized=true;resolve();},reject}));}};
  }
  const ranking={writes:[],reads:[],playerReads:0,availability:[]};let remoteScore=settings.remoteScore;
  if(settings.leaderboard) {
    sdk.isAvailableMethod=async name=>{ranking.availability.push(name);return !settings.ratingUnavailable;};
    sdk.leaderboards={
      getEntries(name,options){ranking.reads.push({name,options});if(settings.ratingReadFails) return Promise.reject(new Error("404"));return settings.entriesPromise||Promise.resolve(settings.entries||{entries:[],userRank:0});},
      async getPlayerEntry(name){ranking.playerReads++;if(settings.ratingPlayerFails) throw new Error("Network unavailable");if(remoteScore===undefined) throw {code:"LEADERBOARD_PLAYER_NOT_PRESENT"};return {score:remoteScore};},
      async setScore(name,score,extra){if(settings.ratingWriteFails) throw new Error("Write unavailable");ranking.writes.push({name,score,extra});remoteScore=score;}
    };
  }
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
  const context = vm.createContext({ window: root, URLSearchParams, Promise, Date:FixtureDate }); vm.runInContext(source, context);
  const api = root.LuckyPickaxe;
  const unity = { SendMessage(receiver, method, value) {
    calls.push({ receiver, method, value });
    if (method === "OnBrowserLanguage" && !settings.manualLanguage) api.confirmLanguage(value);
    if (method === "OnBrowserPause" && !settings.manualGameplay) api.setGameplay(value === "0");
    if (settings.onMessage) settings.onMessage(method, value, api);
  } };
  return { root, doc, api, sdk, calls, data, unity, ads, gameplayCalls, ranking, advanceClock(ms){clock+=ms;}, emitSDK(event) { sdkEvents.get(event)?.(); }, authRequests, get playerCalls(){return playerCalls;}, get initCalls() { return initCalls; }, get readyCalls() { return readyCalls; }, get writes() { return writes; }, get languageReads() { return languageReads; } };
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
  f.api.dispose(); const beforeDispose = f.calls.length; f.root.emit("blur"); f.emitSDK("game_api_pause");
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

  let resolveDisposedSDK;
  f = fixture({ sdkPromise: new Promise(resolve => { resolveDisposedSDK = resolve; }) });
  const abandonedLoad = f.api.init();
  f.api.attachReceiver("Mine Loot Demo"); f.api.attachUnity(f.unity); f.api.markReady();
  f.api.dispose(); const beforeAbandonedLoad = f.calls.length;
  resolveDisposedSDK(f.sdk); await abandonedLoad;
  check(f.readyCalls === 0 && f.calls.length === beforeAbandonedLoad && !f.api.status().ready, "An SDK resolving after page disposal cannot restart the abandoned game");

  f = fixture({ manualGameplay: true }); await start(f);
  check(f.gameplayCalls.length === 0 && !f.api.status().engineActive, "Ready alone cannot report active gameplay before Unity acknowledges its actual pause state");
  f.api.setGameplay(true);
  check(JSON.stringify(f.gameplayCalls) === '["start"]' && f.calls.indexOf("sdk-ready") < f.calls.indexOf("gameplay-start"), "Gameplay starts only after Game Ready and the Unity activity acknowledgement");
  f.api.setGameplay(true); f.root.emit("focus");
  check(f.gameplayCalls.length === 1, "Repeated focus and Unity activity notifications never duplicate the same gameplay start");
  f.root.emit("blur"); f.doc.hidden = true; f.doc.emit("visibilitychange"); f.root.emit("focus");
  check(JSON.stringify(f.gameplayCalls) === '["start","stop"]', "Overlapping browser pause reasons stop gameplay once and focus cannot restart a hidden page");
  f.doc.hidden = false; f.doc.emit("visibilitychange");
  check(JSON.stringify(f.gameplayCalls) === '["start","stop","start"]', "Clearing the final host pause starts the already active Unity session once");
  f.api.setGameplay(false); f.emitSDK("game_api_pause"); f.emitSDK("game_api_resume"); f.root.emit("focus");
  check(f.gameplayCalls.at(-1) === "stop" && f.gameplayCalls.length === 4, "SDK and browser resume cannot report gameplay while Unity remains independently paused");
  f.api.setGameplay(true);
  check(f.gameplayCalls.at(-1) === "start" && f.gameplayCalls.length === 5, "Unity's actual resume releases the remaining activity condition");
  f.api.showRewarded("activity-video");
  check(f.gameplayCalls.at(-1) === "stop" && f.calls.lastIndexOf("gameplay-stop") < f.calls.lastIndexOf("sdk-video"), "Gameplay is stopped before asking the SDK to open a rewarded video");
  f.ads[0].onRewarded(); f.emitSDK("game_api_pause"); f.ads[0].onClose();
  check(f.gameplayCalls.at(-1) === "stop" && f.gameplayCalls.length === 6, "A reward and video close cannot resume gameplay through an outstanding SDK pause");
  f.emitSDK("game_api_resume");
  check(f.gameplayCalls.at(-1) === "start" && f.gameplayCalls.length === 7, "The real final resume reports one new gameplay start after video");
  f.api.dispose(); f.api.dispose(); const callsAfterDisposal = f.calls.length;
  f.api.setGameplay(true); f.api.attachUnity(f.unity); f.api.attachReceiver("new"); f.api.markReady(); f.api.confirmLanguage("ru");
  check(f.gameplayCalls.at(-1) === "stop" && f.gameplayCalls.length === 8 && f.calls.length === callsAfterDisposal && !f.api.showRewarded("after-disposal") && f.api.status().disposed, "Disposal stops once and no late Unity or video request can reactivate the completed session");

  for (const settings of [{ noGameplay: true }, { gameplayThrows: true }]) {
    f = fixture(settings); await start(f); f.root.emit("blur"); f.root.emit("focus");
    check(f.api.status().ready && !f.api.status().paused && f.readyCalls === 1, "Optional gameplay API absence or failure never disables the existing game and pause controls");
  }
  f = fixture(); await start(f);
  f.root.emit("pagehide", { persisted: true });
  check(!f.api.status().disposed && f.api.status().paused && f.gameplayCalls.at(-1) === "stop", "Back-forward cache entry saves and stops the session without disposing it");
  f.root.emit("pageshow");
  check(f.api.status().ready && !f.api.status().paused && f.gameplayCalls.at(-1) === "start" && f.readyCalls === 1, "Back-forward cache restoration resumes the same session without repeating Game Ready");
  f.root.emit("pagehide", { persisted: false }); const beforePermanentExit = f.calls.length;
  f.root.emit("pageshow"); f.emitSDK("game_api_resume"); await f.api.init();
  check(f.api.status().disposed && !f.api.status().ready && f.calls.length === beforePermanentExit && f.initCalls === 1, "Permanent page departure releases listeners and cannot resurrect the finished session");

  let rejectDisposedSDK;
  f = fixture({ sdkPromise: new Promise((resolve, reject) => { rejectDisposedSDK = reject; }) });
  const abandonedFailure = f.api.init(); await Promise.resolve(); await Promise.resolve();
  f.api.dispose(); rejectDisposedSDK(new Error("Late network failure")); await abandonedFailure;
  check(!f.api.status().failed && f.api.status().disposed && f.readyCalls === 0, "A rejected SDK request after disposal cannot reopen loading or show a stale error");

  let pauseInsideGameplayStart = true;
  f = fixture({ onGameplay(phase) {
    if (phase === "start" && pauseInsideGameplayStart) { pauseInsideGameplayStart = false; f.emitSDK("game_api_pause"); }
  } });
  await start(f);
  check(JSON.stringify(f.gameplayCalls) === '["start","stop"]' && f.api.status().paused && !f.api.status().gameplayReported, "A synchronous SDK pause during GameplayAPI.start produces one stop without recursive start signals");
  f.emitSDK("game_api_resume");
  check(JSON.stringify(f.gameplayCalls) === '["start","stop","start"]' && !f.api.status().paused, "Resume after a synchronous SDK event restores one active gameplay phase");

  async function settleAccount(){for(let i=0;i<12;i++) await Promise.resolve();}
  f=fixture({auth:true});await start(f);await settleAccount();
  check(f.api.status().accountStatus==="guest" && f.authRequests.length===0,"Reading a guest profile never opens authentication automatically");
  check(f.api.authorize() && f.api.status().authorizationPending && f.api.status().paused,"Explicit login opens the SDK dialog and pauses gameplay");
  check(!f.api.authorize() && !f.api.showRewarded("during-auth") && f.authRequests.length===1,"Authentication cannot overlap another dialog or video");
  f.root.emit("blur");f.authRequests[0].resolve();await settleAccount();
  check(f.api.status().accountStatus==="authorized" && !f.api.status().authorizationPending && f.api.status().paused,"SDK authorization keeps an independent browser pause intact");
  f.root.emit("focus");check(!f.api.status().paused && !f.api.authorize() && f.playerCalls===2,"Successful login refreshes the SDK player once and removes the authorization offer");
  f=fixture({auth:true});await start(f);await settleAccount();f.api.authorize();f.authRequests[0].reject(new Error("Cancelled"));await settleAccount();
  check(f.api.status().accountStatus==="guest" && !f.api.status().paused && !f.api.status().failed,"Cancelling login resumes the existing guest game without an SDK failure");
  f=fixture({auth:true,authThrows:true});await start(f);await settleAccount();
  check(!f.api.authorize() && !f.api.status().paused && !f.api.status().authorizationPending,"A synchronous dialog error cannot leave gameplay paused");
  f=fixture({auth:true,playerFails:true});await start(f);await settleAccount();
  check(f.api.status().accountStatus==="guest" && !f.api.status().failed,"Unavailable profile data does not block game readiness");
  f=fixture({auth:true});await start(f);await settleAccount();f.api.authorize();f.api.dispose();const callsAfterAuthDispose=f.calls.length;
  f.authRequests[0].resolve();await settleAccount();
  check(f.calls.length===callsAfterAuthDispose && f.playerCalls===1 && f.api.status().disposed,"A late login result cannot restart a disposed game or fetch another player");
  f=fixture();await start(f);check(!f.api.authorize() && f.api.status().accountStatus==="unavailable","A missing SDK auth provider is reported without a fake login");

  const submission=JSON.stringify({version:1,score:12,blocks:100,coins:100000,tickets:20,xp:1000,resets:2,test:false});
  const rankingEvent=(f,method="OnBrowserRating")=>JSON.parse(f.calls.filter(e=>e.method===method).at(-1)?.value||"{}");
  async function settleRating(){for(let i=0;i<30;i++) await Promise.resolve();}
  f=fixture({leaderboard:true});await start(f);
  check(f.api.requestRating(),"A guest can explicitly load the public leaderboard");await settleRating();
  check(f.ranking.reads.length===1 && f.ranking.reads[0].name==="mmr" && !f.ranking.reads[0].options.includeUser && rankingEvent(f).status==="ready","An empty real leaderboard remains empty and never invents players");
  check(!f.api.submitRating(submission) && f.ranking.writes.length===0,"A guest cannot submit an online rating");
  f.api.requestRating();await settleRating();check(f.ranking.reads.length===1,"Repeated leaderboard clicks reuse the thirty-second cache");
  f.advanceClock(30001);f.api.requestRating();await settleRating();check(f.ranking.reads.length===2,"The table may reload after its SDK request cooldown");
  f=fixture({auth:true,authorized:true,leaderboard:true,entries:{userRank:7,entries:[{rank:7,score:12,player:{publicName:"Player"},extraData:submission},{rank:1,score:20000,player:{publicName:"Leader"},extraData:"bad JSON"},{rank:1,score:20000,player:{publicName:"Duplicate"}},{rank:0,score:-1}]}});await start(f);await settleRating();
  f.api.requestRating();await settleRating();const table=rankingEvent(f);
  check(table.entries.length===2 && table.entries[0].rank===1 && table.entries[1].me && table.entries[1].hasStats && table.entries[1].blocks===100,"Entries are sorted, deduplicated, validated and mark the current player with real summary data");
  check(!table.entries[0].hasStats && f.ranking.reads[0].options.includeUser,"Missing summary JSON does not hide a valid score or invent statistics");
  check(f.api.submitRating(submission) && !f.api.submitRating(submission),"A paid progress snapshot starts only one in-flight ranking submission");await settleRating();
  check(f.ranking.writes.length===1 && f.ranking.playerReads===1 && f.ranking.writes[0].score===12 && rankingEvent(f,"OnBrowserRatingSaved").status==="saved","A missing player record writes the real score and summary once");
  check(f.ranking.availability.includes("leaderboards.setScore") && f.ranking.availability.includes("leaderboards.getPlayerEntry"),"Ranking checks both SDK methods before submitting");
  check(!f.api.submitRating(submission),"Repeated score posts obey the thirty-second submission cooldown");
  f.advanceClock(30001);f.api.submitRating(submission);await settleRating();check(f.ranking.writes.length===1,"An equal saved score is acknowledged without rewriting the leaderboard");
  f=fixture({auth:true,authorized:true,leaderboard:true,remoteScore:100});await start(f);await settleRating();f.api.submitRating(submission);await settleRating();
  check(f.ranking.writes.length===0 && rankingEvent(f,"OnBrowserRatingSaved").score===100,"A lower local save cannot overwrite a higher Yandex record");
  f=fixture({auth:true,authorized:true,leaderboard:true,ratingPlayerFails:true});await start(f);await settleRating();f.api.submitRating(submission);await settleRating();
  check(f.ranking.writes.length===0 && rankingEvent(f,"OnBrowserRatingSaved").status==="error" && !f.api.status().ratingSendPending,"A network failure reading the previous record never writes a potentially lower score");
  f=fixture({auth:true,authorized:true,leaderboard:true,ratingWriteFails:true});await start(f);await settleRating();f.api.submitRating(submission);await settleRating();
  check(rankingEvent(f,"OnBrowserRatingSaved").status==="error" && !f.api.status().paused,"A failed ranking write cannot pause gameplay or report success");
  f=fixture({auth:true,authorized:true,leaderboard:true,ratingUnavailable:true});await start(f);await settleRating();f.api.submitRating(submission);await settleRating();
  check(f.ranking.playerReads===0 && f.ranking.writes.length===0 && rankingEvent(f,"OnBrowserRatingSaved").status==="error","Unavailable leaderboard methods are a recoverable error");
  f=fixture({auth:true,authorized:true,leaderboard:true});await start(f);await settleRating();
  for(const data of [{test:true},{score:-1},{score:2.5},{score:2147483648},{xp:-1},{version:2}]) check(!f.api.submitRating(JSON.stringify({...JSON.parse(submission),...data})),"Invalid or test-only ranking snapshots never reach the platform: "+JSON.stringify(data));
  check(f.ranking.writes.length===0,"Temporary resource tests cannot create an online record");
  f=fixture({leaderboard:true,ratingReadFails:true});await start(f);f.api.requestRating();await settleRating();check(rankingEvent(f).status==="error" && !f.api.status().failed && !f.api.status().paused,"An unavailable table preserves the active game and exposes a retry state");
  let finishEntries;
  f=fixture({leaderboard:true,entriesPromise:new Promise(resolve=>finishEntries=resolve)});await start(f);f.api.requestRating();await settleRating();f.api.dispose();const disposedCalls=f.calls.length;finishEntries({entries:[]});await settleRating();
  check(f.calls.length===disposedCalls,"A late leaderboard response cannot update a disposed Unity instance");
  f=fixture({auth:true,leaderboard:true});await start(f);await settleRating();f.api.requestRating();await settleRating();f.api.authorize();f.authRequests[0].resolve();await settleRating();f.api.requestRating();await settleRating();
  check(f.ranking.reads.length===2 && f.ranking.reads[1].options.includeUser,"Signing in invalidates the guest cache and includes the real current-player row");
  f=fixture();await start(f);check(!f.api.requestRating() && !f.api.submitRating(submission),"A missing leaderboard provider is not replaced by a fake online table");

  fs.mkdirSync(path.join(__dirname, "../Logs"), { recursive: true });
  fs.writeFileSync(path.join(__dirname, "../Logs/web-platform-check.json"), JSON.stringify({ result: "PASS", checks }));
  console.log(`LUCKY_PICKAXE_WEB_PLATFORM: PASS (${checks} checks)`);
}
run().catch(error => { console.error(error); process.exitCode = 1; });
