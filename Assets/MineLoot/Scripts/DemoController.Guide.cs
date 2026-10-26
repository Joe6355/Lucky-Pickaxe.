using UnityEngine;
using UnityEngine.UI;

namespace MineLoot
{
    // The existing controller owns these hints; they never intercept gameplay input.
    public sealed partial class DemoController
    {
        private GameObject contractCard, guideCard;
        private Text guideTitle, guideDetail, guideActionLabel;
        private Image guideIcon, guideFill;
        private RectTransform guideFrame, guideTarget;
        private readonly Image[] guideEdges = new Image[4];
        private readonly Vector3[] guideCorners = new Vector3[4];

        private void BuildGuide()
        {
            guideCard = Card(shell, "First steps", 24, 794, 432, 83, Hex("d5dfa9")).gameObject;
            guideIcon = Icon(guideCard.transform, "tile-coal", 10, 19, 32, 32);
            guideTitle = Label(guideCard.transform, "", 52, 4, 268, 24, 19, Ink, FontStyle.Bold);
            guideDetail = Label(guideCard.transform, "", 52, 28, 268, 42, 15, Ink);
            guideDetail.name = "Guide detail";
            var action = ButtonAt(guideCard.transform, "К шагу", 332, 7, 88, 32, FollowGuide, Hex("b5ce80"), Ink);
            action.name = "Guide action"; guideActionLabel = action.GetComponentInChildren<Text>(); guideActionLabel.fontSize = 16;
            var skip = ButtonAt(guideCard.transform, "Пропуск", 332, 46, 88, 24, SkipMiningGuide, Panel, Muted);
            skip.name = "Skip guide"; skip.GetComponentInChildren<Text>().fontSize = 14;
            PanelAt(guideCard.transform, "Guide track", Hex("8e9a72"), 52, 74, 268, 3);
            guideFill = PanelAt(guideCard.transform, "Guide progress", Hex("526e36"), 52, 74, 0, 3);

            guideFrame = MakeRect(shell, "Guide focus", 0, 0, 1, 1);
            for (int i = 0; i < guideEdges.Length; i++)
                guideEdges[i] = PanelAt(guideFrame, "Focus edge " + i, Hex("e4f29a"), 0, 0, 1, 1);
        }

        private bool WorkerGuideReady => game.MinerLevel >= game.UpgradeUnlock(UpgradeKind.Worker) && game.CanBuyUpgrade(UpgradeKind.Worker);

