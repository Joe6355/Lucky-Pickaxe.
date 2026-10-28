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
async function tap(x, y) { await page.mouse.click(x, y); await page.waitForTimeout(120); }
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
async function open(profile) {
  context = await chromium.launchPersistentContext(profile, {
    channel: process.env.WEBGL_CHANNEL || "msedge", headless: true,
    viewport: { width: 480, height: 960 }, deviceScaleFactor: 1,
    ignoreHTTPSErrors: true, hasTouch: true,
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
// This fixture replaces only video callbacks inside this isolated test profile.
// It never ships in WebGL or represents a real advertising/monetization check.
function rewardedFixture() {
  const initialize = window.YaGames.init.bind(window.YaGames);
  window.YaGames.init = async function (...args) {
    const sdk = await initialize(...args);
    const fixture = window.rewardedFixture = { requests: 0, callbacks: null };
    const adv = new Proxy(sdk.adv || {}, { get(target, property) {
      if (property === "showRewardedVideo") return ({ callbacks }) => { fixture.requests++; fixture.callbacks = callbacks; callbacks.onOpen(); };
      const value = Reflect.get(target, property, target);
      return typeof value === "function" ? value.bind(target) : value;
    } });
    return new Proxy(sdk, { get(target, property) {
      if (property === "adv") return adv;
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
  const fresh = await read();
  check(fresh.coins === 80 && fresh.tickets === 2 && fresh.guideStep === 0, "Fresh browser session uses the existing starting resources and guide");
  await shot("webgl-start");
  for (let i = 0; i < 12; i++) await tap(215, 380);
  let saved = await read();
  check(saved.totalClicks === 12 && saved.oresMined === 2 && saved.coins === 200 && saved.tickets === 3, "Real canvas clicks mine ore, grant a ticket and synchronously journal the reward");
  await tap(120, 734); saved = await read();
  check(saved.upgradeLevel === 1 && saved.coins === 80, "Real upgrade button pays the ordinary price in WebGL");
  await page.reload(); await waitReady(); saved = await read();
  check(saved.upgradeLevel === 1 && saved.coins === 80 && saved.totalClicks === 12, "Page reload restores the paid upgrade and mining progress");

  await seed({ ...fresh, coins: 600, experience: 100, swapLevel: 1, tickets: 1, guideStep: 5, lastSeenUtc: Math.floor(Date.now() / 1000) });
  await tap(240, 916); await tap(240, 740);
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

  await seed({ ...fresh, coins: 0, workerCount: 1, guideStep: 5, offlineRateMilli: 2000, offlineCapacitySeconds: 14400, lastSeenUtc: Math.floor(Date.now() / 1000) - 600 });
  saved = await read(); const crate = saved.offlineCoins;
  check(crate >= 600 && crate <= 630 && saved.totalOfflineCoins === 0, "A seeded ten-minute absence prepares the bounded half-rate warehouse reward without auto-claiming it");
  await shot("webgl-offline");
  await tap(240, 624); saved = await read();
  check(saved.offlineCoins === 0 && saved.totalOfflineCoins === crate && saved.coins >= crate, "Real warehouse claim journals the credited wallet and empty crate together");

  // Headless Edge keeps all tabs visible. Deliver the browser lifecycle events explicitly;
  // this verifies the shipped bridge and Unity player, not physical tab switching on a device.
  await page.evaluate(() => window.dispatchEvent(new Event("blur")));
  await page.waitForFunction(() => LuckyPickaxe.status().paused, null, { polling: 100, timeout: 10000 });
  const departure = await read(); await page.waitForTimeout(1200);
  check(JSON.stringify(await read()) === JSON.stringify(departure), "Browser blur event freezes live production and the departure timestamp in the built player");
  await shot("webgl-paused");
  await page.evaluate(() => window.dispatchEvent(new Event("focus")));
  await page.waitForFunction(() => !LuckyPickaxe.status().paused, null, { polling: 100, timeout: 10000 });
  check((await read()).coins === departure.coins, "Browser focus event resumes without silently crediting the offline crate to the wallet");

  await page.route("**/sdk.js", async route => {
    const response = await route.fetch();
    await route.fulfill({ response, body: (await response.text()) + "\n(" + rewardedFixture.toString() + ")();\n" });
  });
  await seed({ ...fresh, coins: 80, workerCount: 1, guideStep: 5, offlineRateMilli: 2000, offlineCapacitySeconds: 14400, lastSeenUtc: Math.floor(Date.now() / 1000) - 600 });
  await shot("webgl-rewarded-offer");
  await tap(240, 684);
  await page.waitForFunction(() => LuckyPickaxe.status().rewardedPending && window.rewardedFixture?.requests === 1);
  const beforeVideo = await read();
  await tap(240, 624); await tap(240, 684);
  check(JSON.stringify(await read()) === JSON.stringify(beforeVideo) && await page.evaluate(() => rewardedFixture.requests === 1 && LuckyPickaxe.status().paused), "Real video button crosses the WebGL bridge once and blocks ordinary claim and live production");
  await page.evaluate(() => { rewardedFixture.callbacks.onRewarded(); rewardedFixture.callbacks.onRewarded(); });
  await page.waitForFunction(key => JSON.parse(localStorage.getItem(key)).offlineCoins === 0, key);
  const videoPaid = await read();
  check(videoPaid.coins === beforeVideo.coins + 2 * beforeVideo.offlineCoins && videoPaid.totalOfflineCoins === 2 * beforeVideo.offlineCoins, "onRewarded journals exactly twice the crate before close; duplicate callbacks cannot pay again");
  await page.evaluate(() => rewardedFixture.callbacks.onClose());
  await page.waitForFunction(() => !LuckyPickaxe.status().paused);
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

  await page.setViewportSize({ width: 1280, height: 720 });
  await seed({ ...fresh, coins: 1000, guideStep: 5, lastSeenUtc: Math.floor(Date.now() / 1000) });
  await shot("webgl-desktop-mine");
  for (let i = 0; i < 6; i++) await tap(375, 380);
  saved = await read();
  check(saved.totalClicks === 6 && saved.oresMined === 1 && saved.coins === 1060, "Wide canvas mines through the projected ore target");
  await tap(989, 328); saved = await read();
  check(saved.upgradeLevel === 1 && saved.coins === 940, "Existing sidebar upgrade purchases work beside the wide mine");
  await seed({ ...fresh, coins: 1000, experience: 100, guideStep: 5, lastSeenUtc: Math.floor(Date.now() / 1000) });
  await tap(1169, 194); await tap(989, 328); saved = await read();
  check(saved.workerCount === 1 && saved.coins >= 700 && saved.coins <= 704, "Wide production tab hires a worker at the ordinary 300-coin price");
  await shot("webgl-desktop-production");
  await page.reload(); await waitReady(); saved = await read();
  check(saved.workerCount === 1 && saved.coins >= 700, "Wide interface reload preserves the purchased machinery");

  await seed({ ...fresh, lastSeenUtc: Math.floor(Date.now() / 1000) });
  await page.setViewportSize({ width: 390, height: 844 }); await page.waitForTimeout(300);
  for (let i = 0; i < 6; i++) { await page.touchscreen.tap(175, 341); await page.waitForTimeout(120); }
  saved = await read();
  check(saved.totalClicks === 6 && saved.oresMined === 1 && saved.coins === 140, "Touch input mines the first block in a 390 by 844 viewport");
  await shot("webgl-mobile");
  await page.setViewportSize({ width: 960, height: 540 }); await page.waitForTimeout(300); await shot("webgl-landscape");
  check(await page.evaluate(() => document.documentElement.scrollWidth === innerWidth && document.documentElement.scrollHeight === innerHeight), "Wide browser layout remains inside the viewport without page scrolling");

  check(errors.length === 0, "Built game has no unexpected browser or JavaScript errors: " + errors.join("; "));
  fs.writeFileSync(path.join(logs, "web-browser-check.json"), JSON.stringify({ result: "PASS", checks, browser: browserVersion, sdkEnvironment: "official dev proxy 0.0.2", profile, realDevice: false, lifecycleEvents: "dispatched blur/focus in headless Edge", rewardedVideo: "controlled video callbacks in isolated profile; no real ad inventory tested" }, null, 2));
  console.log(`LUCKY_PICKAXE_WEB_BROWSER: PASS (${checks} checks, Edge ${browserVersion})`);
}
run().catch(error => {
  fs.writeFileSync(path.join(logs, "web-browser-check.json"), JSON.stringify({ result: "FAIL", checks, error: error.message }, null, 2));
  console.error(error); process.exitCode = 1;
}).finally(async () => {
  fs.writeFileSync(path.join(logs, "web-browser-console.json"), JSON.stringify(consoleLogs, null, 2));
  if (context) await context.close();
});
