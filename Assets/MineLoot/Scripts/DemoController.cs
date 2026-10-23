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
        private static readonly string[][] Layers = { new[] { "grass", "stone", "iron", "gold", "diamond" }, new[] { "stone", "iron", "diamond", "stone", "diamond" }, new[] { "stone", "ember", "iron", "ember", "gold" } };
        private static readonly string[] OreNames = { "Уголь", "Железо", "Золото", "Алмаз", "Изумруд" };
        private static readonly string[] PickNames = { "Деревянная", "Каменная", "Железная", "Золотая", "Алмазная" };
        private static readonly string[] PickSprites = { "pick1", "pick-stone", "pick2", "pick-gold", "pick4" };
        private readonly PixelArt art = new PixelArt();
        private readonly Image[,] blocks = new Image[5, 5];
        private readonly Image[] cracks = new Image[5];
        private readonly Image[] picks = new Image[5], chests = new Image[5];
        private readonly Text[] powers = new Text[5];
        private readonly int[] shownDepths = new int[5];
        private readonly int[] shownDamage = new int[5];
        private MineGame game;
        private System.Random random;
        private Canvas canvas;
        private RectTransform shell, rock;
        private GameObject minePage, rollPage, modal;
        private Text coinLabel, ticketLabel, mineStatus, rollStatus, progressLabel, upgradeLabel, powerLabel, stratumLabel, upgradeHint;
        private Button mineTab, rollTab, spinButton, upgradeButton;
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
        public bool FastRoll { get; private set; }
        public MineGame Game => game;
        public bool IsBusy => busy;

        private void Awake()
        {
            if (SmokeMode) Application.runInBackground = true;
            game = SmokeMode ? new MineGame() : ProgressStore.Load();
            random = new System.Random();
            sounds = gameObject.AddComponent<GameAudio>();
            Application.targetFrameRate = 60;
            BuildInterface();
            Array.Copy(game.Progress.depths, shownDepths, 5);
            Array.Copy(game.Progress.damage, shownDamage, 5);
            DrawBoard(); Refresh(); ShowPage(false);
            if (SmokeMode) gameObject.AddComponent<DemoSmokeCheck>();
        }

        private void Update()
        {
            if (lastWidth != Screen.width || lastHeight != Screen.height || lastSafe != Screen.safeArea) FitScreen();
            float dt = Time.unscaledDeltaTime;
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
            visualTicket = Mathf.MoveTowards(visualTicket, game.Progress.hits / (float)MineGame.HitsPerTicket, dt * 4);
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
            if (game.Progress.hits == 0) visualTicket = 0;
            Persist(); if (hit.broken || hit.levels > 0) Flush(); Refresh();
        }

        public void Upgrade()
        {
            if (busy) return;
            int tier = game.PickTier;
            if (!game.Upgrade()) return;
            mineStatus.text = "Кирка улучшена! +" + game.ClickPower + " за удар";
            upgradePulse = 1;
            if (game.PickTier != tier) Notice(PickNames[game.PickTier].ToUpperInvariant() + " КИРКА", "+4 монеты за удар\nУрон по блоку: " + game.MiningDamage);
            sounds.Play("upgrade");
            Persist(); Refresh();
        }

        public void Forge()
        {
            if (busy || !game.Forge()) return;
            sounds.Play("upgrade"); mineStatus.text = "Кузница: алмаз " + game.DiamondChance + "% · бонус силы +" + game.PowerBonus;
            Persist(); Refresh();
        }

        public void ClaimContract()
        {
            if (busy) return;
            int reward = game.ContractReward;
            if (!game.ClaimContract()) return;
            sounds.Play("reward"); Notice("КОНТРАКТ ВЫПОЛНЕН", "+" + reward + " монет · +1 жетон"); Persist(); Flush(); Refresh();
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
                    if (button.name == "Toggle speed") button.GetComponentInChildren<Text>().text = "Скорость ×" + (FastRoll ? "2" : "1");
        }

        public void ToggleSound() { sounds.Toggle(); Refresh(); }

        public void ShowPage(bool showRoulette)
        {
            if (busy) return;
            holding = false;
            roulette = showRoulette; minePage.SetActive(!roulette); rollPage.SetActive(roulette);
            mineTab.image.color = roulette ? Panel : Hex("a4c975");
            rollTab.image.color = roulette ? Hex("a4c975") : Panel;
            mineTab.GetComponentInChildren<Text>().color = Ink;
            rollTab.GetComponentInChildren<Text>().color = Ink;
            mineNavMark.enabled = !roulette; rollNavMark.enabled = roulette;
            stage.gameObject.SetActive(!roulette);
        }

        public void Spin()
        {
            if (busy) return;
            foreach (var pick in picks) pick.transform.SetAsLastSibling();
            if (game.Progress.tickets == 0) { ShowPage(false); mineStatus.text = "До жетона: " + (MineGame.HitsPerTicket - game.Progress.hits) + " ударов"; return; }
            var result = game.Roll(() => random.NextDouble());
            if (result == null) return;
            displayedCoins = Math.Max(0, game.Progress.coins - result.reward);
            busy = true;
            // Commit all rewards before the coroutine; UI only presents the completed transaction.
            Persist(); Flush();
            Array.Copy(result.before, shownDepths, 5);
            Array.Copy(result.damageBefore, shownDamage, 5);
            DrawBoard(); Refresh(); StartCoroutine(AnimateRoll(result));
        }

        private IEnumerator AnimateRoll(RollResult result)
        {
            rollStatus.text = "Какие кирки пойдут в шахту?";
            float speed = FastRoll ? .5f : 1;
            for (int frame = 0; frame < 16; frame++)
            {
                for (int c = 0; c < 5; c++)
                {
                    int stop = 6 + c * 2;
                    if (frame < stop)
                    {
                        int type = new[] { 1, 2, 4 }[(frame + c) % 3];
                        picks[c].sprite = art.Get("pick" + type);
                        picks[c].rectTransform.localEulerAngles = new Vector3(0, 0, frame % 2 == 0 ? -10 : 10);
                        picks[c].rectTransform.anchoredPosition = new Vector2(48 + c * 81, -83 + (frame % 2 == 0 ? 5 : -5));
                        powers[c].text = "· · ·"; powers[c].color = Muted;
                        reels[c].color = Panel;
                    }
                    else
                    {
                        picks[c].sprite = art.Get("pick" + result.types[c]);
                        picks[c].rectTransform.localEulerAngles = Vector3.zero;
                        picks[c].rectTransform.anchoredPosition = new Vector2(48 + c * 81, -83);
                        picks[c].rectTransform.localScale = Vector3.one * (frame == stop ? 1.18f : 1);
                        powers[c].text = result.powers[c] + " силы";
                        powers[c].color = result.types[c] == 4 ? Hex("1b6658") : Ink;
                        reels[c].color = result.types[c] == 4 ? Hex("addbc0") : result.types[c] == 2 ? Hex("d9e0d5") : Hex("d1c6a8");
                        if (frame == stop) sounds.Play("tick");
                    }
                }
                yield return new WaitForSecondsRealtime((.04f + frame * .003f) * speed);
            }
            int earned = 0;
            var origins = new Vector2[5]; var targets = new Vector2[5];
            int maxPower = 0; foreach (int power in result.powers) maxPower = Math.Max(maxPower, power);
            for (int step = 0; step < maxPower; step++)
            {
                bool changes = false;
                for (int c = 0; c < 5; c++)
                {
                    origins[c] = picks[c].rectTransform.anchoredPosition;
                    targets[c] = new Vector2(48 + c * 81, -(136 + shownDepths[c] * 44));
                    if (step < result.powers[c] && shownDepths[c] < 5) changes = true;
                }
                if (!changes) continue;
                float approach = 0;
                while (approach < 1)
                {
                    approach = Mathf.Min(1, approach + Time.unscaledDeltaTime / (.11f * speed));
                    float ease = approach * approach;
                    for (int c = 0; c < 5; c++) if (step < result.powers[c] && shownDepths[c] < 5)
                    {
                        picks[c].rectTransform.anchoredPosition = Vector2.Lerp(origins[c], targets[c], ease);
                        picks[c].rectTransform.localEulerAngles = new Vector3(0, 0, Mathf.Lerp(16, -35, ease));
                    }
                    yield return null;
                }
                for (int c = 0; c < 5; c++)
                {
                    if (step >= result.powers[c] || shownDepths[c] >= 5) continue;
                    shownDamage[c]++; changes = true;
                    int row = shownDepths[c];
                    if (shownDamage[c] >= result.hardness)
                    {
                        shownDamage[c] = 0; shownDepths[c]++; earned += game.BlockReward;
                        if (shownDepths[c] == 5) earned += game.ChestReward;
                        SpawnDebris(c, row);
                        if (shownDepths[c] == 5) chests[c].rectTransform.localScale = Vector3.one * 1.2f;
                    }
                    picks[c].rectTransform.anchoredPosition = new Vector2(48 + c * 81, -(136 + row * 44));
                    picks[c].rectTransform.localEulerAngles = new Vector3(0, 0, -24);
                }
                sounds.Play("break"); DrawBoard(); rollStatus.text = "Добываем… +" + earned + " монет";
                coinLabel.text = Math.Min(MineGame.CoinLimit, displayedCoins + earned).ToString("N0");
                yield return new WaitForSecondsRealtime(.08f * speed);
                for (int c = 0; c < 5; c++) { picks[c].rectTransform.localEulerAngles = Vector3.zero; chests[c].rectTransform.localScale = Vector3.one; }
                yield return new WaitForSecondsRealtime(.03f * speed);
            }
            for (int c = 0; c < 5; c++) picks[c].rectTransform.anchoredPosition = new Vector2(48 + c * 81, -83);
            Array.Copy(result.after, shownDepths, 5); Array.Copy(result.damageAfter, shownDamage, 5); DrawBoard();
            rollStatus.text = result.completed ? "Пласт пройден! +" + result.reward + " монет / +1 жетон" : result.reward == 0 ? "Урон сохранён · продолжай добычу" : "+" + result.reward + " монет" + (result.scrap > 0 ? " · включая лом +" + result.scrap : result.chests > 0 ? " · сундуков: " + result.chests : " · копаем дальше");
            sounds.Play(result.chests > 0 || result.completed ? "reward" : "hit");
            if (result.levelAfter > result.levelBefore) { sounds.Play("level"); LevelNotice(result.levelBefore, result.levelAfter); }
            else if (result.completed) Notice("ПЛАСТ ПРОЙДЕН!", "+" + result.reward + " монет · +1 жетон\nСледующий пласт готов к добыче");
            displayedCoins = -1; busy = false; Refresh();
        }

        private void SpawnDebris(int column, int row)
        {
            var origin = new Vector2(78 + column * 81, -(185 + row * 44));
            for (int i = 0; i < 4; i++)
            {
                var chip = PanelAt(rollPage.transform, "Rock chip", row > 2 ? Hex("e6c977") : Hex("a4afb1"), origin.x, -origin.y, 7, 7);
                chip.raycastTarget = false;
                StartCoroutine(FlyChip(chip.rectTransform, origin, new Vector2((i - 1.5f) * 80, 85 + i * 12)));
            }
        }

        private IEnumerator FlyChip(RectTransform chip, Vector2 origin, Vector2 velocity)
        {
            float elapsed = 0;
            while (elapsed < .5f)
            {
                elapsed += Time.unscaledDeltaTime;
                chip.anchoredPosition = origin + velocity * elapsed + Vector2.down * (500 * elapsed * elapsed);
                chip.localEulerAngles = new Vector3(0, 0, elapsed * 240);
                yield return null;
            }
            Destroy(chip.gameObject);
        }

        private void DrawBoard()
        {
            int cleared = 0;
            for (int c = 0; c < 5; c++)
            {
                for (int r = 0; r < 5; r++)
                {
                    blocks[c, r].sprite = art.Get("tile-" + Layers[game.Biome][r]);
                    blocks[c, r].color = r < shownDepths[c] ? new Color(1, 1, 1, .08f) : Color.white;
                }
                chests[c].sprite = art.Get(shownDepths[c] == 5 ? "chest-open" : "chest");
                cracks[c].enabled = shownDepths[c] < 5 && shownDamage[c] > 0;
                cracks[c].rectTransform.anchoredPosition = new Vector2(41 + c * 81, -(165 + shownDepths[c] * 44));
                cleared += shownDepths[c];
            }
            boardFill.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, cleared / 25f * 398);
        }

        private void Refresh()
        {
            coinLabel.text = (displayedCoins >= 0 ? displayedCoins : game.Progress.coins).ToString("N0");
            // Long values reduce their font size inside their existing bounds.
            coinLabel.resizeTextForBestFit = true; coinLabel.resizeTextMinSize = 16; coinLabel.resizeTextMaxSize = 28;
            ticketLabel.text = game.Progress.tickets.ToString("N0");
            ticketLabel.resizeTextForBestFit = true; ticketLabel.resizeTextMinSize = 16; ticketLabel.resizeTextMaxSize = 28;
            powerLabel.text = "+" + game.ClickPower + " за удар";
            progressLabel.text = game.Progress.hits + " / " + MineGame.HitsPerTicket;
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
            nextUpgrade.text = game.PickTier == 4 ? "Алмазная кирка · ур. " + (game.Progress.upgradeLevel + 1) + " · реликвий " + game.Relics : "След.: " + PickNames[game.PickTier + 1].ToLowerInvariant() + " кирка · ещё " + (game.NextPickUpgrade - game.Progress.upgradeLevel) + " улучш.";
            upgradeHint.text = "Реликвий: " + game.Relics;
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
            spinButton.interactable = !busy;
            spinButton.GetComponentInChildren<Text>().text = busy ? "Кирки в работе…" : game.Progress.tickets == 0 ? "Добыть новые жетоны →" : game.IsComplete ? "Открыть новый пласт · 1 жетон" : "Отправить кирки · 1 жетон";
            mineTab.interactable = rollTab.interactable = !busy;
            string biome = new[] { "Мшистые штольни", "Кристальный грот", "Угольные недра" }[game.Biome];
            stratumLabel.text = "Пласт " + game.Progress.stratum.ToString("00") + " · " + biome;
            floorHint.text = "Прочность " + game.Hardness;
            stage.SetBiome(game.Biome);
            stage.SetOre(game.OreKey, game.Progress.oreDamage / (float)game.OreHealth);
            clickPick.sprite = workshopPick.sprite = art.Get(PickSprites[game.PickTier]);
        }

        public static bool SmokeMode => Array.IndexOf(Environment.GetCommandLineArgs(), "-demoSmokeTest") >= 0;
        private void Persist() { if (!dirty) nextSave = Time.unscaledTime + 3; dirty = true; }
        private void Flush() { if (game != null && !SmokeMode && dirty) ProgressStore.Save(game); dirty = false; }
        private void OnApplicationPause(bool pause) { if (pause) { holding = false; Flush(); } }
        private void OnApplicationFocus(bool focus) { if (!focus) { holding = false; Flush(); } }
        private void OnApplicationQuit() { Flush(); }
        private void OnDestroy() => art.Dispose();
    }
}
