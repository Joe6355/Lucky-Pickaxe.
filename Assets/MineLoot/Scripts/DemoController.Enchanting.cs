using System;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace MineLoot
{
    public sealed partial class DemoController
    {
        // Temporary controls requested for the draft. Disable before a public release.
        public const bool DraftToolsEnabled = true;
        private int workshopSection;
        private Button enchantingTab, draftToolsTab;
        private GameObject enchantingArea, draftToolsArea;
        private RectTransform enchantingWindow;
        private RawImage enchantingDisplay;
        private VoxelStage enchantingWorld;
        private Image enchantingPick;
        private Button enchantingTableHelp;
        private Text enchantingXp, enchantingCurrent, enchantingHint, draftToolsState;
        private readonly Image[] enchantCards = new Image[3];
        private readonly Text[] enchantDescriptions = new Text[3];
        private readonly Button[] enchantButtons = new Button[3];
        private Material pickGlint;

        private void BuildEnchantmentSection(Transform page)
        {
            enchantingTab = ButtonAt(page, "ЗАЧАРОВАНИЕ", 24, 98, 211, 26, ShowEnchanting, Panel, Ink);
            enchantingTab.name = "Enchanting section"; enchantingTab.GetComponentInChildren<Text>().fontSize = 17;
            draftToolsTab = ButtonAt(page, "ТЕСТ", 245, 98, 211, 26, () => SelectUpgradeSection(3), Hex("e6cc8e"), Ink);
            draftToolsTab.name = "Draft tools section"; draftToolsTab.gameObject.SetActive(DraftToolsEnabled);
            enchantingArea = MakeRect(page, "Enchanting area", 0, 0, 480, 720).gameObject;
            var window = Card(enchantingArea.transform, "Enchanting room", 24, 148, 432, 196, Hex("3c354a")); enchantingWindow = window.rectTransform;
            var display = MakeRect(window.transform, "Enchanting world view", 0, 0, 432, 196);
            enchantingDisplay = display.gameObject.AddComponent<RawImage>(); enchantingDisplay.raycastTarget = false; Stretch(display);
            var world = new GameObject("Voxel enchanting room"); world.transform.SetParent(transform, false);
            enchantingWorld = world.AddComponent<VoxelStage>(); enchantingWorld.Initialize(art, false, true);
            enchantingDisplay.texture = enchantingWorld.Target;
            enchantingTableHelp=ButtonAt(window.transform,"",86,84,260,110,ShowEnchantmentGuide,Color.clear,Ink);
            enchantingTableHelp.name="Enchanting table help";enchantingTableHelp.image.sprite=null;enchantingTableHelp.image.color=new Color(1,1,1,.001f);
            var slot = Card(window.transform, "Equipped enchanting slot", 334, 12, 84, 94, Hex("ddd2e5"));
            enchantingPick = Icon(slot.transform, PickSprites[0], 16, 4, 52, 52);
            Label(slot.transform, "Кирка", 4, 62, 76, 24, 16, Ink, FontStyle.Bold, TextAnchor.MiddleCenter);
            enchantingXp = Label(enchantingArea.transform, "", 24, 350, 432, 32, 22, Ink, FontStyle.Bold);
            enchantingCurrent = Label(enchantingArea.transform, "", 24, 386, 432, 64, 17, Ink);
            for (int i = 0; i < 3; i++)
            {
                int slotIndex = i;
                enchantCards[i] = Card(enchantingArea.transform, "Enchantment offer " + i, 24, 458 + i * 80, 432, 72, Hex("e1d7e8"));
                Label(enchantCards[i].transform, new[] { "СТЕПЕНЬ I", "СТЕПЕНЬ II", "СТЕПЕНЬ III" }[i], 12, 4, 250, 25, 19, Ink, FontStyle.Bold);
                enchantDescriptions[i] = Label(enchantCards[i].transform, "", 12, 30, 258, 38, 16, Ink);
                enchantButtons[i] = ButtonAt(enchantCards[i].transform, "", 282, 12, 138, 48, () => EnchantEquippedPick(slotIndex), Hex("c4acd8"), Ink);
                enchantButtons[i].name = "Enchant offer " + i; enchantButtons[i].GetComponentInChildren<Text>().fontSize = 17;
            }
            enchantingHint = Label(enchantingArea.transform, "", 24, 696, 432, 22, 16, Muted, FontStyle.Normal, TextAnchor.MiddleCenter);
            BuildDraftTools(page);
            pickGlint = new Material(Resources.Load<Shader>("PickEnchantment")) { name = "Equipped pick enchantment glint" };
        }

        private void ReflowEnchanting(float width)
        {
            float left = (width - 80) * .52f, right = width - left - 72;
            Frame(enchantingArea.GetComponent<RectTransform>(), 0, 0, width, 740);
            Frame(enchantingWindow, 24, 130, left, 440);
            ButtonFrame(enchantingTableHelp,left*.2f,198,left*.6f,220);
            Frame(enchantingWindow, "Equipped enchanting slot", left - 100, 14, 84, 94);
            Frame(enchantingXp.rectTransform, 24, 582, left, 38); FontSize(enchantingXp, 25);
            Frame(enchantingCurrent.rectTransform, 24, 630, left, 76); FontSize(enchantingCurrent, 20);
            Frame(enchantingHint.rectTransform, 24, 708, width - 48, 24);
            for (int i = 0; i < 3; i++)
            {
                Frame(enchantCards[i].rectTransform, left + 48, 130 + i * 168, right, 152);
                Frame(enchantCards[i].transform.GetChild(0).GetComponent<RectTransform>(), 16, 8, right - 32, 32);
                Frame(enchantDescriptions[i].rectTransform, 16, 46, right - 32, 54); FontSize(enchantDescriptions[i], 20);
                ButtonFrame(enchantButtons[i], 16, 104, right - 32, 40);
                enchantButtons[i].GetComponentInChildren<Text>().resizeTextForBestFit = true;
                enchantButtons[i].GetComponentInChildren<Text>().resizeTextMinSize = 16;
                enchantButtons[i].GetComponentInChildren<Text>().resizeTextMaxSize = 19;
            }
            Frame(draftToolsArea.GetComponent<RectTransform>(), (width - 448) / 2, 130, 432, 540, 1.04f);
        }

        private static string DescribeEnchantments(int set, bool compact = false)
        {
            var text = new StringBuilder(); string[] roman = { "", "I", "II", "III", "IV", "V" };
            foreach (EnchantKind kind in Enum.GetValues(typeof(EnchantKind)))
            {
                int level = MineGame.EnchantmentLevel(set, kind); if (level == 0) continue;
                if (text.Length > 0) text.Append(" · ");
                text.Append(compact ? new[] { "Эфф.", "Прочн.", "Удача", "Починка" }[(int)kind] : MineGame.EnchantmentName(kind));
                text.Append(' ').Append(roman[level]);
            }
            return text.Length == 0 ? "Без зачарований" : text.ToString();
        }

        public void ShowEnchanting()
        {
            if (applicationPaused || EconomyBusy || modal != null) return;
            ShowWorkshop(); SelectUpgradeSection(2);
        }
        private void SelectUpgradeSection(int section)
        {
            if (applicationPaused || EconomyBusy || modal != null || section == 3 && !DraftToolsEnabled) return;
            workshopSection = section; productionTab = section == 1; holding = false;
            RefreshWorkshop(); RefreshEnchanting(); RefreshGuide();
        }
        private void ApplyWorkshopSection()
        {
            if (enchantingArea == null) return;
            enchantingArea.SetActive(workshopSection == 2); draftToolsArea.SetActive(workshopSection == 3 && DraftToolsEnabled);
            enchantingTab.image.color = workshopSection == 2 ? Hex("b7a0d0") : Panel;
            draftToolsTab.image.color = workshopSection == 3 ? Hex("e6cc8e") : Panel;
            RefreshEnchantmentVisibility();
        }
        private void RefreshEnchantmentVisibility()
        {
            if (enchantingWorld != null) enchantingWorld.gameObject.SetActive(selectedPage == 2 && workshopSection == 2);
        }
        private void RefreshEnchanting()
        {
            if (enchantingArea == null) return;
            enchantingXp.text = "Опыт на чары: " + ShortAmount(game.Progress.enchantingExperience);
            enchantingCurrent.text = "На кирке: " + DescribeEnchantments(game.Progress.enchantments,true) + "\nНовый материал кирки снимет чары.";
            enchantingPick.sprite = art.Get(PickSprites[game.PickTier]);
            foreach (var image in new[] { clickPick, workshopPick, equippedIcon, enchantingPick }) image.material = game.IsEnchanted ? pickGlint : null;
            enchantingWorld.SetEnchantment(game.IsEnchanted);
            for (int i = 0; i < 3; i++)
            {
                enchantDescriptions[i].text = ShortAmount(game.EnchantmentCost(i)) + " опыта";
                enchantButtons[i].GetComponentInChildren<Text>().text = game.IsEnchanted ? "ЗАМЕНИТЬ" : "ЗАЧАРОВАТЬ";
                enchantButtons[i].interactable = !EconomyBusy && !applicationPaused && game.CanEnchant(i);
            }
            enchantingHint.text = "Нажми на стол — описание зачарований";
            draftToolsState.text = "Монеты: " + ShortAmount(game.Progress.coins) + "\nОпыт на чары: " + ShortAmount(game.Progress.enchantingExperience) + "\nЖетоны: " + game.Progress.tickets;
        }
        public void EnchantEquippedPick(int slot)
        {
            if(applicationPaused || EconomyBusy || modal!=null) return;
            long cost=game.EnchantmentCost(slot);
            if(!game.Enchant(slot,()=>random.NextDouble())) return;
            Persist();Flush();enchantingWorld.PlayEnchantment();sounds.Play("rare");Refresh();ShowEnchantmentResult(cost);
            Notice("КИРКА ЗАЧАРОВАНА", DescribeEnchantments(game.Progress.enchantments, true));
        }
        private static string EnchantmentEffect(EnchantKind kind,int level)
        {
            switch(kind)
            {
                case EnchantKind.Efficiency:return "+"+(level*25)+"% к урону по камню и руде";
                case EnchantKind.Unbreaking:return "Тратит прочность раз в "+(level+1)+" удара";
                case EnchantKind.Fortune:return "+"+(level*20)+"% монет за разбитую руду";
                default:return "1 опыт чинит 2 ед. прочности кирки";
            }
        }
        private void ShowEnchantmentResult(long paid)
        {
            modal=CreateModal("Enchantment result modal");Card(modal.transform,"Enchantment result card",24,104,432,752,Panel);
            Label(modal.transform,"ЧАРЫ ОТКРЫТЫ",44,120,392,38,27,Ink,FontStyle.Bold,TextAnchor.MiddleCenter);
            var pick=Icon(modal.transform,PickSprites[game.PickTier],192,166,96,96);pick.material=pickGlint;
            int row=0;
            foreach(EnchantKind kind in Enum.GetValues(typeof(EnchantKind)))
            {
                int level=game.EnchantmentLevel(kind);if(level==0) continue;
                var card=Card(modal.transform,"Enchantment revealed "+kind,44,284+row++*82,392,74,Hex("ded3eb"));
                Label(card.transform,MineGame.EnchantmentName(kind)+" "+new[]{"","I","II","III","IV","V"}[level],12,6,368,27,21,Ink,FontStyle.Bold);
                Label(card.transform,EnchantmentEffect(kind,level),12,35,368,32,17,Ink);
            }
            Label(modal.transform,"Списано: "+paid+" опыта · осталось: "+ShortAmount(game.Progress.enchantingExperience),44,636,392,44,18,Muted,FontStyle.Normal,TextAnchor.MiddleCenter);
            ButtonAt(modal.transform,"ОСТАВИТЬ ЧАРЫ",44,748,392,48,CloseModal,Hex("bad5ca"),Ink).name="Close enchantment result";
        }
        private void ShowEnchantmentGuide()
        {
            if(applicationPaused || modal!=null) return;
            modal=CreateModal("Enchantment guide modal");Card(modal.transform,"Enchantment guide card",24,105,432,750,Panel);
            Label(modal.transform,"КАК РАБОТАЮТ ЧАРЫ",44,120,392,40,24,Ink,FontStyle.Bold);
            string[] rules={"Каждый уровень добавляет 25% к урону главной кирки. Максимум V. Быстрее ломает камень и руду.","Уровень I расходует прочность раз в 2 удара, II — раз в 3, III — раз в 4. Урон не меняется.","Каждый уровень добавляет 20% монет за разрушенный блок руды. Максимум III. Камень даёт только опыт.","Часть полученного опыта ремонтирует кирку: 1 опыт восстанавливает 2 прочности. Этот опыт не копится на новые чары."};
            for(int i=0;i<4;i++)
            {
                var card=Card(modal.transform,"Enchantment rule "+i,44,176+i*136,392,126,Hex("ded3eb"));
                Label(card.transform,MineGame.EnchantmentName((EnchantKind)i),12,6,368,28,21,Ink,FontStyle.Bold);
                Label(card.transform,rules[i],12,40,368,78,18,Ink);
            }
            Label(modal.transform,"Новый материал кирки снимает все чары.",44,730,392,28,17,Muted);
            ButtonAt(modal.transform,"Понятно",44,790,392,40,CloseModal,Hex("bad5ca"),Ink).name="Close enchantment guide";
        }

        public void RepairEquippedPick()
        {
            if (applicationPaused || EconomyBusy || modal != null || !game.RepairPick()) return;
            Persist(); Flush(); sounds.Play("metal"); Refresh();
        }
        private void BuildDraftTools(Transform page)
        {
            draftToolsArea = Card(page, "Draft tools", 24, 148, 432, 540, Hex("ede1c2")).gameObject;
            Label(draftToolsArea.transform, "ВРЕМЕННЫЕ ИНСТРУМЕНТЫ", 20, 14, 392, 38, 22, Ink, FontStyle.Bold);
            Label(draftToolsArea.transform, "Для проверки черновика.\nОтключить перед публикацией.", 20, 62, 392, 58, 19, Muted);
            ButtonAt(draftToolsArea.transform, "+100 000 000 МОНЕТ", 20, 136, 392, 56, () => GrantDraftResources(0), Hex("dbbf75"), Ink).name = "Draft coins";
            ButtonAt(draftToolsArea.transform, "+10 000 ОПЫТА НА ЧАРЫ", 20, 212, 392, 56, () => GrantDraftResources(1), Hex("c7b1df"), Ink).name = "Draft experience";
            ButtonAt(draftToolsArea.transform, "+10 ЖЕТОНОВ", 20, 288, 392, 56, () => GrantDraftResources(2), Hex("bad5ca"), Ink).name = "Draft tickets";
            draftToolsState = Label(draftToolsArea.transform, "", 20, 386, 392, 100, 21, Ink);
        }
        public void GrantDraftResources(int kind)
        {
            if (!DraftToolsEnabled || applicationPaused || EconomyBusy || modal != null || game.EconomyLocked) return;
            if (kind == 0) game.Progress.coins = Math.Min(MineGame.CoinLimit, game.Progress.coins + 100000000);
            else if (kind == 1) game.Progress.enchantingExperience = Math.Min(MineGame.EnchantingExperienceLimit, game.Progress.enchantingExperience + 10000);
            else if (kind == 2) game.Progress.tickets = Math.Min(1000000, game.Progress.tickets + 10);
            else return;
            game.Progress.ratingTestProfile=true;
            Persist(); Flush(); Refresh();
        }
    }
}
