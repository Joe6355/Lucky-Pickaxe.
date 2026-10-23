using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MineLoot
{
    // Presentation for the existing controller. Economy, input and save logic stay in DemoController.
    public sealed partial class DemoController
    {
        private static Font interfaceFont, interfaceBold;
        private Text contractDetail, pickDetail, forgeDetail, stageBadge;
        private Image contractFill, mineNavMark, rollNavMark;
        private Text levelLabel, pickTitle, nextUpgrade, noticeTitle, noticeDetail;
        private Image experienceFill, oreFill, workshopPick;
        private readonly Image[] oreSlots = new Image[5], oreIcons = new Image[5], reels = new Image[5];
        private readonly Text[] oreNames = new Text[5];
        private RectTransform noticeRect;
        private CanvasGroup noticeGroup;
        private VoxelStage stage;

        private void BuildInterface()
        {
            interfaceFont = Resources.Load<Font>("Fonts/Tiny5-Regular") ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            interfaceBold = interfaceFont;
            var canvasObject = new GameObject("Game UI", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            canvas = canvasObject.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var backdrop = PanelAt(canvasObject.transform, "Backdrop", Back, 0, 0, 480, 960).rectTransform;
            backdrop.anchorMin = Vector2.zero; backdrop.anchorMax = Vector2.one; backdrop.sizeDelta = Vector2.zero; backdrop.anchoredPosition = Vector2.zero;
            shell = MakeRect(canvasObject.transform, "Portrait game", 0, 0, 480, 960);
            shell.anchorMin = shell.anchorMax = new Vector2(.5f, .5f); shell.pivot = new Vector2(.5f, .5f); shell.anchoredPosition = Vector2.zero;
            var surface = PanelAt(shell, "Surface", Color.white, 0, 0, 480, 960);
            surface.sprite = art.Get("tex-dirt"); surface.type = Image.Type.Tiled; surface.pixelsPerUnitMultiplier = .32f;
            PanelAt(shell, "Header shadow", new Color(.12f,.13f,.12f,.85f), 0, 0, 480, 164);
            Card(shell, "Inventory base", 12, 602, 456, 348, Hex("f1f0ea"));
            Icon(shell, "pick4", 23, 21, 44, 44);
            var logoShadow = Label(shell, "КИРКА УДАЧИ", 81, 24, 320, 42, 32, Hex("161a1a"), FontStyle.Bold);
            Label(shell, "КИРКА УДАЧИ", 78, 21, 320, 42, 32, Hex("fff4b4"), FontStyle.Bold);
            Label(shell, "КОПАЙ • УЛУЧШАЙ • ИСПЫТАЙ УДАЧУ", 80, 63, 326, 20, 14, Hex("b8cc99"));
            var sound = ButtonAt(shell, "♪", 411, 25, 45, 45, ToggleSound, Hex("b5b7ae"), Hex("222923"));
            sound.name = "Toggle sound"; soundLabel = sound.GetComponentInChildren<Text>();

            Card(shell, "Coin wallet", 24, 92, 270, 62, Hex("d9d8cb"));
            Icon(shell, "coin", 36, 103, 41, 41);
            coinLabel = Label(shell, "", 91, 98, 190, 29, 26, Ink, FontStyle.Bold);
            Label(shell, "Монеты", 92, 127, 182, 22, 16, Muted);
            Card(shell, "Ticket wallet", 306, 92, 150, 62, Hex("d9d8cb"));
            Icon(shell, "ticket", 316, 107, 39, 32);
            ticketLabel = Label(shell, "", 367, 98, 77, 29, 26, Ink, FontStyle.Bold);
            Label(shell, "Жетоны", 368, 127, 76, 22, 16, Muted);

            minePage = MakeRect(shell, "Clicker page", 0, 168, 480, 614).gameObject;
            rollPage = MakeRect(shell, "Roulette page", 0, 168, 480, 614).gameObject;
            BuildMine(minePage.transform); BuildRoll(rollPage.transform);

            Card(shell, "Contract card", 24, 798, 432, 73, Hex("d4ccaf"));
            Icon(shell, "ticket", 38, 818, 35, 31);
            Label(shell, "Контракт шахтёра", 88, 806, 248, 26, 20, Ink, FontStyle.Bold);
            contractDetail = Label(shell, "", 88, 836, 247, 22, 15, Muted);
            PanelAt(shell, "Contract track", Hex("7d7966"), 88, 862, 240, 4);
            contractFill = PanelAt(shell, "Contract progress", Hex("79ba45"), 88, 862, 0, 4);
            contractButton = ButtonAt(shell, "", 347, 817, 96, 38, ClaimContract, Hex("dcb979"), Hex("30291e"));
            contractButton.name = "Claim contract"; contractLabel = contractButton.GetComponentInChildren<Text>();
            contractLabel.fontSize = 16;

            Card(shell, "Navigation", 24, 888, 432, 58, Hex("9d9f98"));
            mineTab = ButtonAt(shell, "ШАХТА", 33, 893, 205, 45, () => ShowPage(false), Panel, Ink);
            rollTab = ButtonAt(shell, "РУЛЕТКА", 242, 893, 205, 45, () => ShowPage(true), Panel, Ink);
            mineTab.name = "КЛИКЕР"; rollTab.name = "РУЛЕТКА ШАХТЁРА";
            mineNavMark = PanelAt(shell, "Mine selected", Hex("d8ed88"), 91, 940, 86, 3);
            rollNavMark = PanelAt(shell, "Roll selected", Hex("d8ed88"), 299, 940, 86, 3);
            var notice = Card(shell, "Progress notification", 24, 10, 374, 76, Hex("cfdfa1"));
            noticeRect = notice.rectTransform; noticeGroup = notice.gameObject.AddComponent<CanvasGroup>();
            noticeGroup.alpha = 0; noticeGroup.blocksRaycasts = false; noticeGroup.interactable = false;
            noticeTitle = Label(notice.transform, "", 10, 4, 354, 28, 22, Ink, FontStyle.Bold, TextAnchor.MiddleCenter);
            noticeDetail = Label(notice.transform, "", 10, 32, 354, 40, 16, Ink, FontStyle.Normal, TextAnchor.MiddleCenter);
            if (FindObjectOfType<EventSystem>() == null)
            {
                var events = new GameObject("Event System", typeof(EventSystem), typeof(StandaloneInputModule)); events.transform.SetParent(transform, false);
            }
            FitScreen();
        }

        private void BuildMine(Transform page)
        {
            var sceneTitle = Card(page, "Scene header", 24, 0, 432, 50, Hex("76975d"));
            levelLabel = Label(page, "", 38, 4, 242, 30, 22, Hex("f2f6dc"), FontStyle.Bold);
            stageBadge = Label(page, "", 280, 7, 160, 25, 16, Hex("eef5d9"), FontStyle.Normal, TextAnchor.MiddleRight);
            PanelAt(page, "Experience track", Hex("35432a"), 38, 39, 404, 5);
            experienceFill = PanelAt(page, "Experience progress", Hex("c5ed77"), 38, 39, 0, 5);
            mineBackdrop = Hero(page, "Mine illustration", 24, 58, 432, 280);
            var hero = mineBackdrop.transform.parent;
            Card(hero, "Ore badge", 12, 12, 190, 29, Hex("d4dfc5"));
            veinLabel = Label(hero, "", 12, 12, 190, 29, 18, Ink, FontStyle.Normal, TextAnchor.MiddleCenter);
            PanelAt(hero, "Ore health track", Hex("3c4935"), 18, 44, 178, 6);
            oreFill = PanelAt(hero, "Ore health", Hex("d7c990"), 18, 44, 178, 6);
            Card(hero, "Power badge", 281, 12, 138, 29, Hex("c4dc9d"));
            powerLabel = Label(hero, "", 281, 12, 138, 29, 18, Hex("24452d"), FontStyle.Bold, TextAnchor.MiddleCenter);
            var rockButton = ButtonAt(hero, "", 82, 63, 215, 180, () => { if (!holdRepeated) Mine(); holdRepeated = false; }, Color.clear, Ink);
            rockButton.name = "Mine rock"; rockButton.transition = Selectable.Transition.None;
            // Keep a near-transparent graphic so the hit target survives Canvas transparent-mesh culling.
            rockButton.image.sprite = null; rockButton.image.type = Image.Type.Simple; rockButton.image.color = new Color(1,1,1,.001f);
            rock = rockButton.GetComponent<RectTransform>(); rock.pivot = new Vector2(.5f, .5f); rock.anchoredPosition += new Vector2(107.5f, -90);
            var trigger = rock.gameObject.AddComponent<EventTrigger>();
            AddPointer(trigger, EventTriggerType.PointerDown, () => { holding = true; holdRepeated = false; nextHold = Time.unscaledTime + .28f; });
            AddPointer(trigger, EventTriggerType.PointerUp, () => holding = false);
            AddPointer(trigger, EventTriggerType.PointerExit, () => { holding = false; holdRepeated = false; });
            clickPick = Icon(hero, "pick4", 264, 115, 96, 96);
            clickPick.rectTransform.pivot = new Vector2(.2f, .12f);
            clickPick.rectTransform.anchoredPosition += new Vector2(19.2f, -84.48f);
            pickHome = clickPick.rectTransform.anchoredPosition;
            Card(hero, "Tap hint", 12, 240, 408, 29, new Color(.2f,.24f,.18f,.92f));
            mineStatus = Label(hero, "Нажимай или удерживай блок", 12, 240, 408, 29, 17, Hex("eff5dc"), FontStyle.Normal, TextAnchor.MiddleCenter);

            for (int i = 0; i < 5; i++)
            {
                int x = 24 + i * 87;
                oreSlots[i] = Card(page, "Ore collection " + i, x, 346, 84, 46, Panel);
                oreIcons[i] = Icon(page, "tile-" + MineGame.OreKeyAt(i), x + 30, 351, 24, 18);
                oreNames[i] = Label(page, "", x + 2, 371, 80, 20, 16, Ink, FontStyle.Normal, TextAnchor.MiddleCenter);
            }

            Card(page, "Next ticket", 24, 400, 432, 48, Hex("c5c7b8"));
            Icon(page, "ticket", 35, 407, 32, 30);
            Label(page, "Следующий жетон", 80, 400, 228, 27, 20, Ink);
            progressLabel = Label(page, "", 315, 403, 127, 24, 18, Muted, FontStyle.Normal, TextAnchor.MiddleRight);
            PanelAt(page, "Ticket track", Hex("4d5449"), 80, 433, 360, 7);
            ticketFill = PanelAt(page, "Ticket progress", Hex("7dc844"), 80, 433, 0, 7);

            Label(page, "УЛУЧШЕНИЯ", 24, 458, 220, 28, 24, Ink, FontStyle.Bold);
            upgradeHint = Label(page, "", 243, 461, 213, 22, 16, Muted, FontStyle.Normal, TextAnchor.MiddleRight);
            Card(page, "Pick workshop card", 24, 492, 211, 94, Panel);
            workshopPick = Icon(page, "pick1", 36, 500, 37, 37);
            pickTitle = Label(page, "Кирка", 83, 493, 138, 27, 20, Ink, FontStyle.Bold);
            pickDetail = Label(page, "", 83, 519, 141, 22, 16, Muted);
            upgradeButton = ButtonAt(page, "", 36, 548, 187, 30, Upgrade, Hex("6fcbbb"), Hex("16382f"));
            upgradeButton.name = "Upgrade pickaxe"; upgradeLabel = upgradeButton.GetComponentInChildren<Text>();
            Card(page, "Forge workshop card", 245, 492, 211, 94, Panel);
            Icon(page, "forge", 257, 500, 37, 37);
            Label(page, "Кузница", 304, 493, 136, 27, 22, Ink, FontStyle.Bold);
            forgeDetail = Label(page, "", 304, 519, 140, 22, 16, Muted);
            forgeButton = ButtonAt(page, "", 257, 548, 187, 30, Forge, Hex("e5c488"), Hex("3a3020"));
            forgeButton.name = "Upgrade forge"; forgeLabel = forgeButton.GetComponentInChildren<Text>();
            nextUpgrade = Label(page, "", 24, 596, 432, 22, 16, Muted, FontStyle.Normal, TextAnchor.MiddleCenter);
        }

        private void BuildRoll(Transform page)
        {
            Card(page, "Expedition header", 24, 0, 432, 60, Hex("76975d"));
            Label(page, "РУЛЕТКА ШАХТЁРА", 36, 2, 308, 34, 24, Hex("f2f6dc"), FontStyle.Bold);
            stratumLabel = Label(page, "", 37, 34, 300, 23, 16, Hex("e1e8ce"));
            Card(page, "Hardness badge", 340, 16, 116, 29, Panel);
            floorHint = Label(page, "", 340, 16, 116, 29, 16, Ink, FontStyle.Bold, TextAnchor.MiddleCenter);
            rollBackdrop = Hero(page, "Expedition illustration", 24, 70, 432, 382);
            string[] layers = { "grass", "stone", "iron", "gold", "diamond" };
            for (int column = 0; column < 5; column++)
            {
                int x = 41 + column * 81;
                reels[column] = Card(page, "Reel " + column, x, 81, 74, 73, Hex("cdd0c6"));
                picks[column] = Icon(page, "pick1", x + 9, 83, 48, 48); picks[column].preserveAspect = true;
                powers[column] = Label(page, "1 сила", x, 131, 74, 20, 16, Ink, FontStyle.Normal, TextAnchor.MiddleCenter);
                for (int row = 0; row < 5; row++) blocks[column, row] = Icon(page, "tile-" + layers[row], x, 165 + row * 44, 74, 41);
                cracks[column] = Icon(page, "crack2", x, 165, 74, 41);
                chests[column] = Icon(page, "chest", x + 11, 389, 52, 48); chests[column].preserveAspect = true;
            }
            PanelAt(page, "Board track", Hex("343e2d"), 41, 445, 398, 6);
            boardFill = PanelAt(page, "Board completion", Hex("83c94a"), 41, 445, 0, 6);
            Card(page, "Result panel", 24, 458, 432, 43, Hex("e4e3d4"));
            rollStatus = Label(page, "Пять кирок. Пять сундуков.", 32, 463, 416, 34, 20, Ink, FontStyle.Normal, TextAnchor.MiddleCenter);
            spinButton = ButtonAt(page, "ЗАПУСТИТЬ КИРКИ", 24, 515, 432, 58, Spin, Hex("b3d17a"), Hex("293e20"));
            spinButton.name = "ЗАПУСТИТЬ КИРКИ";
            ButtonAt(page, "Шансы и правила", 24, 589, 246, 34, ShowOdds, Panel, Muted).name = "ШАНСЫ И ПРАВИЛА";
            ButtonAt(page, "Скорость ×1", 280, 589, 176, 34, ToggleSpeed, Panel, Muted).name = "Toggle speed";
        }

        private Image Hero(Transform parent, string name, float x, float y, float width, float height)
        {
            var window = Card(parent, name, x, y, width, height, Hex("737c6e"));
            var mask = window.gameObject.AddComponent<Mask>(); mask.showMaskGraphic = false;
            var image = Icon(window.transform, "tex-stone", 0, 0, width, height); image.type = Image.Type.Tiled; image.pixelsPerUnitMultiplier = .4f;
            image.color = Hex("69736a");
            if (name == "Mine illustration")
            {
                var world = new GameObject("Voxel mining world"); world.transform.SetParent(transform, false);
                stage = world.AddComponent<VoxelStage>(); stage.Initialize(art);
                var rect = MakeRect(window.transform, "Live block world", 0, 0, width, height);
                var display = rect.gameObject.AddComponent<RawImage>(); display.texture = stage.Target; display.raycastTarget = false;
            }
            return image;
        }

        private void ShowOdds()
        {
            if (modal != null) return;
            holding = false;
            modal = MakeRect(shell, "Rules modal", 0, 0, 480, 960).gameObject;
            var dim = PanelAt(modal.transform, "Modal overlay", new Color(.12f, .15f, .1f, .82f), 0, 0, 480, 960); dim.raycastTarget = true;
            Card(modal.transform, "Rules card", 24, 164, 432, 632, Panel);
            Label(modal.transform, "Каждая кирка — шанс", 47, 185, 386, 38, 23, Ink, FontStyle.Bold);
            Label(modal.transform, "Кузница уровня " + game.Progress.forgeLevel, 48, 230, 380, 22, 12, Muted);
            int[] chances = { 100 - game.IronChance - game.DiamondChance, game.IronChance, game.DiamondChance };
            for (int i = 0; i < 3; i++)
            {
                int x = 47 + i * 132;
                Card(modal.transform, "Rarity " + i, x, 271, 121, 118, Hex("a8b0a1"));
                Icon(modal.transform, "pick" + new[] { 1, 2, 4 }[i], x + 28, 282, 65, 56).preserveAspect = true;
                Label(modal.transform, chances[i] + "%", x, 345, 121, 27, 24, new[] { Muted, Ink, Hex("1c675e") }[i], FontStyle.Bold, TextAnchor.MiddleCenter);
            }
            Label(modal.transform, "Сила кирок: " + (1 + game.PowerBonus) + " / " + (2 + game.PowerBonus) + " / " + (4 + game.PowerBonus) + "\n\nПрочность породы: " + game.Hardness + ". Урон сохраняется.\nБлок: " + game.BlockReward + " монет · сундук: " + game.ChestReward + " монет\nВесь пласт: +" + game.FloorReward + " монет и жетон\n\nКирки в открытых столбцах дают лом:\n2 монеты за единицу силы.\nКузница повышает редкость и силу кирок.\n12 ударов в шахте дают новый жетон.", 48, 415, 384, 265, 18, Ink);
            ButtonAt(modal.transform, "Продолжить добычу", 47, 713, 386, 55, () => { Destroy(modal); modal = null; }, Hex("e7c78d"), Hex("362b1b")).name = "ПОНЯТНО";
        }

        private Image Card(Transform parent, string name, float x, float y, float width, float height, Color color)
        {
            var image = PanelAt(parent, name, color, x, y, width, height); image.sprite = art.Get("ui-card"); image.type = Image.Type.Sliced; image.pixelsPerUnitMultiplier=.5f; return image;
        }

        private static void AddPointer(EventTrigger trigger, EventTriggerType type, UnityEngine.Events.UnityAction action)
        {
            var entry = new EventTrigger.Entry { eventID = type }; entry.callback.AddListener(_ => action()); trigger.triggers.Add(entry);
        }

        private IEnumerator FloatReward(Transform parent, float x, float y)
        {
            var label = Label(parent, "", x, y, 180, 54, 19, Hex("fff0b8"), FontStyle.Bold, TextAnchor.MiddleCenter);
            label.name = "Mining rewards"; miningGain = label;
            var outline = label.gameObject.AddComponent<Outline>(); outline.effectColor = Hex("273422"); outline.effectDistance = new Vector2(1, -1);
            float elapsed = 0;
            while (elapsed < .65f)
            {
                elapsed += Time.unscaledDeltaTime;
                label.text = "+" + ShortAmount(gainCoins) + " монет\n+" + gainExperience + " опыта";
                label.rectTransform.anchoredPosition = new Vector2(x, -y + (1 - Mathf.Pow(1 - elapsed / .65f, 3)) * 54);
                label.color = new Color(1, .94f, .72f, Mathf.Clamp01((.65f - elapsed) / .23f)); yield return null;
            }
            Destroy(label.gameObject); miningGain = null;
        }

        private static Color Hex(string hex) { ColorUtility.TryParseHtmlString("#" + hex, out var c); return c; }
        private static string ShortAmount(long value)
        {
            if (value >= 1000000) return (value / 1000000d).ToString("0.#") + " млн";
            if (value >= 10000) return (value / 1000d).ToString("0.#") + " тыс.";
            return value.ToString("N0");
        }
        private static RectTransform MakeRect(Transform parent, string name, float x, float y, float width, float height)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1); rect.pivot = new Vector2(0, 1);
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
            var text = rect.gameObject.AddComponent<Text>(); text.font = weight == FontStyle.Bold ? interfaceBold : interfaceFont;
            text.text = value; text.fontSize = size < 16 ? 16 : size; text.fontStyle = FontStyle.Normal; text.color = color; text.alignment = align;
            if (text.font.material.mainTexture != null) text.font.material.mainTexture.filterMode = FilterMode.Point;
            text.raycastTarget = false; text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }
        private Button ButtonAt(Transform parent, string value, float x, float y, float width, float height, UnityEngine.Events.UnityAction action, Color back, Color front)
        {
            var image = Card(parent, value.Length == 0 ? "Button" : value, x, y, width, height, back); image.raycastTarget = true;
            var button = image.gameObject.AddComponent<Button>(); button.targetGraphic = image;
            var colors = button.colors; colors.highlightedColor = new Color(1.05f, 1.05f, 1.05f); colors.pressedColor = new Color(.76f, .8f, .72f); colors.disabledColor = new Color(.84f, .84f, .84f, 1); colors.fadeDuration = 0; button.colors = colors;
            Label(image.transform, value, 8, 0, width - 16, height, 20, front, FontStyle.Bold, TextAnchor.MiddleCenter);
            var navigation = button.navigation; navigation.mode = Navigation.Mode.None; button.navigation = navigation;
            button.onClick.AddListener(action); return button;
        }
    }
}
