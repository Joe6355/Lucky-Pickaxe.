using UnityEngine;
using UnityEngine.UI;

namespace MineLoot
{
    // Direct access to existing progression and SDK transactions; no separate economy.
    public sealed partial class DemoController
    {
        private GameObject rewardsPage, equipmentAside;
        private Button rewardsTab, videoTicketsButton;
        private Image rewardsNavMark, equippedIcon;
        private Text videoTicketsState, rewardsWarehouse, rewardsGoals, equippedTitle, equippedStats, equippedNext;
        private readonly Image[] materialIcons = new Image[6], materialFrames = new Image[6];

        private void BuildEquipmentAside(Transform parent)
        {
            equipmentAside = Card(parent, "Equipped tool", 0, 0, 480, 612, Panel).gameObject;
            equipmentAside.transform.SetAsFirstSibling();
            Label(equipmentAside.transform, "ТВОЙ ИНСТРУМЕНТ", 24, 18, 432, 32, 23, Muted, FontStyle.Bold);
            equippedIcon = Icon(equipmentAside.transform, "pick-wood", 30, 70, 106, 106);
            equippedTitle = Label(equipmentAside.transform, "", 158, 66, 290, 38, 29, Ink, FontStyle.Bold);
            equippedStats = Label(equipmentAside.transform, "", 158, 112, 290, 58, 20, Muted);
            PanelAt(equipmentAside.transform, "Tool divider", Hex("ccd5ce"), 24, 198, 432, 2);
            for (int i = 0; i < 6; i++)
            {
                materialFrames[i] = Card(equipmentAside.transform, "Tool material " + i, 24 + i * 73, 218, 66, 64, Hex("d7dfd9"));
                materialIcons[i] = Icon(materialFrames[i].transform, PickSprites[i], 12, 5, 42, 42);
                Label(materialFrames[i].transform, new[] { "0", "2", "5", "12", "20", "32" }[i], 2, 42, 62, 22, 16, Muted, FontStyle.Normal, TextAnchor.MiddleCenter);
            }
            equippedNext = Label(equipmentAside.transform, "", 24, 294, 432, 52, 21, Ink);
            ButtonAt(equipmentAside.transform, "ЗАЧАРОВАТЬ КИРКУ →", 24, 474, 432, 44, ShowEnchanting, Hex("c9bce1"), Ink).name = "Open enchantments";
            ButtonAt(equipmentAside.transform, "ШАХТЁРЫ И ТЕХНИКА →", 24, 532, 432, 44, ShowTeam, Hex("bad5ca"), Ink).name = "Open team desktop";
        }

        private void RefreshEquipmentAside()
        {
            if(!equipmentAside.activeInHierarchy) return;
            equippedIcon.sprite = art.Get(PickSprites[game.PickTier]);
            equippedTitle.text = PickNames[game.PickTier];
            equippedStats.text = "Урон " + game.MiningDamage + " за удар\n+" + game.ManualClickPower + " монет за удар";
            equippedNext.text = game.PickTier == 5 ? "Последний материал открыт.\nУсиливай кирку и зачарования." : "Следующая — " + PickNames[game.PickTier + 1].ToLowerInvariant() + "\nНа ранге " + game.NextPickUpgrade + " · сейчас " + game.Progress.upgradeLevel;
            for (int i = 0; i < 6; i++)
            {
                materialFrames[i].color = i == game.PickTier ? Hex("bce0ca") : Hex("dae1dc");
                materialIcons[i].color = i <= game.PickTier ? Color.white : new Color(.5f, .55f, .6f, .6f);
            }
        }

        private void BuildRewards(Transform parent)
        {
            Card(parent, "Rewards heading", 24, 0, 432, 60, Hex("355a60"));
            Label(parent, "НАГРАДЫ", 40, 0, 400, 40, 26, Hex("fff2d8"), FontStyle.Bold);
            Label(parent, "Видео, достижения и добыча склада", 40, 37, 400, 22, 16, Hex("d3e5dd"));
            var video = Card(parent, "Video reward", 24, 78, 432, 216, Panel);
            Icon(video.transform, "ticket", 20, 22, 54, 46);
            Label(video.transform, "+10 ЖЕТОНОВ", 94, 14, 316, 36, 27, Ink, FontStyle.Bold);
            Label(video.transform, "За полный просмотр видео", 94, 52, 316, 26, 19, Muted);
            videoTicketsState = Label(video.transform, "", 20, 92, 392, 48, 18, Muted);
            videoTicketsButton = ButtonAt(video.transform, "СМОТРЕТЬ ВИДЕО · +10 ЖЕТОНОВ", 20, 154, 392, 44, ClaimTicketsWithVideo, Hex("d8bf80"), Ink);
            videoTicketsButton.name = "Video tickets"; videoTicketsButton.GetComponentInChildren<Text>().fontSize = 20;
            var warehouse = Card(parent, "Warehouse reward", 24, 310, 432, 132, Panel);
            Icon(warehouse.transform, "warehouse", 20, 20, 48, 48);
            rewardsWarehouse = Label(warehouse.transform, "", 86, 12, 326, 60, 21, Ink, FontStyle.Bold);
            ButtonAt(warehouse.transform, "ОТКРЫТЬ СКЛАД", 20, 82, 392, 36, ShowOfflineReward, Hex("bad5ca"), Ink).name = "Rewards warehouse";
            var goals = Card(parent, "Goals reward", 24, 458, 432, 98, Panel);
            rewardsGoals = Label(goals.transform, "", 20, 8, 392, 32, 21, Ink, FontStyle.Bold);
            ButtonAt(goals.transform, "ДОСТИЖЕНИЯ И НАГРАДЫ →", 20, 50, 392, 36, ShowMilestones, Hex("c9bce1"), Ink).name = "Rewards milestones";
        }

        private void RefreshRewards()
        {
            if(!rewardsPage.activeInHierarchy) return;
            long wait = game.VideoWaitSeconds(SessionUtc);
            videoTicketsState.text = RewardedVideoPending ? "Ожидаем результат просмотра…" : wait > 0 ? "Следующий подарок через " + Duration(wait) : !rewardedAvailable ? "Видео доступно в Яндекс Играх.\nЕсли рекламы нет, попробуй позже." : game.EconomyLocked ? "Сначала заверши бросок рулетки." : "По желанию · один подарок раз в 3 минуты";
            videoTicketsButton.interactable = !EconomyBusy && !applicationPaused && rewardedAvailable && game.CanClaimVideoTickets(SessionUtc);
            rewardsWarehouse.text = game.HasOfflineReward ? "На складе: " + ShortAmount(game.Progress.offlineCoins) + " монет\nМожно забрать или утроить за видео" : "Офлайн-добыча · до " + game.OfflineHours + " ч.\nШахтёры работают, пока тебя нет";
            rewardsGoals.text = game.ReadyMilestones > 0 ? "ДОСТУПНО НАГРАД: " + game.ReadyMilestones : "ДОСТИЖЕНИЯ · " + game.ClaimedMilestones + "/" + MineGame.MilestoneCount;
        }
    }
}
