using System;

namespace MineLoot
{
    public enum EnchantKind { Efficiency, Unbreaking, Fortune, Mending, Slowness, Greed }

    public sealed partial class MineGame
    {
        public const long EnchantingExperienceLimit = 1000000000;
        public const int CurseChance = 80, MendingChancePerThousand = 1, TopEnchantChance = 10;
        public const int SlownessPercentPerRank = 25, GreedPercentPerRank = 15;
        private static readonly int[] PickLifetimes = { 160, 320, 640, 320, 1280, 1920 };
        public int PickLifetime => PickLifetimes[PickTier] * (HasPrestigeUpgrade(10)?150:100)/100;
        public int PickCondition => Math.Max(0, PickLifetime - Progress.pickWear);
        public long RepairCost => Math.Max(1, (30L + PickTier * 50) * Progress.pickWear / PickLifetime * (HasPrestigeUpgrade(11)?50:100)/100);
        public bool CanRepair => !EconomyLocked && Progress.pickWear > 0 && Progress.coins >= RepairCost;
        public float MiningDelayMultiplier => 1 + EnchantmentLevel(EnchantKind.Slowness) * SlownessPercentPerRank / 100f;
        public bool IsEnchanted => Progress.enchantments != 0;
        public int EnchantmentLevel(EnchantKind kind) => EnchantmentLevel(Progress.enchantments, kind);
        public static int EnchantmentLevel(int set, EnchantKind kind) => (set >> ((int)kind * 4)) & 15;
        public static int EnchantmentMax(EnchantKind kind) => kind == EnchantKind.Efficiency || kind >= EnchantKind.Slowness ? 5 : kind == EnchantKind.Mending ? 1 : 3;
        public static string EnchantmentName(EnchantKind kind) => new[] { "Эффективность", "Прочность", "Удача", "Починка", "Тяжесть", "Скупость" }[(int)kind];

        private void SanitizeEnchanting(bool legacy)
        {
            Progress.enchantingExperience = legacy ? Progress.experience : Math.Max(0, Math.Min(EnchantingExperienceLimit, Progress.enchantingExperience));
            Progress.enchantCount = Math.Max(0, Math.Min(1000000, Progress.enchantCount));
            Progress.enchantSeed = Math.Max(1, Progress.enchantSeed);
            int set = 0;
            foreach (EnchantKind kind in Enum.GetValues(typeof(EnchantKind)))
                set |= Math.Min(EnchantmentMax(kind), EnchantmentLevel(Math.Max(0, Progress.enchantments), kind)) << ((int)kind * 4);
            Progress.enchantments = set;
            Progress.pickBreaks = Math.Max(0, Math.Min(1000000000, Progress.pickBreaks));
            Progress.pickBreakStreak = Math.Max(0, Math.Min(MaxUpgrade, Progress.pickBreakStreak));
            Progress.pickWear = Math.Max(0, Math.Min(PickLifetime, Progress.pickWear));
            Progress.wearCarry = Math.Max(0, Math.Min(EnchantmentLevel(EnchantKind.Unbreaking), Progress.wearCarry));
        }

        private int RollEnchantments(int slot,int seed)
        {
            uint state=unchecked((uint)seed^(uint)Progress.enchantSeed^(uint)(slot+1)*0x9e3779b9u^(uint)(Progress.enchantCount+1)*0x85ebca6bu);
            if(state==0) state=6355;
            uint Next() { state^=state<<13;state^=state>>17;state^=state<<5;return state; }
            int count=slot==0?1:slot==1?2:2+(int)(Next()%2);
            if(Next()%100<20) count++;
            if(HasPrestigeUpgrade(17)) count++;
            bool mending = slot == 2 && Next() % 1000 < MendingChancePerThousand;
            bool curse = Next() % 100 < CurseChance;
            count = Math.Min(count, 3);
            if (mending && count == 3 && curse) count--;
            int[] kinds = { 0, 1, 2 };
            int set = 0;
            for(int i=0;i<count;i++)
            {
                int chosen=i+(int)(Next()%(uint)(kinds.Length-i));int kind=kinds[chosen];kinds[chosen]=kinds[i];kinds[i]=kind;
                int max = EnchantmentMax((EnchantKind)kind);
                int low = slot == 2 ? 2 : 1;
                int high = Math.Min(max - 1, slot + 2);
                bool topAllowed = slot == 2 && !(mending && kind == (int)EnchantKind.Unbreaking);
                int level = topAllowed && Next() % 100 < TopEnchantChance ? max : low + (int)(Next() % (uint)(high - low + 1));
                set |= level << (kind * 4);
            }
            if (mending) set |= 1 << ((int)EnchantKind.Mending * 4);
            if (curse)
            {
                var kind = Next() % 2 == 0 ? EnchantKind.Slowness : EnchantKind.Greed;
                int rank = (Next() % 100) switch { < 40 => 1, < 68 => 2, < 85 => 3, < 95 => 4, _ => 5 };
                set |= rank << ((int)kind * 4);
            }
            return set;
        }
        private void ReplacePickMaterial(int oldTier, bool newTool = true)
        {
            if(PickTier==oldTier) return;
            Progress.pickWear=Progress.wearCarry=Progress.enchantments=Progress.enchantCount=0;
            if (newTool) Progress.pickBreakStreak = 0;
        }

