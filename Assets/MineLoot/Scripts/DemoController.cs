using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MineLoot
{
    public sealed class DemoController : MonoBehaviour
    {
        private static readonly Color Back = Hex("111824"), Panel = Hex("202c3e"), Muted = Hex("a3b4cb"), Ink = Hex("eef4fb"), Lime = Hex("b6ec79");
        private static readonly string[][] Layers = { new[] { "grass", "stone", "iron", "gold", "diamond" }, new[] { "stone", "iron", "diamond", "stone", "diamond" }, new[] { "stone", "ember", "iron", "ember", "gold" } };
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
        private Image mineBackdrop, rollBackdrop, mineGlow, rockCrack, boardFill;
        private Image clickPick;
        private Text forgeLabel, contractLabel, soundLabel, veinLabel, floorHint;
        private Button forgeButton, contractButton;
        private bool dirty, holding, holdRepeated;
        private float nextHold, lastMineTime = -1, nextSave;
        private int effects;
        private long displayedCoins = -1;
        public bool FastRoll { get; private set; }
        public MineGame Game => game;
        public bool IsBusy => busy;

        private void Awake()
        {
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
            rockPulse = Mathf.MoveTowards(rockPulse, 0, Time.unscaledDeltaTime * 6);
            rock.localScale = Vector3.one * (1 - rockPulse * .065f);
            rock.localEulerAngles = new Vector3(0, 0, -4 + rockPulse * 7);
            clickPick.rectTransform.localEulerAngles = new Vector3(0, 0, -24 + rockPulse * 45);
            mineGlow.color = new Color(.55f, .91f, .73f, .55f + Mathf.Sin(Time.unscaledTime * 1.4f) * .08f);
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

        private void BuildInterface()
        {
            var canvasObject = new GameObject("Game UI", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            canvas = canvasObject.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            PanelAt(canvasObject.transform, "Backdrop", Back, 0, 0, 480, 960);
            var fullBack = canvasObject.transform.GetChild(0).GetComponent<RectTransform>();
            fullBack.anchorMin = Vector2.zero; fullBack.anchorMax = Vector2.one; fullBack.sizeDelta = Vector2.zero; fullBack.anchoredPosition = Vector2.zero;
            shell = MakeRect(canvasObject.transform, "Portrait game", 0, 0, 480, 960);
            shell.anchorMin = shell.anchorMax = new Vector2(.5f, .5f); shell.pivot = new Vector2(.5f, .5f);
            shell.anchoredPosition = Vector2.zero;
            PanelAt(shell, "Surface", Hex("172132"), 0, 0, 480, 960);
            Icon(shell, "pick4", 23, 24, 52, 52);
            Label(shell, "LUCKY <color=#f5d68c>PICKAXE</color>", 87, 25, 312, 37, 27, Ink, FontStyle.Bold);
            Label(shell, "КИРКА УДАЧИ  /  ШАХТА ЖДЁТ", 88, 68, 350, 20, 11, Muted);
            var sound = ButtonAt(shell, "", 409, 24, 47, 38, ToggleSound, Panel, Ink);
            sound.name = "Toggle sound"; soundLabel = sound.GetComponentInChildren<Text>();
            Frame(shell, "Coins", 24, 112, 252, 60);
            Icon(shell, "coin", 37, 127, 30, 30);
            coinLabel = Label(shell, "", 80, 121, 182, 42, 25, Ink, FontStyle.Bold);
            Frame(shell, "Tickets", 286, 112, 170, 60);
            Icon(shell, "ticket", 299, 127, 31, 31);
            ticketLabel = Label(shell, "", 341, 121, 106, 42, 21, Ink, FontStyle.Bold);
            mineTab = ButtonAt(shell, "КЛИКЕР", 24, 188, 211, 46, () => ShowPage(false), Panel, Ink);
            rollTab = ButtonAt(shell, "РУЛЕТКА ШАХТЁРА", 245, 188, 211, 46, () => ShowPage(true), Panel, Ink);
            minePage = MakeRect(shell, "Clicker page", 0, 248, 480, 624).gameObject;
            rollPage = MakeRect(shell, "Roulette page", 0, 248, 480, 624).gameObject;
            BuildMine(minePage.transform); BuildRoll(rollPage.transform);
            contractButton = ButtonAt(shell, "", 24, 878, 432, 54, ClaimContract, Panel, Ink);
            contractButton.name = "Claim contract"; contractLabel = contractButton.GetComponentInChildren<Text>();
            Label(shell, "ДЕМО 0.2 · ПРОГРЕСС СОХРАНЯЕТСЯ НА УСТРОЙСТВЕ", 24, 941, 432, 15, 9, Muted, FontStyle.Normal, TextAnchor.MiddleCenter);
            if (FindObjectOfType<EventSystem>() == null)
            {
                var events = new GameObject("Event System", typeof(EventSystem), typeof(StandaloneInputModule));
                events.transform.SetParent(transform, false);
            }
            FitScreen();
        }

        private void BuildMine(Transform page)
        {
            mineBackdrop = Icon(page, "cave0", 24, 0, 432, 330);
            mineGlow = Icon(page, "glow", 99, 55, 280, 260); mineGlow.color = Hex("8be9b1");
            PanelAt(page, "Mine header", new Color(.05f, .1f, .13f, .9f), 24, 0, 432, 44);
            veinLabel = Label(page, "", 39, 10, 280, 25, 12, Hex("c9dce7"));
            powerLabel = Label(page, "", 310, 10, 128, 25, 12, Lime, FontStyle.Bold, TextAnchor.MiddleRight);
            Label(page, "ДОБЫВАЙ БОГАТСТВА", 80, 63, 320, 24, 11, Hex("b5c9c0"), FontStyle.Bold, TextAnchor.MiddleCenter);
            var rockButton = ButtonAt(page, "", 161, 112, 158, 158, () => { if (!holdRepeated) Mine(); holdRepeated = false; }, Color.white, Ink);
            rockButton.name = "Mine rock"; rockButton.image.sprite = art.Get("diamond"); rock = rockButton.GetComponent<RectTransform>();
            rock.pivot = new Vector2(.5f, .5f); rock.anchoredPosition += new Vector2(79, -79);
            rockCrack = Icon(rock, "crack1", 0, 0, 158, 158); rockCrack.color = Color.clear;
            var trigger = rock.gameObject.AddComponent<EventTrigger>();
            AddPointer(trigger, EventTriggerType.PointerDown, () => { holding = true; holdRepeated = false; nextHold = Time.unscaledTime + .28f; });
            AddPointer(trigger, EventTriggerType.PointerUp, () => holding = false);
            AddPointer(trigger, EventTriggerType.PointerExit, () => { holding = false; holdRepeated = false; });
            clickPick = Icon(page, "pick4", 278, 107, 99, 99);
            mineStatus = Label(page, "Нажимай или удерживай руду", 40, 286, 400, 30, 16, Ink, FontStyle.Bold, TextAnchor.MiddleCenter);
            Label(page, "ДО СЛЕДУЮЩЕГО ЖЕТОНА", 24, 346, 285, 24, 11, Muted);
            progressLabel = Label(page, "", 304, 346, 152, 24, 12, Muted, FontStyle.Normal, TextAnchor.MiddleRight);
            PanelAt(page, "Ticket track", Panel, 24, 380, 432, 7);
            ticketFill = PanelAt(page, "Ticket progress", Lime, 24, 380, 0, 7);
            upgradeButton = ButtonAt(page, "", 24, 411, 432, 60, Upgrade, Lime, Hex("263823"));
            upgradeButton.name = "Upgrade pickaxe";
            upgradeLabel = upgradeButton.GetComponentInChildren<Text>();
            forgeButton = ButtonAt(page, "", 24, 484, 432, 60, Forge, Hex("e8c580"), Hex("362d24"));
            forgeButton.name = "Upgrade forge"; forgeLabel = forgeButton.GetComponentInChildren<Text>();
            upgradeHint = Label(page, "", 24, 556, 432, 26, 12, Muted, FontStyle.Normal, TextAnchor.MiddleCenter);
            Label(page, "3 пройденных пласта = реликвия: +1 монета за удар", 24, 591, 432, 24, 11, Hex("d6c297"), FontStyle.Normal, TextAnchor.MiddleCenter);
        }

        private void BuildRoll(Transform page)
        {
            rollBackdrop = Icon(page, "cave0", 24, 0, 432, 448);
            PanelAt(page, "Shaft shade", new Color(.03f, .09f, .12f, .7f), 24, 0, 432, 448);
            stratumLabel = Label(page, "", 39, 11, 304, 26, 12, Hex("c9dce7"));
            floorHint = Label(page, "", 318, 11, 119, 26, 11, Lime, FontStyle.Bold, TextAnchor.MiddleRight);
            string[] layers = { "grass", "stone", "iron", "gold", "diamond" };
            for (int column = 0; column < 5; column++)
            {
                int x = 41 + column * 81;
                Frame(page, "Reel " + column, x, 49, 74, 91);
                picks[column] = Icon(page, "pick1", x + 9, 52, 56, 56);
                powers[column] = Label(page, "1 силы", x, 112, 74, 21, 11, Muted, FontStyle.Normal, TextAnchor.MiddleCenter);
                for (int row = 0; row < 5; row++)
                    blocks[column, row] = Icon(page, layers[row], x, 155 + row * 44, 74, 41);
                cracks[column] = Icon(page, "crack2", x, 155, 74, 41);
                chests[column] = Icon(page, "chest", x + 12, 383, 50, 50);
            }
            PanelAt(page, "Board track", Panel, 41, 443, 398, 5);
            boardFill = PanelAt(page, "Board completion", Hex("e9c683"), 41, 443, 0, 5);
            rollStatus = Label(page, "Каждая кирка приближает к сундуку", 24, 459, 432, 42, 15, Ink, FontStyle.Bold, TextAnchor.MiddleCenter);
            spinButton = ButtonAt(page, "ЗАПУСТИТЬ КИРКИ · 1 ЖЕТОН", 24, 511, 432, 59, Spin, Lime, Hex("263823"));
            ButtonAt(page, "ШАНСЫ И ПРАВИЛА", 24, 583, 246, 40, ShowOdds, Panel, Muted);
            ButtonAt(page, "СКОРОСТЬ ×1", 280, 583, 176, 40, ToggleSpeed, Panel, Muted).name = "Toggle speed";
        }

        public void Mine()
        {
            if (busy || Time.unscaledTime - lastMineTime < .075f || modal != null) return;
            lastMineTime = Time.unscaledTime;
            bool ticket = game.Mine(); rockPulse = 1;
            mineStatus.text = "+" + game.ClickPower + " монет" + (ticket ? "   /   +1 жетон!" : "");
            rockCrack.color = new Color(1, 1, 1, game.Progress.hits / (float)MineGame.HitsPerTicket);
            sounds.Play(ticket ? "reward" : "hit");
            if (effects < 8) StartCoroutine(FloatReward("+" + game.ClickPower, minePage.transform, 210, 120));
            Persist(); Refresh();
        }

        public void Upgrade()
        {
            if (busy || !game.Upgrade()) return;
            mineStatus.text = "Кирка улучшена! +" + game.ClickPower + " за удар";
            sounds.Play("upgrade");
            Persist(); Refresh();
        }

        public void Forge()
        {
            if (busy || !game.Forge()) return;
            sounds.Play("upgrade"); mineStatus.text = "Кузница улучшена! Кирки стали сильнее";
            Persist(); Refresh();
        }

        public void ClaimContract()
        {
            if (busy || !game.ClaimContract()) return;
            sounds.Play("reward"); Persist(); Flush(); Refresh();
        }

        public void ToggleSpeed()
        {
            FastRoll = !FastRoll;
            foreach (var button in rollPage.GetComponentsInChildren<Button>())
                if (button.name == "Toggle speed") button.GetComponentInChildren<Text>().text = "СКОРОСТЬ ×" + (FastRoll ? "2" : "1");
        }

        public void ToggleSound() { sounds.Toggle(); Refresh(); }

        public void ShowPage(bool showRoulette)
        {
            if (busy) return;
            holding = false;
            roulette = showRoulette; minePage.SetActive(!roulette); rollPage.SetActive(roulette);
            mineTab.image.color = roulette ? Panel : Lime;
            rollTab.image.color = roulette ? Lime : Panel;
            mineTab.GetComponentInChildren<Text>().color = roulette ? Ink : Hex("263823");
            rollTab.GetComponentInChildren<Text>().color = roulette ? Hex("263823") : Ink;
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
            for (int frame = 0; frame < 9; frame++)
            {
                for (int c = 0; c < 5; c++)
                {
                    int power = new[] { 1, 2, 4 }[(frame + c) % 3];
                    picks[c].sprite = art.Get("pick" + power);
                    picks[c].rectTransform.localEulerAngles = new Vector3(0, 0, frame % 2 == 0 ? -12 : 12);
                    powers[c].text = "· · ·";
                }
                sounds.Play("tick"); yield return new WaitForSecondsRealtime(.065f * speed);
            }
            for (int c = 0; c < 5; c++)
            {
                picks[c].sprite = art.Get("pick" + result.types[c]);
                picks[c].rectTransform.localEulerAngles = Vector3.zero;
                powers[c].text = result.powers[c] + " силы";
                yield return new WaitForSecondsRealtime(.07f * speed);
            }
            int earned = 0;
            int maxPower = 0; foreach (int power in result.powers) maxPower = Math.Max(maxPower, power);
            for (int step = 0; step < maxPower; step++)
            {
                bool changes = false;
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
                    }
                    picks[c].rectTransform.anchoredPosition = new Vector2(50 + c * 81, -(126 + row * 44));
                    picks[c].rectTransform.localEulerAngles = new Vector3(0, 0, -24);
                }
                if (!changes) continue;
                sounds.Play("break"); DrawBoard(); rollStatus.text = "Добываем… +" + earned + " монет";
                coinLabel.text = Math.Min(MineGame.CoinLimit, displayedCoins + earned).ToString("N0");
                yield return new WaitForSecondsRealtime(.16f * speed);
                for (int c = 0; c < 5; c++) picks[c].rectTransform.localEulerAngles = Vector3.zero;
                yield return new WaitForSecondsRealtime(.06f * speed);
            }
            for (int c = 0; c < 5; c++) picks[c].rectTransform.anchoredPosition = new Vector2(50 + c * 81, -52);
            Array.Copy(result.after, shownDepths, 5); Array.Copy(result.damageAfter, shownDamage, 5); DrawBoard();
            rollStatus.text = result.completed ? "Пласт пройден! +" + result.reward + " монет / +1 жетон" : result.reward == 0 ? "Урон сохранён · продолжай добычу" : "+" + result.reward + " монет" + (result.scrap > 0 ? " · включая лом +" + result.scrap : result.chests > 0 ? " · сундуков: " + result.chests : " · копаем дальше");
            sounds.Play(result.chests > 0 || result.completed ? "reward" : "hit");
            displayedCoins = -1; busy = false; Refresh();
        }

        private void SpawnDebris(int column, int row)
        {
            var origin = new Vector2(78 + column * 81, -(175 + row * 44));
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
                    blocks[c, r].sprite = art.Get(Layers[game.Biome][r]);
                    blocks[c, r].color = r < shownDepths[c] ? new Color(1, 1, 1, .08f) : Color.white;
                }
                chests[c].sprite = art.Get(shownDepths[c] == 5 ? "chest-open" : "chest");
                cracks[c].enabled = shownDepths[c] < 5 && shownDamage[c] > 0;
                cracks[c].rectTransform.anchoredPosition = new Vector2(41 + c * 81, -(155 + shownDepths[c] * 44));
                cleared += shownDepths[c];
            }
            boardFill.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, cleared / 25f * 398);
        }

        private void Refresh()
        {
            coinLabel.text = (displayedCoins >= 0 ? displayedCoins : game.Progress.coins).ToString("N0");
            // Long values reduce their font size inside their existing bounds.
            coinLabel.resizeTextForBestFit = true; coinLabel.resizeTextMinSize = 13; coinLabel.resizeTextMaxSize = 25;
            ticketLabel.text = game.Progress.tickets.ToString("N0");
            ticketLabel.resizeTextForBestFit = true; ticketLabel.resizeTextMinSize = 12; ticketLabel.resizeTextMaxSize = 21;
            powerLabel.text = "+" + game.ClickPower + " / УДАР";
            progressLabel.text = game.Progress.hits + " / " + MineGame.HitsPerTicket + " ударов";
            ticketFill.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 432 * game.Progress.hits / MineGame.HitsPerTicket);
            upgradeLabel.text = game.Progress.upgradeLevel >= MineGame.MaxUpgrade ? "КИРКА УЛУЧШЕНА ДО МАКСИМУМА" : "КИРКА · +4 ЗА УДАР\n" + game.UpgradeCost.ToString("N0") + " монет";
            upgradeLabel.resizeTextForBestFit = true; upgradeLabel.resizeTextMinSize = 11; upgradeLabel.resizeTextMaxSize = 16;
            upgradeHint.text = "Кирка ур. " + (game.Progress.upgradeLevel + 1) + "   /   реликвий: " + game.Relics;
            upgradeButton.interactable = !busy && game.CanUpgrade;
            forgeButton.interactable = !busy && game.CanForge;
            forgeLabel.text = game.Progress.forgeLevel == MineGame.MaxForge ? "КУЗНИЦА · МАКСИМУМ" : "КУЗНИЦА · УР. " + game.Progress.forgeLevel + " → " + (game.Progress.forgeLevel + 1) + "\n" + game.ForgeCost.ToString("N0") + " монет · редкость и сила";
            contractLabel.text = game.CanClaim ? "КОНТРАКТ ВЫПОЛНЕН · ЗАБРАТЬ\n+" + game.ContractReward + " монет и 1 жетон" : "КОНТРАКТ · ДОБЫТЬ " + game.ContractTarget + " БЛОКОВ\n" + game.Progress.totalBlocks + " / " + game.ContractTarget + " · награда: " + game.ContractReward + " монет и жетон";
            contractButton.interactable = !busy && game.CanClaim;
            contractButton.image.color = game.CanClaim ? Hex("e8c580") : Panel;
            contractLabel.color = game.CanClaim ? Hex("362d24") : Ink;
            soundLabel.text = sounds.Muted ? "♪×" : "♪";
            spinButton.interactable = !busy;
            spinButton.GetComponentInChildren<Text>().text = busy ? "КИРКИ В РАБОТЕ…" : game.Progress.tickets == 0 ? "ЗА ЖЕТОНАМИ → В КЛИКЕР" : game.IsComplete ? "НОВЫЙ ПЛАСТ · 1 ЖЕТОН" : "ЗАПУСТИТЬ КИРКИ · 1 ЖЕТОН";
            mineTab.interactable = rollTab.interactable = !busy;
            string biome = new[] { "МШИСТЫЕ ШТОЛЬНИ", "КРИСТАЛЬНЫЙ ГРОТ", "УГОЛЬНЫЕ НЕДРА" }[game.Biome];
            stratumLabel.text = "ПЛАСТ " + game.Progress.stratum.ToString("00") + " · " + biome;
            stratumLabel.fontSize = 10; veinLabel.text = biome;
            floorHint.text = "ПРОЧНОСТЬ " + game.Hardness;
            mineBackdrop.sprite = rollBackdrop.sprite = art.Get("cave" + game.Biome);
            clickPick.sprite = art.Get(game.Progress.upgradeLevel < 2 ? "pick1" : game.Progress.upgradeLevel < 5 ? "pick2" : "pick4");
        }

        private void ShowOdds()
        {
            if (modal != null) return;
            modal = MakeRect(shell, "Rules modal", 0, 0, 480, 960).gameObject;
            var overlay = PanelAt(modal.transform, "Dim", new Color(0, 0, 0, .8f), 0, 0, 480, 960);
            overlay.raycastTarget = true;
            holding = false;
            Frame(modal.transform, "Rules", 24, 191, 432, 578);
            Label(modal.transform, "КИРКИ И НАГРАДЫ", 47, 212, 386, 40, 23, Ink, FontStyle.Bold);
            Label(modal.transform, "Текущая кузница: уровень " + game.Progress.forgeLevel + "\n\nДеревянная: " + (100 - game.IronChance - game.DiamondChance) + "% · сила " + (1 + game.PowerBonus) + "\nЖелезная: " + game.IronChance + "% · сила " + (2 + game.PowerBonus) + "\nАлмазная: " + game.DiamondChance + "% · сила " + (4 + game.PowerBonus) + "\n\nПрочность блока: " + game.Hardness + ". Урон сохраняется.\nБлок: " + game.BlockReward + " монет. Сундук: " + game.ChestReward + ".\nВесь пласт: +" + game.FloorReward + " монет и жетон.\nКирки в открытых столбцах дают лом:\n2 монеты за единицу силы.\n\nКузница: +редкость каждый уровень,\n+1 сила всем киркам каждые 2 уровня.\n12 ударов в кликере = 1 жетон.", 47, 269, 386, 396, 15, Muted);
            ButtonAt(modal.transform, "ПОНЯТНО", 47, 691, 386, 51, () => { Destroy(modal); modal = null; }, Lime, Hex("263823"));
        }

        private Image Frame(Transform parent, string name, float x, float y, float width, float height)
        {
            var image = PanelAt(parent, name, Color.white, x, y, width, height);
            image.sprite = art.Get("frame"); image.type = Image.Type.Sliced; return image;
        }

        private static void AddPointer(EventTrigger trigger, EventTriggerType type, UnityEngine.Events.UnityAction action)
        {
            var entry = new EventTrigger.Entry { eventID = type }; entry.callback.AddListener(_ => action()); trigger.triggers.Add(entry);
        }

        private IEnumerator FloatReward(string value, Transform parent, float x, float y)
        {
            effects++;
            var label = Label(parent, value, x, y, 110, 30, 22, Hex("ffe2a0"), FontStyle.Bold, TextAnchor.MiddleCenter);
            float elapsed = 0;
            while (elapsed < .65f)
            {
                elapsed += Time.unscaledDeltaTime;
                label.rectTransform.anchoredPosition = new Vector2(x, -y + elapsed * 70);
                label.color = new Color(1, .88f, .61f, 1 - elapsed / .65f); yield return null;
            }
            Destroy(label.gameObject); effects--;
        }

        private static Color Hex(string hex) { ColorUtility.TryParseHtmlString("#" + hex, out var c); return c; }

        private static RectTransform MakeRect(Transform parent, string name, float x, float y, float width, float height)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false); rect.anchorMin = rect.anchorMax = new Vector2(0, 1); rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(width, height); return rect;
        }

        private static Image PanelAt(Transform parent, string name, Color color, float x, float y, float width, float height)
        {
            var rect = MakeRect(parent, name, x, y, width, height);
            var image = rect.gameObject.AddComponent<Image>(); image.color = color; image.raycastTarget = false; return image;
        }

        private Image Icon(Transform parent, string sprite, float x, float y, float width, float height)
        {
            var image = PanelAt(parent, sprite, Color.white, x, y, width, height); image.sprite = art.Get(sprite); return image;
        }

        private static Text Label(Transform parent, string value, float x, float y, float width, float height, int size, Color color, FontStyle weight = FontStyle.Normal, TextAnchor align = TextAnchor.MiddleLeft)
        {
            var rect = MakeRect(parent, "Label", x, y, width, height);
            var text = rect.gameObject.AddComponent<Text>(); text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = value; text.fontSize = size; text.fontStyle = weight; text.color = color; text.alignment = align;
            text.raycastTarget = false; text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }

        private static Button ButtonAt(Transform parent, string value, float x, float y, float width, float height, UnityEngine.Events.UnityAction action, Color back, Color front)
        {
            PanelAt(parent, "Button shadow", new Color(0, 0, 0, .25f), x, y + 4, width, height);
            var image = PanelAt(parent, value.Length == 0 ? "Button" : value, back, x, y, width, height); image.raycastTarget = true;
            PanelAt(image.transform, "Top light", new Color(1, 1, 1, .12f), 2, 0, width - 4, 2);
            var button = image.gameObject.AddComponent<Button>(); button.targetGraphic = image;
            var colors = button.colors; colors.highlightedColor = new Color(.9f, 1, .86f); colors.pressedColor = new Color(.7f, .8f, .66f); colors.disabledColor = new Color(.47f, .52f, .55f); button.colors = colors;
            Label(image.transform, value, 10, 0, width - 20, height, 15, front, FontStyle.Bold, TextAnchor.MiddleCenter);
            var navigation = button.navigation; navigation.mode = Navigation.Mode.None; button.navigation = navigation;
            button.onClick.AddListener(action); return button;
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
