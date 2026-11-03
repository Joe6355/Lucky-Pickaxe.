using System;

namespace MineLoot
{
    public enum EnchantKind { Efficiency, Unbreaking, Fortune, Mending }

    public sealed partial class MineGame
    {
        public const long EnchantingExperienceLimit = 1000000000;
        private static readonly int[] PickLifetimes = { 160, 320, 640, 320, 1280, 1920 };
        public int PickLifetime => PickLifetimes[PickTier] * (HasPrestigeUpgrade(10)?150:100)/100;
        public int PickCondition => Math.Max(0, PickLifetime - Progress.pickWear);
        public long RepairCost => Math.Max(1, (30L + PickTier * 50) * Progress.pickWear / PickLifetime * (HasPrestigeUpgrade(11)?50:100)/100);
        public bool CanRepair => !EconomyLocked && Progress.pickWear > 0 && Progress.coins >= RepairCost;
        public bool IsEnchanted => Progress.enchantments != 0;
        public int EnchantmentLevel(EnchantKind kind) => EnchantmentLevel(Progress.enchantments, kind);
        public static int EnchantmentLevel(int set, EnchantKind kind) => (set >> ((int)kind * 4)) & 15;
        public static int EnchantmentMax(EnchantKind kind) => kind == EnchantKind.Efficiency ? 5 : kind == EnchantKind.Mending ? 1 : 3;
        public static string EnchantmentName(EnchantKind kind) => new[] { "Эффективность", "Прочность", "Удача", "Починка" }[(int)kind];

        private void SanitizeEnchanting(bool legacy)
        {
            Progress.enchantingExperience = legacy ? Progress.experience : Math.Max(0, Math.Min(EnchantingExperienceLimit, Progress.enchantingExperience));
            Progress.enchantCount = Math.Max(0, Math.Min(1000000, Progress.enchantCount));
            Progress.enchantSeed = Math.Max(1, Progress.enchantSeed);
            int set = 0;
            foreach (EnchantKind kind in Enum.GetValues(typeof(EnchantKind)))
                set |= Math.Min(EnchantmentMax(kind), EnchantmentLevel(Math.Max(0, Progress.enchantments), kind)) << ((int)kind * 4);
            Progress.enchantments = set;
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
            int[] kinds=slot==2?new[]{0,1,2,3}:new[]{0,1,2};
            count=Math.Min(count,kinds.Length); int set=0;
            for(int i=0;i<count;i++)
            {
                int chosen=i+(int)(Next()%(uint)(kinds.Length-i));int kind=kinds[chosen];kinds[chosen]=kinds[i];kinds[i]=kind;
                int max=EnchantmentMax((EnchantKind)kind),low=slot==2&&max>1?2:1;
                int high=Math.Min(max,slot==0?2:slot==1?3:5);
                int level=low+(int)(Next()%(uint)(high-low+1));set|=level<<(kind*4);
            }
            return set;
        }
        private void ReplacePickMaterial(int oldTier)
        {
            if(PickTier==oldTier) return;
            Progress.pickWear=Progress.wearCarry=Progress.enchantments=Progress.enchantCount=0;
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
            Progress.enchantingExperience -= EnchantmentCost(slot);
            Progress.enchantments = set; Progress.enchantCount++; Progress.enchantSeed = seed; Progress.wearCarry = 0;
            return true;
        }
        public bool RepairPick()
        {
            if (!CanRepair) return false;
            Progress.coins -= RepairCost; Progress.pickWear = Progress.wearCarry = 0;
            return true;
        }
        private void WearPick()
        {
            if (++Progress.wearCarry >= EnchantmentLevel(EnchantKind.Unbreaking) + 1)
            {
                Progress.wearCarry = 0; Progress.pickWear = Math.Min(PickLifetime, Progress.pickWear + 1);
            }
        }
        private void CreditEnchantingExperience(int amount)
        {
            int repairXp = EnchantmentLevel(EnchantKind.Mending) > 0 ? Math.Min(amount, (Progress.pickWear + 1) / 2) : 0;
            Progress.pickWear = Math.Max(0, Progress.pickWear - repairXp * 2);
            Progress.enchantingExperience = Math.Min(EnchantingExperienceLimit, Progress.enchantingExperience + amount - repairXp);
        }
    }
}
