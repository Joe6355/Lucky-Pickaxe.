using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MineLoot
{
    // Reflow the existing controls. The game, save and paid-roll state are shared by both layouts.
    public sealed partial class DemoController
    {
        private readonly Dictionary<RectTransform, (Vector2 position, Vector2 size, Vector3 scale)> portraitRects = new Dictionary<RectTransform, (Vector2, Vector2, Vector3)>();
        private readonly Dictionary<Text, int> portraitFonts = new Dictionary<Text, int>();
        private RectTransform headerLayout, coinLayout, ticketWalletLayout, navigationLayout;
        private RectTransform experienceLayout, oresLayout, ticketLayout, campLayout, quickUpgradesLayout;
        private RectTransform shaftPresentation, expeditionHeader, expeditionResult, expeditionActions;
        private RectTransform dialogRoot;
        private GameObject dialogShade, desktopRollHints;
        private RawImage mineDisplay, shaftDisplay;
        private bool wideLayout;
        private int selectedPage;
        private float experienceWidth = 404, ticketWidth = 360, miningVisualScale = 1;
        private Vector2 noticeHome = new Vector2(24, -10);
        public bool IsWideLayout => wideLayout;

        private void InitializeResponsiveLayout()
        {
            headerLayout = GroupRegion(shell, "Header layout", new Rect(0, 0, 480, 164));
            coinLayout = GroupRegion(headerLayout, "Coin layout", new Rect(24, 92, 270, 62));
            ticketWalletLayout = GroupRegion(headerLayout, "Ticket wallet layout", new Rect(306, 92, 150, 62));
            navigationLayout = GroupRegion(shell, "Navigation layout", new Rect(24, 888, 432, 58));
            var mine = minePage.transform;
            experienceLayout = GroupRegion(mine, "Experience layout", new Rect(24, 0, 432, 50));
            oresLayout = GroupRegion(mine, "Ore collection layout", new Rect(24, 346, 432, 46));
            ticketLayout = GroupRegion(mine, "Ticket layout", new Rect(24, 400, 432, 48));
            campLayout = GroupRegion(mine, "Camp layout", new Rect(24, 458, 432, 30));
            quickUpgradesLayout = GroupRegion(mine, "Quick upgrades layout", new Rect(24, 492, 432, 94));
            var expedition = rollPage.transform;
            expeditionHeader = GroupRegion(expedition, "Expedition header layout", new Rect(24, 0, 432, 60));
            shaftPresentation = GroupRegion(expedition, "Shaft presentation", new Rect(18, 65, 444, 429));
            expeditionResult = GroupRegion(expedition, "Expedition result layout", new Rect(24, 498, 432, 46));
            expeditionActions = GroupRegion(expedition, "Expedition actions layout", new Rect(24, 600, 432, 25));
            Remember(clickPick.rectTransform);
            BuildDesktopHints();
            guideCard.transform.SetAsLastSibling(); guideFrame.SetAsLastSibling(); noticeRect.SetAsLastSibling();
            dialogShade = PanelAt(shell, "Dialog shade", new Color(.12f, .15f, .1f, .9f), 0, 0, 480, 960).gameObject;
            Stretch(dialogShade.GetComponent<RectTransform>()); dialogShade.GetComponent<Image>().raycastTarget = true;
            dialogShade.SetActive(false);
            dialogRoot = MakeRect(shell, "Dialog layout", 0, 0, 480, 960);
        }

        private RectTransform GroupRegion(Transform parent, string name, Rect area)
        {
            var members = new List<RectTransform>();
            foreach (Transform child in parent)
            {
                var rect = child as RectTransform;
                if (rect == null || rect == noticeRect || rect == guideFrame) continue;
                Vector2 position = rect.anchoredPosition, size = rect.sizeDelta;
                float x = position.x - rect.pivot.x * size.x, y = -position.y - (1 - rect.pivot.y) * size.y;
                if (x >= area.x - .1f && y >= area.y - .1f && x + size.x <= area.xMax + .1f && y + size.y <= area.yMax + .1f) members.Add(rect);
            }
            var group = MakeRect(parent, name, area.x, area.y, area.width, area.height);
            foreach (var rect in members)
            {
                rect.SetParent(group, false); rect.anchoredPosition -= new Vector2(area.x, -area.y);
            }
            return group;
        }

        private void Remember(RectTransform rect)
        {
            if (!portraitRects.ContainsKey(rect)) portraitRects.Add(rect, (rect.anchoredPosition, rect.sizeDelta, rect.localScale));
        }
        private void Frame(RectTransform rect, float x, float y, float width, float height, float scale = 1)
        {
            Remember(rect);
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = new Vector2(x + width * rect.pivot.x * scale, -y - height * (1 - rect.pivot.y) * scale);
            rect.localScale = Vector3.one * scale;
        }
        private void Frame(Transform parent, string name, float x, float y, float width, float height) => Frame((RectTransform)parent.Find(name), x, y, width, height);
        private void FontSize(Text text, int size)
        {
            if (!portraitFonts.ContainsKey(text)) portraitFonts.Add(text, text.fontSize);
            text.fontSize = size;
        }
        private void ButtonFrame(Button button, float x, float y, float width, float height)
        {
            Frame(button.GetComponent<RectTransform>(), x, y, width, height);
            Frame(button.GetComponentInChildren<Text>().rectTransform, 8, 0, width - 16, height);
        }
        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private void ApplyResponsiveLayout(Rect safe)
        {
            if (headerLayout == null) return;
            foreach (var entry in portraitRects)
            {
                entry.Key.anchoredPosition = entry.Value.position; entry.Key.sizeDelta = entry.Value.size; entry.Key.localScale = entry.Value.scale;
            }
            foreach (var entry in portraitFonts) entry.Key.fontSize = entry.Value;
            wideLayout = safe.width >= 800 && safe.width / safe.height >= 1.35f;
            canvas.scaleFactor = Mathf.Max(.1f, wideLayout ? safe.height / 960f : Mathf.Min(safe.width / 480f, safe.height / 960f));
            float width = wideLayout ? safe.width / canvas.scaleFactor : 480;
            shell.sizeDelta = new Vector2(width, 960);
            experienceWidth = 404; ticketWidth = 360; miningVisualScale = 1; noticeHome = new Vector2(24, -10);
            quickUpgradesLayout.gameObject.SetActive(!wideLayout);
            workshopTab.gameObject.SetActive(!wideLayout); workshopNavMark.gameObject.SetActive(!wideLayout);
            desktopRollHints.SetActive(false);
            if (wideLayout) ReflowDesktop(width);
            pickHome = clickPick.rectTransform.anchoredPosition;
            Frame(dialogRoot, (width - 480) / 2, 0, 480, 960);
            ResizeWorldView(stage, mineDisplay, 2);
            ResizeWorldView(shaft, shaftDisplay, shaftPresentation.localScale.x);
            ApplyPageVisibility();
            Refresh();
        }

        private void ReflowDesktop(float width)
        {
            const float side = 1.08f;
            float left = width - 576;
            Frame(shell, "Surface", 0, 0, width, 960);
            Frame(shell, "Inventory base", 12, 164, width - 24, 786);
            Frame(headerLayout, 0, 0, width, 164);
            Frame(headerLayout, "Header shadow", 0, 0, width, 164);
            Frame(coinLayout, width - 525, 29, 270, 62);
            Frame(ticketWalletLayout, width - 243, 29, 150, 62);
            ButtonFrame(soundLabel.GetComponentInParent<Button>(), width - 70, 37, 45, 45);
            float navWidth = Mathf.Min(600, left);
            Frame(navigationLayout, 24, 96, navWidth, 58);
            Frame(navigationLayout, "Navigation", 0, 0, navWidth, 58);
            float navButton = (navWidth - 18) / 2;
            ButtonFrame(mineTab, 9, 5, navButton, 45); ButtonFrame(rollTab, navButton + 12, 5, navButton, 45);
            Frame(mineNavMark.rectTransform, 9 + (navButton - 66) / 2, 52, 66, 3);
            Frame(rollNavMark.rectTransform, 12 + navButton + (navButton - 66) / 2, 52, 66, 3);
            Frame(minePage.GetComponent<RectTransform>(), 0, 168, width - 528, 782);
            Frame(rollPage.GetComponent<RectTransform>(), 0, 168, width - 528, 710);
            Frame(workshopPage.GetComponent<RectTransform>(), width - 528, 168, 480, 650, side);
            for (int i = 0; i < workshopCards.Length; i++)
            {
                Frame(workshopCards[i].GetComponent<RectTransform>(), 24 + i % 2 * 221, 108 + i / 2 * 164, 211, 164);
                Frame(workshopEffects[i].rectTransform, 12, 54, 187, 42);
                Frame(workshopStates[i].rectTransform, 12, 98, 187, 20);
                Frame(workshopCards[i].transform, "Upgrade progress track " + i, 12, 118, 187, 3);
                Frame(workshopProgress[i].rectTransform, 12, 118, 187, 3);
                ButtonFrame(workshopButtons[i], 12, 125, 187, 32);
            }
            Frame(productionSummary.GetComponent<RectTransform>(), 245, 436, 211, 164);
            ButtonFrame(purchaseAmountButton, 24, 612, 211, 30);
            Frame(workshopFootnote.rectTransform, 245, 612, 211, 24);
            ButtonFrame(offlineButton, 245, 612, 211, 30);
            Frame(contractCard.GetComponent<RectTransform>(), width - 528, 866 - 798 * side, 480, 960, side);
            Frame(guideCard.GetComponent<RectTransform>(), width - 528 + 24 * side, 866, 432, 83, side);
            noticeHome = new Vector2((width - 374) / 2, -10);
            Frame(noticeRect, noticeHome.x, 10, 374, 76);
            ReflowMine(left);
            ReflowExpedition(left);
        }

        private void ReflowMine(float width)
        {
            Frame(experienceLayout, 24, 0, width, 50);
            Frame(experienceLayout, "Scene header", 0, 0, width, 50);
            Frame(stageBadge.rectTransform, width - 180, 7, 164, 25);
            Frame(experienceLayout, "Experience track", 14, 39, width - 28, 5); experienceWidth = width - 28;
            var hero = (RectTransform)mineBackdrop.transform.parent;
            Frame(hero, 24, 58, width, 520); miningVisualScale = 520f / 280;
            Frame(hero, "Power badge", width - 151, 12, 138, 29);
            Frame(powerLabel.rectTransform, width - 151, 12, 138, 29);
            Frame(richVeinTimer.rectTransform, width - 151, 43, 138, 22);
            ButtonFrame(richVeinButton, width - 114, 66, 96, 42);
            Frame(hero, "Tap hint", 12, 480, width - 24, 29);
            Frame(mineStatus.rectTransform, 12, 480, width - 24, 29); FontSize(mineStatus, 20);
            Frame(oresLayout, 24, 586, width, 54);
            float slot = (width - 40) / 5;
            for (int i = 0; i < 5; i++)
            {
                float x = i * (slot + 10);
                Frame(oreSlots[i].rectTransform, x, 0, slot, 54);
                Frame(oreIcons[i].rectTransform, x + (slot - 28) / 2, 2, 28, 28);
                Frame(oreNames[i].rectTransform, x + 2, 31, slot - 4, 22); FontSize(oreNames[i], 18);
            }
            Frame(ticketLayout, 24, 648, width, 48);
            Frame(ticketLayout, "Next ticket", 0, 0, width, 48);
            Frame(progressLabel.rectTransform, width - 141, 3, 127, 24);
            Frame(ticketLayout, "Ticket track", 56, 33, width - 72, 7); ticketWidth = width - 72;
            Frame(campLayout, 24, 706, width, 30);
            Frame(upgradeHint.rectTransform, width - 213, 3, 213, 22);
            Frame(nextUpgrade.rectTransform, 24, 742, width, 22); FontSize(nextUpgrade, 18);
        }

        private void ReflowExpedition(float width)
        {
            Frame(expeditionHeader, 24, 0, width, 60);
            Frame(expeditionHeader, "Expedition header", 0, 0, width, 60);
            Frame(expeditionHeader, "Hardness badge", width - 108, 13, 96, 32);
            Frame(floorHint.rectTransform, width - 108, 13, 96, 32);
            bool spacious = width >= 1000;
            float shaftScale = spacious ? 640f / 444 : 508f / 429;
            Frame(shaftPresentation, spacious ? 24 : 24 + (width - 444 * shaftScale) / 2, 65, 444, 429, shaftScale);
            if (spacious)
            {
                float x = 688, column = width - 664;
                Frame(expeditionResult, x, 65, column, 156);
                Frame(expeditionResult, "Result panel", 0, 0, column, 156);
                Frame(rollRewardIcon.rectTransform, 16, 20, 48, 48);
                Frame(rollGain.rectTransform, 80, 12, column - 96, 64); FontSize(rollGain, 26);
                Frame(rollStatus.rectTransform, 16, 86, column - 32, 58); FontSize(rollStatus, 20);
                ButtonFrame(spinButton, x, 237, column, 64);
                Frame(expeditionActions, x, 317, column, 98);
                ButtonFrame(expeditionActions.Find("ШАНСЫ И ПРАВИЛА").GetComponent<Button>(), 0, 0, (column - 12) / 2, 40);
                ButtonFrame(abandonButton, (column + 12) / 2, 0, (column - 12) / 2, 40);
                ButtonFrame(expeditionActions.Find("Toggle speed").GetComponent<Button>(), 0, 52, column, 34);
                Frame(desktopRollHints.GetComponent<RectTransform>(), x, 419, column, 266);
                desktopRollHints.SetActive(true);
            }
            else
            {
                Frame(expeditionResult, 24, 580, width, 46);
                Frame(expeditionResult, "Result panel", 0, 2, width, 42);
                Frame(rollGain.rectTransform, 58, 0, width - 76, 27);
                Frame(rollStatus.rectTransform, 58, 25, width - 76, 19);
                ButtonFrame(spinButton, 24, 632, width, 44);
                Frame(expeditionActions, 24, 682, width, 25);
                float button = (width - 16) / 3;
                ButtonFrame(expeditionActions.Find("ШАНСЫ И ПРАВИЛА").GetComponent<Button>(), 0, 0, button, 25);
                ButtonFrame(abandonButton, button + 8, 0, button, 25);
                ButtonFrame(expeditionActions.Find("Toggle speed").GetComponent<Button>(), 2 * (button + 8), 0, button, 25);
            }
        }

        private void ResizeWorldView(VoxelStage world, RawImage display, float density)
        {
            Vector2 size = ((RectTransform)display.transform.parent).rect.size;
            float factor = Mathf.Min(density, 1536f / size.x, 1024f / size.y);
            world.ResizeViewport(Mathf.RoundToInt(size.x * factor), Mathf.RoundToInt(size.y * factor));
            display.texture = world.Target;
            if (world == stage && wideLayout)
            {
                Vector3 p = stage.View.WorldToViewportPoint(stage.MiningOrePosition);
                Vector2 center = new Vector2(p.x * size.x, (1 - p.y) * size.y);
                Frame(rock, center.x - 107.5f * miningVisualScale, center.y - 90 * miningVisualScale, 215 * miningVisualScale, 180 * miningVisualScale);
                Frame(clickPick.rectTransform, center.x + 74.5f * miningVisualScale, center.y - 38 * miningVisualScale, 96 * miningVisualScale, 96 * miningVisualScale);
                pickHome = clickPick.rectTransform.anchoredPosition;
            }
        }

        private void BuildDesktopHints()
        {
            desktopRollHints = Card(rollPage.transform, "Expedition desktop hints", 0, 0, 432, 266, Hex("bfc8ad")).gameObject;
            Label(desktopRollHints.transform, "НА ПУТИ К СУНДУКАМ", 16, 12, 320, 32, 22, Ink, FontStyle.Bold);
            Icon(desktopRollHints.transform, "tile-obsidian", 16, 68, 64, 64);
            Label(desktopRollHints.transform, "Обсидиан\nПробивает только\nалмазная кирка", 96, 64, 224, 78, 18, Ink);
            Icon(desktopRollHints.transform, "tile-bedrock", 16, 168, 64, 64);
            Label(desktopRollHints.transform, "Бедрок\nЛишает прохода\nи его сундука", 96, 164, 224, 78, 18, Ink);
            desktopRollHints.SetActive(false);
        }

        private GameObject CreateModal(string name)
        {
            dialogShade.SetActive(true); dialogRoot.SetAsLastSibling();
            return MakeRect(dialogRoot, name, 0, 0, 480, 960).gameObject;
        }
    }
}
