"use strict";
// Uses an isolated browser profile. Seeded fixtures and clicks never touch an installed player's save.
const { chromium } = require("playwright");
const fs = require("node:fs"), path = require("node:path"), assert = require("node:assert/strict");
const root = path.resolve(__dirname, ".."), logs = path.join(root, "Logs");
const url = process.argv[2] || "https://localhost:8126/";
const key = "LuckyPickaxe.MineLoot.Progress.v2.journal";
let checks = 0, context, page;
const errors = [], consoleLogs = [];
function check(value, label) { checks++; assert.ok(value, label); console.log("PASS " + checks + ": " + label); }
async function waitReady() {
  await page.waitForFunction(() => window.LuckyPickaxe?.status().ready && document.getElementById("loading").hidden, null, { timeout: 120000 });
  await page.waitForTimeout(200);
}
async function read() { return page.evaluate(key => JSON.parse(localStorage.getItem(key)), key); }
async function shot(name) { await page.screenshot({ path: path.join(logs, name + ".png") }); }
async function tap(x, y) {
  await page.mouse.move(x, y); await page.mouse.down();
  // Keep the press through a Unity update even when software rendering takes longer than 60 ms.
  await page.evaluate(() => new Promise(resolve => requestAnimationFrame(() => requestAnimationFrame(resolve))));
  await page.mouse.up(); await page.waitForTimeout(120);
}
async function seed(values) {
  const data = { ...(await read()), ...values };
  // Apply after pagehide has flushed the old live instance, before the new Unity instance loads.
  const token = "fixture-" + Date.now() + "-" + Math.random();
  await page.addInitScript(({ key, data, token }) => {
    if (sessionStorage.getItem(token)) return;
    localStorage.setItem(key, JSON.stringify(data)); sessionStorage.setItem(token, "1");
  }, { key, data, token });
  await page.reload(); await waitReady();
}
async function open(profile, device = {}) {
  context = await chromium.launchPersistentContext(profile, {
    channel: process.env.WEBGL_CHANNEL || "msedge", headless: true,
    viewport: { width: 480, height: 960 }, deviceScaleFactor: 1,
    ignoreHTTPSErrors: true, hasTouch: true,
    ...device,
    args: ["--use-angle=swiftshader", "--enable-unsafe-swiftshader", "--autoplay-policy=no-user-gesture-required"]
  });
  page = context.pages()[0] || await context.newPage();
  page.on("pageerror", error => errors.push(error.message));
  page.on("console", message => {
    consoleLogs.push({ type: message.type(), text: message.text(), url: message.location().url });
    if (message.type() === "error") errors.push(message.text());
  });
  await page.goto(url, { waitUntil: "domcontentloaded" }); await waitReady();
}
// This fixture controls video, account callbacks and SDK language in this isolated test profile.
// It never ships in WebGL or represents a real advertising/monetization check.
function rewardedFixture() {
  const initialize = window.YaGames.init.bind(window.YaGames);
  window.YaGames.init = async function (...args) {
    const sdk = await initialize(...args);
    const fixtureLanguage = sessionStorage.getItem("test-sdk-language");
    const fixture = window.rewardedFixture = { requests: 0, callbacks: null };
    let authorized = false;
    const account = window.accountFixture = { requests: 0, resolve: null, reject: null };
    const fullscreen = window.fullscreenFixture = { requests:0, userGesture:false };
    const screen = {fullscreen:{status:"off",request(){fullscreen.requests++;fullscreen.userGesture=!!navigator.userActivation?.isActive;return Promise.resolve();}}};
    const ranking = window.rankingFixture = { reads: [], writes: [], playerReads: 0 };
    let remoteScore;
    const leaderboards = {
      async getEntries(name,options) {
        ranking.reads.push({name,options});
        const entries=[20000,10000,5000,2500,1200].map((score,index)=>({rank:index+1,score,player:{publicName:"Шахтёр "+(index+1)},extraData:JSON.stringify({version:1,blocks:10000-index*1000,resets:4})}));
        if(options.includeUser) entries.push({rank:7,score:remoteScore||0,player:{publicName:"Тестовый шахтёр"},extraData:JSON.stringify({version:1,blocks:1000,resets:2})});
        return {userRank:options.includeUser?7:0,entries};
      },
      async getPlayerEntry(){ranking.playerReads++;if(remoteScore===undefined) throw {code:"LEADERBOARD_PLAYER_NOT_PRESENT"};return {score:remoteScore};},
      async setScore(name,score,extra){ranking.writes.push({name,score,extra});remoteScore=score;}
    };
    const player = { isAuthorized: () => authorized, getName: () => authorized ? "Тестовый шахтёр" : "" };
    const auth = new Proxy(sdk.auth || {}, { get(target, property) {
      if (property === "openAuthDialog") return () => {
        account.requests++;
        return new Promise((resolve, reject) => {
          account.resolve = () => { authorized = true; resolve(); };
          account.reject = reject;
        });
      };
      const value = Reflect.get(target, property, target);
      return typeof value === "function" ? value.bind(target) : value;
    } });
    const adv = new Proxy(sdk.adv || {}, { get(target, property) {
      if (property === "showRewardedVideo") return ({ callbacks }) => { fixture.requests++; fixture.callbacks = callbacks; callbacks.onOpen(); };
      const value = Reflect.get(target, property, target);
      return typeof value === "function" ? value.bind(target) : value;
    } });
    return new Proxy(sdk, { get(target, property) {
      if (property === "adv") return adv;
      if (property === "screen") return screen;
      if (property === "auth") return auth;
      if (property === "getPlayer") return async () => player;
      if (property === "leaderboards") return leaderboards;
      if (property === "isAvailableMethod") return async () => true;
      if (property === "environment" && fixtureLanguage) return { ...target.environment, i18n: { lang: fixtureLanguage } };
      const value = Reflect.get(target, property, target);
      return typeof value === "function" ? value.bind(target) : value;
    } });
  };
}
async function run() {
  fs.mkdirSync(logs, { recursive: true }); fs.mkdirSync(path.join(root, "_test-concept"), { recursive: true });
  const profile = fs.mkdtempSync(path.join(root, "_test-concept/web-profile-"));
  await open(profile);
  const browserVersion = context.browser().version();
  const state = await page.evaluate(() => LuckyPickaxe.status());
  check(state.ready && state.sdk && !state.local && !state.paused, "Built WebGL initializes the official SDK dev environment and unpauses");
  check(consoleLogs.filter(value => value.text.includes("LoadingAPI.ready()")).length === 1, "Game Ready reaches the official dev SDK once");
  check(state.language === "ru" && state.languageApplied && await page.locator("html").getAttribute("lang") === "ru", "The built Unity language acknowledgement applies Russian before Game Ready");
  check(state.engineActive && state.gameplayReported, "The built Unity controller reports active gameplay through the official SDK after Game Ready");
  const fresh = await read();
  check(fresh.coins === 80 && fresh.tickets === 2 && fresh.guideStep === 0, "Fresh browser session uses the existing starting resources and guide");
  await shot("webgl-start");
  await page.mouse.move(215, 380); await page.mouse.down({ button: "right" });
  await page.waitForTimeout(600); await page.mouse.up({ button: "right" });
  const afterRightHold = await read();
  check(afterRightHold.totalClicks === fresh.totalClicks && afterRightHold.coins === fresh.coins && afterRightHold.tickets === fresh.tickets,
    "Holding the secondary mouse button cannot mine ore or earn resources: " + JSON.stringify({ clicks: afterRightHold.totalClicks, coins: afterRightHold.coins }));
  for (let i = 0; i < 12; i++) await tap(215, 380);
  let saved = await read();
  check(saved.totalClicks === 12 && saved.oresMined === 0 && saved.oreDamage === 12 && saved.coins === 176 && saved.tickets === 2, "Real canvas clicks mine ore, grant a ticket and synchronously journal the reward");
  await tap(120, 734); saved = await read();
  check(saved.upgradeLevel === 1 && saved.coins === 56, "Real upgrade button pays the ordinary price in WebGL: " + JSON.stringify({ rank: saved.upgradeLevel, coins: saved.coins, clicks: saved.totalClicks }));
  await page.reload(); await waitReady(); saved = await read();
  check(saved.upgradeLevel === 1 && saved.coins === 56 && saved.totalClicks === 12, "Page reload restores the paid upgrade and mining progress");

  const audioKey="LuckyPickaxe.MineLoot.Audio.v1.journal";
  const readAudio=()=>page.evaluate(key=>JSON.parse(localStorage.getItem(key)),audioKey);
  let audioProgress=await read();
  await page.screenshot({path:path.join(logs,"webgl-v0310-audio-on.png"),clip:{x:0,y:0,width:480,height:164}});
  await tap(434,47);await shot("webgl-v0310-settings");
  audioProgress=await read();
  check(await page.evaluate(()=>!LuckyPickaxe.status().gameplayReported),"Opening audio settings stops SDK gameplay while their controls remain usable");
  await tap(274,327);await tap(155,455);await tap(342,583);
  let audio=await readAudio();
  check(audio?.version===1&&Math.abs(audio.master-.6)<.015&&Math.abs(audio.mining-.25)<.015&&Math.abs(audio.roulette-.8)<.015,"Real settings sliders save independent master, mining and roulette volumes");
  await tap(215,380);
  check(JSON.stringify(await read())===JSON.stringify(audioProgress),"Audio settings block mining behind the modal without modifying game progress");
  await tap(240,198);check((await readAudio()).muted,"The settings mute button preserves the three chosen volumes while disabling sound");
  await tap(240,810);
  check(await page.evaluate(()=>LuckyPickaxe.status().gameplayReported),"Closing audio settings resumes SDK gameplay");
  await page.screenshot({path:path.join(logs,"webgl-v0310-audio-off.png"),clip:{x:0,y:0,width:480,height:164}});
  await page.reload();await waitReady();await tap(434,47);await tap(240,198);audio=await readAudio();
  check(!audio.muted&&Math.abs(audio.master-.6)<.015&&Math.abs(audio.mining-.25)<.015&&Math.abs(audio.roulette-.8)<.015,"Reload applies the saved mute preference and all category volumes before the panel opens again");
  await page.evaluate(()=>window.dispatchEvent(new Event("blur")));await page.waitForFunction(()=>LuckyPickaxe.status().paused);
  await page.evaluate(()=>window.dispatchEvent(new Event("focus")));await page.waitForFunction(()=>!LuckyPickaxe.status().paused);
  check(JSON.stringify(await readAudio())===JSON.stringify(audio),"Platform pause and resume retain the player's sound preferences");
  await tap(140,654);await tap(340,654);await shot("webgl-v0310-settings-adjusted");
  const beforeAudioReset=await read();await tap(240,716);audio=await readAudio();
  check(audio.master===.5&&audio.mining===1&&audio.roulette===1&&!audio.muted&&JSON.stringify(await read())===JSON.stringify(beforeAudioReset),"Reset restores fifty percent master volume and leaves the paid game progress intact");
  await tap(240,810);
  await tap(434,47);await tap(70,327);await tap(240,810);
  await page.screenshot({path:path.join(logs,"webgl-v0310-audio-zero.png"),clip:{x:0,y:0,width:480,height:164}});
  await tap(434,47);await tap(240,716);await tap(240,810);

  await seed({ ...fresh, coins: 345, tickets: 8, hits: 59, experience: 39, oreKind: 9, oreDamage: 31, veinCharge: 23, veinBoostLeft: 12, guideStep: 5 });
  await shot("webgl-stone-v0310"); await tap(215, 380); saved=await read();
  check(saved.coins===345 && saved.tickets===8 && saved.hits===59 && saved.experience===57 && saved.enchantingExperience===18 && saved.miningBlocks===1 && saved.oresMined===0 && saved.veinCharge===23,
    "Real stone break pays XP only, including its level-up, without ticket or vein progress: "+JSON.stringify(saved));
  await page.reload(); await waitReady(); saved=await read();
  check(saved.version===16 && saved.coins===345 && saved.tickets===8 && saved.miningBlocks===1 && saved.enchantingExperience===18 && saved.oreKind!==9,"Reload preserves the XP-only stone break and advances to a valuable resource");

  await seed({ ...fresh, coins: 100000, tickets: 20, experience: 25480, swapLevel: 1, guideStep: 5, lastSeenUtc: Math.floor(Date.now()/1000) });
  await tap(292,916); await tap(240,740); await tap(82,916);
  const tabPaused=await read(); await page.waitForTimeout(2500); let frozen=await read();
  check(frozen.rollPending && frozen.totalRolls===1 && frozen.tickets===19 && JSON.stringify(frozen.pendingTypes)===JSON.stringify(tabPaused.pendingTypes),"Switching to Mine pauses the paid reels instead of disabling navigation");
  await tap(215,380); saved=await read();
  check(saved.totalClicks===tabPaused.totalClicks+1 && saved.coins>tabPaused.coins && saved.tickets===19 && saved.rollPending,"Real mining remains playable while the exact roulette choice is paused");
  await shot("webgl-v0310-tab-paused"); await tap(292,916); await page.waitForTimeout(2200); await tap(240,740);
  await page.waitForFunction(key=>!JSON.parse(localStorage.getItem(key)).rollPending,key);
  saved=await read(); check(saved.totalRolls===1 && saved.tickets===19,"Returning to Roulette resumes its original paid draw without spending another ticket");
  await seed({...fresh,tickets:20,experience:25480,guideStep:5,lastSeenUtc:Math.floor(Date.now()/1000)});
  await tap(292,916); await tap(376,740); await tap(397,916);
  const queuePause=await read(); await page.waitForTimeout(3500); saved=await read();
  check(saved.batchRemaining===queuePause.batchRemaining && saved.batchCompleted===queuePause.batchCompleted && saved.totalRolls===queuePause.totalRolls && saved.tickets===queuePause.tickets,"The ten-roll queue stays frozen on Rewards and cannot charge another roll while hidden");
  await tap(292,916); await tap(376,740);
  await page.waitForFunction(key=>!JSON.parse(localStorage.getItem(key)).rollPending,key);

  await seed({ ...fresh, coins: 600, experience: 100, swapLevel: 1, tickets: 1, guideStep: 5, lastSeenUtc: Math.floor(Date.now() / 1000) });
  await tap(292, 916); await tap(240, 740);
  await page.waitForFunction(key => JSON.parse(localStorage.getItem(key)).rollPending, key);
  const paid = await read();
  check(paid.tickets === 0 && paid.pendingSwapLimit === 1, "Beginning a real draw saves its picks, purchased exchange allowance and spent last ticket");
  await page.reload(); await waitReady(); saved = await read();
  check(saved.rollPending && saved.tickets === 0 && JSON.stringify(saved.pendingTypes) === JSON.stringify(paid.pendingTypes), "Reload during reel animation restores the same paid choice without another ticket");
  await shot("webgl-paid-choice");
  await tap(240, 740);
  await page.waitForFunction(key => !JSON.parse(localStorage.getItem(key)).rollPending, key);
  const resolved = await read();
  check(resolved.totalRolls === 1 && !resolved.rollPending, "Real confirm commits one roulette result before its animation");
  await context.close(); await open(profile); saved = await read();
  check(saved.totalRolls === 1 && saved.coins === resolved.coins && JSON.stringify(saved.depths) === JSON.stringify(resolved.depths), "Closing and reopening the browser keeps the resolved reward and depths exactly once");

  await seed({ ...fresh, coins: 1250, tickets: 3, boardSeed: 1758, depths: [5, 1, 2, 0, 3], guideStep: 5, lastSeenUtc: Math.floor(Date.now() / 1000) });
  await tap(292, 916); const beforeChange = await read();
  await tap(266, 780); saved = await read();
  check(JSON.stringify(saved) === JSON.stringify(beforeChange), "Opening the shaft-change confirmation never spends a ticket or rerolls the board");
  await shot("webgl-abandon-confirm");
  await tap(240, 740); saved = await read();
  check(saved.tickets === 3 && !saved.rollPending && saved.boardSeed === beforeChange.boardSeed, "Confirmation shade blocks the underlying spin button");
  await tap(240, 596); saved = await read();
  check(JSON.stringify(saved) === JSON.stringify(beforeChange), "Cancelling a shaft change keeps the entire saved progress unchanged");
  await tap(266, 780); await page.reload(); await waitReady(); saved = await read();
  check(saved.tickets === 3 && saved.coins === 1250 && saved.boardSeed === beforeChange.boardSeed && JSON.stringify(saved.depths) === JSON.stringify(beforeChange.depths), "Reloading an unconfirmed change keeps the original paid progress and all tickets");
  await tap(292, 916); await tap(266, 780); await tap(240, 654); await tap(240, 654); saved = await read();
  const changedBoard = saved.boardSeed;
  check(saved.tickets === 2 && saved.coins === 1250 && saved.stratum === beforeChange.stratum && saved.depths.every(depth => depth === 0) && saved.lostColumns.every(lost => !lost), "Confirming twice spends one ticket, keeps earned coins and clears only this board");
  await page.reload(); await waitReady(); saved = await read();
  check(saved.tickets === 2 && saved.coins === 1250 && saved.boardSeed === changedBoard && saved.depths.every(depth => depth === 0), "The confirmed shaft change persists without a second charge after reload");

  await seed({ ...fresh, coins: 0, workerCount: 1, guideStep: 5, offlineRateMilli: 2000, offlineCapacitySeconds: 14400, lastSeenUtc: Math.floor(Date.now() / 1000) - 600 });
  saved = await read(); const crate = saved.offlineCoins;
  check(crate >= 600 && crate <= 630 && saved.totalOfflineCoins === 0, "A seeded ten-minute absence prepares the bounded half-rate warehouse reward without auto-claiming it");
  await shot("webgl-offline");
  await tap(240, 624); saved = await read();
  check(saved.offlineCoins === 0 && saved.totalOfflineCoins === crate && saved.coins >= crate, "Real warehouse claim journals the credited wallet and empty crate together");

  // Headless Edge keeps all tabs visible. Deliver the browser lifecycle events explicitly;
  // this verifies the shipped bridge and Unity player, not physical tab switching on a device.
  await page.evaluate(() => window.dispatchEvent(new Event("blur")));
  await page.waitForFunction(() => LuckyPickaxe.status().paused && !LuckyPickaxe.status().engineActive, null, { polling: 100, timeout: 10000 });
  check(await page.evaluate(() => !LuckyPickaxe.status().gameplayReported), "Gameplay reporting stops together with the actual Unity pause after blur");
  const departure = await read(); await page.waitForTimeout(1200);
  check(JSON.stringify(await read()) === JSON.stringify(departure), "Browser blur event freezes live production and the departure timestamp in the built player");
  await shot("webgl-paused");
  await page.evaluate(() => window.dispatchEvent(new Event("focus")));
  await page.waitForFunction(() => !LuckyPickaxe.status().paused && LuckyPickaxe.status().engineActive, null, { polling: 100, timeout: 10000 });
  check(await page.evaluate(() => LuckyPickaxe.status().gameplayReported), "Focus reports a gameplay restart only after Unity actually resumes");
  check((await read()).coins === departure.coins, "Browser focus event resumes without silently crediting the offline crate to the wallet");

  await page.route("**/sdk.js", async route => {
    const response = await route.fetch();
    await route.fulfill({ response, body: (await response.text()) + "\n(" + rewardedFixture.toString() + ")();\n" });
  });
  await seed({ ...fresh, coins: 6543, enchantingExperience: 777, guideStep: 5, lastSeenUtc: Math.floor(Date.now() / 1000) });
  await page.waitForFunction(() => LuckyPickaxe.status().accountStatus === "guest");
  check(await page.evaluate(() => accountFixture.requests === 0), "Loading a guest account never opens the Yandex authorization dialog automatically");
  await tap(240, 73); await shot("webgl-v0310-account-guest");
  await tap(240, 584);
  await page.waitForFunction(() => LuckyPickaxe.status().authorizationPending && accountFixture.requests === 1);
  const loginPending = await read();
  await tap(240, 584); await tap(215, 380); await page.waitForTimeout(700);
  check(JSON.stringify(await read()) === JSON.stringify(loginPending) && await page.evaluate(() => accountFixture.requests === 1 && !LuckyPickaxe.status().engineActive), "The visible sign-in button crosses the WebGL bridge once and freezes input and production during authorization");
  await page.evaluate(() => accountFixture.resolve());
  await page.waitForFunction(() => LuckyPickaxe.status().accountStatus === "authorized" && !LuckyPickaxe.status().paused && LuckyPickaxe.status().engineActive);
  saved = await read();
  check(saved.coins === 6543 && saved.enchantingExperience === 777 && saved.totalClicks === 0, "Successful account authorization resumes the game without replacing the local save or granting resources");
  await shot("webgl-v0310-account-connected"); await tap(240, 726);
  await seed({ ...fresh, coins: 6543, guideStep: 5, lastSeenUtc: Math.floor(Date.now() / 1000) });
  await page.waitForFunction(() => LuckyPickaxe.status().accountStatus === "guest");
  await tap(240, 73); await tap(240, 584);
  await page.waitForFunction(() => LuckyPickaxe.status().authorizationPending);
  await page.evaluate(() => accountFixture.reject(new Error("Cancelled by test player")));
  await page.waitForFunction(() => LuckyPickaxe.status().accountStatus === "guest" && !LuckyPickaxe.status().paused);
  check((await read()).coins === 6543 && await page.evaluate(() => LuckyPickaxe.status().engineActive), "Cancelling sign-in returns to a playable guest session with the same wallet");
  await tap(240, 726);
  await seed({...fresh,earnedCoins:1000000,miningBlocks:800,totalBlocks:200,spentTickets:200,spentEnchantingExperience:10000,prestigeCount:2,ratingTestProfile:true,guideStep:5,lastSeenUtc:Math.floor(Date.now()/1000)});
  saved=await read();check(!saved.ratingTestProfile&&saved.earnedCoins===1000000&&saved.spentEnchantingExperience===10000,"A previously excluded save clears its obsolete marker without losing ranked progress");
  await tap(400,73);
  await page.waitForFunction(()=>rankingFixture.reads.length===1&&!LuckyPickaxe.status().ratingLoadPending);
  check(await page.evaluate(()=>rankingFixture.reads[0].name==="mmr"&&!rankingFixture.reads[0].options.includeUser&&rankingFixture.writes.length===0),"The header MMR badge opens a real guest leaderboard request without a fake score submission");
  await shot("webgl-v0310-rating-guest");const ratingWallet=await read();await tap(240,770);
  check(JSON.stringify(await read())===JSON.stringify(ratingWallet)&&await page.evaluate(()=>rankingFixture.reads.length===1),"The leaderboard cooldown blocks repeated SDK reads without changing player resources");
  await tap(342,282);await shot("webgl-v0310-medals");await tap(138,282);await tap(240,820);await tap(240,584);
  await page.waitForFunction(()=>LuckyPickaxe.status().authorizationPending);await page.evaluate(()=>accountFixture.resolve());
  await page.waitForFunction(()=>rankingFixture.writes.length===1&&!LuckyPickaxe.status().ratingSendPending);
  const ranked=await page.evaluate(()=>rankingFixture.writes[0]);
  check(ranked.score===120&&ranked.name==="mmr"&&JSON.parse(ranked.extra).blocks===1000&&JSON.parse(ranked.extra).resets===2,"Successful sign-in submits 120 MMR from the actual lifetime counters, weighted formula and reset multiplier");
  await tap(240,726);await tap(400,73);await page.waitForFunction(()=>rankingFixture.reads.length===2&&!LuckyPickaxe.status().ratingLoadPending);await tap(420,718);await shot("webgl-v0310-rating-own");
  check(await page.evaluate(()=>rankingFixture.reads[1].options.includeUser),"Signing in refreshes the table to include the player's Yandex row");
  await tap(240,877);await tap(190,916);await tap(350,282);await tap(240,480);saved=await read();
  check(!saved.ratingTestProfile&&saved.coins===100000080&&saved.earnedCoins===1000000&&await page.evaluate(()=>rankingFixture.writes.length===1),"Resource grants keep the profile eligible for rating without inventing extra lifetime earnings or rewriting an equal score");
  await seed({ ...fresh, coins: 80, workerCount: 1, guideStep: 5, offlineRateMilli: 2000, offlineCapacitySeconds: 14400, lastSeenUtc: Math.floor(Date.now() / 1000) - 600 });
  await shot("webgl-rewarded-offer");
  await tap(240, 684);
  await page.waitForFunction(() => LuckyPickaxe.status().rewardedPending && window.rewardedFixture?.requests === 1);
  const beforeVideo = await read();
  check(await page.evaluate(() => !LuckyPickaxe.status().engineActive && !LuckyPickaxe.status().gameplayReported), "Opening rewarded video stops both Unity and GameplayAPI before callbacks can award coins");
  await tap(240, 624); await tap(240, 684);
  check(JSON.stringify(await read()) === JSON.stringify(beforeVideo) && await page.evaluate(() => rewardedFixture.requests === 1 && LuckyPickaxe.status().paused), "Real video button crosses the WebGL bridge once and blocks ordinary claim and live production");
  await page.evaluate(() => { rewardedFixture.callbacks.onRewarded(); rewardedFixture.callbacks.onRewarded(); });
  await page.waitForFunction(key => JSON.parse(localStorage.getItem(key)).offlineCoins === 0, key);
  const videoPaid = await read();
  check(videoPaid.coins === beforeVideo.coins + 3 * beforeVideo.offlineCoins && videoPaid.totalOfflineCoins === 3 * beforeVideo.offlineCoins, "onRewarded journals exactly three times the crate before close; duplicate callbacks cannot pay again");
  await page.evaluate(() => rewardedFixture.callbacks.onClose());
  await page.waitForFunction(() => !LuckyPickaxe.status().paused && LuckyPickaxe.status().engineActive);
  check(await page.evaluate(() => LuckyPickaxe.status().gameplayReported), "Closing the rewarded fixture restores GameplayAPI after the controller releases its independent ad pause");
  check((await read()).offlineCoins === 0 && !await page.evaluate(() => LuckyPickaxe.status().rewardedPending), "Video close resumes gameplay without creating another crate from ad time");
  await page.reload(); await waitReady(); saved = await read();
  check(saved.totalOfflineCoins === videoPaid.totalOfflineCoins && saved.coins >= videoPaid.coins && saved.offlineCoins < 10, "Reload preserves the paid video result and cannot recreate its original crate");

  await seed({ ...fresh, coins: 80, workerCount: 1, guideStep: 5, offlineRateMilli: 2000, offlineCapacitySeconds: 14400, lastSeenUtc: Math.floor(Date.now() / 1000) - 600 });
  await tap(240, 684); await page.waitForFunction(() => LuckyPickaxe.status().rewardedPending);
  const cancelled = await read();
  await page.evaluate(() => rewardedFixture.callbacks.onClose()); await page.waitForFunction(() => !LuckyPickaxe.status().paused);
  saved = await read();
  check(saved.offlineCoins === cancelled.offlineCoins && saved.totalOfflineCoins === 0 && saved.coins === cancelled.coins, "Closing a video without onRewarded preserves the ordinary warehouse reward");
  await shot("webgl-rewarded-cancelled");
  await tap(240, 684); await page.waitForFunction(() => LuckyPickaxe.status().rewardedPending);
  const failedVideo = await read();
  await page.evaluate(() => rewardedFixture.callbacks.onError()); await page.waitForFunction(() => !LuckyPickaxe.status().paused);
  saved = await read();
  check(saved.offlineCoins === failedVideo.offlineCoins && saved.totalOfflineCoins === 0 && saved.coins === failedVideo.coins, "SDK video error releases the pause and preserves all ordinary coins");
  await shot("webgl-rewarded-error");
  await tap(240, 624); saved = await read();
  check(saved.offlineCoins === 0 && saved.totalOfflineCoins === failedVideo.offlineCoins && saved.coins >= failedVideo.coins + failedVideo.offlineCoins, "Ordinary canvas claim still works after cancellation and video failure");

  await seed({ ...fresh, coins: 80, workerCount: 1, guideStep: 5, offlineRateMilli: 2000, offlineCapacitySeconds: 14400, lastSeenUtc: Math.floor(Date.now() / 1000) - 600 });
  await tap(240, 684); await page.waitForFunction(() => LuckyPickaxe.status().rewardedPending);
  await page.evaluate(() => rewardedFixture.callbacks.onRewarded());
  await page.waitForFunction(key => JSON.parse(localStorage.getItem(key)).offlineCoins === 0, key);
  const beforeCloseReload = await read();
  await page.reload(); await waitReady(); saved = await read();
  check(saved.totalOfflineCoins === beforeCloseReload.totalOfflineCoins && saved.coins >= beforeCloseReload.coins && saved.offlineCoins < 10, "Reload between onRewarded and onClose keeps the saved reward exactly once");

  await seed({ ...fresh, guideStep: 5, lastSeenUtc: Math.floor(Date.now() / 1000) });
  await tap(397, 916); await shot("webgl-rewards"); await tap(240, 422);
  await page.waitForFunction(() => LuckyPickaxe.status().rewardedPending && window.rewardedFixture?.requests === 1);
  const ticketsBefore = await read();
  check(await page.evaluate(() => LuckyPickaxe.status().paused && !LuckyPickaxe.status().gameplayReported), "The visible ticket-video action pauses gameplay through the existing SDK bridge");
  await page.evaluate(() => { rewardedFixture.callbacks.onRewarded(); rewardedFixture.callbacks.onRewarded(); });
  await page.waitForFunction(key => JSON.parse(localStorage.getItem(key)).videoTicketClaims === 1, key);
  saved = await read();
  check(saved.tickets === ticketsBefore.tickets + 10 && saved.coins === ticketsBefore.coins && saved.videoTicketClaims === 1, "Ticket video journals ten tickets exactly once even with duplicate onRewarded");
  await page.evaluate(() => rewardedFixture.callbacks.onClose()); await page.waitForFunction(() => !LuckyPickaxe.status().paused);
  await page.reload(); await waitReady(); saved = await read();
  check(saved.tickets === 12 && saved.videoTicketClaims === 1 && saved.videoTicketsReadyUtc > Math.floor(Date.now() / 1000), "Reload retains the ticket gift and its cooldown");
  await tap(397, 916); await tap(240, 422);
  check(!await page.evaluate(() => LuckyPickaxe.status().rewardedPending), "The visible cooldown prevents opening another ticket video");
  await seed({ ...fresh, guideStep: 5, lastSeenUtc: Math.floor(Date.now() / 1000) });
  await tap(397, 916); await tap(240, 422); await page.waitForFunction(() => LuckyPickaxe.status().rewardedPending);
  await page.evaluate(() => rewardedFixture.callbacks.onClose()); await page.waitForFunction(() => !LuckyPickaxe.status().paused);
  check((await read()).tickets === 2 && (await read()).videoTicketClaims === 0, "Closing ticket video without onRewarded gives no gift");
  await tap(240, 422); await page.waitForFunction(() => LuckyPickaxe.status().rewardedPending);
  await page.evaluate(() => rewardedFixture.callbacks.onError()); await page.waitForFunction(() => !LuckyPickaxe.status().paused);
  check((await read()).tickets === 2 && (await read()).videoTicketClaims === 0, "Ticket-video SDK error releases input without paying or consuming the cooldown");

  await page.setViewportSize({ width: 1280, height: 720 });
  await seed({ ...fresh, coins: 1000, guideStep: 5, lastSeenUtc: Math.floor(Date.now() / 1000) });
  await shot("webgl-desktop-mine");
  for (let i = 0; i < 6; i++) await tap(375, 380);
  saved = await read();
  check(saved.totalClicks === 6 && saved.oresMined === 0 && saved.coins === 1048, "Wide canvas mines through the projected ore target: " + JSON.stringify({ clicks: saved.totalClicks, ores: saved.oresMined, coins: saved.coins }));
  await tap(981, 490); saved = await read();
  check(saved.upgradeLevel === 1 && saved.coins === 928, "The focused mining sidebar upgrades its equipped pickaxe");
  await seed({ ...fresh, coins: 1000, experience: 100, guideStep: 5, lastSeenUtc: Math.floor(Date.now() / 1000) });
  await tap(1064, 585); await tap(348, 381); saved = await read();
  check(saved.workerCount === 1 && saved.coins >= 700 && saved.coins <= 704, "Direct team action opens production and hires a worker for the ordinary price");
  await shot("webgl-desktop-production");
  await page.reload(); await waitReady(); saved = await read();
  check(saved.workerCount === 1 && saved.coins >= 700, "Reload preserves machinery bought through the dedicated production page");

  await seed({ ...fresh, coins: 10000, experience: 25480, guideStep: 5, lastSeenUtc: Math.floor(Date.now() / 1000) });
  await tap(480, 92); await tap(466,248); const helpWallet=await read();
  await tap(932,381); saved=await read();
  check(saved.coins===helpWallet.coins && saved.hasteLevel===0,"Upgrade question opens a modal that blocks purchases behind its shade");
  await shot("webgl-v0310-upgrade-info"); await tap(640,526);
  await tap(640,381); await tap(932,381); saved=await read();
  check(saved.fortuneLevel === 1 && saved.hasteLevel === 1 && saved.coins === 9200, "Direct enchantments tab buys real Fortune and Haste ranks with their displayed prices");
  await shot("webgl-enchantments"); await page.reload(); await waitReady(); saved = await read();
  check(saved.fortuneLevel === 1 && saved.hasteLevel === 1 && saved.coins === 9200, "Reload retains both new enchantments");
  await seed({ ...fresh, coins: 10000, experience: 700, enchantingExperience: 10000, guideStep: 5, lastSeenUtc: Math.floor(Date.now() / 1000) });
  await tap(480, 92); await tap(749, 197); await shot("webgl-v0310-enchanting-room");
  await tap(867, 569); saved = await read();
  check(saved.enchantCount === 1 && saved.enchantments > 0 && saved.enchantingExperience === 9880 && saved.experience === 700, "The dedicated enchanting room spends 120 available XP and applies a real pick enchantment without reducing miner level");
  await tap(867, 317);
  check((await read()).enchantCount === 1 && (await read()).enchantingExperience === 9880, "The paid result modal blocks re-enchanting behind it before the player closes the reveal");
  await shot("webgl-v0310-enchantment-reveal"); await tap(640, 579);
  await tap(867, 317); saved = await read();
  check(saved.enchantCount === 2 && saved.enchantingExperience === 9820 && saved.enchantments > 0, "Re-enchanting draws a paid random set and its first-tier price grows from 40 to 60 XP");
  const rerolled = saved.enchantments;
  await tap(640, 579);
  await page.reload(); await waitReady(); saved = await read();
  check(saved.enchantCount === 2 && saved.enchantments === rerolled && saved.enchantingExperience === 9820 && saved.experience === 700, "Reload retains the exact paid enchantments, reroll counter and unspent XP");
  await tap(480, 92); await tap(967, 197); await tap(640, 351); await tap(640, 411); await tap(640, 470); saved = await read();
  check(saved.coins === 100010000 && saved.enchantingExperience === 19820 && saved.tickets === 12, "Temporary draft tools expose explicit buttons for 100 million coins, enchanting XP and a ten-roll test budget");
  await shot("webgl-v0310-draft-tools");
  await seed({ ...fresh, coins: 1000, pickWear: 160, guideStep: 5, lastSeenUtc: Math.floor(Date.now() / 1000) });
  await tap(1147, 490); saved = await read();
  check(saved.pickWear === 0 && saved.coins === 970, "The mine's direct repair button restores the existing worn pick for its displayed thirty coins");

  await seed({ ...fresh, coins: 10000, experience: 700, guideStep: 5, lastSeenUtc: Math.floor(Date.now() / 1000) });
  await tap(480, 92); await tap(320, 632); await tap(640, 572); saved = await read();
  check(saved.swapLevel === 2 && saved.coins === 7000, "Batch exchange purchase stops at its next miner-level gate");
  await seed({ ...fresh, coins: 1000, experience: 100, guideStep: 5, lastSeenUtc: Math.floor(Date.now() / 1000) });
  await tap(480, 92); await tap(320, 632); await tap(320, 632); await tap(531, 197); await tap(348, 381); saved = await read();
  check(saved.workerCount === 2 && saved.coins >= 346 && saved.coins <= 350, "Maximum purchase remains selected across sections and hires exactly two affordable workers");
  await page.setViewportSize({ width: 480, height: 960 });
  await seed({ ...fresh, coins: 264, enchantments: 1, enchantCount: 2, enchantingExperience: 77, guideStep: 5, lastSeenUtc: Math.floor(Date.now() / 1000) });
  await tap(120, 734); saved = await read();
  check(saved.upgradeLevel === 1 && saved.enchantments === 1 && saved.enchantCount === 2, "A rank upgrade within the wooden material preserves its paid enchantments");
  await tap(120, 734); saved = await read();
  check(saved.upgradeLevel === 2 && saved.coins === 0 && saved.enchantments === 0 && saved.enchantCount === 0 && saved.enchantingExperience === 77, "Upgrading wood to stone clears the old enchantments and reroll count while preserving the XP bank");
  await seed({ ...fresh, tickets: 12, experience: 25480, swapLevel: 3, guideStep: 5, lastSeenUtc: Math.floor(Date.now() / 1000) });
  await tap(292, 916); await tap(376, 740);
  await page.waitForFunction(key => JSON.parse(localStorage.getItem(key)).batchActive, key);
  saved = await read();
  check(saved.totalRolls >= 1 && saved.batchRemaining < 10 && saved.tickets < 12, "Ten-roll button journals the first paid draw and its remaining queue before animation");
  await page.reload(); await waitReady();
  await page.waitForFunction(key => { const d=JSON.parse(localStorage.getItem(key)); return d.batchCompleted===10 && !d.batchActive && !d.rollPending; }, key, { timeout: 60000 });
  saved=await read();
  check(saved.totalRolls === 10 && saved.coins === 80 + saved.batchReward && saved.tickets >= 2, "Reload resumes the paid series, commits exactly ten rewards and never starts an eleventh draw");
  await page.waitForTimeout(1000); await shot("webgl-v0310-ten-roll-result");
  await seed({ ...fresh, tickets: 10, guideStep: 5, lastSeenUtc: Math.floor(Date.now() / 1000) });
  await tap(292, 916); await tap(376, 740); await tap(376, 740);
  const stopped = await read();
  await page.waitForFunction(key => !JSON.parse(localStorage.getItem(key)).rollPending, key);
  saved=await read();
  check(!saved.batchActive && saved.totalRolls === stopped.totalRolls && saved.totalRolls < 10 && saved.tickets >= 10 - saved.totalRolls, "Stop cancels unstarted draws while preserving the current paid result and the remaining ticket stock");

  await page.setViewportSize({ width: 390, height: 844 });
  await seed({ ...fresh, guideStep: 5, lastSeenUtc: Math.floor(Date.now() / 1000) });
  for (let i = 0; i < 6; i++) { await page.touchscreen.tap(175, 341); await page.waitForTimeout(120); }
  saved = await read();
  check(saved.totalClicks === 6 && saved.oresMined === 0 && saved.oreDamage === 6 && saved.coins === 128, "Touch input damages durable ore in a 390 by 844 viewport");
  await shot("webgl-mobile");
  await page.setViewportSize({ width: 960, height: 540 }); await page.waitForTimeout(300); await shot("webgl-landscape");
  check(await page.evaluate(() => document.documentElement.scrollWidth === innerWidth && document.documentElement.scrollHeight === innerHeight), "Wide browser layout stays inside the viewport without scrolling");

  await seed({ ...fresh, coins: 1234567, guideStep: 5, lastSeenUtc: Math.floor(Date.now() / 1000) });
  const beforeLanguage = await read();
  for (const language of ["en", "kk", "tr"]) {
    await page.evaluate(value => sessionStorage.setItem("test-sdk-language", value), language);
    await page.reload(); await waitReady();
    const languageState = await page.evaluate(() => LuckyPickaxe.status());
    saved = await read();
    check(languageState.requestedLanguage === language && languageState.language === "ru" && languageState.languageApplied && saved.coins === beforeLanguage.coins && saved.totalRolls === beforeLanguage.totalRolls && saved.upgradeLevel === beforeLanguage.upgradeLevel,
      "SDK language " + language + " selects the only declared Russian translation without changing progress");
  }
  await shot("webgl-language-fallback");

  await context.close();
  await verifyPhones(fresh);
  check(errors.length === 0, "Built game has no unexpected browser or JavaScript errors: " + errors.join("; "));
  fs.writeFileSync(path.join(logs, "web-browser-check.json"), JSON.stringify({ result: "PASS", checks, browser: browserVersion, sdkEnvironment: "official dev proxy 0.0.2", profile, realDevice: false, mobile:"Android and iPhone UA/viewport/touch emulation in Chromium; 320x568 through 412x915, portrait with rotation pause; Safari engine and real fullscreen not tested", audio: "actual Unity sliders, mute, reload, platform pause, preview and reset in an isolated browser profile", leaderboard: "controlled SDK table and submissions in isolated profile; no real account score posted", language: "Russian acknowledgement plus en/kk/tr SDK fixtures; no English translation declared", lifecycleEvents: "dispatched blur/focus and real resize/touchcancel in headless Edge", rewardedVideo: "controlled video callbacks in isolated profile; no real ad inventory tested" }, null, 2));
  console.log(`LUCKY_PICKAXE_WEB_BROWSER: PASS (${checks} checks, Edge ${browserVersion})`);
}
async function phonePoint(x,y) {
  return page.locator("#unity-canvas").evaluate((canvas,[x,y])=>{
    const r=canvas.getBoundingClientRect(),s=Math.min(r.width/480,r.height/960);
    return {x:r.x+(r.width-480*s)/2+x*s,y:r.y+(r.height-960*s)/2+y*s};
  },[x,y]);
}
async function phoneTap(x,y) { const p=await phonePoint(x,y);await page.touchscreen.tap(p.x,p.y);await page.waitForTimeout(160); }
async function playPhone() {await page.getByRole("button",{name:"ИГРАТЬ",exact:true}).tap();await page.waitForFunction(()=>!LuckyPickaxe.status().paused);}
async function phoneSeed(data) {await seed(data);await playPhone();}
async function verifyPhones(fresh) {
  for(const device of [
    {name:"android",userAgent:"Mozilla/5.0 (Linux; Android 13; Pixel 7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0.0.0 Mobile Safari/537.36",viewport:{width:360,height:780}},
    {name:"iphone",userAgent:"Mozilla/5.0 (iPhone; CPU iPhone OS 17_5 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.5 Mobile/15E148 Safari/604.1",viewport:{width:390,height:844}}
  ]) {
    const profile=fs.mkdtempSync(path.join(root,"_test-concept/web-phone-"));
    await open(profile,{userAgent:device.userAgent,viewport:device.viewport,deviceScaleFactor:3,isMobile:true});
    await page.route("**/sdk.js",async route=>{const response=await route.fetch();await route.fulfill({response,body:(await response.text())+"\n("+rewardedFixture.toString()+")();\n"});});
    await page.reload();await waitReady();
    const startup=await page.evaluate(()=>LuckyPickaxe.status());
    check(startup.device==="mobile" && startup.paused && startup.viewportPaused && await page.locator("#mobile-start").isVisible(),device.name+": the SDK selects a mobile start screen and waits for a real user gesture");
    check(await page.locator("#unity-canvas").evaluate(canvas=>canvas.width<=canvas.clientWidth*1.51),device.name+": a high-density phone caps WebGL rendering at DPR 1.5");
    await shot("webgl-v0310-"+device.name+"-start");await playPhone();
    check(await page.evaluate(()=>fullscreenFixture.requests===1 && fullscreenFixture.userGesture),device.name+": the play tap requests SDK fullscreen inside the browser user gesture");
    await phoneSeed({...fresh,guideStep:5,lastSeenUtc:Math.floor(Date.now()/1000)});
    for(let i=0;i<6;i++) await phoneTap(215,380);
    let data=await read();check(data.totalClicks===6 && data.oreDamage===6,device.name+": actual touch events mine ore exactly once per tap");
    const session=await context.newCDPSession(page),p=await phonePoint(215,380);
    await session.send("Input.dispatchTouchEvent",{type:"touchStart",touchPoints:[{x:p.x,y:p.y,id:0}]});await page.waitForTimeout(800);
    await session.send("Input.dispatchTouchEvent",{type:"touchEnd",touchPoints:[]});await page.waitForTimeout(200);
    data=await read();const held=data.totalClicks;await page.waitForTimeout(500);
    check(held>6 && (await read()).totalClicks===held,device.name+": holding mines repeatedly and lifting the finger stops it");
    await session.send("Input.dispatchTouchEvent",{type:"touchStart",touchPoints:[{x:p.x,y:p.y,id:0}]});
    await session.send("Input.dispatchTouchEvent",{type:"touchCancel",touchPoints:[]});await page.waitForTimeout(600);
    check((await read()).totalClicks===held,device.name+": a cancelled touch cannot leave auto-mining stuck on");
    for(const size of [{width:320,height:568},{width:360,height:640},{width:390,height:844},{width:412,height:915}]) {
      await page.setViewportSize(size);await page.waitForTimeout(350);
      const before=await read();await phoneTap(215,380);data=await read();
      check(data.totalClicks===before.totalClicks+1 && await page.evaluate(()=>document.documentElement.scrollWidth===innerWidth && document.documentElement.scrollHeight===innerHeight),device.name+": touch coordinates and no page scrolling at "+size.width+"x"+size.height);
    }
    await page.setViewportSize(device.viewport);
    await phoneSeed({...fresh,coins:1000,experience:700,enchantingExperience:1000,guideStep:5,lastSeenUtc:Math.floor(Date.now()/1000)});
    await phoneTap(120,734);data=await read();check(data.upgradeLevel===1 && data.coins===880,device.name+": the touch upgrade button spends its displayed price");
    await phoneTap(188,916);await phoneTap(350,249);await phoneTap(130,454);data=await read();
    check(data.workerCount===1 && data.coins>=580 && data.coins<600,device.name+": touch navigation opens production and hires a miner");
    await phoneTap(125,279);await shot("webgl-v0310-"+device.name+"-enchanting");await phoneTap(375,662);data=await read();
    check(data.enchantCount===1 && data.enchantingExperience===960 && data.enchantments>0,device.name+": touch enchanting spends XP and reveals a paid random result");
    await phoneSeed({...fresh,coins:1000,tickets:12,experience:700,swapLevel:1,guideStep:5,lastSeenUtc:Math.floor(Date.now()/1000)});
    await phoneTap(292,916);await phoneTap(240,740);await page.waitForFunction(key=>JSON.parse(localStorage.getItem(key)).rollPending,key);
    const paid=await read();await page.setViewportSize({width:device.viewport.height,height:device.viewport.width});
    await page.waitForFunction(()=>LuckyPickaxe.status().viewportPaused);await page.waitForTimeout(1200);
    data=await read();check(await page.locator("#rotate").isVisible() && data.tickets===paid.tickets && data.totalRolls===paid.totalRolls && data.rollPending && JSON.stringify(data.pendingTypes)===JSON.stringify(paid.pendingTypes),device.name+": landscape pauses the same paid roulette without another charge");
    await shot("webgl-v0310-"+device.name+"-rotation");
    await page.evaluate(()=>window.dispatchEvent(new Event("focus")));
    check(await page.evaluate(()=>LuckyPickaxe.status().paused && !LuckyPickaxe.status().gameplayReported),device.name+": focus cannot resume roulette behind the rotation screen");
    await page.setViewportSize(device.viewport);await page.waitForFunction(()=>!LuckyPickaxe.status().paused);
    await page.reload();await waitReady();data=await read();
    check(data.rollPending && data.tickets===paid.tickets && JSON.stringify(data.pendingTypes)===JSON.stringify(paid.pendingTypes),device.name+": reloading keeps the exact paid pick choice behind the mobile start screen");
    await playPhone();await phoneTap(240,740);await page.waitForFunction(key=>!JSON.parse(localStorage.getItem(key)).rollPending,key);
    data=await read();check(data.totalRolls===1 && data.tickets===11,device.name+": the resumed touch confirmation completes the original draw once");
    await phoneSeed({...fresh,guideStep:5,lastSeenUtc:Math.floor(Date.now()/1000)});
    await page.locator("body").evaluate(body=>{body.style.padding="24px 10px 20px 10px";});
    await page.waitForTimeout(350);
    await phoneTap(434,30);await phoneTap(274,327);await phoneTap(240,810);
    check(await page.evaluate(()=>Math.abs(JSON.parse(localStorage.getItem("LuckyPickaxe.MineLoot.Audio.v1.journal")).master-.6)<.02),device.name+": touch audio controls respect simulated screen safe insets");
    await shot("webgl-v0310-"+device.name+"-mine");
    check(await page.locator("audio,video").count()===0,device.name+": the game creates no HTML audio/video elements or system media player");
    await context.close();
  }
}
run().catch(error => {
  fs.writeFileSync(path.join(logs, "web-browser-check.json"), JSON.stringify({ result: "FAIL", checks, error: error.message }, null, 2));
  console.error(error); process.exitCode = 1;
}).finally(async () => {
  fs.writeFileSync(path.join(logs, "web-browser-console.json"), JSON.stringify(consoleLogs, null, 2));
  if (context) await context.close();
});
