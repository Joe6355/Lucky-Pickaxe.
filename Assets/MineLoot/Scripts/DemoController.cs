using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MineLoot
{
    public sealed partial class DemoController : MonoBehaviour
    {
        private static readonly Color Back = Hex("514a3e"), Panel = Hex("c6cbc0"), Muted = Hex("535d4b"), Ink = Hex("29362c");
        private static readonly string[] OreNames = { "Уголь", "Железо", "Золото", "Алмаз", "Изумруд" };
        private static readonly string[] PickNames = { "Деревянная", "Каменная", "Железная", "Золотая", "Алмазная" };
        private static readonly string[] PickSprites = { "pick1", "pick-stone", "pick2", "pick-gold", "pick4" };
        private static readonly string[] CampNames = { "ЛАГЕРЬ I", "ШТОЛЬНЯ II", "РУДНИК III", "ПРИИСК IV", "АЛМАЗНЫЙ V" };
        private static readonly string[] CampFeatures = { "", "Вагонетка", "Крепления штольни", "Подъёмник", "Алмазные фонари" };
        private static readonly string[] ForgeFeatures = { "", "Печь и наковальня", "Дымоход и стойка", "Навес мастерской", "Оснащение мастера" };
        private static readonly string[] UpgradeNames = { "Кирка", "Кузница", "Перестановка", "Острота", "Пресс жетонов", "Сокровища", "Шахтёры", "Буры", "Вагонетки", "Рельсы", "Склад" };
        private readonly PixelArt art = new PixelArt();
        private readonly Image[] picks = new Image[5];
        private readonly Text[] powers = new Text[5];
        private readonly int[] shownDepths = new int[5];
        private readonly int[] shownDamage = new int[5];
        private readonly bool[] shownLost = new bool[5];
        private MineGame game;
        private System.Random random;
        private Canvas canvas;
        private RectTransform shell, rock;
        private GameObject minePage, rollPage, workshopPage, modal;
        private Text coinLabel, ticketLabel, mineStatus, rollStatus, progressLabel, upgradeLabel, powerLabel, stratumLabel, upgradeHint;
        private Button mineTab, rollTab, workshopTab, spinButton, upgradeButton;
        private Image ticketFill;
        private bool busy, roulette;
        private float rockPulse;
        private int lastWidth, lastHeight;
        private Rect lastSafe;
        private GameAudio sounds;
        private Image mineBackdrop, rollBackdrop, boardFill;
        private Image clickPick;
        private Text forgeLabel, contractLabel, soundLabel, veinLabel, floorHint;
        private Button forgeButton, contractButton;
        private bool dirty, holding, holdRepeated;
        private float nextHold, lastMineTime = -1, nextSave;
        private Text miningGain;
        private long gainCoins;
        private int gainExperience;
        private long displayedCoins = -1;
        private Vector2 pickHome;
        private float visualHealth = 1, visualExperience, visualTicket, upgradePulse, noticeTime;
        private int previousLevel, previousOre = -1;
        private int selectedPick = -1;
        private bool applicationPaused;
        private bool skipEconomyFrame;
        private float nextEconomyRefresh;
        private bool smokeClockRunning;
        private bool offlineNoticeQueued;
        private long sessionUtc;
        private double sessionRealtime;
        private string smokeSessionKey;
        // Session time advances from a monotonic clock; changing the wall clock while playing has no effect.
        private long SessionUtc => sessionUtc + (long)Math.Max(0, Time.realtimeSinceStartupAsDouble - sessionRealtime);
        public bool FastRoll { get; private set; }
        public MineGame Game => game;
        public bool IsBusy => busy;
        public bool AwaitingPickChoice => game.HasPendingRoll && RollPhase == "choice";
        public int SelectedPick => selectedPick;

        private void Awake()
        {
            Application.runInBackground = true;
            game = SmokeMode ? new MineGame() : ProgressStore.Load();
            sessionUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds(); sessionRealtime = Time.realtimeSinceStartupAsDouble;
            if (!SmokeMode)
            {
                game.PrepareOffline(SessionUtc);
                ProgressStore.Save(game); // Save the prepared crate and timestamp before exposing its claim button.
            }
            random = SmokeMode ? new System.Random(6355) : new System.Random();
            sounds = gameObject.AddComponent<GameAudio>();
            Application.targetFrameRate = 60;
            BuildInterface();
            RestorePresentation();
            if (SmokeMode) gameObject.AddComponent<DemoSmokeCheck>();
        }

        private void RestorePresentation()
        {
            busy = false; displayedCoins = -1; selectedPick = -1; RollPhase = "idle";
            if (modal != null) { Destroy(modal); modal = null; }
            milestoneBookOpen = offlineWindowOpen = false;
            offlineNoticeQueued = game.HasOfflineReward;
            productionTab = false;
            noticeTime = 0; noticeGroup.alpha = 0;
            mineStatus.text = "Нажимай или удерживай блок";
            Array.Copy(game.Progress.depths, shownDepths, 5);
            Array.Copy(game.Progress.damage, shownDamage, 5);
            Array.Copy(game.Progress.lostColumns, shownLost, 5);
            stage.SetDevelopment(game.PickTier, game.Progress.forgeLevel, false);
            DrawBoard(); Refresh(); ShowPage(game.HasPendingRoll);
            if (game.HasPendingRoll) EnterPickChoice();
        }

        internal void RestoreForSmoke(MineProgress progress)
        {
            if (!SmokeMode) return;
            game = new MineGame(progress); RestorePresentation();
        }

        private void Update()
        {
            if (lastWidth != Screen.width || lastHeight != Screen.height || lastSafe != Screen.safeArea) FitScreen();
            float dt = Time.unscaledDeltaTime;
            if ((!SmokeMode || smokeClockRunning) && !applicationPaused && !skipEconomyFrame) TickEconomy(dt, minePage.activeSelf && !busy && modal == null && Application.isFocused);
            skipEconomyFrame = false;
            if (offlineNoticeQueued && !busy && modal == null && !game.HasPendingRoll) ShowOfflineReward();
            rockPulse = Mathf.MoveTowards(rockPulse, 0, dt * 4.5f);
            upgradePulse = Mathf.MoveTowards(upgradePulse, 0, dt * 2);
            float strike = rockPulse * rockPulse, follow = Mathf.Sin((1 - rockPulse) * Mathf.PI) * rockPulse;
            clickPick.rectTransform.localEulerAngles = new Vector3(0, 0, -24 + strike * 58 + follow * 30);
            clickPick.rectTransform.anchoredPosition = pickHome + new Vector2(-16 * strike, -8 * strike);
            float celebrate = Mathf.Sin(upgradePulse * Mathf.PI);
            clickPick.rectTransform.localScale = Vector3.one * (1 + celebrate * .15f);
            workshopPick.rectTransform.localScale = Vector3.one * (1 + celebrate * .12f);
            visualHealth = Mathf.MoveTowards(visualHealth, 1 - game.Progress.oreDamage / (float)game.OreHealth, dt * 4);
            visualExperience = Mathf.MoveTowards(visualExperience, game.LevelGoal == 0 ? 1 : game.LevelExperience / (float)game.LevelGoal, dt * 2);
            visualTicket = Mathf.MoveTowards(visualTicket, game.Progress.hits / (float)game.HitsRequired, dt * 4);
            oreFill.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 178 * visualHealth);
            experienceFill.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 404 * visualExperience);
            ticketFill.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 360 * visualTicket);
            if (noticeTime > 0)
            {
                noticeTime = Mathf.Max(0, noticeTime - dt);
                float age = 2.7f - noticeTime;
                noticeGroup.alpha = Mathf.Min(Mathf.Clamp01(age / .12f), Mathf.Clamp01(noticeTime / .25f));
                noticeRect.anchoredPosition = new Vector2(24, -10 - 10 * (1 - Mathf.SmoothStep(0, 1, age / .2f)));
            }
            if (holding && Time.unscaledTime >= nextHold)
            {
                holdRepeated = true; nextHold = Time.unscaledTime + .16f; Mine();
            }
            if (dirty && Time.unscaledTime >= nextSave) Flush();
        }

        private void FitScreen()
        {
            lastWidth = Screen.width; lastHeight = Screen.height; lastSafe = Screen.safeArea;
            var safe = lastSafe.width > 0 ? lastSafe : new Rect(0, 0, Screen.width, Screen.height);
            canvas.scaleFactor = Mathf.Max(.1f, Mathf.Min(safe.width / 480f, safe.height / 960f));
            shell.anchoredPosition = (safe.center - new Vector2(Screen.width * .5f, Screen.height * .5f)) / canvas.scaleFactor;
        }

        public void Mine()
        {
            if (busy || Time.unscaledTime - lastMineTime < .075f || modal != null) return;
            lastMineTime = Time.unscaledTime;
            game.Mine(); var hit = game.LastHit; rockPulse = 1;
            mineStatus.text = hit.broken ? OreNames[hit.ore] + " добыт · +" + hit.experience + " опыта" : "Урон " + hit.damage + " · +" + hit.coins + " монет";
            if (hit.tickets > 0) mineStatus.text += " · +" + hit.tickets + " жетон";
            stage.Hit(hit.broken);
            sounds.Play(hit.levels > 0 ? "level" : hit.broken ? "break" : hit.ore >= 3 ? "crystal" : hit.ore > 0 ? "metal" : "hit");
            if (miningGain == null)
            {
                gainCoins = hit.coins; gainExperience = hit.experience;
                StartCoroutine(FloatReward(minePage.transform, 104, 186));
            }
            else { gainCoins += hit.coins; gainExperience += hit.experience; }
            if (hit.levels > 0) LevelNotice(game.MinerLevel - hit.levels, game.MinerLevel);
            else if (hit.veinFound) Notice("БОГАТАЯ ЖИЛА", "Нажми золотую кнопку рядом с рудой\nВременное усиление ручной добычи");
            if (game.Progress.hits == 0) visualTicket = 0;
            Persist(); if (hit.broken || hit.levels > 0) Flush(); Refresh();
        }

        private void TickEconomy(double seconds, bool miningActive, bool forceRefresh = false)
        {
            bool offer = game.VeinOffered, boost = game.VeinActive;
            long earned = game.AdvanceTime(seconds, miningActive);
            if (game.ProductionMilli > 0 || miningActive && (offer || boost)) Persist();
            bool expired = offer != game.VeinOffered || boost != game.VeinActive;
            if (expired) Flush();
            if (forceRefresh || expired || (earned > 0 || offer || boost) && Time.unscaledTime >= nextEconomyRefresh)
            {
                nextEconomyRefresh = Time.unscaledTime + .25f; Refresh();
            }
        }

        internal void AdvanceForSmoke(double seconds, bool miningActive)
        {
            if (SmokeMode) TickEconomy(seconds, miningActive, true);
        }
        internal void UseLiveClockForSmoke(bool enabled) { if (SmokeMode) smokeClockRunning = enabled; }
        internal void ReturnForSmoke(long utc)
        {
            if (SmokeMode) PrepareReturn(utc);
        }
        internal void ConfigureSessionForSmoke(long utc, string key)
        {
            if (!SmokeMode) return;
            sessionUtc = utc; sessionRealtime = Time.realtimeSinceStartupAsDouble; smokeSessionKey = key;
        }
        internal void AdvanceSessionForSmoke(int seconds) { if (SmokeMode) sessionUtc += seconds; }
        internal void PauseForSmoke(bool pause) { if (SmokeMode) OnApplicationPause(pause); }
        internal void SaveSessionForSmoke() { if (SmokeMode) Flush(true); }

        private void PrepareReturn(long utc)
        {
            game.PrepareOffline(utc); Persist(); Flush(true);
            if (offlineWindowOpen) CloseModal();
            offlineNoticeQueued = game.HasOfflineReward; Refresh();
        }

        public void ClaimOfflineReward()
        {
            if (busy || !offlineWindowOpen) return;
            long before = game.Progress.coins;
            if (!game.ClaimOffline()) return;
            long credit = game.Progress.coins - before;
            Persist(); Flush(); CloseModal(); sounds.Play("reward");
            Notice("СКЛАД РАЗГРУЖЕН", "+" + ShortAmount(credit) + " монет" + (game.HasOfflineReward ? "\nОстаток сохранён на складе" : ""));
        }

        public void ActivateRichVein()
        {
            if (busy || modal != null || !game.ActivateVein()) return;
            Persist(); Flush(); Refresh(); sounds.Play("crystal");
            Notice("БОГАТАЯ ЖИЛА · ×3", "Монеты от ручной добычи\n12 секунд на экране шахты");
        }

        public void Upgrade()
        {
            if (busy || modal != null) return;
            int tier = game.PickTier;
            if (!game.Upgrade()) return;
            mineStatus.text = "Кирка улучшена! +" + game.ClickPower + " за удар";
            upgradePulse = 1;
            if (game.PickTier != tier) Notice(CampNames[game.PickTier], CampFeatures[game.PickTier] + "\n" + PickNames[game.PickTier] + " кирка · урон " + game.MiningDamage);
            sounds.Play("upgrade");
            Persist(); Flush(); Refresh();
        }

        public void Forge()
        {
            int previousStage = stage.ForgeStage;
            if (busy || modal != null || !game.Forge()) return;
            sounds.Play("upgrade"); mineStatus.text = "Кузница: алмаз " + game.DiamondChance + "% · бонус силы +" + game.PowerBonus;
            Persist(); Flush(); Refresh();
            if (stage.ForgeStage > previousStage) Notice("КУЗНИЦА УЛУЧШЕНА", ForgeFeatures[stage.ForgeStage] + "\nАлмаз " + game.DiamondChance + "% · бонус силы +" + game.PowerBonus);
        }

        public void ClaimContract()
        {
            if (busy || modal != null) return;
            int reward = game.ContractReward;
            if (!game.ClaimContract()) return;
            sounds.Play("reward"); Notice("КОНТРАКТ ВЫПОЛНЕН", "+" + reward + " монет · +1 жетон"); Persist(); Flush(); Refresh();
        }

        public void ClaimMilestone(int index)
        {
            if (busy || !milestoneBookOpen || !game.ClaimMilestone(index)) return;
            var goal = MineGame.MilestoneAt(index);
            Persist(); Flush(); Refresh(); RefreshMilestoneBook();
            sounds.Play("reward"); Notice("ЦЕЛЬ ВЫПОЛНЕНА", goal.title + "\n+" + goal.coins + " монет · +" + goal.tickets + " жет.");
        }

        public void Purchase(UpgradeKind kind)
        {
            if (kind == UpgradeKind.Pickaxe) { Upgrade(); return; }
            if (kind == UpgradeKind.Forge) { Forge(); return; }
            if (busy || modal != null || !game.BuyUpgrade(kind)) return;
            sounds.Play("upgrade");
            Notice(UpgradeNames[(int)kind].ToUpperInvariant(), "Уровень " + game.UpgradeLevel(kind) + "\n" + UpgradeEffect(kind, false).Replace("\n", " · "));
            Persist(); Flush(); Refresh();
        }

        private void Notice(string title, string detail)
        {
            noticeTitle.text = title; noticeDetail.text = detail; noticeTime = 2.7f;
            noticeRect.SetAsLastSibling();
        }

        private void LevelNotice(int before, int after)
        {
            string unlock = ""; int coins = 0;
            for (int i = 1; i < OreNames.Length; i++) if (MineGame.OreUnlockLevel(i) > before && MineGame.OreUnlockLevel(i) <= after) unlock = "Открыто: " + OreNames[i] + "\n";
            for (int level = before + 1; level <= after; level++) coins += level * 40;
            Notice("ШАХТЁР · УРОВЕНЬ " + after, unlock + "+" + coins + " монет · +" + (after - before) + " жетон");
        }

        public void ToggleSpeed()
        {
            FastRoll = !FastRoll;
            foreach (var button in rollPage.GetComponentsInChildren<Button>())
                    if (button.name == "Toggle speed") button.GetComponentInChildren<Text>().text = "×" + (FastRoll ? "2" : "1");
        }

        public void ToggleSound() { sounds.Toggle(); Refresh(); }

        public void ShowPage(bool showRoulette)
        {
            SelectPage(showRoulette ? 1 : 0);
        }

        public void ShowWorkshop() => SelectPage(2);

        private void SelectPage(int page)
        {
            if (busy || modal != null) return;
            holding = false;
            roulette = page == 1;
            minePage.SetActive(page == 0); rollPage.SetActive(roulette); workshopPage.SetActive(page == 2);
            mineTab.image.color = page == 0 ? Hex("a4c975") : Panel;
            rollTab.image.color = roulette ? Hex("a4c975") : Panel;
            workshopTab.image.color = page == 2 ? Hex("a4c975") : Panel;
            mineTab.GetComponentInChildren<Text>().color = Ink;
            rollTab.GetComponentInChildren<Text>().color = Ink;
            mineNavMark.enabled = page == 0; rollNavMark.enabled = roulette; workshopNavMark.enabled = page == 2;
            stage.gameObject.SetActive(page == 0); shaft.gameObject.SetActive(roulette);
        }

        public void Spin()
        {
            if (AwaitingPickChoice) CommitPicks();
            else StartRoll(() => random.NextDouble());
        }

        internal void SpinForSmoke(Func<double> draw) { if (SmokeMode) StartRoll(draw); }

        private void StartRoll(Func<double> draw)
        {
            if (busy || modal != null) return;
            if (game.Progress.tickets == 0) { ShowPage(false); mineStatus.text = "До жетона: " + (game.HitsRequired - game.Progress.hits) + " ударов"; return; }
            if (!game.BeginRoll(draw)) return;
            busy = true;
            selectedPick = -1;
            // Commit the spent ticket and fixed picks before showing the reels.
            Persist(); Flush();
            Array.Copy(game.Progress.depths, shownDepths, 5);
            Array.Copy(game.Progress.damage, shownDamage, 5);
            Array.Copy(game.Progress.lostColumns, shownLost, 5);
            DrawBoard(); Refresh(); StartCoroutine(AnimateReels());
        }

        private void CommitPicks()
        {
            if (!AwaitingPickChoice || modal != null) return;
            long walletBefore = game.Progress.coins;
            var result = game.ResolveRoll();
            if (result == null) return;
            displayedCoins = walletBefore; selectedPick = -1;
            // The second transaction awards the resolved strike exactly once, before its animation.
            Persist(); Flush(); Refresh(); StartCoroutine(AnimateRoll(result));
        }

        private void EnterPickChoice()
        {
            busy = true; RollPhase = "choice"; SettledReels = 5; selectedPick = -1;
            ShowPendingPicks();
            if (game.Progress.pendingSwapLimit == 0) { CommitPicks(); return; }
            rollGain.text = "ОБМЕНЫ: " + game.SwapsRemaining + " / " + game.Progress.pendingSwapLimit;
            rollStatus.text = game.CanSwap ? "Нажми две разные кирки или копай сразу" : "Можно копать · новый жетон не нужен";
            rollGain.color = Hex("f5d88d"); rollRewardIcon.sprite = art.Get("pick4");
            Refresh();
        }

        private void ShowPendingPicks()
        {
            for (int c = 0; c < 5; c++)
            {
                bool active = game.IsActiveColumn(c);
                picks[c].transform.SetParent(reelWindows[c], false);
                picks[c].rectTransform.anchoredPosition = new Vector2(9, 0);
                picks[c].rectTransform.localEulerAngles = Vector3.zero;
                picks[c].rectTransform.localScale = Vector3.one;
                picks[c].sprite = art.Get(active ? "pick" + game.Progress.pendingTypes[c] : shownLost[c] ? "tile-bedrock" : "chest-open");
                picks[c].color = active ? Color.white : new Color(.55f, .55f, .55f);
                powers[c].text = active ? PowerText(game.PendingPower(c)) : shownLost[c] ? "ПОТЕРЯН" : "СУНДУК";
                powers[c].color = active ? game.Progress.pendingTypes[c] == 4 ? Hex("9ae5cb") : Hex("ead9ad") : Hex("aa8a80");
                reels[c].color = Color.white;
                reels[c].sprite = art.Get(active && game.Progress.pendingTypes[c] == 4 ? "ui-brass" : "ui-timber");
                reelSymbols[c, 0].enabled = reelSymbols[c, 2].enabled = false;
                picks[c].enabled = true;
            }
        }

        public void SelectPick(int column)
        {
            if (!AwaitingPickChoice || modal != null || !game.CanSwap || !game.IsActiveColumn(column)) return;
            if (selectedPick == column)
            {
                selectedPick = -1; rollStatus.text = "Нажми две разные кирки или копай сразу"; Refresh(); return;
            }
            if (selectedPick < 0)
            {
                selectedPick = column; rollStatus.text = "Кирка " + (column + 1) + " выбрана · выбери вторую";
                sounds.Play("tick"); Refresh(); return;
            }
            int first = selectedPick;
            if (!game.SwapPicks(first, column))
            {
                rollStatus.text = "Одинаковые кирки · выбери другую"; return;
            }
            selectedPick = -1; RollPhase = "exchange";
            // Save the chosen arrangement before moving its icons; reload cannot restore the exchange.
            Persist(); Flush(); Refresh(); StartCoroutine(AnimateExchange(first, column));
        }

        private IEnumerator AnimateExchange(int first, int second)
        {
            rollGain.text = "МЕНЯЕМ КИРКИ МЕСТАМИ";
            rollStatus.text = "Обмен применён · осталось: " + game.SwapsRemaining;
            var start = ReelHome(first); var end = ReelHome(second);
            picks[first].transform.SetParent(rollPage.transform, false);
            picks[second].transform.SetParent(rollPage.transform, false);
            pickBorders[first].enabled = pickBorders[second].enabled = true;
            float t = 0;
            while (t < 1)
            {
                t = Mathf.Min(1, t + Time.unscaledDeltaTime / (FastRoll ? .22f : .36f));
                float ease = t * t * (3 - 2 * t), arc = Mathf.Sin(t * Mathf.PI) * 24;
                picks[first].rectTransform.anchoredPosition = Vector2.Lerp(start, end, ease) + Vector2.up * arc;
                picks[second].rectTransform.anchoredPosition = Vector2.Lerp(end, start, ease) - Vector2.up * arc;
                picks[first].rectTransform.localEulerAngles = new Vector3(0, 0, -Mathf.Sin(t * Mathf.PI) * 15);
                picks[second].rectTransform.localEulerAngles = new Vector3(0, 0, Mathf.Sin(t * Mathf.PI) * 15);
                yield return null;
            }
            sounds.Play("reel-stop"); EnterPickChoice();
        }

        private static readonly int[] ReelTypes = { 1, 2, 4 };
        private static string PowerText(int power) => power + " " + (power == 1 ? "СИЛА" : power < 5 && power > 1 ? "СИЛЫ" : "СИЛ");
        public string RollPhase { get; private set; } = "idle";
        public int SettledReels { get; private set; }
        private static Vector2 ReelHome(int column) => new Vector2(54 + column * 81, -85);

        private Vector2 BoardPoint(int column, int row)
        {
            Vector2 p = shaft.BlockViewport(column, row);
            return new Vector2(24 + p.x * 432 - 13, -(70 + (1 - p.y) * ShaftWindowHeight) + 24);
        }

        private IEnumerator AnimateReels()
        {
            var types = game.Progress.pendingTypes;
            var powerValues = new int[5];
            for (int c = 0; c < 5; c++) powerValues[c] = game.PendingPower(c);
            float speed = FastRoll ? .5f : 1;
            RollPhase = "reels"; SettledReels = 0;
            Refresh();
            rollGain.text = "КАКИЕ КИРКИ ВЫПАДУТ?";
            rollStatus.text = "Дерево · железо · алмаз";
            rollGain.color = Hex("f5d88d"); rollRewardIcon.sprite = art.Get("coin");
            var stopped = new bool[5];
            for (int c = 0; c < 5; c++)
            {
                picks[c].transform.SetParent(reelWindows[c], false);
                picks[c].rectTransform.localEulerAngles = Vector3.zero;
                picks[c].rectTransform.localScale = Vector3.one;
                reels[c].sprite = art.Get("ui-timber"); reels[c].color = Color.white;
                bool active = powerValues[c] > 0;
                powers[c].text = active ? "· · ·" : shownLost[c] ? "ПОТЕРЯН" : "СУНДУК";
                powers[c].color = active ? Hex("c7c4ae") : Hex("aa8a80");
                picks[c].color = active ? Color.white : new Color(.55f, .55f, .55f);
                for (int i = 0; i < 3; i++) reelSymbols[c, i].enabled = active || i == 1;
                if (!active)
                {
                    stopped[c] = true; SettledReels++;
                    picks[c].sprite = art.Get(shownLost[c] ? "tile-bedrock" : "chest-open");
                    picks[c].rectTransform.anchoredPosition = new Vector2(9, 0);
                }
            }
            float elapsed = 0, lastTick = -1;
            var indices = new int[5];
            while (elapsed < 1.85f)
            {
                elapsed += Time.unscaledDeltaTime / speed;
                for (int c = 0; c < 5; c++)
                {
                    if (powerValues[c] == 0) continue;
                    float duration = .95f + c * .18f;
                    float t = Mathf.Clamp01(elapsed / duration);
                    int destination = 11 + c * 3;
                    float travel = destination * 52 * (1 - Mathf.Pow(1 - t, 3));
                    int index = Mathf.FloorToInt(travel / 52);
                    float offset = travel - index * 52;
                    if (index != indices[c] && elapsed - lastTick >= .045f && t < 1)
                    { sounds.Play("tick"); lastTick = elapsed; }
                    indices[c] = index;
                    for (int i = 0; i < 3; i++)
                    {
                        int symbol = index + i;
                        int type = symbol == destination + 1 ? types[c] : ReelTypes[(symbol + c) % 3];
                        reelSymbols[c, i].sprite = art.Get("pick" + type);
                        reelSymbols[c, i].rectTransform.anchoredPosition = new Vector2(9, -((i - 1) * 52 - offset));
                    }
                    if (t >= 1 && !stopped[c])
                    {
                        stopped[c] = true; SettledReels++;
                        sounds.Play(types[c] == 4 ? "rare" : "reel-stop");
                        reels[c].sprite = art.Get(types[c] == 4 ? "ui-brass" : "ui-timber");
                        powers[c].text = PowerText(powerValues[c]);
                        powers[c].color = types[c] == 4 ? Hex("9ae5cb") : Hex("ead9ad");
                    }
                    if (stopped[c])
                    {
                        float settle = Mathf.Clamp01((elapsed - duration) / .14f);
                        picks[c].rectTransform.localScale = Vector3.one * (1 + Mathf.Sin(settle * Mathf.PI) * .13f);
                    }
                }
                yield return null;
            }
            EnterPickChoice();
        }

        private IEnumerator AnimateRoll(RollResult result)
        {
            float speed = FastRoll ? .5f : 1;
            for (int c = 0; c < 5; c++)
            {
                reelSymbols[c, 0].enabled = reelSymbols[c, 2].enabled = false;
                if (result.powers[c] > 0) picks[c].sprite = art.Get("pick" + result.types[c]);
                picks[c].transform.SetParent(rollPage.transform, false);
                picks[c].rectTransform.anchoredPosition = ReelHome(c);
                picks[c].rectTransform.localScale = Vector3.one;
            }
            RollPhase = "mining";
            rollGain.text = "КИРКИ ЗА РАБОТУ!";
            rollStatus.text = "Удары разбивают породу до сундуков";
            var origins = new Vector2[5]; var targets = new Vector2[5];
            int maxPower = 0, earned = 0;
            foreach (int power in result.usedPower) maxPower = Mathf.Max(maxPower, power);
            long walletBefore = displayedCoins;
            for (int step = 0; step < maxPower; step++)
            {
                bool active = false;
                for (int c = 0; c < 5; c++)
                {
                    origins[c] = picks[c].rectTransform.anchoredPosition;
                    targets[c] = BoardPoint(c, Mathf.Min(4, shownDepths[c]));
                    if (step < result.usedPower[c] && shownDepths[c] < 5 && !shownLost[c]) active = true;
                }
                if (!active) continue;
                float phase = 0;
                while (phase < 1)
                {
                    phase = Mathf.Min(1, phase + Time.unscaledDeltaTime / (.13f * speed));
                    float ease = phase * phase;
                    for (int c = 0; c < 5; c++) if (step < result.usedPower[c] && shownDepths[c] < 5 && !shownLost[c])
                    {
                        picks[c].rectTransform.anchoredPosition = Vector2.Lerp(origins[c], targets[c], ease) + Vector2.up * (Mathf.Sin(phase * Mathf.PI) * 10);
                        picks[c].rectTransform.localEulerAngles = new Vector3(0, 0, Mathf.Lerp(18, -40, ease));
                        picks[c].rectTransform.localScale = Vector3.one * 1.12f;
                    }
                    yield return null;
                }
                bool chest = false, failed = false; BlockKind obstacle = BlockKind.Ore;
                for (int c = 0; c < 5; c++)
                {
                    if (step >= result.usedPower[c] || shownDepths[c] >= 5 || shownLost[c]) continue;
                    int row = shownDepths[c];
                    bool snapped = result.blockedBy[c] != BlockKind.Ore && step == result.usedPower[c] - 1;
                    if (snapped)
                    {
                        failed = true; obstacle = result.blockedBy[c];
                        shaft.HitBlock(c, row, false); shaft.ShatterPick(c, row);
                        shownLost[c] = obstacle == BlockKind.Bedrock;
                        picks[c].sprite = art.Get("pick-broken");
                        reels[c].sprite = art.Get("ui-timber"); reels[c].color = Hex("e7aaa0");
                        powers[c].text = obstacle == BlockKind.Bedrock ? "БЕДРОК" : "СЛОМАНА";
                        powers[c].color = Hex("ffb3a6");
                        StartCoroutine(FlyPickHead(c, result.types[c], picks[c].rectTransform.anchoredPosition));
                    }
                    else
                    {
                        bool broken = ++shownDamage[c] >= game.BlockHardness(c, row);
                        shaft.HitBlock(c, row, broken);
                        if (broken)
                        {
                            shownDamage[c] = 0; shownDepths[c]++; earned += game.BlockReward;
                            if (shownDepths[c] == 5) { earned += game.ChestReward; chest = true; }
                        }
                        powers[c].text = PowerText(result.powers[c] - step - 1);
                    }
                }
                DrawBoard(); sounds.Play(failed ? "shatter" : chest ? "chest" : "break");
                rollGain.text = failed ? "КИРКА СЛОМАЛАСЬ!" : earned > 0 ? "+ " + ShortAmount(earned) + " МОНЕТ" : "ПОРОДА ТРЕСКАЕТСЯ";
                rollStatus.text = failed ? obstacle == BlockKind.Bedrock ? "Бедрок · сундук этого прохода потерян" : "Обсидиан · нужна алмазная кирка" : chest ? "Сундук открыт! Сокровища твои" : "Ещё один удар — ещё глубже";
                coinLabel.text = Math.Min(MineGame.CoinLimit, walletBefore + earned).ToString("N0");
                phase = 0;
                while (phase < 1)
                {
                    phase = Mathf.Min(1, phase + Time.unscaledDeltaTime / (.12f * speed));
                    for (int c = 0; c < 5; c++) if (step < result.usedPower[c] && origins[c] != targets[c])
                    {
                        picks[c].rectTransform.localEulerAngles = new Vector3(0, 0, Mathf.Lerp(-40, -12, phase));
                    }
                    yield return null;
                }
            }
            Array.Copy(result.after, shownDepths, 5); Array.Copy(result.damageAfter, shownDamage, 5); Array.Copy(result.lostAfter, shownLost, 5); DrawBoard();
            for (int c = 0; c < 5; c++) origins[c] = picks[c].rectTransform.anchoredPosition;
            float returning = 0;
            while (returning < 1)
            {
                returning = Mathf.Min(1, returning + Time.unscaledDeltaTime / (.19f * speed));
                float ease = 1 - Mathf.Pow(1 - returning, 3);
                for (int c = 0; c < 5; c++)
                {
                    picks[c].rectTransform.anchoredPosition = Vector2.Lerp(origins[c], ReelHome(c), ease);
                    picks[c].rectTransform.localEulerAngles = new Vector3(0, 0, -12 * (1 - ease));
                    picks[c].rectTransform.localScale = Vector3.one * Mathf.Lerp(1.12f, 1, ease);
                }
                yield return null;
            }
            for (int c = 0; c < 5; c++)
            {
                picks[c].transform.SetParent(reelWindows[c], false);
                picks[c].rectTransform.anchoredPosition = new Vector2(9, 0);
                powers[c].text = result.blockedBy[c] == BlockKind.Bedrock ? "БЕДРОК" : result.blockedBy[c] == BlockKind.Obsidian ? "СЛОМАНА" : result.powers[c] == 0 ? result.lostBefore[c] ? "ПОТЕРЯН" : "СУНДУК" : PowerText(result.powers[c]);
            }
            RollPhase = "reward";
            float count = 0;
            while (count < 1)
            {
                count = Mathf.Min(1, count + Time.unscaledDeltaTime / (.32f * speed));
                float ease = 1 - Mathf.Pow(1 - count, 3);
                displayedCoins = walletBefore + (long)Math.Round((game.Progress.coins - walletBefore) * ease);
                coinLabel.text = displayedCoins.ToString("N0");
                rollGain.text = "+ " + ShortAmount(earned + (long)Math.Round((result.reward - earned) * ease)) + " МОНЕТ";
                yield return null;
            }
            bool miss = result.reward == 0 && result.brokenPicks > 0;
            rollGain.text = miss ? "НЕУДАЧА · 0 МОНЕТ" : result.reward == 0 ? "БЕЗ ДОБЫЧИ" : "+ " + ShortAmount(result.reward) + " МОНЕТ";
            rollGain.color = miss ? Hex("f18b7a") : Hex("f5d88d");
            rollRewardIcon.sprite = art.Get(miss ? "pick-broken" : "coin");
            rollStatus.text = "Сундуки: +" + result.chests + " · потеряно: " + result.lostChests + " · сломано: " + result.brokenPicks;
            sounds.Play(miss ? "shatter" : result.chests > 0 || result.perfect ? "reward" : "hit");
            if (result.levelAfter > result.levelBefore) { sounds.Play("level"); LevelNotice(result.levelBefore, result.levelAfter); }
            else if (result.completed) Notice(result.perfect ? "ПЛАСТ ПРОЙДЕН!" : "ВЫЛАЗКА ЗАВЕРШЕНА", result.perfect ? "+" + result.reward + " монет · +1 жетон" : "Часть сундуков потеряна\nБонус пласта не получен");
            displayedCoins = -1; busy = false; RollPhase = "result"; Refresh();
        }

        private IEnumerator FlyPickHead(int column, int type, Vector2 origin)
        {
            var image = Icon(rollPage.transform, "pick-head" + type, origin.x, -origin.y, 48, 48);
            float elapsed = 0;
            while (elapsed < .55f)
            {
                elapsed += Time.unscaledDeltaTime;
                image.rectTransform.anchoredPosition = origin + new Vector2(column % 2 == 0 ? -65 : 65, 135) * elapsed + Vector2.down * (340 * elapsed * elapsed);
                image.rectTransform.localEulerAngles = new Vector3(0, 0, -40 + elapsed * 300);
                image.color = new Color(1, 1, 1, 1 - elapsed / .55f); yield return null;
            }
            Destroy(image.gameObject);
        }

        public void AbandonFloor()
        {
            if (busy || modal != null || !game.Abandon(() => random.NextDouble())) return;
            Array.Copy(game.Progress.depths, shownDepths, 5); Array.Copy(game.Progress.damage, shownDamage, 5); Array.Copy(game.Progress.lostColumns, shownLost, 5);
            for (int c = 0; c < 5; c++) { picks[c].sprite = art.Get("pick1"); picks[c].color = Color.white; powers[c].text = PowerText(1 + game.PowerBonus); powers[c].color = Hex("ead9ad"); }
            rollGain.text = "НОВАЯ РАСКЛАДКА"; rollGain.color = Hex("f5d88d"); rollRewardIcon.sprite = art.Get("coin");
            rollStatus.text = "-1 жетон · незабранные сундуки потеряны";
            DrawBoard(); Persist(); Flush(); Refresh();
        }

        private void DrawBoard()
        {
            int opened = 0, lost = 0, progress = 0;
            for (int c = 0; c < 5; c++)
            {
                if (shownDepths[c] == 5) opened++;
                if (shownLost[c]) lost++;
                progress += shownLost[c] ? 5 : shownDepths[c];
            }
            shaft.SetBoard(game, shownDepths, shownDamage, shownLost);
            boardText.text = "Вылазка " + (progress * 4) + "% · сундуки " + opened + "/5" + (lost > 0 ? " · потеряно " + lost : "");
            boardFill.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, progress / 25f * 398);
        }

        private void Refresh()
        {
            coinLabel.text = (displayedCoins >= 0 ? displayedCoins : game.Progress.coins).ToString("N0");
            // Long values reduce their font size inside their existing bounds.
            coinLabel.resizeTextForBestFit = true; coinLabel.resizeTextMinSize = 16; coinLabel.resizeTextMaxSize = 28;
            ticketLabel.text = game.Progress.tickets.ToString("N0");
            ticketLabel.resizeTextForBestFit = true; ticketLabel.resizeTextMinSize = 16; ticketLabel.resizeTextMaxSize = 28;
            powerLabel.text = "+" + game.ManualClickPower + " за удар";
            richVeinButton.gameObject.SetActive(game.VeinOffered);
            richVeinTimer.text = game.VeinActive ? "×3 · " + (int)Math.Ceiling(game.Progress.veinBoostLeft) + " сек." : game.VeinOffered ? (int)Math.Ceiling(game.Progress.veinOfferLeft) + " сек. на сбор" : "";
            progressLabel.text = game.Progress.hits + " / " + game.HitsRequired;
            levelLabel.text = "ШАХТЁР · УР. " + game.MinerLevel;
            stageBadge.text = game.LevelGoal == 0 ? "МАКС. УРОВЕНЬ" : game.LevelExperience + "/" + game.LevelGoal + " ОПЫТА";
            if (previousLevel != game.MinerLevel) { previousLevel = game.MinerLevel; visualExperience = 0; }
            if (previousOre != game.Progress.oreKind || game.LastHit.broken) { previousOre = game.Progress.oreKind; visualHealth = 1; }
            veinLabel.text = OreNames[game.Progress.oreKind] + " · " + (game.OreHealth - game.Progress.oreDamage) + "/" + game.OreHealth;
            oreFill.color = game.Progress.oreKind >= 3 ? Hex("80e3bb") : Hex("e1ce82");
            for (int i = 0; i < 5; i++)
            {
                bool unlocked = i < game.UnlockedOres;
                oreIcons[i].color = unlocked ? Color.white : new Color(.35f, .4f, .38f, .7f);
                oreSlots[i].color = i == game.Progress.oreKind ? Hex("c9dd9c") : unlocked ? Panel : Hex("858e83");
                oreNames[i].text = unlocked ? OreNames[i] : "Ур. " + MineGame.OreUnlockLevel(i);
                oreNames[i].color = unlocked ? Ink : Hex("e7e8d7");
            }
            upgradeLabel.text = game.Progress.upgradeLevel >= MineGame.MaxUpgrade ? "Максимум" : game.UpgradeCost.ToString("N0") + " монет";
            upgradeLabel.resizeTextForBestFit = true; upgradeLabel.resizeTextMinSize = 16; upgradeLabel.resizeTextMaxSize = 20;
            pickTitle.text = PickNames[game.PickTier];
            pickDetail.text = "+4/удар · урон " + game.MiningDamage;
            nextUpgrade.text = game.PickTier == 4 ? "Шахта развита · урон " + game.MiningDamage + " · реликвий " + game.Relics : "Далее: " + CampFeatures[game.PickTier + 1] + " · ещё " + (game.NextPickUpgrade - game.Progress.upgradeLevel) + " улучш.";
            campLabel.text = CampNames[game.PickTier];
            for (int i = 0; i < campSteps.Length; i++) campSteps[i].color = i <= game.PickTier ? Hex("d4ed96") : Hex("455742");
            forgeTitle.text = "Кузница " + game.Progress.forgeLevel;
            upgradeHint.text = game.ProductionMilli > 0 ? "+" + Rate(game.ProductionMilli) + " монет/с" : "Реликвий: " + game.Relics;
            upgradeButton.interactable = !busy && game.CanUpgrade;
            forgeButton.interactable = !busy && game.CanForge;
            forgeLabel.text = game.Progress.forgeLevel == MineGame.MaxForge ? "Максимум" : game.ForgeCost.ToString("N0") + " монет";
            forgeDetail.text = "Алмаз " + game.DiamondChance + "% · +" + game.PowerBonus;
            contractLabel.text = game.CanClaim ? "Забрать" : "В работе";
            contractDetail.text = ShortAmount(System.Math.Min(game.Progress.totalBlocks, game.ContractTarget)) + "/" + ShortAmount(game.ContractTarget) + " · +" + ShortAmount(game.ContractReward);
            contractFill.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 240 * Mathf.Clamp01((game.Progress.totalBlocks - game.Progress.claimedContracts * 25) / 25f));
            contractButton.interactable = !busy && game.CanClaim;
            contractButton.image.color = game.CanClaim ? Hex("b3d17a") : Hex("a9ada3");
            contractLabel.color = game.CanClaim ? Ink : Muted;
            upgradeButton.image.color = game.CanUpgrade ? Hex("b2d27b") : Hex("a9ada3");
            upgradeLabel.color = game.CanUpgrade ? Ink : Muted;
            forgeButton.image.color = game.CanForge ? Hex("e7cf89") : Hex("a9ada3");
            forgeLabel.color = game.CanForge ? Ink : Muted;
            soundLabel.text = sounds.Muted ? "♪×" : "♪";
            spinButton.interactable = !busy || AwaitingPickChoice;
            abandonButton.interactable = !busy && game.CanAbandon;
            string action = AwaitingPickChoice ? "КОПАТЬ · БЕЗ ДОПЛАТЫ" :
                busy ? RollPhase == "reels" ? "ВЫПАДАЮТ КИРКИ…" : RollPhase == "exchange" ? "ОБМЕН КИРОК…" : "КИРКИ В РАБОТЕ…" :
                game.Progress.tickets == 0 ? "В ШАХТУ ЗА ЖЕТОНАМИ" : game.IsComplete ? "НОВЫЙ ПЛАСТ · 1 ЖЕТОН" : "КИРКИ · 1 ЖЕТОН";
            spinButton.GetComponentInChildren<Text>().text = action;
            for (int c = 0; c < 5; c++)
            {
                reelButtons[c].interactable = AwaitingPickChoice && game.CanSwap && game.IsActiveColumn(c);
                pickBorders[c].enabled = AwaitingPickChoice && selectedPick == c;
                if (AwaitingPickChoice) picks[c].rectTransform.localScale = Vector3.one * (selectedPick == c ? 1.1f : 1);
            }
            mineTab.interactable = rollTab.interactable = workshopTab.interactable = !busy;
            milestoneButton.interactable = !busy;
            milestoneButtonLabel.text = game.ReadyMilestones > 0 ? "ЦЕЛИ " + game.ReadyMilestones : "ЦЕЛИ";
            milestoneButton.image.color = game.ReadyMilestones > 0 ? Hex("c8d981") : Hex("a8b78e");
            RefreshWorkshop();
            RefreshOfflineWindow();
            if (!busy && RollPhase == "idle")
            {
                rollGain.text = game.Progress.swapLevel == 0 ? "ОБМЕН В ПРОКАЧКЕ" : "ПЕРЕСТАНОВКА · УР. " + game.Progress.swapLevel;
                rollStatus.text = game.Progress.swapLevel == 0 ? "Открой в мастерской с уровня 3" : "До " + game.Progress.swapLevel + " обменов кирок перед ударом";
            }
            string biome = new[] { "Мшистые штольни", "Кристальный грот", "Угольные недра" }[game.Biome];
            stratumLabel.text = "Пласт " + game.Progress.stratum.ToString("00") + " · " + biome;
            floorHint.text = "Прочность " + game.Hardness;
            stage.SetBiome(game.Biome);
            stage.SetDevelopment(game.PickTier, game.Progress.forgeLevel);
            stage.SetProduction(game.Progress.workerCount, game.Progress.drillCount, game.Progress.cartCount, game.VeinOffered || game.VeinActive);
            stage.SetOre(game.OreKey, game.Progress.oreDamage / (float)game.OreHealth);
            clickPick.sprite = workshopPick.sprite = art.Get(PickSprites[game.PickTier]);
        }

        public static bool SmokeMode => Array.IndexOf(Environment.GetCommandLineArgs(), "-demoSmokeTest") >= 0;
        private void Persist() { if (!dirty) nextSave = Time.unscaledTime + 3; dirty = true; }
        private void Flush(bool force = false)
        {
            if (game != null && (!SmokeMode || smokeSessionKey != null) && (dirty || force))
            {
                if (!applicationPaused) game.RecordSession(SessionUtc);
                ProgressStore.Save(game, SmokeMode ? smokeSessionKey : ProgressStore.Key);
            }
            dirty = false;
        }
        private void OnApplicationPause(bool pause)
        {
            bool wasPaused = applicationPaused;
            if (pause) { holding = false; Flush(true); }
            applicationPaused = pause; skipEconomyFrame = true;
            if (!pause && wasPaused && (!SmokeMode || smokeSessionKey != null))
            {
                PrepareReturn(SessionUtc);
            }
        }
        private void OnApplicationFocus(bool focus) { if (!focus) { holding = false; Flush(true); } }
        private void OnApplicationQuit() { Flush(true); }
        private void OnDestroy() => art.Dispose();
    }
}
