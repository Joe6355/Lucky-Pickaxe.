(function (root) {
  "use strict";
  let sdk, unity, receiver, options = {}, initialization;
  let gameReady = false, ready = false, local = false, failed = false;
  let sdkPaused = false, hidden = !!root.document.hidden, blurred = false, sentPause;
  let warnedStorage = false, rewardRequest, sentRewardedAvailable;
  const removers = [];
  const loopback = /^(localhost|127\.0\.0\.1|\[::1\])$/.test(root.location.hostname);
  const journalKey = key => "LuckyPickaxe." + key + ".journal";

  function send(method, value) {
    if (unity && receiver) unity.SendMessage(receiver, method, value);
  }
  function notify(force) {
    const paused = isPaused();
    if (unity && receiver && (force || paused !== sentPause)) {
      sentPause = paused;
      send("OnBrowserPause", paused ? "1" : "0");
    }
    const available = !!(ready && !local && sdk && sdk.adv && typeof sdk.adv.showRewardedVideo === "function");
    if (unity && receiver && (force || available !== sentRewardedAvailable)) {
      sentRewardedAvailable = available;
      send("OnRewardedAvailable", available ? "1" : "0");
    }
    if (options.onPause) options.onPause(ready && paused);
  }
  function isPaused() { return sdkPaused || hidden || blurred || !!rewardRequest || !ready; }
  function showRewarded(id) {
    if (isPaused() || local || !unity || !receiver || !sdk || !sdk.adv || typeof sdk.adv.showRewardedVideo !== "function" || typeof id !== "string" || !/^[a-zA-Z0-9-]{1,64}$/.test(id)) return false;
    const request = { id, paid: false };
    rewardRequest = request; notify();
    const finish = result => {
      if (rewardRequest !== request) return;
      // Deliver completion while the ad pause still holds Unity input and audio.
      // The controller saves the result before this pause reason is released.
      send("OnRewardedVideo", id + ":" + result);
      rewardRequest = null; notify();
    };
    try {
      sdk.adv.showRewardedVideo({ callbacks: {
        onOpen() { if (rewardRequest === request) notify(); },
        onRewarded() {
          if (rewardRequest !== request || request.paid) return;
          request.paid = true;
          send("OnRewardedVideo", id + ":rewarded");
        },
        onClose() { finish("closed"); },
        onError() { finish("error"); }
      } });
    } catch (error) { finish("error"); }
    return true;
  }
  function fail(error) {
    failed = true; ready = false; notify();
    root.console.error("Lucky Pickaxe: browser initialization failed", error);
    if (options.onError) options.onError(error);
  }
  function finishReady() {
    if (ready || failed || !gameReady || !unity || (!sdk && !local)) return;
    ready = true;
    // Remove our loading cover before reporting that the Unity interface is usable.
    if (options.onReady) options.onReady(local);
    try { if (sdk) sdk.features.LoadingAPI.ready(); }
    catch (error) { fail(error); return; }
    notify(true);
  }
  function listen(target, event, callback) {
    target.addEventListener(event, callback);
    removers.push(() => target.removeEventListener(event, callback));
  }
  function init(settings) {
    if (initialization) return initialization;
    options = settings || {};
    local = loopback && new URLSearchParams(root.location.search).get("local") === "1";
    listen(root.document, "visibilitychange", () => { hidden = !!root.document.hidden; notify(); });
    listen(root, "blur", () => { blurred = true; notify(); });
    listen(root, "focus", () => { blurred = false; hidden = !!root.document.hidden; notify(); });
    listen(root, "pagehide", () => { send("OnBrowserSave", ""); hidden = true; notify(); });
    listen(root, "pageshow", () => { hidden = !!root.document.hidden; blurred = false; notify(); });
    if (local) { initialization = Promise.resolve(); finishReady(); return initialization; }
    initialization = new Promise((resolve, reject) => {
      if (root.YaGames) { resolve(); return; }
      const script = root.document.createElement("script");
      script.src = "/sdk.js"; script.async = true;
      script.onload = resolve; script.onerror = () => reject(new Error("Yandex SDK unavailable"));
      root.document.head.appendChild(script);
    }).then(() => root.YaGames.init()).then(value => {
      if (!value || !value.features || !value.features.LoadingAPI || typeof value.features.LoadingAPI.ready !== "function" || typeof value.on !== "function")
        throw new Error("Incomplete Yandex SDK");
      sdk = value;
      const pause = () => { sdkPaused = true; notify(); };
      const resume = () => { sdkPaused = false; notify(); };
      sdk.on("game_api_pause", pause); sdk.on("game_api_resume", resume);
      removers.push(() => { sdk.off("game_api_pause", pause); sdk.off("game_api_resume", resume); });
      finishReady();
    }).catch(error => { fail(error); throw error; });
    return initialization;
  }
  function storageWarning(error) {
    if (warnedStorage) return;
    warnedStorage = true;
    root.console.warn("Lucky Pickaxe: synchronous journal unavailable; PlayerPrefs remains active", error);
  }
  root.LuckyPickaxe = {
    init,
    attachReceiver(name) { receiver = name; notify(true); },
    attachUnity(instance) { unity = instance; notify(true); finishReady(); },
    markReady() { gameReady = true; finishReady(); },
    showRewarded,
    readProgress(key) {
      try { return root.localStorage.getItem(journalKey(key)); }
      catch (error) { storageWarning(error); return null; }
    },
    writeProgress(key, json) {
      // setItem replaces one complete JSON atomically; no asynchronous write window.
      try { root.localStorage.setItem(journalKey(key), json); return true; }
      catch (error) { storageWarning(error); return false; }
    },
    dispose() { rewardRequest = null; while (removers.length) removers.pop()(); },
    status() { return { ready, local, sdk: !!sdk, paused: isPaused(), failed, rewardedPending: !!rewardRequest }; }
  };
})(window);
