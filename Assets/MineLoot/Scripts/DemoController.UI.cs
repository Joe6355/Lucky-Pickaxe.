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

        private void BuildInterface()
        {
            interfaceFont = Resources.Load<Font>("Fonts/Manrope") ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            interfaceBold = Resources.Load<Font>("Fonts/Manrope-Semibold") ?? interfaceFont;
            var canvasObject = new GameObject("Game UI", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            canvas = canvasObject.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var backdrop = PanelAt(canvasObject.transform, "Backdrop", Back, 0, 0, 480, 960).rectTransform;
            backdrop.anchorMin = Vector2.zero; backdrop.anchorMax = Vector2.one; backdrop.sizeDelta = Vector2.zero; backdrop.anchoredPosition = Vector2.zero;
            shell = MakeRect(canvasObject.transform, "Portrait game", 0, 0, 480, 960);
            shell.anchorMin = shell.anchorMax = new Vector2(.5f, .5f); shell.pivot = new Vector2(.5f, .5f); shell.anchoredPosition = Vector2.zero;
            PanelAt(shell, "Surface", Back, 0, 0, 480, 960);
            Icon(shell, "pick4", 23, 21, 44, 44);
            Label(shell, "Кирка удачи", 80, 20, 298, 33, 24, Ink, FontStyle.Bold);
            Label(shell, "LUCKY PICKAXE", 82, 57, 280, 18, 10, Muted);
            var sound = ButtonAt(shell, "♪", 411, 24, 45, 45, ToggleSound, Panel, Ink);
            sound.name = "Toggle sound"; soundLabel = sound.GetComponentInChildren<Text>();

            Card(shell, "Coin wallet", 24, 92, 270, 62, Panel);
            Icon(shell, "coin", 36, 103, 41, 41);
            coinLabel = Label(shell, "", 91, 98, 190, 29, 23, Ink, FontStyle.Bold);
            Label(shell, "Монеты", 92, 129, 182, 17, 10, Muted);
            Card(shell, "Ticket wallet", 306, 92, 150, 62, Panel);
            Icon(shell, "ticket", 316, 107, 39, 32);
            ticketLabel = Label(shell, "", 367, 98, 77, 29, 22, Ink, FontStyle.Bold);
            Label(shell, "Жетоны", 368, 129, 76, 17, 10, Muted);

            minePage = MakeRect(shell, "Clicker page", 0, 168, 480, 614).gameObject;
            rollPage = MakeRect(shell, "Roulette page", 0, 168, 480, 614).gameObject;
            BuildMine(minePage.transform); BuildRoll(rollPage.transform);

            Card(shell, "Contract card", 24, 798, 432, 73, Panel);
            Icon(shell, "ticket", 38, 818, 35, 31);
            Label(shell, "Контракт шахтёра", 88, 808, 248, 23, 14, Ink, FontStyle.Bold);
            contractDetail = Label(shell, "", 88, 836, 247, 18, 10, Muted);
            PanelAt(shell, "Contract track", Hex("2b3b4d"), 88, 861, 240, 3);
            contractFill = PanelAt(shell, "Contract progress", Hex("e8c88a"), 88, 861, 0, 3);
            contractButton = ButtonAt(shell, "", 347, 817, 96, 38, ClaimContract, Hex("dcb979"), Hex("30291e"));
            contractButton.name = "Claim contract"; contractLabel = contractButton.GetComponentInChildren<Text>();
            contractLabel.fontSize = 12;

            Card(shell, "Navigation", 24, 888, 432, 58, Hex("152332"));
            mineTab = ButtonAt(shell, "Добыча", 33, 893, 205, 45, () => ShowPage(false), Panel, Ink);
            rollTab = ButtonAt(shell, "Экспедиция", 242, 893, 205, 45, () => ShowPage(true), Panel, Ink);
            mineTab.name = "КЛИКЕР"; rollTab.name = "РУЛЕТКА ШАХТЁРА";
            mineNavMark = PanelAt(shell, "Mine selected", Hex("76d7cc"), 91, 941, 86, 2);
            rollNavMark = PanelAt(shell, "Roll selected", Hex("76d7cc"), 299, 941, 86, 2);
            if (FindObjectOfType<EventSystem>() == null)
            {
                var events = new GameObject("Event System", typeof(EventSystem), typeof(StandaloneInputModule)); events.transform.SetParent(transform, false);
            }
            FitScreen();
        }

        private void BuildMine(Transform page)
        {
            veinLabel = Label(page, "", 24, 12, 318, 31, 21, Ink, FontStyle.Bold);
            var badge = Card(page, "Stage badge", 353, 14, 103, 28, Panel);
            stageBadge = Label(badge.transform, "", 0, 0, 103, 28, 10, Hex("d7b985"), FontStyle.Bold, TextAnchor.MiddleCenter);
            mineBackdrop = Hero(page, "Mine illustration", 24, 58, 432, 300);
            var hero = mineBackdrop.transform.parent;
            mineGlow = Icon(hero, "glow", 79, 7, 280, 280); mineGlow.color = new Color(.25f, .8f, .83f, .6f);
            Card(hero, "Power badge", 285, 14, 132, 28, new Color(.04f, .09f, .13f, .88f));
            powerLabel = Label(hero, "", 285, 14, 132, 28, 11, Hex("8de3d6"), FontStyle.Bold, TextAnchor.MiddleCenter);
            Label(hero, "АЛМАЗНАЯ ЖИЛА", 17, 19, 244, 22, 10, Hex("b5c9cf"));
            var rockButton = ButtonAt(hero, "", 109, 54, 215, 208, () => { if (!holdRepeated) Mine(); holdRepeated = false; }, Color.white, Ink);
            rockButton.name = "Mine rock"; rockButton.transition = Selectable.Transition.None;
            rockButton.image.sprite = art.Get("hero-ore"); rockButton.image.type = Image.Type.Simple; rockButton.image.preserveAspect = true;
            rock = rockButton.GetComponent<RectTransform>(); rock.pivot = new Vector2(.5f, .5f); rock.anchoredPosition += new Vector2(107.5f, -104);
            rockCrack = Icon(rock, "crack1", 30, 30, 150, 150); rockCrack.color = Color.clear;
            var trigger = rock.gameObject.AddComponent<EventTrigger>();
            AddPointer(trigger, EventTriggerType.PointerDown, () => { holding = true; holdRepeated = false; nextHold = Time.unscaledTime + .28f; });
            AddPointer(trigger, EventTriggerType.PointerUp, () => holding = false);
            AddPointer(trigger, EventTriggerType.PointerExit, () => { holding = false; holdRepeated = false; });
            clickPick = Icon(hero, "pick4", 282, 93, 86, 86);
            Card(hero, "Tap hint", 78, 262, 276, 26, new Color(.035f, .075f, .1f, .86f));
            mineStatus = Label(hero, "Нажимай или удерживай руду", 78, 262, 276, 26, 11, Ink, FontStyle.Normal, TextAnchor.MiddleCenter);

            Card(page, "Next ticket", 24, 370, 432, 60, Hex("122633"));
            Icon(page, "ticket", 35, 382, 32, 30);
            Label(page, "Следующий жетон", 80, 378, 228, 24, 13, Ink);
            progressLabel = Label(page, "", 315, 380, 127, 20, 11, Muted, FontStyle.Normal, TextAnchor.MiddleRight);
            PanelAt(page, "Ticket track", Hex("2b3b4d"), 80, 414, 360, 4);
            ticketFill = PanelAt(page, "Ticket progress", Hex("69cfc6"), 80, 414, 0, 4);

            Label(page, "Мастерская", 24, 443, 220, 26, 17, Ink, FontStyle.Bold);
            upgradeHint = Label(page, "", 243, 446, 213, 22, 11, Muted, FontStyle.Normal, TextAnchor.MiddleRight);
            Card(page, "Pick workshop card", 24, 480, 211, 106, Panel);
            Icon(page, "pick2", 36, 488, 37, 37);
            Label(page, "Кирка", 83, 484, 138, 24, 15, Ink, FontStyle.Bold);
            pickDetail = Label(page, "+4 монеты / удар", 83, 510, 141, 17, 10, Muted);
            upgradeButton = ButtonAt(page, "", 36, 539, 187, 35, Upgrade, Hex("6fcbbb"), Hex("16382f"));
            upgradeButton.name = "Upgrade pickaxe"; upgradeLabel = upgradeButton.GetComponentInChildren<Text>();
            Card(page, "Forge workshop card", 245, 480, 211, 106, Panel);
            Icon(page, "forge", 257, 488, 37, 37);
            Label(page, "Кузница", 304, 484, 136, 24, 15, Ink, FontStyle.Bold);
            forgeDetail = Label(page, "", 304, 510, 140, 17, 10, Muted);
            forgeButton = ButtonAt(page, "", 257, 539, 187, 35, Forge, Hex("e5c488"), Hex("3a3020"));
            forgeButton.name = "Upgrade forge"; forgeLabel = forgeButton.GetComponentInChildren<Text>();
            Label(page, "Каждые 3 пласта — реликвия и +1 к добыче", 24, 598, 432, 17, 10, Hex("9babb9"), FontStyle.Normal, TextAnchor.MiddleCenter);
        }

        private void BuildRoll(Transform page)
        {
            Label(page, "Экспедиция", 24, 4, 300, 32, 24, Ink, FontStyle.Bold);
            stratumLabel = Label(page, "", 25, 39, 308, 18, 10, Muted);
            Card(page, "Hardness badge", 340, 16, 116, 29, Panel);
            floorHint = Label(page, "", 340, 16, 116, 29, 10, Hex("d7b985"), FontStyle.Bold, TextAnchor.MiddleCenter);
            rollBackdrop = Hero(page, "Expedition illustration", 24, 70, 432, 382);
            PanelAt(rollBackdrop.transform.parent, "Shaft shade", new Color(.025f, .055f, .08f, .8f), 0, 0, 432, 382);
            string[] layers = { "grass", "stone", "iron", "gold", "diamond" };
            for (int column = 0; column < 5; column++)
            {
                int x = 41 + column * 81;
                Card(page, "Reel " + column, x, 81, 74, 73, Hex("1c3040"));
                picks[column] = Icon(page, "pick1", x + 7, 83, 60, 48); picks[column].preserveAspect = true;
                powers[column] = Label(page, "1 сила", x, 131, 74, 18, 10, Hex("a9c0cb"), FontStyle.Normal, TextAnchor.MiddleCenter);
                for (int row = 0; row < 5; row++) blocks[column, row] = Icon(page, "tile-" + layers[row], x, 165 + row * 44, 74, 41);
                cracks[column] = Icon(page, "crack2", x, 165, 74, 41);
                chests[column] = Icon(page, "chest", x + 11, 389, 52, 48); chests[column].preserveAspect = true;
            }
            PanelAt(page, "Board track", Hex("29384a"), 41, 445, 398, 4);
            boardFill = PanelAt(page, "Board completion", Hex("dcbc83"), 41, 445, 0, 4);
            rollStatus = Label(page, "Пять кирок. Пять путей к сокровищам.", 24, 465, 432, 34, 13, Ink, FontStyle.Normal, TextAnchor.MiddleCenter);
            spinButton = ButtonAt(page, "ЗАПУСТИТЬ КИРКИ", 24, 515, 432, 58, Spin, Hex("e8c78b"), Hex("362b1b"));
            spinButton.name = "ЗАПУСТИТЬ КИРКИ";
            ButtonAt(page, "Шансы и правила", 24, 589, 246, 34, ShowOdds, Panel, Muted).name = "ШАНСЫ И ПРАВИЛА";
            ButtonAt(page, "Скорость ×1", 280, 589, 176, 34, ToggleSpeed, Panel, Muted).name = "Toggle speed";
        }

        private Image Hero(Transform parent, string name, float x, float y, float width, float height)
        {
            var window = Card(parent, name, x, y, width, height, Color.white);
            var mask = window.gameObject.AddComponent<Mask>(); mask.showMaskGraphic = false;
            var image = Icon(window.transform, "hero-cave", 0, 0, width, height);
            image.rectTransform.anchorMin = image.rectTransform.anchorMax = new Vector2(.5f, .5f);
            image.rectTransform.pivot = new Vector2(.5f, .5f); image.rectTransform.anchoredPosition = Vector2.zero;
            var aspect = image.gameObject.AddComponent<AspectRatioFitter>(); aspect.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            aspect.aspectRatio = image.sprite.rect.width / image.sprite.rect.height;
            return image;
        }

        private void ShowOdds()
        {
            if (modal != null) return;
            holding = false;
            modal = MakeRect(shell, "Rules modal", 0, 0, 480, 960).gameObject;
            var dim = PanelAt(modal.transform, "Modal overlay", new Color(.015f, .03f, .05f, .93f), 0, 0, 480, 960); dim.raycastTarget = true;
            Card(modal.transform, "Rules card", 24, 164, 432, 632, Panel);
            Label(modal.transform, "Каждая кирка — шанс", 47, 185, 386, 38, 23, Ink, FontStyle.Bold);
            Label(modal.transform, "Кузница уровня " + game.Progress.forgeLevel, 48, 230, 380, 22, 12, Muted);
            int[] chances = { 100 - game.IronChance - game.DiamondChance, game.IronChance, game.DiamondChance };
            for (int i = 0; i < 3; i++)
            {
                int x = 47 + i * 132;
                Card(modal.transform, "Rarity " + i, x, 271, 121, 118, Hex("142332"));
                Icon(modal.transform, "pick" + new[] { 1, 2, 4 }[i], x + 28, 282, 65, 56).preserveAspect = true;
                Label(modal.transform, chances[i] + "%", x, 345, 121, 27, 20, new[] { Muted, Ink, Hex("7ad8d1") }[i], FontStyle.Bold, TextAnchor.MiddleCenter);
            }
            Label(modal.transform, "Сила кирок: " + (1 + game.PowerBonus) + " / " + (2 + game.PowerBonus) + " / " + (4 + game.PowerBonus) + "\n\nПрочность породы: " + game.Hardness + ". Урон сохраняется.\nБлок: " + game.BlockReward + " монет · сундук: " + game.ChestReward + " монет\nВесь пласт: +" + game.FloorReward + " монет и жетон\n\nКирки в открытых столбцах дают лом:\n2 монеты за единицу силы.\nКузница повышает редкость и силу кирок.\n12 ударов в шахте дают новый жетон.", 48, 415, 384, 249, 13, Hex("b4c4d0"));
            ButtonAt(modal.transform, "Продолжить добычу", 47, 713, 386, 55, () => { Destroy(modal); modal = null; }, Hex("e7c78d"), Hex("362b1b")).name = "ПОНЯТНО";
        }

        private Image Card(Transform parent, string name, float x, float y, float width, float height, Color color)
        {
            var image = PanelAt(parent, name, color, x, y, width, height); image.sprite = art.Get("ui-card"); image.type = Image.Type.Sliced; return image;
        }

        private static void AddPointer(EventTrigger trigger, EventTriggerType type, UnityEngine.Events.UnityAction action)
        {
            var entry = new EventTrigger.Entry { eventID = type }; entry.callback.AddListener(_ => action()); trigger.triggers.Add(entry);
        }

        private IEnumerator FloatReward(string value, Transform parent, float x, float y)
        {
            effects++;
            var label = Label(parent, value, x, y, 110, 30, 22, Hex("f2d69d"), FontStyle.Bold, TextAnchor.MiddleCenter);
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
            text.text = value; text.fontSize = size < 12 ? 12 : size; text.fontStyle = FontStyle.Normal; text.color = color; text.alignment = align;
            text.raycastTarget = false; text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }
        private Button ButtonAt(Transform parent, string value, float x, float y, float width, float height, UnityEngine.Events.UnityAction action, Color back, Color front)
        {
            var image = Card(parent, value.Length == 0 ? "Button" : value, x, y, width, height, back); image.raycastTarget = true;
            var button = image.gameObject.AddComponent<Button>(); button.targetGraphic = image;
            var colors = button.colors; colors.highlightedColor = new Color(1.05f, 1.05f, 1.05f); colors.pressedColor = new Color(.8f, .86f, .9f); colors.disabledColor = new Color(.65f, .72f, .8f, .8f); colors.fadeDuration = .1f; button.colors = colors;
            Label(image.transform, value, 8, 0, width - 16, height, 13, front, FontStyle.Bold, TextAnchor.MiddleCenter);
            var navigation = button.navigation; navigation.mode = Navigation.Mode.None; button.navigation = navigation;
            button.onClick.AddListener(action); return button;
        }
    }
}
