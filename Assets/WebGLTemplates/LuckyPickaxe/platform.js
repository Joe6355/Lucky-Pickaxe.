(function (root) {
  "use strict";
  let sdk, unity, receiver, options = {}, initialization;
  let gameReady = false, ready = false, local = false, failed = false;
  let sdkPaused = false, hidden = !!root.document.hidden, blurred = false, sentPause;
  let warnedStorage = false, rewardRequest, sentRewardedAvailable;
  let requestedLanguage, language, sentLanguage = false, languageApplied = false;
  let disposed = false, engineActive = false, gameplayReported = false, gameplayUnavailable = false;
  const removers = [];
  const loopback = /^(localhost|127\.0\.0\.1|\[::1\])$/.test(root.location.hostname);
  const journalKey = key => "LuckyPickaxe." + key + ".journal";

  function send(method, value) {
    if (unity && receiver) unity.SendMessage(receiver, method, value);
  }
  function notify(force) {
    if (disposed) return;
    if (unity && receiver && language && !sentLanguage) {
      sentLanguage = true;
      send("OnBrowserLanguage", language);
    }
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
    syncGameplay();
  }
  function isPaused() { return disposed || sdkPaused || hidden || blurred || !!rewardRequest || !ready; }
  function syncGameplay() {
    if (!sdk || local || gameplayUnavailable) return;
    const active = engineActive && !isPaused();
    if (active === gameplayReported) return;
    try {
      const gameplay = sdk.features?.GameplayAPI;
      if (typeof gameplay?.start !== "function" || typeof gameplay?.stop !== "function") return;
      // Set before calling the SDK so synchronous platform events cannot repeat the same signal.
      gameplayReported = active;
      if (active) gameplay.start(); else gameplay.stop();
    } catch (error) {
      gameplayReported = false; gameplayUnavailable = true;
      root.console.warn("Lucky Pickaxe: gameplay reporting unavailable; local pause remains active", error);
    }
  }
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
    if (disposed) return;
    failed = true; ready = false; notify();
    root.console.error("Lucky Pickaxe: browser initialization failed", error);
    if (options.onError) options.onError(error);
  }
  function finishReady() {
    if (disposed || ready || failed || !gameReady || !unity || !languageApplied || (!sdk && !local)) return;
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
  function selectLanguage(value) {
    requestedLanguage = typeof value === "string" && /^[a-z]{2}(?:[-_][a-z0-9]{2,8})*$/i.test(value.trim())
      ? value.trim().toLowerCase().split(/[-_]/)[0] : "ru";
    // The draft currently declares one complete translation: Russian.
    language = requestedLanguage === "ru" ? requestedLanguage : "ru";
    notify();
  }
  function init(settings) {
    if (disposed) return initialization || Promise.resolve();
    if (initialization) return initialization;
    options = settings || {};
    local = loopback && new URLSearchParams(root.location.search).get("local") === "1";
    listen(root.document, "visibilitychange", () => { hidden = !!root.document.hidden; notify(); });
    listen(root, "blur", () => { blurred = true; notify(); });
    listen(root, "focus", () => { blurred = false; hidden = !!root.document.hidden; notify(); });
    listen(root, "pagehide", event => {
      send("OnBrowserSave", ""); hidden = true; notify();
      if (event?.persisted === false) dispose();
    });
    listen(root, "pageshow", () => { hidden = !!root.document.hidden; blurred = false; notify(); });
    if (local) { initialization = Promise.resolve(); selectLanguage("ru"); finishReady(); return initialization; }
    initialization = new Promise((resolve, reject) => {
      if (root.YaGames) { resolve(); return; }
      const script = root.document.createElement("script");
      script.src = "/sdk.js"; script.async = true;
      script.onload = resolve; script.onerror = () => reject(new Error("Yandex SDK unavailable"));
      root.document.head.appendChild(script);
    }).then(() => disposed ? null : root.YaGames.init()).then(value => {
      if (disposed) return;
      if (!value || !value.features || !value.features.LoadingAPI || typeof value.features.LoadingAPI.ready !== "function" || typeof value.on !== "function")
        throw new Error("Incomplete Yandex SDK");
      sdk = value;
      const pause = () => { sdkPaused = true; notify(); };
      const resume = () => { sdkPaused = false; notify(); };
      sdk.on("game_api_pause", pause); sdk.on("game_api_resume", resume);
      removers.push(() => { sdk.off("game_api_pause", pause); sdk.off("game_api_resume", resume); });
      selectLanguage(sdk.environment?.i18n?.lang);
      finishReady();
    }).catch(error => { if (!disposed) { fail(error); throw error; } });
    return initialization;
  }
  function storageWarning(error) {
    if (warnedStorage) return;
    warnedStorage = true;
    root.console.warn("Lucky Pickaxe: synchronous journal unavailable; PlayerPrefs remains active", error);
  }
  function dispose() {
    if (disposed) return;
    disposed = true; ready = false; engineActive = false;
    if (sentPause !== true) { sentPause = true; send("OnBrowserPause", "1"); }
    syncGameplay(); rewardRequest = null;
    while (removers.length) {
      try { removers.pop()(); }
      catch (error) { root.console.warn("Lucky Pickaxe: platform listener cleanup failed", error); }
    }
    unity = receiver = null;
  }
  root.LuckyPickaxe = {
    init,
    attachReceiver(name) { if (!disposed) { receiver = name; notify(true); } },
    attachUnity(instance) { if (!disposed) { unity = instance; notify(true); finishReady(); } },
    markReady() { if (!disposed) { gameReady = true; finishReady(); } },
    confirmLanguage(value) {
      if (disposed || languageApplied || value !== language || !language) return;
      languageApplied = true;
      if (options.onLanguage) options.onLanguage(language);
      finishReady();
    },
    setGameplay(active) { if (!disposed) { engineActive = active === true; syncGameplay(); } },
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
    dispose,
    status() { return { ready, local, sdk: !!sdk, paused: isPaused(), failed, rewardedPending: !!rewardRequest, requestedLanguage, language, languageApplied, engineActive, gameplayReported, disposed }; }
  };
})(window);
