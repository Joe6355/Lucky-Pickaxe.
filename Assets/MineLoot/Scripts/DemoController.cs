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
        private readonly PixelArt art = new PixelArt();
        private readonly Image[,] blocks = new Image[5, 5];
        private readonly Image[] picks = new Image[5], chests = new Image[5];
        private readonly Text[] powers = new Text[5];
        private readonly int[] shownDepths = new int[5];
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
        public MineGame Game => game;
        public bool IsBusy => busy;

        private void Awake()
        {
            game = SmokeMode ? new MineGame() : ProgressStore.Load();
            random = new System.Random();
            Application.targetFrameRate = 60;
            BuildInterface();
            Array.Copy(game.Progress.depths, shownDepths, 5);
            DrawBoard(); Refresh(); ShowPage(false);
            if (SmokeMode) gameObject.AddComponent<DemoSmokeCheck>();
        }

        private void Update()
        {
            if (lastWidth != Screen.width || lastHeight != Screen.height || lastSafe != Screen.safeArea) FitScreen();
            rockPulse = Mathf.MoveTowards(rockPulse, 0, Time.unscaledDeltaTime * 6);
            rock.localScale = Vector3.one * (1 - rockPulse * .065f);
            rock.localEulerAngles = new Vector3(0, 0, -4 + rockPulse * 7);
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
            Label(shell, "COPPER VALLEY / DEMO", 24, 20, 400, 20, 11, Muted);
            Label(shell, "MINE<color=#b6ec79>LOOT</color>", 23, 43, 330, 46, 38, Ink, FontStyle.Bold);
            Label(shell, "КОПАЙ. КРУТИ. УЛУЧШАЙ.", 25, 94, 400, 22, 12, Muted);
            PanelAt(shell, "Coins", Panel, 24, 130, 252, 58);
            Icon(shell, "coin", 37, 144, 30, 30);
            coinLabel = Label(shell, "", 80, 137, 182, 42, 25, Ink, FontStyle.Bold);
            PanelAt(shell, "Tickets", Panel, 286, 130, 170, 58);
            Icon(shell, "ticket", 299, 144, 31, 31);
            ticketLabel = Label(shell, "", 341, 137, 106, 42, 21, Ink, FontStyle.Bold);
            mineTab = ButtonAt(shell, "КЛИКЕР", 24, 203, 211, 48, () => ShowPage(false), Panel, Ink);
            rollTab = ButtonAt(shell, "РУЛЕТКА ШАХТЁРА", 245, 203, 211, 48, () => ShowPage(true), Panel, Ink);
            minePage = MakeRect(shell, "Clicker page", 0, 268, 480, 624).gameObject;
            rollPage = MakeRect(shell, "Roulette page", 0, 268, 480, 624).gameObject;
            BuildMine(minePage.transform); BuildRoll(rollPage.transform);
            Label(shell, "ЛОКАЛЬНАЯ ДЕМКА · БЕЗ ДЕНЕЖНЫХ СТАВОК", 24, 920, 432, 22, 10, Muted, FontStyle.Normal, TextAnchor.MiddleCenter);
            if (FindObjectOfType<EventSystem>() == null)
            {
                var events = new GameObject("Event System", typeof(EventSystem), typeof(StandaloneInputModule));
                events.transform.SetParent(transform, false);
            }
            FitScreen();
        }

        private void BuildMine(Transform page)
        {
            PanelAt(page, "Mine background", Hex("243d50"), 24, 0, 432, 397);
            Label(page, "ЛИЧНАЯ ШАХТА", 43, 16, 260, 25, 12, Hex("c9dce7"));
            powerLabel = Label(page, "", 305, 16, 132, 25, 12, Lime, FontStyle.Bold, TextAnchor.MiddleRight);
            // Background timber beams frame the mineral vein.
            PanelAt(page, "Left timber", Hex("5f5444"), 56, 64, 15, 246);
            PanelAt(page, "Right timber", Hex("5f5444"), 409, 64, 15, 246);
            PanelAt(page, "Top timber", Hex("8a7957"), 56, 64, 368, 14);
            Label(page, "АЛМАЗНАЯ ЖИЛА", 87, 91, 306, 26, 13, Hex("c9dce7"), FontStyle.Bold, TextAnchor.MiddleCenter);
            var rockButton = ButtonAt(page, "", 153, 143, 174, 174, Mine, Color.white, Ink);
            rockButton.name = "Mine rock"; rockButton.image.sprite = art.Get("diamond"); rock = rockButton.GetComponent<RectTransform>();
            // Center pivot keeps the tap pulse centered.
            rock.pivot = new Vector2(.5f, .5f); rock.anchoredPosition += new Vector2(87, -87);
            mineStatus = Label(page, "Нажимай на руду", 40, 338, 400, 33, 17, Ink, FontStyle.Bold, TextAnchor.MiddleCenter);
            Label(page, "БИЛЕТ В ШАХТУ", 24, 418, 280, 24, 12, Muted);
            progressLabel = Label(page, "", 283, 418, 173, 24, 12, Muted, FontStyle.Normal, TextAnchor.MiddleRight);
            PanelAt(page, "Ticket track", Panel, 24, 452, 432, 9);
            ticketFill = PanelAt(page, "Ticket progress", Lime, 24, 452, 0, 9);
            Label(page, "Улучшай добычу и возвращайся за сундуками", 24, 480, 432, 28, 14, Muted, FontStyle.Normal, TextAnchor.MiddleCenter);
            upgradeButton = ButtonAt(page, "", 24, 524, 432, 62, Upgrade, Lime, Hex("263823"));
            upgradeButton.name = "Upgrade pickaxe";
            upgradeLabel = upgradeButton.GetComponentInChildren<Text>();
            upgradeHint = Label(page, "", 24, 599, 432, 25, 12, Muted, FontStyle.Normal, TextAnchor.MiddleCenter);
        }

        private void BuildRoll(Transform page)
        {
            PanelAt(page, "Shaft background", Hex("243d50"), 24, 0, 432, 450);
            stratumLabel = Label(page, "", 39, 11, 304, 26, 12, Hex("c9dce7"));
            Label(page, "5 КИРОК", 343, 11, 94, 26, 11, Lime, FontStyle.Bold, TextAnchor.MiddleRight);
            string[] layers = { "grass", "stone", "iron", "gold", "diamond" };
            for (int column = 0; column < 5; column++)
            {
                int x = 41 + column * 81;
                PanelAt(page, "Reel " + column, Hex("152331"), x, 49, 74, 91);
                picks[column] = Icon(page, "pick1", x + 9, 52, 56, 56);
                powers[column] = Label(page, "1 блок", x, 112, 74, 21, 11, Muted, FontStyle.Normal, TextAnchor.MiddleCenter);
                for (int row = 0; row < 5; row++)
                    blocks[column, row] = Icon(page, layers[row], x, 155 + row * 44, 74, 41);
                chests[column] = Icon(page, "chest", x + 12, 383, 50, 50);
            }
            rollStatus = Label(page, "Пять кирок. Пять путей к сундукам.", 24, 463, 432, 42, 16, Ink, FontStyle.Bold, TextAnchor.MiddleCenter);
            spinButton = ButtonAt(page, "ЗАПУСТИТЬ КИРКИ · 1 ЖЕТОН", 24, 524, 432, 62, Spin, Lime, Hex("263823"));
            ButtonAt(page, "ШАНСЫ И ПРАВИЛА", 24, 593, 215, 43, ShowOdds, Panel, Muted);
            Label(page, "Сундук: +250 монет", 246, 593, 210, 43, 12, Muted, FontStyle.Normal, TextAnchor.MiddleRight);
        }

        public void Mine()
        {
            if (busy) return;
            bool ticket = game.Mine(); rockPulse = 1;
            mineStatus.text = "+" + game.ClickPower + " монет" + (ticket ? "   /   +1 жетон!" : "");
            Persist(); Refresh();
        }

        public void Upgrade()
        {
            if (busy || !game.Upgrade()) return;
            mineStatus.text = "Кирка улучшена! +" + game.ClickPower + " за удар";
            Persist(); Refresh();
        }

        public void ShowPage(bool showRoulette)
        {
            if (busy) return;
            roulette = showRoulette; minePage.SetActive(!roulette); rollPage.SetActive(roulette);
            mineTab.image.color = roulette ? Panel : Lime;
            rollTab.image.color = roulette ? Lime : Panel;
            mineTab.GetComponentInChildren<Text>().color = roulette ? Ink : Hex("263823");
            rollTab.GetComponentInChildren<Text>().color = roulette ? Hex("263823") : Ink;
        }

        public void Spin()
        {
            if (busy) return;
            if (game.Progress.tickets == 0) { ShowPage(false); mineStatus.text = "Ещё пять ударов — и новый жетон"; return; }
            var result = game.Roll(() => random.NextDouble());
            if (result == null) return;
            busy = true;
            // Commit all rewards before the coroutine; UI only presents the completed transaction.
            Persist();
            Array.Copy(result.before, shownDepths, 5);
            DrawBoard(); Refresh(); StartCoroutine(AnimateRoll(result));
        }

        private IEnumerator AnimateRoll(RollResult result)
        {
            rollStatus.text = "Какие кирки пойдут в шахту?";
            for (int frame = 0; frame < 11; frame++)
            {
                for (int c = 0; c < 5; c++)
                {
                    int power = new[] { 1, 2, 4 }[(frame + c) % 3];
                    picks[c].sprite = art.Get("pick" + power);
                    picks[c].rectTransform.localEulerAngles = new Vector3(0, 0, frame % 2 == 0 ? -12 : 12);
                    powers[c].text = "· · ·";
                }
                yield return new WaitForSecondsRealtime(.065f);
            }
            for (int c = 0; c < 5; c++)
            {
                picks[c].sprite = art.Get("pick" + result.powers[c]);
                picks[c].rectTransform.localEulerAngles = Vector3.zero;
                powers[c].text = result.powers[c] + (result.powers[c] == 1 ? " блок" : " блока");
                yield return new WaitForSecondsRealtime(.08f);
            }
            int earned = 0;
            for (int step = 0; step < 4; step++)
            {
                bool changes = false;
                for (int c = 0; c < 5; c++)
                {
                    if (step >= result.powers[c] || shownDepths[c] >= 5) continue;
                    shownDepths[c]++; earned += MineGame.BlockReward; changes = true;
                    if (shownDepths[c] == 5) earned += MineGame.ChestReward;
                    picks[c].rectTransform.localEulerAngles = new Vector3(0, 0, -24);
                    SpawnDebris(c, shownDepths[c] - 1);
                }
                if (!changes) continue;
                DrawBoard(); rollStatus.text = "Добываем… +" + earned + " монет";
                yield return new WaitForSecondsRealtime(.22f);
                for (int c = 0; c < 5; c++) picks[c].rectTransform.localEulerAngles = Vector3.zero;
                yield return new WaitForSecondsRealtime(.08f);
            }
            rollStatus.text = game.IsComplete ? "Пласт пройден! +" + result.reward + " монет" : "+" + result.reward + " монет" + (result.chests > 0 ? "   /   сундуков: " + result.chests : "   /   копаем дальше");
            busy = false; Refresh();
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
            for (int c = 0; c < 5; c++)
            {
                for (int r = 0; r < 5; r++) blocks[c, r].color = r < shownDepths[c] ? new Color(1, 1, 1, .08f) : Color.white;
                chests[c].sprite = art.Get(shownDepths[c] == 5 ? "chest-open" : "chest");
            }
        }

        private void Refresh()
        {
            coinLabel.text = game.Progress.coins.ToString("N0");
            // Long values reduce their font size inside their existing bounds.
            coinLabel.resizeTextForBestFit = true; coinLabel.resizeTextMinSize = 13; coinLabel.resizeTextMaxSize = 25;
            ticketLabel.text = game.Progress.tickets.ToString("N0");
            ticketLabel.resizeTextForBestFit = true; ticketLabel.resizeTextMinSize = 12; ticketLabel.resizeTextMaxSize = 21;
            powerLabel.text = "+" + game.ClickPower + " / УДАР";
            progressLabel.text = game.Progress.hits + " / 5 ударов";
            ticketFill.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 432 * game.Progress.hits / 5f);
            upgradeLabel.text = game.Progress.upgradeLevel >= MineGame.MaxUpgrade ? "КИРКА УЛУЧШЕНА ДО МАКСИМУМА" : "УЛУЧШИТЬ · " + game.UpgradeCost.ToString("N0") + " МОНЕТ";
            upgradeLabel.resizeTextForBestFit = true; upgradeLabel.resizeTextMinSize = 11; upgradeLabel.resizeTextMaxSize = 16;
            upgradeHint.text = "Кирка ур. " + (game.Progress.upgradeLevel + 1) + " · +10 монет за удар после улучшения";
            upgradeButton.interactable = !busy && game.CanUpgrade;
            spinButton.interactable = !busy;
            spinButton.GetComponentInChildren<Text>().text = busy ? "КИРКИ В РАБОТЕ…" : game.Progress.tickets == 0 ? "ЗА ЖЕТОНАМИ → В КЛИКЕР" : game.IsComplete ? "НОВЫЙ ПЛАСТ · 1 ЖЕТОН" : "ЗАПУСТИТЬ КИРКИ · 1 ЖЕТОН";
            mineTab.interactable = rollTab.interactable = !busy;
            stratumLabel.text = "ПЛАСТ " + game.Progress.stratum.ToString("00") + " · СТАРАЯ ШАХТА";
        }

        private void ShowOdds()
        {
            if (modal != null) return;
            modal = MakeRect(shell, "Rules modal", 0, 0, 480, 960).gameObject;
            var overlay = PanelAt(modal.transform, "Dim", new Color(0, 0, 0, .8f), 0, 0, 480, 960);
            overlay.raycastTarget = true;
            PanelAt(modal.transform, "Rules", Panel, 24, 257, 432, 436);
            Label(modal.transform, "КАК ЭТО РАБОТАЕТ", 47, 279, 386, 40, 23, Ink, FontStyle.Bold);
            Label(modal.transform, "На каждый столбец выпадает своя кирка:\n\nДеревянная: 55% · 1 блок\nЖелезная: 35% · 2 блока\nАлмазная: 10% · 4 блока\n\nБлок: +10 монет. Сундук: +250 монет.\nРазрушенные блоки сохраняются.\nОткрытый столбец ждёт остальных.\n5 ударов в кликере = 1 жетон.", 47, 334, 386, 247, 16, Muted);
            ButtonAt(modal.transform, "ПОНЯТНО", 47, 607, 386, 56, () => { Destroy(modal); modal = null; }, Lime, Hex("263823"));
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
            var button = image.gameObject.AddComponent<Button>(); button.targetGraphic = image;
            var colors = button.colors; colors.highlightedColor = new Color(.9f, 1, .86f); colors.pressedColor = new Color(.7f, .8f, .66f); colors.disabledColor = new Color(.47f, .52f, .55f); button.colors = colors;
            Label(image.transform, value, 10, 0, width - 20, height, 15, front, FontStyle.Bold, TextAnchor.MiddleCenter);
            var navigation = button.navigation; navigation.mode = Navigation.Mode.None; button.navigation = navigation;
            button.onClick.AddListener(action); return button;
        }

        public static bool SmokeMode => Array.IndexOf(Environment.GetCommandLineArgs(), "-demoSmokeTest") >= 0;
        private void Persist() { if (!SmokeMode) ProgressStore.Save(game); }
        private void OnApplicationPause(bool pause) { if (pause && game != null) Persist(); }
        private void OnApplicationQuit() { if (game != null) Persist(); }
        private void OnDestroy() => art.Dispose();
    }
}
