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
        private Text campLabel, forgeTitle;
        private readonly Image[] campSteps = new Image[5];
        private readonly Button[] workshopButtons = new Button[6];
        private readonly Text[] workshopLevels = new Text[6], workshopEffects = new Text[6], workshopStates = new Text[6];
        private readonly Image[] workshopProgress = new Image[6];
        private readonly Image[] workshopIcons = new Image[6];
        private readonly Text[] workshopTitles = new Text[6];
        private readonly GameObject[] workshopCards = new GameObject[6];
        private Button equipmentTab, productionTabButton, richVeinButton;
        private Text workshopFootnote, productionRateLabel, productionBreakdown, productionSynergy, productionTotal, richVeinTimer;
        private GameObject productionSummary;
        private bool productionTab;
        private Button offlineButton, offlineClaimButton;
        private Text offlineButtonLabel, offlineAmountLabel, offlineStateLabel;
        private bool offlineWindowOpen;
        private Image workshopNavMark;
        private Image experienceFill, oreFill, workshopPick;
        private readonly Image[] oreSlots = new Image[5], oreIcons = new Image[5], reels = new Image[5];
        private readonly Text[] oreNames = new Text[5];
        private readonly RectTransform[] reelWindows = new RectTransform[5];
        private readonly Image[,] reelSymbols = new Image[5, 3];
        private readonly Button[] reelButtons = new Button[5];
        private readonly Image[] pickBorders = new Image[5];
        private Text rollGain, boardText;
        private RectTransform noticeRect;
        private CanvasGroup noticeGroup;
        private VoxelStage stage, shaft;
        private const float ShaftWindowHeight = 417;
        private Button abandonButton;
        private Image rollRewardIcon;
        private Button milestoneButton, milestonePrevious, milestoneNext;
        private Text milestoneButtonLabel, milestoneSummary, milestonePageLabel;
        private readonly Image[] milestoneIcons = new Image[4], milestoneCards = new Image[4], milestoneFills = new Image[4];
        private readonly Text[] milestoneTitles = new Text[4], milestoneDetails = new Text[4], milestoneRewards = new Text[4], milestoneValues = new Text[4];
        private readonly Button[] milestoneClaimButtons = new Button[4];
        private int milestonePage;
        private bool milestoneBookOpen;
        private Button prestigeButton, prestigeResetButton, prestigeConfirmButton;
        private Text prestigeSummary, prestigeState, prestigeReward, prestigeLosses, prestigeStart;
        private GameObject prestigeTree, prestigeConfirmation;
        private bool prestigeWindowOpen;
        private readonly Image[] prestigeNodes = new Image[9], prestigeLinks = new Image[9];
        private readonly Button[] prestigeBuyButtons = new Button[9];

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
            workshopPage = MakeRect(shell, "Workshop page", 0, 168, 480, 614).gameObject;
            BuildMine(minePage.transform); BuildRoll(rollPage.transform); BuildWorkshop(workshopPage.transform);

            Card(shell, "Contract card", 24, 798, 432, 73, Hex("d4ccaf"));
            Icon(shell, "ticket", 38, 818, 35, 31);
            Label(shell, "КОНТРАКТ", 88, 806, 153, 26, 20, Ink, FontStyle.Bold);
            milestoneButton = ButtonAt(shell, "ЦЕЛИ", 247, 803, 96, 27, ShowMilestones, Hex("a8b78e"), Ink);
            milestoneButton.name = "Open milestones"; milestoneButtonLabel = milestoneButton.GetComponentInChildren<Text>(); milestoneButtonLabel.fontSize = 16;
            contractDetail = Label(shell, "", 88, 836, 247, 22, 15, Muted);
            PanelAt(shell, "Contract track", Hex("7d7966"), 88, 862, 240, 4);
            contractFill = PanelAt(shell, "Contract progress", Hex("79ba45"), 88, 862, 0, 4);
            contractButton = ButtonAt(shell, "", 347, 817, 96, 38, ClaimContract, Hex("dcb979"), Hex("30291e"));
            contractButton.name = "Claim contract"; contractLabel = contractButton.GetComponentInChildren<Text>();
            contractLabel.fontSize = 16;

            Card(shell, "Navigation", 24, 888, 432, 58, Hex("9d9f98"));
            mineTab = ButtonAt(shell, "ШАХТА", 33, 893, 136, 45, () => ShowPage(false), Panel, Ink);
            rollTab = ButtonAt(shell, "РУЛЕТКА", 172, 893, 136, 45, () => ShowPage(true), Panel, Ink);
            workshopTab = ButtonAt(shell, "ПРОКАЧКА", 311, 893, 136, 45, ShowWorkshop, Panel, Ink);
            workshopTab.name = "МАСТЕРСКАЯ";
            mineTab.name = "КЛИКЕР"; rollTab.name = "РУЛЕТКА ШАХТЁРА";
            mineNavMark = PanelAt(shell, "Mine selected", Hex("d8ed88"), 68, 940, 66, 3);
            rollNavMark = PanelAt(shell, "Roll selected", Hex("d8ed88"), 207, 940, 66, 3);
            workshopNavMark = PanelAt(shell, "Workshop selected", Hex("d8ed88"), 346, 940, 66, 3);
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
            richVeinButton = ButtonAt(hero, "ЖИЛА!", 318, 66, 96, 42, ActivateRichVein, Color.white, Hex("35291c"));
            richVeinButton.image.sprite = art.Get("ui-brass"); richVeinButton.name = "Activate rich vein";
            richVeinTimer = Label(hero, "", 281, 43, 138, 22, 16, Hex("fff0aa"), FontStyle.Bold, TextAnchor.MiddleCenter);

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

            campLabel = Label(page, "ЛАГЕРЬ I", 24, 458, 220, 28, 24, Ink, FontStyle.Bold);
            for (int i = 0; i < campSteps.Length; i++) campSteps[i] = PanelAt(page, "Mine stage " + i, Hex("455742"), 24 + i * 40, 485, 36, 3);
            upgradeHint = Label(page, "", 243, 461, 213, 22, 16, Muted, FontStyle.Normal, TextAnchor.MiddleRight);
            Card(page, "Pick workshop card", 24, 492, 211, 94, Panel);
            workshopPick = Icon(page, "pick1", 36, 500, 37, 37);
            pickTitle = Label(page, "Кирка", 83, 493, 138, 27, 20, Ink, FontStyle.Bold);
            pickDetail = Label(page, "", 83, 519, 141, 22, 16, Muted);
            upgradeButton = ButtonAt(page, "", 36, 548, 187, 30, Upgrade, Hex("6fcbbb"), Hex("16382f"));
            upgradeButton.name = "Upgrade pickaxe"; upgradeLabel = upgradeButton.GetComponentInChildren<Text>();
            Card(page, "Forge workshop card", 245, 492, 211, 94, Panel);
            Icon(page, "forge", 257, 500, 37, 37);
            forgeTitle = Label(page, "Кузница 0", 304, 493, 136, 27, 22, Ink, FontStyle.Bold);
            forgeDetail = Label(page, "", 304, 519, 140, 22, 16, Muted);
            forgeButton = ButtonAt(page, "", 257, 548, 187, 30, Forge, Hex("e5c488"), Hex("3a3020"));
            forgeButton.name = "Upgrade forge"; forgeLabel = forgeButton.GetComponentInChildren<Text>();
            nextUpgrade = Label(page, "", 24, 596, 432, 22, 16, Muted, FontStyle.Normal, TextAnchor.MiddleCenter);
        }

        private void BuildRoll(Transform page)
        {
            Card(page, "Expedition header", 24, 0, 432, 60, Color.white, "ui-timber");
            Label(page, "РУЛЕТКА", 38, 2, 278, 34, 28, Hex("ffe6aa"), FontStyle.Bold);
            stratumLabel = Label(page, "", 39, 34, 306, 23, 16, Hex("d1c5a7"));
            Card(page, "Hardness badge", 348, 13, 96, 32, Hex("b7c4ba"));
            floorHint = Label(page, "", 348, 13, 96, 32, 16, Ink, FontStyle.Bold, TextAnchor.MiddleCenter);
            Card(page, "Shaft outer frame", 18, 65, 444, 429, Color.white, "ui-timber");
            rollBackdrop = Hero(page, "Expedition illustration", 24, 70, 432, ShaftWindowHeight);
            for (int column = 0; column < 5; column++)
            {
                int pickColumn = column;
                int x = 41 + column * 81;
                pickBorders[column] = PanelAt(page, "Selected pick " + column, Hex("c8ed79"), x - 3, 77, 80, 84);
                pickBorders[column].enabled = false;
                reels[column] = Card(page, "Reel " + column, x, 80, 74, 78, Color.white, "ui-timber");
                reels[column].raycastTarget = true;
                reelButtons[column] = reels[column].gameObject.AddComponent<Button>();
                reelButtons[column].targetGraphic = reels[column];
                reelButtons[column].transition = Selectable.Transition.None;
                var navigation = reelButtons[column].navigation; navigation.mode = Navigation.Mode.None; reelButtons[column].navigation = navigation;
                reelButtons[column].onClick.AddListener(() => SelectPick(pickColumn));
                var window = PanelAt(reels[column].transform, "Reel window " + column, Hex("202a2c"), 4, 5, 66, 51);
                window.gameObject.AddComponent<Mask>().showMaskGraphic = true; reelWindows[column] = window.rectTransform;
                for (int symbol = 0; symbol < 3; symbol++)
                {
                    reelSymbols[column, symbol] = Icon(window.transform, "pick1", 9, (symbol - 1) * 52, 48, 48);
                    reelSymbols[column, symbol].enabled = symbol == 1;
                }
                picks[column] = reelSymbols[column, 1];
                powers[column] = Label(page, "1 СИЛА", x, 136, 74, 20, 16, Hex("ead9ad"), FontStyle.Normal, TextAnchor.MiddleCenter);
            }
            boardText = Label(page, "", 41, 462, 398, 22, 16, Hex("ead9ad"), FontStyle.Normal, TextAnchor.MiddleCenter);
            PanelAt(page, "Board track", Hex("202b29"), 41, 485, 398, 3);
            boardFill = PanelAt(page, "Board completion", Hex("b5c96e"), 41, 485, 0, 3);
            Card(page, "Result panel", 24, 500, 432, 42, Color.white, "ui-timber");
            rollRewardIcon = Icon(page, "coin", 38, 508, 28, 28);
            rollGain = Label(page, "ДОБЕРИСЬ ДО СУНДУКОВ", 82, 498, 356, 27, 21, Hex("f5d88d"), FontStyle.Bold);
            rollStatus = Label(page, "Открой перестановку в мастерской", 82, 523, 356, 19, 16, Hex("c6c8b8"));
            spinButton = ButtonAt(page, "КИРКИ · 1 ЖЕТОН", 24, 550, 432, 44, Spin, Color.white, Hex("35291c"));
            spinButton.image.sprite = art.Get("ui-brass"); spinButton.GetComponentInChildren<Text>().fontSize = 22;
            spinButton.name = "ЗАПУСТИТЬ КИРКИ";
            ButtonAt(page, "Правила", 24, 600, 148, 25, ShowOdds, Panel, Muted).name = "ШАНСЫ И ПРАВИЛА";
            abandonButton = ButtonAt(page, "Сменить · 1 жетон", 180, 600, 172, 25, AbandonFloor, Panel, Muted);
            abandonButton.name = "Abandon floor"; abandonButton.GetComponentInChildren<Text>().fontSize = 16;
            ButtonAt(page, "×1", 360, 600, 96, 25, ToggleSpeed, Panel, Muted).name = "Toggle speed";
        }

        private void BuildWorkshop(Transform page)
        {
            Card(page, "Workshop header", 24, 0, 432, 58, Color.white, "ui-timber");
            Label(page, "МАСТЕРСКАЯ", 40, 2, 250, 32, 27, Hex("ffe6aa"), FontStyle.Bold);
            Label(page, "Шахта · рулетка · добыча", 40, 34, 250, 22, 16, Hex("d1c5a7"));
            prestigeButton = ButtonAt(page, "ПРЕСТИЖ", 302, 14, 142, 31, ShowPrestige, Hex("bac6a1"), Ink);
            prestigeButton.name = "Open prestige"; prestigeButton.GetComponentInChildren<Text>().fontSize = 18;
            equipmentTab = ButtonAt(page, "СНАРЯЖЕНИЕ", 24, 68, 211, 31, () => SetWorkshopSection(false), Panel, Ink);
            equipmentTab.name = "Equipment section";
            productionTabButton = ButtonAt(page, "ПРОИЗВОДСТВО", 245, 68, 211, 31, () => SetWorkshopSection(true), Panel, Ink);
            productionTabButton.name = "Production section";
            equipmentTab.GetComponentInChildren<Text>().fontSize = productionTabButton.GetComponentInChildren<Text>().fontSize = 18;
            for (int i = 0; i < workshopButtons.Length; i++)
            {
                int slot = i, x = 24 + i % 2 * 221, y = 108 + i / 2 * 156;
                var card = Card(page, "Upgrade card " + i, x, y, 211, 156, Panel); workshopCards[i] = card.gameObject;
                workshopIcons[i] = Icon(card.transform, "pick1", 12, 12, 34, 34);
                workshopTitles[i] = Label(card.transform, "", 54, 7, 145, 25, 20, Ink, FontStyle.Bold);
                workshopLevels[i] = Label(card.transform, "", 54, 32, 145, 20, 16, Muted);
                workshopEffects[i] = Label(card.transform, "", 12, 54, 187, 34, 16, Ink);
                workshopStates[i] = Label(card.transform, "", 12, 88, 187, 20, 16, Muted);
                PanelAt(card.transform, "Upgrade progress track " + i, Hex("717b6a"), 12, 108, 187, 3);
                workshopProgress[i] = PanelAt(card.transform, "Upgrade progress " + i, Hex("a6cb6a"), 12, 108, 0, 3);
                workshopButtons[i] = ButtonAt(card.transform, "", 12, 115, 187, 32, () => Purchase(WorkshopKind(slot)), Hex("d9bd80"), Hex("352b20"));
            }
            productionSummary = Card(page, "Production summary", 245, 420, 211, 156, Hex("b1c197")).gameObject;
            Label(productionSummary.transform, "РУДНИК", 12, 8, 187, 28, 23, Ink, FontStyle.Bold);
            productionRateLabel = Label(productionSummary.transform, "", 12, 38, 187, 31, 22, Ink, FontStyle.Bold);
            productionRateLabel.resizeTextForBestFit = true; productionRateLabel.resizeTextMinSize = 16; productionRateLabel.resizeTextMaxSize = 22;
            productionBreakdown = Label(productionSummary.transform, "", 12, 76, 187, 22, 16, Muted);
            productionSynergy = Label(productionSummary.transform, "", 12, 100, 187, 22, 16, Muted);
            productionTotal = Label(productionSummary.transform, "", 12, 125, 187, 22, 16, Muted);
            workshopFootnote = Label(page, "", 24, 589, 432, 24, 16, Muted, FontStyle.Normal, TextAnchor.MiddleCenter);
            offlineButton = ButtonAt(page, "", 24, 584, 432, 32, ShowOfflineReward, Hex("b5c48f"), Ink);
            offlineButton.name = "Open warehouse"; offlineButtonLabel = offlineButton.GetComponentInChildren<Text>(); offlineButtonLabel.fontSize = 18;
        }

        private UpgradeKind WorkshopKind(int slot) => (UpgradeKind)(productionTab ? (int)UpgradeKind.Worker + slot : slot);
        private static string Rate(long milli) => (milli / 1000d).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture).Replace('.', ',');
        private void SetWorkshopSection(bool production)
        {
            if (busy || modal != null) return;
            productionTab = production; RefreshWorkshop();
        }

        private string UpgradeEffect(UpgradeKind kind, bool preview)
        {
            int rank = game.UpgradeLevel(kind), target = Mathf.Min(rank + (preview ? 1 : 0), MineGame.UpgradeMax(kind));
            string Value(int before, int after) => preview && rank < MineGame.UpgradeMax(kind) ? before + " > " + after : before.ToString();
            switch (kind)
            {
                case UpgradeKind.Pickaxe: return "Доход: " + Value(game.ClickPower, game.ClickPower + game.ClickUpgradeGain) + "\nУрон: " + Value(game.MiningDamage, 1 + target / 5 + game.Progress.efficiencyLevel);
                case UpgradeKind.Forge: return "Алмаз: " + Value(game.DiamondChance, 10 + target) + "%\nСила: +" + Value(game.PowerBonus, target / 2);
                case UpgradeKind.Swap: return "Обменов: " + Value(rank, target) + "\nВ рулетке";
                case UpgradeKind.Efficiency: return "Урон: " + Value(game.MiningDamage, 1 + game.Progress.upgradeLevel / 5 + target) + "\nДобыча руды";
                case UpgradeKind.Press: return "Ударов: " + Value(game.HitsRequired, MineGame.HitsPerTicket - target) + "\nЗа один жетон";
                case UpgradeKind.Treasure: return "Сундук: +" + Value(rank * 15, target * 15) + "%\nНаграды в рулетке";
                case UpgradeKind.Worker: return "По " + Rate(game.WorkerProductionMilli) + " монеты/с\nШахтёры: " + Value(rank, target);
                case UpgradeKind.Drill: return "По " + Rate(game.DrillProductionMilli) + " монет/с\nКузница: +" + game.DrillBonus + "%";
                case UpgradeKind.Cart: return "+" + (10 + 2 * game.Progress.railLevel) + "% каждая\nК общей добыче";
                case UpgradeKind.Rails: return "Бонус: " + Value(10 + 2 * rank, 10 + 2 * target) + "%\nКаждой вагонетке";
                default: return "Часов: " + Value(game.OfflineHours, 4 + target * 2) + "\nОфлайн: 50% дохода";
            }
        }

        private void RefreshWorkshop()
        {
            string[] icons = { PickSprites[game.PickTier], "forge", "swap", "pick-head2", "ticket", "chest", "worker", "drill", "cart", "rails", "warehouse" };
            equipmentTab.image.color = productionTab ? Panel : Hex("a4c975"); productionTabButton.image.color = productionTab ? Hex("a4c975") : Panel;
            productionSummary.SetActive(productionTab);
            productionRateLabel.text = "+" + Rate(game.ProductionMilli) + " монет/с";
            productionBreakdown.text = "Буры: +" + game.DrillBonus + "%";
            productionSynergy.text = "Логистика: +" + game.CartBonus + "%";
            productionTotal.text = "Добыто: " + ShortAmount(game.Progress.totalProduced);
            workshopFootnote.gameObject.SetActive(!productionTab); workshopFootnote.text = "Каждая покупка сохраняется сразу";
            offlineButton.gameObject.SetActive(productionTab); offlineButton.interactable = !busy;
            offlineButtonLabel.text = game.HasOfflineReward ? "СКЛАД: " + ShortAmount(game.Progress.offlineCoins) + " МОНЕТ" : "СКЛАД · ОФЛАЙН ДО " + game.OfflineHours + " Ч.";
            offlineButton.image.color = game.HasOfflineReward ? Hex("d9bd80") : Hex("b5c48f");
            for (int i = 0; i < workshopButtons.Length; i++)
            {
                bool visible = !productionTab || i < 5; workshopCards[i].SetActive(visible); if (!visible) continue;
                var kind = WorkshopKind(i);
                workshopTitles[i].text = UpgradeNames[(int)kind]; workshopIcons[i].sprite = art.Get(icons[(int)kind]); workshopButtons[i].name = "Buy " + kind;
                int rank = game.UpgradeLevel(kind), max = MineGame.UpgradeMax(kind), unlock = game.UpgradeUnlock(kind);
                bool capped = rank == max, locked = game.MinerLevel < unlock;
                workshopLevels[i].text = "Ур. " + rank + " / " + max;
                workshopEffects[i].text = UpgradeEffect(kind, true);
                string prerequisite = game.HasUpgradePrerequisite(kind) ? "" : "Нужен: " + UpgradeNames[(int)game.UpgradePrerequisite(kind).Value];
                workshopStates[i].text = capped ? "Полностью улучшено" : locked ? "Нужен шахтёр ур. " + unlock : game.HasPendingRoll ? "Заверши бросок" : prerequisite.Length > 0 ? prerequisite : game.CanBuyUpgrade(kind) ? "Можно улучшить" : "Нужно ещё: " + ShortAmount(game.UpgradePrice(kind) - game.Progress.coins);
                workshopStates[i].color = locked ? Hex("805644") : Muted;
                workshopProgress[i].rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 187f * rank / max);
                var button = workshopButtons[i]; button.interactable = !busy && game.CanBuyUpgrade(kind);
                button.image.color = button.interactable ? Hex("d9bd80") : Hex("a9ada3");
                var label = button.GetComponentInChildren<Text>(); label.text = capped ? "Максимум" : ShortAmount(game.UpgradePrice(kind)) + " монет";
                label.resizeTextForBestFit = true; label.resizeTextMinSize = 16; label.resizeTextMaxSize = 20;
            }
        }

        private static string Duration(long seconds)
        {
            if (seconds < 60) return seconds + " сек.";
            if (seconds < 3600) return seconds / 60 + " мин.";
            return seconds / 3600 + " ч. " + seconds % 3600 / 60 + " мин.";
        }

        public void ShowPrestige()
        {
            if (busy || modal != null) return;
            holding = false; prestigeWindowOpen = true;
            modal = MakeRect(shell, "Prestige modal", 0, 0, 480, 960).gameObject;
            var dim = PanelAt(modal.transform, "Modal overlay", new Color(.12f, .15f, .1f, .92f), 0, 0, 480, 960); dim.raycastTarget = true;
            Card(modal.transform, "Prestige card", 24, 103, 432, 752, Panel);
            Icon(modal.transform, "crystal", 40, 119, 30, 30);
            Label(modal.transform, "НОВАЯ ШАХТА", 83, 116, 350, 34, 28, Ink, FontStyle.Bold);
            prestigeSummary = Label(modal.transform, "", 40, 156, 400, 25, 18, Ink);
            prestigeState = Label(modal.transform, "", 40, 184, 400, 22, 16, Muted);
            prestigeReward = Label(modal.transform, "", 40, 211, 400, 24, 18, Ink);
            prestigeTree = MakeRect(modal.transform, "Prestige tree", 0, 0, 480, 850).gameObject;
            for (int column = 0; column < 3; column++)
            {
                int x = 40 + column * 136;
                Label(prestigeTree.transform, new[] { "ДОХОД", "СТАРТ", "ЖИЛЫ" }[column], x, 241, 128, 22, 18, Ink, FontStyle.Bold, TextAnchor.MiddleCenter);
                for (int row = 0; row < 3; row++)
                {
                    int index = column * 3 + row, y = 268 + row * 132;
                    var upgrade = MineGame.PrestigeUpgradeAt(index);
                    if (row > 0) prestigeLinks[index] = PanelAt(prestigeTree.transform, "Prestige link " + index, Hex("59654e"), x + 62, y - 6, 4, 6);
                    prestigeNodes[index] = Card(prestigeTree.transform, "Prestige node " + index, x, y, 128, 126, Hex("aeb6a2"));
                    Icon(prestigeTree.transform, upgrade.icon, x + 7, y + 9, 23, 23);
                    Label(prestigeTree.transform, upgrade.title, x + 36, y + 4, 86, 38, 16, Ink, FontStyle.Bold);
                    Label(prestigeTree.transform, upgrade.detail, x + 7, y + 47, 114, 36, 16, Muted, FontStyle.Normal, TextAnchor.MiddleCenter);
                    prestigeBuyButtons[index] = ButtonAt(prestigeTree.transform, "", x + 7, y + 89, 114, 30, () => BuyPrestigeUpgrade(index), Hex("d4bc83"), Ink);
                    prestigeBuyButtons[index].name = "Buy prestige " + index; prestigeBuyButtons[index].GetComponentInChildren<Text>().fontSize = 16;
                }
            }
            Label(prestigeTree.transform, "Опыт: +" + (game.PrestigeExperiencePercent - 100) + "% · за переезд +15%, до +75%", 40, 667, 400, 25, 16, Muted, FontStyle.Normal, TextAnchor.MiddleCenter);
            prestigeResetButton = ButtonAt(prestigeTree.transform, "", 40, 703, 400, 44, AskPrestige, Hex("dfc285"), Ink);
            prestigeResetButton.name = "Prepare prestige";
            ButtonAt(prestigeTree.transform, "Вернуться в игру", 40, 771, 400, 38, CloseModal, Panel, Ink).name = "Close prestige";
            prestigeConfirmation = MakeRect(modal.transform, "Prestige confirmation", 40, 243, 400, 540).gameObject;
            Label(prestigeConfirmation.transform, "ПЕРЕЕЗД НА НОВЫЙ РУДНИК", 0, 0, 400, 32, 22, Ink, FontStyle.Bold);
            prestigeLosses = Label(prestigeConfirmation.transform, "", 0, 44, 400, 146, 18, Ink);
            Label(prestigeConfirmation.transform, "Сохранятся:\nДостижения и их прогресс\nКристаллы и постоянные улучшения\nОбщая статистика и контракты", 0, 205, 400, 92, 18, Ink);
            prestigeStart = Label(prestigeConfirmation.transform, "", 0, 318, 400, 66, 16, Muted);
            prestigeConfirmButton = ButtonAt(prestigeConfirmation.transform, "", 0, 409, 400, 48, ConfirmPrestige, Hex("dfb277"), Ink);
            prestigeConfirmButton.name = "Confirm prestige";
            ButtonAt(prestigeConfirmation.transform, "Отмена", 0, 480, 400, 40, () => { prestigeConfirmation.SetActive(false); prestigeTree.SetActive(true); RefreshPrestigeWindow(); }, Panel, Ink).name = "Cancel prestige";
            prestigeConfirmation.SetActive(false); RefreshPrestigeWindow();
        }

        private void AskPrestige()
        {
            if (!prestigeWindowOpen || !game.CanPrestige) return;
            prestigeTree.SetActive(false); prestigeConfirmation.SetActive(true); RefreshPrestigeWindow();
        }

        private void RefreshPrestigeWindow()
        {
            if (!prestigeWindowOpen || modal == null) return;
            prestigeSummary.text = "Кристаллы: " + ShortAmount(game.Progress.crystals) + " · Переездов: " + ShortAmount(game.Progress.prestigeCount);
            string reason = game.MinerLevel < MineGame.MaxMinerLevel ? "Нужен шахтёр ур. 50 · сейчас " + game.MinerLevel : game.HasOfflineReward ? "Сначала забери награду со склада" : game.HasPendingRoll ? "Сначала заверши оплаченный бросок" : game.PrestigeReward == 0 || game.Progress.prestigeCount == MineGame.MaxPrestiges ? "Достигнут предел переездов" : "Шахтёр ур. 50 · можно начать новую шахту";
            prestigeState.text = reason; prestigeReward.text = "За переезд: +" + game.PrestigeReward + " кристаллов";
            for (int i = 0; i < MineGame.PrestigeUpgradeCount; i++)
            {
                bool owned = game.HasPrestigeUpgrade(i), prerequisite = game.PrestigePrerequisiteMet(i);
                prestigeNodes[i].color = owned ? Hex("c5d99e") : prerequisite ? Hex("bbc5a9") : Hex("9ca695");
                if (prestigeLinks[i] != null) prestigeLinks[i].color = prerequisite ? Hex("b2d573") : Hex("59654e");
                prestigeBuyButtons[i].interactable = !busy && game.CanBuyPrestigeUpgrade(i);
                prestigeBuyButtons[i].GetComponentInChildren<Text>().text = owned ? "Куплено" : !prerequisite ? "Купи выше" : game.Progress.prestigeCount == 0 ? "Закрыто" : MineGame.PrestigeUpgradeAt(i).cost + " кр.";
            }
            prestigeResetButton.interactable = prestigeConfirmButton.interactable = !busy && game.CanPrestige;
            prestigeResetButton.GetComponentInChildren<Text>().text = game.CanPrestige ? "НОВАЯ ШАХТА · +" + game.PrestigeReward + " КР." : "ПЕРЕЕЗД НЕДОСТУПЕН";
            prestigeConfirmButton.GetComponentInChildren<Text>().text = "ПОДТВЕРДИТЬ · +" + game.PrestigeReward + " КР.";
            prestigeLosses.text = "Будут сброшены:\nМонеты: " + ShortAmount(game.Progress.coins) + " · жетоны: " + game.Progress.tickets + "\nУровень шахтёра и глубина\nКирка, кузница и покупки этой шахты\nПовреждения, реликвии и богатая жила";
            prestigeStart.text = "Новый старт: " + game.PrestigeStartCoins + " монет · 2 жетона\nКирка ур. " + game.PrestigeStartPick + " · шахтёров: " + game.PrestigeStartWorkers + "\nОпыт в новой шахте: +" + (Mathf.Min(5, game.Progress.prestigeCount + 1) * 15) + "%";
        }

        public void ShowOfflineReward()
        {
            if (busy || modal != null || game.HasPendingRoll) return;
            holding = false; offlineNoticeQueued = false; offlineWindowOpen = true;
            modal = MakeRect(shell, "Offline reward modal", 0, 0, 480, 960).gameObject;
            var dim = PanelAt(modal.transform, "Modal overlay", new Color(.12f, .15f, .1f, .9f), 0, 0, 480, 960); dim.raycastTarget = true;
            Card(modal.transform, "Offline reward card", 24, 169, 432, 608, Panel);
            Icon(modal.transform, "warehouse", 46, 188, 48, 48);
            Label(modal.transform, "СКЛАД РУДНИКА", 110, 189, 323, 35, 27, Ink, FontStyle.Bold);
            Label(modal.transform, "Рабочие добывают, пока ты отдыхаешь", 47, 245, 386, 25, 16, Muted);
            Card(modal.transform, "Stored reward", 47, 283, 386, 114, Hex("d0d9af"));
            offlineAmountLabel = Label(modal.transform, "", 61, 295, 358, 43, 32, Ink, FontStyle.Bold, TextAnchor.MiddleCenter);
            offlineAmountLabel.resizeTextForBestFit = true; offlineAmountLabel.resizeTextMinSize = 20; offlineAmountLabel.resizeTextMaxSize = 32;
            offlineStateLabel = Label(modal.transform, "", 61, 344, 358, 39, 16, Muted, FontStyle.Normal, TextAnchor.MiddleCenter);
            bool pending = game.HasOfflineReward;
            long capacity = pending ? game.Progress.offlineRewardCapacity : game.OfflineHours * 3600;
            string detail = pending ? "Тебя не было: " + Duration(game.Progress.offlineElapsedSeconds) + "\nУчтено: " + Duration(game.Progress.offlineRewardSeconds) + " из " + capacity / 3600 + " ч. лимита\nДоход: 50% от " + Rate(game.Progress.offlineRewardRateMilli) + " монет/с" : "Офлайн-доход: 50% производства\nВремя накопления: до " + game.OfflineHours + " ч.\n" + (game.ProductionMilli > 0 ? "Производство: " + Rate(game.ProductionMilli) + " монет/с" : "Найми шахтёров в производстве");
            Label(modal.transform, detail, 48, 419, 384, 75, 18, Ink);
            Label(modal.transform, "Только монеты: без опыта и жетонов.\nНаграда ждёт получения. Пока она\nлежит на складе, новая не копится.", 48, 506, 384, 72, 16, Muted);
            offlineClaimButton = ButtonAt(modal.transform, "Забрать монеты", 47, 600, 386, 48, ClaimOfflineReward, Hex("d9bd80"), Ink);
            offlineClaimButton.name = "Claim offline reward";
            ButtonAt(modal.transform, "Позже", 47, 672, 386, 43, CloseModal, Panel, Ink).name = "Close warehouse";
            RefreshOfflineWindow();
        }

        private void RefreshOfflineWindow()
        {
            if (!offlineWindowOpen || modal == null) return;
            offlineAmountLabel.text = game.HasOfflineReward ? "+" + game.Progress.offlineCoins.ToString("N0") + " монет" : "Пока пусто";
            offlineStateLabel.text = game.HasOfflineReward ? game.Progress.coins == MineGame.CoinLimit ? "Кошелёк заполнен\nПотрать монеты и вернись за наградой" : "Готово к получению" : "Оставь рабочим время на добычу";
            offlineClaimButton.interactable = !busy && game.CanClaimOffline;
        }

        private Image Hero(Transform parent, string name, float x, float y, float width, float height)
        {
            var window = Card(parent, name, x, y, width, height, Hex("737c6e"));
            var mask = window.gameObject.AddComponent<Mask>(); mask.showMaskGraphic = false;
            var image = Icon(window.transform, "tex-stone", 0, 0, width, height); image.type = Image.Type.Tiled; image.pixelsPerUnitMultiplier = .4f;
            image.color = Hex("69736a");
            if (name == "Mine illustration" || name == "Expedition illustration")
            {
                bool expedition = name == "Expedition illustration";
                var world = new GameObject(expedition ? "Voxel expedition shaft" : "Voxel mining world"); world.transform.SetParent(transform, false);
                var renderer = world.AddComponent<VoxelStage>(); renderer.Initialize(art, expedition);
                if (expedition) shaft = renderer; else stage = renderer;
                var rect = MakeRect(window.transform, "Live block world", 0, 0, width, height);
                var display = rect.gameObject.AddComponent<RawImage>(); display.texture = renderer.Target; display.raycastTarget = false;
            }
            return image;
        }

        private void ShowOdds()
        {
            if (modal != null) return;
            holding = false;
            modal = MakeRect(shell, "Rules modal", 0, 0, 480, 960).gameObject;
            var dim = PanelAt(modal.transform, "Modal overlay", new Color(.12f, .15f, .1f, .9f), 0, 0, 480, 960); dim.raycastTarget = true;
            Card(modal.transform, "Rules card", 24, 102, 432, 750, Panel);
            Label(modal.transform, "КИРКА + ПОРОДА = ИСХОД", 47, 140, 386, 35, 23, Ink, FontStyle.Bold);
            Label(modal.transform, "Кузница " + game.Progress.forgeLevel + " · бонус силы +" + game.PowerBonus, 48, 183, 380, 22, 16, Muted);
            int[] chances = { 100 - game.IronChance - game.DiamondChance, game.IronChance, game.DiamondChance };
            for (int i = 0; i < 3; i++)
            {
                int x = 47 + i * 132;
                Card(modal.transform, "Rarity " + i, x, 219, 121, 112, Hex("a8b0a1"));
                Icon(modal.transform, "pick" + new[] { 1, 2, 4 }[i], x + 32, 230, 56, 56);
                Label(modal.transform, chances[i] + "%", x, 295, 121, 27, 24, Ink, FontStyle.Bold, TextAnchor.MiddleCenter);
            }
            for (int i = 0; i < 2; i++)
            {
                int x = 47 + i * 200;
                Card(modal.transform, "Obstacle rule " + i, x, 345, 186, 66, Hex("a8b0a1"));
                Icon(modal.transform, i == 0 ? "tile-bedrock" : "tile-obsidian", x + 10, 358, 36, 36);
                Label(modal.transform, i == 0 ? "БЕДРОК" : "ОБСИДИАН", x + 56, 349, 124, 26, 18, Ink);
                Label(modal.transform, i == 0 ? "Сундук потерян" : "Только алмаз", x + 56, 378, 124, 24, 16, Muted);
            }
            string migration = game.Progress.boardSeed == 0 ? "Текущий старый пласт без ловушек.\n" : "";
            Label(modal.transform, migration + "Кирки выпадают за 1 жетон.\nПерестановка: " + game.Progress.swapLevel + " / 3 уровня.\nОбмен открывается покупкой с уровня 3.\nРанги дают 1 / 2 / 3 обмена за бросок.\nОбмены сохраняются при выходе.\nДерево / железо / алмаз: " + (1 + game.PowerBonus) + " / " + (2 + game.PowerBonus) + " / " + (4 + game.PowerBonus) + " силы.\nОбсидиан: " + (game.Hardness + 2) + " прочности, только алмаз.\nДерево и железо ломаются без урона.\nБедрок закрывает проход этой вылазки.\nНа проход: 18% бедрок, 30% обсидиан,\n52% без препятствия. Слой заранее скрыт.\nПустые проходы дают 0. Жетон можно проиграть.\nБонус пласта — только за все 5 сундуков.\nСмена раскладки: 1 жетон, сундуки теряются.\nБлок: " + game.BlockReward + " · сундук: " + game.ChestReward + " · пласт: " + game.FloorReward + " монет.\n" + game.HitsRequired + " ударов в шахте дают новый жетон.", 48, 425, 384, 342, 16, Ink);
            ButtonAt(modal.transform, "Продолжить добычу", 47, 783, 386, 50, CloseModal, Hex("e7c78d"), Hex("362b1b")).name = "ПОНЯТНО";
        }

        public void ShowMilestones()
        {
            if (busy || modal != null) return;
            holding = false; milestonePage = 0;
            int firstUnclaimed = -1, firstReady = -1;
            for (int i = 0; i < MineGame.MilestoneCount; i++)
            {
                if (firstUnclaimed < 0 && !game.MilestoneClaimed(i)) firstUnclaimed = i;
                if (firstReady < 0 && game.CanClaimMilestone(i)) firstReady = i;
            }
            milestonePage = Mathf.Max(0, firstReady >= 0 ? firstReady : firstUnclaimed) / 4;
            milestoneBookOpen = true;
            modal = MakeRect(shell, "Milestones modal", 0, 0, 480, 960).gameObject;
            var dim = PanelAt(modal.transform, "Modal overlay", new Color(.12f, .15f, .1f, .9f), 0, 0, 480, 960); dim.raycastTarget = true;
            Card(modal.transform, "Milestones card", 24, 105, 432, 750, Panel);
            Icon(modal.transform, "medal", 43, 123, 30, 30);
            Label(modal.transform, "КНИГА ШАХТЁРА", 83, 121, 350, 34, 27, Ink, FontStyle.Bold);
            milestoneSummary = Label(modal.transform, "", 48, 161, 384, 24, 16, Muted);
            for (int i = 0; i < 4; i++)
            {
                int slot = i, y = 193 + i * 133;
                milestoneCards[i] = Card(modal.transform, "Milestone slot " + i, 40, y, 400, 124, Hex("b1b8a6"));
                milestoneIcons[i] = Icon(modal.transform, "medal", 52, y + 10, 34, 34);
                milestoneTitles[i] = Label(modal.transform, "", 98, y + 6, 330, 27, 21, Ink, FontStyle.Bold);
                milestoneDetails[i] = Label(modal.transform, "", 98, y + 33, 330, 22, 16, Muted);
                milestoneRewards[i] = Label(modal.transform, "", 52, y + 58, 376, 22, 16, Ink);
                milestoneValues[i] = Label(modal.transform, "", 52, y + 84, 230, 22, 16, Muted);
                PanelAt(modal.transform, "Milestone track " + i, Hex("65705d"), 52, y + 111, 230, 4);
                milestoneFills[i] = PanelAt(modal.transform, "Milestone progress " + i, Hex("8ebd56"), 52, y + 111, 0, 4);
                milestoneClaimButtons[i] = ButtonAt(modal.transform, "", 300, y + 84, 128, 30, () => ClaimMilestone(milestonePage * 4 + slot), Hex("d9bd80"), Ink);
                milestoneClaimButtons[i].name = "Claim milestone " + i;
                milestoneClaimButtons[i].GetComponentInChildren<Text>().fontSize = 16;
            }
            milestonePrevious = ButtonAt(modal.transform, "<", 40, 735, 84, 34, () => ChangeMilestonePage(-1), Hex("a3b492"), Ink);
            milestonePrevious.name = "Milestones previous";
            milestonePageLabel = Label(modal.transform, "", 132, 735, 216, 34, 18, Ink, FontStyle.Normal, TextAnchor.MiddleCenter);
            milestoneNext = ButtonAt(modal.transform, ">", 356, 735, 84, 34, () => ChangeMilestonePage(1), Hex("a3b492"), Ink);
            milestoneNext.name = "Milestones next";
            ButtonAt(modal.transform, "Продолжить игру", 40, 790, 400, 44, CloseModal, Hex("e7c78d"), Hex("362b1b")).name = "Close milestones";
            RefreshMilestoneBook();
        }

        private void ChangeMilestonePage(int delta)
        {
            milestonePage = Mathf.Clamp(milestonePage + delta, 0, (MineGame.MilestoneCount - 1) / 4);
            RefreshMilestoneBook();
        }

        private void RefreshMilestoneBook()
        {
            if (!milestoneBookOpen || modal == null) return;
            milestoneSummary.text = "Награды: " + game.ClaimedMilestones + " / " + MineGame.MilestoneCount + " · Готово: " + game.ReadyMilestones;
            milestonePageLabel.text = "Страница " + (milestonePage + 1) + " / " + ((MineGame.MilestoneCount + 3) / 4);
            milestonePrevious.interactable = milestonePage > 0;
            milestoneNext.interactable = (milestonePage + 1) * 4 < MineGame.MilestoneCount;
            for (int i = 0; i < 4; i++)
            {
                int index = milestonePage * 4 + i;
                var goal = MineGame.MilestoneAt(index);
                bool claimed = game.MilestoneClaimed(index), ready = game.CanClaimMilestone(index);
                milestoneCards[i].color = claimed ? Hex("a2ac9b") : ready ? Hex("d1d7ac") : Hex("b1b8a6");
                milestoneIcons[i].sprite = art.Get(goal.icon); milestoneIcons[i].color = claimed ? new Color(.7f, .75f, .65f) : Color.white;
                milestoneTitles[i].text = goal.title; milestoneDetails[i].text = goal.detail;
                milestoneRewards[i].text = "Награда: " + ShortAmount(goal.coins) + " монет · +" + goal.tickets + " жет.";
                milestoneValues[i].text = claimed ? "Награда получена" : ShortAmount(game.MilestoneValue(index)) + " / " + goal.target;
                milestoneFills[i].rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 230f * game.MilestoneValue(index) / goal.target);
                milestoneClaimButtons[i].interactable = ready;
                milestoneClaimButtons[i].image.color = ready ? Hex("d9bd80") : Hex("a1aa98");
                milestoneClaimButtons[i].GetComponentInChildren<Text>().text = claimed ? "Получено" : ready ? "Забрать" : "В работе";
            }
        }

        private void CloseModal()
        {
            if (modal == null) return;
            milestoneBookOpen = offlineWindowOpen = prestigeWindowOpen = false; modal.SetActive(false); Destroy(modal); modal = null; Refresh();
        }

        private Image Card(Transform parent, string name, float x, float y, float width, float height, Color color, string sprite = "ui-card")
        {
            var image = PanelAt(parent, name, color, x, y, width, height); image.sprite = art.Get(sprite); image.type = Image.Type.Sliced; image.pixelsPerUnitMultiplier=.5f; return image;
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