        private void RefreshGuide()
        {
            if (guideCard == null) return;
            contractCard.SetActive(!game.GuideActive || busy);
            if (!game.GuideActive) { guideCard.SetActive(false); guideFrame.gameObject.SetActive(false); return; }
            int step = game.Progress.guideStep;
            guideFill.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 268f * step / MineGame.GuideStepCount);
            string title, detail, icon;
            guideActionLabel.text = "К шагу";
            switch (step)
            {
                case 0:
                    title = "Первый блок"; icon = "tile-coal";
                    int hits = Mathf.CeilToInt((game.OreHealth - game.Progress.oreDamage) / (float)game.MiningDamage);
                    detail = "Нажимай или удерживай руду.\nДо добычи: " + hits + " ударов.";
                    guideTarget = minePage.activeSelf ? rock : mineTab.GetComponent<RectTransform>();
                    break;
                case 1:
                    title = "Улучши кирку"; icon = "pick1";
                    detail = game.CanUpgrade ? "+" + game.ClickUpgradeGain + " монеты за удар.\nЦена: " + ShortAmount(game.UpgradeCost) + " монет." : "Добудь ещё " + ShortAmount(System.Math.Max(0, game.UpgradeCost - game.Progress.coins)) + " монет.\nКирка увеличит доход за удар.";
                    guideTarget = minePage.activeSelf ? game.CanUpgrade ? upgradeButton.GetComponent<RectTransform>() : rock : mineTab.GetComponent<RectTransform>();
                    break;
                case 2:
                    title = "Рулетка за жетон"; icon = "ticket";
                    detail = game.Progress.tickets == 0 ? "Каждые " + game.HitsRequired + " ударов дают жетон.\nЗапуск может не принести монет." : "Бедрок губит сундук; обсидиан\nпробивает лишь алмазная кирка.";
                    guideTarget = game.Progress.tickets == 0 ? minePage.activeSelf ? rock : mineTab.GetComponent<RectTransform>() : rollPage.activeSelf ? spinButton.GetComponent<RectTransform>() : rollTab.GetComponent<RectTransform>();
                    break;
                case 3:
                    title = "Найми шахтёра"; icon = "worker";
                    detail = game.MinerLevel < game.UpgradeUnlock(UpgradeKind.Worker) ? "Докопай до уровня шахтёра 2.\nРабочий будет добывать монеты." : !WorkerGuideReady ? "Нужно " + ShortAmount(game.UpgradePrice(UpgradeKind.Worker)) + " монет на рабочего.\nОн приносит +" + Rate(game.WorkerProductionMilli) + " монеты в секунду." : "Производство → Шахтёры.\nПервый рабочий: +" + Rate(game.WorkerProductionMilli) + " монеты/с.";
                    guideTarget = !WorkerGuideReady ? minePage.activeSelf ? rock : mineTab.GetComponent<RectTransform>() : !workshopPage.activeSelf ? workshopTab.GetComponent<RectTransform>() : !productionTab ? productionTabButton.GetComponent<RectTransform>() : workshopButtons[0].GetComponent<RectTransform>();
                    break;
                default:
                    title = "Забери награду"; icon = "medal";
                    detail = game.ReadyMilestones > 0 ? "В книге целей есть награда.\nОткрой её и нажми «Забрать»." : "Нанеси 30 ударов: " + System.Math.Min(30, game.Progress.totalClicks) + "/30.\nЗатем забери награду в целях.";
                    guideActionLabel.text = game.ReadyMilestones > 0 ? "Цели" : "К шагу";
                    guideTarget = game.ReadyMilestones > 0 ? null : minePage.activeSelf ? rock : mineTab.GetComponent<RectTransform>();
                    break;
            }
            guideTitle.text = (step + 1) + "/" + MineGame.GuideStepCount + " · " + title;
            guideDetail.text = detail; guideIcon.sprite = art.Get(icon);
            AnimateGuide();
        }

        private void FollowGuide()
        {
            if (busy || modal != null || !game.GuideActive) return;
            switch (game.Progress.guideStep)
            {
                case 2: ShowPage(game.Progress.tickets > 0); break;
                case 3:
                    if (WorkerGuideReady) { ShowWorkshop(); SetWorkshopSection(true); }
                    else ShowPage(false);
                    break;
                case 4:
                    if (game.ReadyMilestones > 0) ShowMilestones();
                    else ShowPage(false);
                    break;
                default: ShowPage(false); break;
            }
        }

        private void SkipMiningGuide()
        {
            if (busy || modal != null || !game.SkipGuide()) return;
            Persist(); Flush(); Refresh();
        }

        private void StartMiningGuide()
        {
            if (busy || game.HasPendingRoll) return;
            CloseModal();
            bool active = game.StartGuide(); Persist(); Flush(); ShowPage(false); Refresh();
            if (!active) Notice("ОСНОВЫ УЖЕ ОСВОЕНЫ", "Добыча, кирка, рулетка, рабочие и цели");
        }

        private void AnimateGuide()
        {
            if (guideCard == null) return;
            bool visible = game.GuideActive && !busy && modal == null;
            guideCard.SetActive(visible);
            bool focus = visible && guideTarget != null && guideTarget.gameObject.activeInHierarchy;
            guideFrame.gameObject.SetActive(focus);
            if (!focus) return;
            guideTarget.GetWorldCorners(guideCorners);
            Vector3 top = shell.InverseTransformPoint(guideCorners[1]), bottom = shell.InverseTransformPoint(guideCorners[3]);
            float width = bottom.x - top.x + 6, height = top.y - bottom.y + 6;
            guideFrame.anchoredPosition = new Vector2(top.x - shell.rect.xMin - 3, top.y - shell.rect.yMax + 3);
            guideFrame.sizeDelta = new Vector2(width, height);
            for (int i = 0; i < guideEdges.Length; i++)
            {
                var edge = guideEdges[i].rectTransform;
                edge.anchoredPosition = new Vector2(i == 3 ? width - 2 : 0, i == 1 ? -height + 2 : 0);
                edge.sizeDelta = new Vector2(i < 2 ? width : 2, i < 2 ? 2 : height);
                guideEdges[i].color = new Color(.9f, .97f, .6f, .65f + .25f * Mathf.Sin(Time.unscaledTime * 4));
            }
        }
    }
}
