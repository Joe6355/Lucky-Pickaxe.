using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MineLoot
{
    public sealed partial class DemoController : MonoBehaviour
    {
        private static readonly Color Back = Hex("0b1521"), Panel = Hex("192a3a"), Muted = Hex("8fa6b8"), Ink = Hex("e6eff4"), Lime = Hex("e8c78b");
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
                    if (button.name == "Toggle speed") button.GetComponentInChildren<Text>().text = "Скорость ×" + (FastRoll ? "2" : "1");
        }

        public void ToggleSound() { sounds.Toggle(); Refresh(); }

        public void ShowPage(bool showRoulette)
        {
            if (busy) return;
            holding = false;
            roulette = showRoulette; minePage.SetActive(!roulette); rollPage.SetActive(roulette);
            mineTab.image.color = roulette ? Hex("152332") : Hex("203d48");
            rollTab.image.color = roulette ? Hex("203d48") : Hex("152332");
            mineTab.GetComponentInChildren<Text>().color = roulette ? Muted : Hex("8be1d3");
            rollTab.GetComponentInChildren<Text>().color = roulette ? Hex("8be1d3") : Muted;
            mineNavMark.enabled = !roulette; rollNavMark.enabled = roulette;
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
                    picks[c].rectTransform.anchoredPosition = new Vector2(48 + c * 81, -(136 + row * 44));
                    picks[c].rectTransform.localEulerAngles = new Vector3(0, 0, -24);
                }
                if (!changes) continue;
                sounds.Play("break"); DrawBoard(); rollStatus.text = "Добываем… +" + earned + " монет";
                coinLabel.text = Math.Min(MineGame.CoinLimit, displayedCoins + earned).ToString("N0");
                yield return new WaitForSecondsRealtime(.16f * speed);
                for (int c = 0; c < 5; c++) picks[c].rectTransform.localEulerAngles = Vector3.zero;
                yield return new WaitForSecondsRealtime(.06f * speed);
            }
            for (int c = 0; c < 5; c++) picks[c].rectTransform.anchoredPosition = new Vector2(48 + c * 81, -83);
            Array.Copy(result.after, shownDepths, 5); Array.Copy(result.damageAfter, shownDamage, 5); DrawBoard();
            rollStatus.text = result.completed ? "Пласт пройден! +" + result.reward + " монет / +1 жетон" : result.reward == 0 ? "Урон сохранён · продолжай добычу" : "+" + result.reward + " монет" + (result.scrap > 0 ? " · включая лом +" + result.scrap : result.chests > 0 ? " · сундуков: " + result.chests : " · копаем дальше");
            sounds.Play(result.chests > 0 || result.completed ? "reward" : "hit");
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
            coinLabel.resizeTextForBestFit = true; coinLabel.resizeTextMinSize = 13; coinLabel.resizeTextMaxSize = 25;
            ticketLabel.text = game.Progress.tickets.ToString("N0");
            ticketLabel.resizeTextForBestFit = true; ticketLabel.resizeTextMinSize = 12; ticketLabel.resizeTextMaxSize = 21;
            powerLabel.text = "+" + game.ClickPower + " за удар";
            progressLabel.text = game.Progress.hits + " / " + MineGame.HitsPerTicket;
            ticketFill.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 360 * game.Progress.hits / MineGame.HitsPerTicket);
            upgradeLabel.text = game.Progress.upgradeLevel >= MineGame.MaxUpgrade ? "Максимум" : game.UpgradeCost.ToString("N0") + " монет";
            upgradeLabel.resizeTextForBestFit = true; upgradeLabel.resizeTextMinSize = 10; upgradeLabel.resizeTextMaxSize = 13;
            pickDetail.text = "Ур. " + (game.Progress.upgradeLevel + 1) + " · +4 за удар";
            upgradeHint.text = "Реликвий: " + game.Relics;
            upgradeButton.interactable = !busy && game.CanUpgrade;
            forgeButton.interactable = !busy && game.CanForge;
            forgeLabel.text = game.Progress.forgeLevel == MineGame.MaxForge ? "Максимум" : game.ForgeCost.ToString("N0") + " монет";
            forgeDetail.text = "Ур. " + game.Progress.forgeLevel + " · редкость кирок";
            contractLabel.text = game.CanClaim ? "Забрать" : "В работе";
            contractDetail.text = ShortAmount(System.Math.Min(game.Progress.totalBlocks, game.ContractTarget)) + " / " + ShortAmount(game.ContractTarget) + " · +" + ShortAmount(game.ContractReward) + " и жетон";
            contractFill.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 240 * Mathf.Clamp01((game.Progress.totalBlocks - game.Progress.claimedContracts * 25) / 25f));
            contractButton.interactable = !busy && game.CanClaim;
            contractButton.image.color = game.CanClaim ? Hex("e8c580") : Hex("2b3f50");
            contractLabel.color = game.CanClaim ? Hex("362d24") : Muted;
            upgradeButton.image.color = game.CanUpgrade ? Hex("6fcbbb") : Hex("2b3f50");
            upgradeLabel.color = game.CanUpgrade ? Hex("16382f") : Hex("b3c4d0");
            forgeButton.image.color = game.CanForge ? Hex("e5c488") : Hex("2b3f50");
            forgeLabel.color = game.CanForge ? Hex("3a3020") : Hex("b3c4d0");
            soundLabel.text = sounds.Muted ? "♪×" : "♪";
            spinButton.interactable = !busy;
            spinButton.GetComponentInChildren<Text>().text = busy ? "Кирки в работе…" : game.Progress.tickets == 0 ? "Добыть новые жетоны →" : game.IsComplete ? "Открыть новый пласт · 1 жетон" : "Отправить кирки · 1 жетон";
            mineTab.interactable = rollTab.interactable = !busy;
            string biome = new[] { "Мшистые штольни", "Кристальный грот", "Угольные недра" }[game.Biome];
            stratumLabel.text = "Пласт " + game.Progress.stratum.ToString("00") + " · " + biome;
            veinLabel.text = biome; stageBadge.text = "ПЛАСТ " + game.Progress.stratum.ToString("00");
            floorHint.text = "Прочность " + game.Hardness;
            mineBackdrop.color = rollBackdrop.color = new[] { Color.white, new Color(.8f, .96f, 1), new Color(1, .8f, .7f) }[game.Biome];
            clickPick.sprite = art.Get(game.Progress.upgradeLevel < 2 ? "pick1" : game.Progress.upgradeLevel < 5 ? "pick2" : "pick4");
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