        public long EnchantmentCost(int slot)
        {
            if (slot < 0 || slot > 2) throw new ArgumentOutOfRangeException(nameof(slot));
            long baseCost = 40 + PickTier * 30L;
            // Bounded growth keeps rerolls available in long sessions and avoids overflow.
            return Math.Max(1,Math.Min(100000, baseCost + Progress.enchantCount * (20L + PickTier * 5)) * (slot + 1) * (100-EnchantmentDiscountPercent)/100);
        }
        public bool CanEnchant(int slot) => slot >= 0 && slot < 3 && !EconomyLocked && Progress.enchantCount < 1000000 && Progress.enchantingExperience >= EnchantmentCost(slot);
        public bool Enchant(int slot, Func<double> random)
        {
            if (!CanEnchant(slot)) return false;
            int seed = DrawSeed(random), set = RollEnchantments(slot,seed);
            long paid=EnchantmentCost(slot);
            Progress.enchantingExperience -= paid;
            Progress.spentEnchantingExperience=Math.Min(CoinLimit,Progress.spentEnchantingExperience+paid);
            Progress.enchantments = set; Progress.enchantCount++; Progress.enchantSeed = seed; Progress.wearCarry = 0;
            return true;
        }
        public bool RepairPick()
        {
            if (!CanRepair) return false;
            Progress.coins -= RepairCost; Progress.pickWear = Progress.wearCarry = Progress.pickBreakStreak = 0;
            return true;
        }
        private int WearPick(bool brokeObsidian)
        {
            int wear = 0;
            if (++Progress.wearCarry >= EnchantmentLevel(EnchantKind.Unbreaking) + 1)
            {
                Progress.wearCarry = 0;
                wear++;
            }
            if (brokeObsidian)
            {
                int percent = HasPrestigeUpgrade(ObsidianUpgrade) ? ObsidianWearPercent / 2 : ObsidianWearPercent;
                wear += (PickLifetime * percent + 99) / 100;
            }

            bool wasExhausted = PickCondition == 0;
            Progress.pickWear = Math.Min(PickLifetime, Progress.pickWear + wear);
            if (wasExhausted || PickCondition > 0) return 0;
            if (HasPrestigeUpgrade(ShieldUpgrade) && !Progress.prestigeShieldUsed)
            {
                Progress.prestigeShieldUsed = true;
                Progress.pickWear = Progress.wearCarry = 0;
                return 0;
            }

            int oldTier = PickTier;
            Progress.pickBreaks = Math.Min(1000000000, Progress.pickBreaks + 1);
            Progress.pickBreakStreak = Math.Min(MaxUpgrade, Progress.pickBreakStreak + 1);
            int lost = Math.Min(Progress.upgradeLevel, Progress.pickBreakStreak);
            Progress.upgradeLevel -= lost;
            ReplacePickMaterial(oldTier, false);
            Progress.pickWear = Progress.wearCarry = 0;
            return lost;
        }
        private void CreditEnchantingExperience(int amount)
        {
            int repairXp = EnchantmentLevel(EnchantKind.Mending) > 0 ? Math.Min(amount, (Progress.pickWear + 1) / 2) : 0;
            Progress.pickWear = Math.Max(0, Progress.pickWear - repairXp * 2);
            if (repairXp > 0 && Progress.pickWear == 0) Progress.pickBreakStreak = 0;
            Progress.enchantingExperience = Math.Min(EnchantingExperienceLimit, Progress.enchantingExperience + amount - repairXp);
        }
    }
}
