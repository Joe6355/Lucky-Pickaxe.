(function (root) {
  "use strict";
  let sdk, unity, receiver, options = {}, initialization;
  let gameReady = false, ready = false, local = false, failed = false;
  let sdkPaused = false, hidden = !!root.document.hidden, blurred = false, sentPause;
  let warnedStorage = false, rewardRequest, sentRewardedAvailable;
  let requestedLanguage, language, sentLanguage = false, languageApplied = false;
  let disposed = false, engineActive = false, gameplayReported = false, gameplayUnavailable = false;
  let account = { available: false, status: "unavailable", name: "" }, authPending = false, sentAccount, accountGeneration=0;
  const ratingName="mmr";
  let ratingCache, ratingLoadPending=false, ratingSendPending=false, ratingLoadAfter=0, ratingSendAfter=0, ratingLoadGeneration=-1;
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
    const accountJson=JSON.stringify(account);
    if(unity && receiver && (force || accountJson!==sentAccount)) { sentAccount=accountJson;send("OnBrowserAccount",accountJson); }
    syncGameplay();
  }
  function isPaused() { return disposed || sdkPaused || hidden || blurred || !!rewardRequest || authPending || !ready; }
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
  function accountFrom(player) {
    const authorized=!!(player && typeof player.isAuthorized==="function" && player.isAuthorized());
    return {available:true,status:authorized?"authorized":"guest",name:authorized&&typeof player.getName==="function"?String(player.getName()||"").slice(0,40):""};
  }
  function readAccount() {
    if(!sdk || typeof sdk.getPlayer!=="function" || typeof sdk.auth?.openAuthDialog!=="function") return;
    const generation=++accountGeneration;account={available:true,status:"checking",name:""};notify();
    Promise.resolve().then(()=>disposed?null:sdk.getPlayer()).then(player=>{if(!disposed && generation===accountGeneration){account=accountFrom(player);notify();}}).catch(()=>{if(!disposed && generation===accountGeneration){account={available:true,status:"guest",name:""};notify();}});
  }
  function authorize() {
    if(disposed || !ready || local || isPaused() || !account.available || authPending || account.status==="authorized") return false;
    const generation=++accountGeneration;ratingCache=null;ratingLoadAfter=0;authPending=true;account={available:true,status:"pending",name:""};notify();
    let dialog;
    try { dialog=sdk.auth.openAuthDialog(); }
    catch(error) { authPending=false;account={available:true,status:"guest",name:""};notify();return false; }
    Promise.resolve(dialog).then(()=>disposed?null:sdk.getPlayer()).then(player=>{if(!disposed && generation===accountGeneration) account=accountFrom(player);}).catch(()=>{if(!disposed && generation===accountGeneration) account={available:true,status:"guest",name:""};}).finally(()=>{if(!disposed && generation===accountGeneration){authPending=false;notify();}});
    return true;
  }
  function fail(error) {
    if (disposed) return;
    failed = true; ready = false; notify();
    root.console.error("Lucky Pickaxe: browser initialization failed", error);
    if (options.onError) options.onError(error);
  }
  function ratingFailure(error) {
    return error?.code==="LEADERBOARD_PLAYER_NOT_PRESENT";
  }
  function requestRating() {
    if(disposed || !ready || local || !sdk?.leaderboards || typeof sdk.leaderboards.getEntries!=="function") return false;
    if(ratingLoadPending && ratingLoadGeneration===accountGeneration) return true;
    if(Date.now()<ratingLoadAfter) {if(ratingCache) send("OnBrowserRating",JSON.stringify(ratingCache));return true;}
    const generation=accountGeneration;ratingLoadGeneration=generation;ratingLoadPending=true;ratingLoadAfter=Date.now()+30000;
    send("OnBrowserRating",JSON.stringify({status:"loading",entries:[]}));
    Promise.resolve().then(()=>sdk.leaderboards.getEntries(ratingName,{quantityTop:5,includeUser:account.status==="authorized",quantityAround:2})).then(data=>{
      if(disposed || generation!==accountGeneration) return;
      const userRank=Number.isSafeInteger(data?.userRank)&&data.userRank>0?data.userRank:0;
      const ranks=new Set();
      const entries=(Array.isArray(data?.entries)?data.entries:[]).slice(0,20).filter(e=>e && Number.isSafeInteger(e.rank)&&e.rank>0&&Number.isSafeInteger(e.score)&&e.score>=0&&e.score<=2147483647&&!ranks.has(e.rank)&&ranks.add(e.rank)).map(e=>{
        let extra;try {if(typeof e.extraData==="string" && e.extraData.length<=512) extra=JSON.parse(e.extraData);} catch {}
        const hasStats=extra?.version===1&&Number.isSafeInteger(extra.blocks)&&extra.blocks>=0&&extra.blocks<=1e12&&Number.isSafeInteger(extra.resets)&&extra.resets>=0&&extra.resets<=1e6;
        return {rank:e.rank,score:e.score,name:String(e.player?.publicName||"Игрок").slice(0,40),me:userRank>0&&e.rank===userRank,hasStats,blocks:hasStats?extra.blocks:0,resets:hasStats?extra.resets:0};
      }).sort((a,b)=>a.rank-b.rank);
      ratingCache={status:"ready",userRank,entries};send("OnBrowserRating",JSON.stringify(ratingCache));
    }).catch(()=>{if(!disposed && generation===accountGeneration) {ratingCache={status:"error",entries:[]};send("OnBrowserRating",JSON.stringify(ratingCache));}}).finally(()=>{if(generation===ratingLoadGeneration) ratingLoadPending=false;});
    return true;
  }
  function submitRating(json) {
    if(disposed || !ready || local || account.status!=="authorized" || authPending || ratingSendPending || Date.now()<ratingSendAfter || !sdk?.leaderboards || typeof sdk.leaderboards.setScore!=="function" || typeof sdk.leaderboards.getPlayerEntry!=="function" || typeof sdk.isAvailableMethod!=="function" || typeof json!=="string" || json.length>512) return false;
    let data;try {data=JSON.parse(json);} catch {return false;}
    if(data?.version!==1 || data.test!==false || !Number.isSafeInteger(data.score) || data.score<=0 || data.score>2147483647 || ![data.blocks,data.coins,data.tickets,data.xp].every(n=>Number.isSafeInteger(n)&&n>=0&&n<=1e12) || !Number.isSafeInteger(data.resets) || data.resets<0 || data.resets>1e6) return false;
    const generation=accountGeneration;ratingSendPending=true;ratingSendAfter=Date.now()+30000;
    Promise.resolve().then(async()=>{
      if(!await sdk.isAvailableMethod("leaderboards.setScore") || !await sdk.isAvailableMethod("leaderboards.getPlayerEntry")) throw new Error("Rating unavailable");
      let previous=0;
      try {const entry=await sdk.leaderboards.getPlayerEntry(ratingName);if(Number.isSafeInteger(entry?.score)&&entry.score>0) previous=entry.score;}
      catch(error) {if(!ratingFailure(error)) throw error;}
      if(disposed || generation!==accountGeneration) return;
      if(data.score>previous) await sdk.leaderboards.setScore(ratingName,data.score,JSON.stringify(data));
      if(disposed || generation!==accountGeneration) return;
      ratingCache=null;ratingLoadAfter=0;
      send("OnBrowserRatingSaved",JSON.stringify({status:"saved",score:Math.max(data.score,previous)}));
    }).catch(()=>{if(!disposed && generation===accountGeneration) send("OnBrowserRatingSaved",JSON.stringify({status:"error"}));}).finally(()=>{ratingSendPending=false;});
    return true;
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
      readAccount();
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
    syncGameplay(); rewardRequest = null;authPending=false;
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
    authorize,
    requestRating,
    submitRating,
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
    status() { return { ready, local, sdk: !!sdk, paused: isPaused(), failed, rewardedPending: !!rewardRequest, requestedLanguage, language, languageApplied, engineActive, gameplayReported, disposed, accountStatus:account.status, authorizationPending:authPending, ratingLoadPending, ratingSendPending }; }
  };
})(window);
