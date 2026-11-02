using System;

namespace MineLoot
{
    public enum EnchantKind { Efficiency, Unbreaking, Fortune, Mending }

    public sealed partial class MineGame
    {
        public const long EnchantingExperienceLimit = 1000000000;
        private static readonly int[] PickLifetimes = { 160, 320, 640, 320, 1280, 1920 };
        public int PickLifetime => PickLifetimes[PickTier];
        public int PickCondition => Math.Max(0, PickLifetime - Progress.pickWear);
        public long RepairCost => Math.Max(1, (30L + PickTier * 50) * Progress.pickWear / PickLifetime);
        public bool CanRepair => !ActionsLocked && Progress.pickWear > 0 && Progress.coins >= RepairCost;
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

        // Offers depend only on saved state; reopening the table cannot reroll them for free.
        public int EnchantmentOffer(int slot)
        {
            if (slot < 0 || slot > 2) throw new ArgumentOutOfRangeException(nameof(slot));
            uint state = unchecked((uint)Progress.enchantSeed ^ (uint)(slot + 1) * 0x9e3779b9u ^ (uint)Progress.enchantCount * 0x85ebca6bu);
            uint Next() { state ^= state << 13; state ^= state >> 17; state ^= state << 5; return state; }
            int efficiency = 1 + (int)(Next() % (uint)(slot + 2));
            int set = Math.Min(5, efficiency + (PickTier >= 4 && slot == 2 ? 1 : 0));
            if (slot >= 1) set |= (1 + (int)(Next() % 3)) << 4;
            if (slot == 2 || Next() % 3 == 0) set |= (1 + (int)(Next() % (uint)(slot + 1))) << 8;
            if (slot == 2 && Next() % 4 == 0) set |= 1 << 12;
            return set;
        }

        public long EnchantmentCost(int slot)
        {
            if (slot < 0 || slot > 2) throw new ArgumentOutOfRangeException(nameof(slot));
            long baseCost = 40 + PickTier * 30L;
            // Bounded growth keeps rerolls available in long sessions and avoids overflow.
            return Math.Min(100000, baseCost + Progress.enchantCount * (20L + PickTier * 5)) * (slot + 1);
        }
        public bool CanEnchant(int slot) => slot >= 0 && slot < 3 && !ActionsLocked && Progress.enchantCount < 1000000 && Progress.enchantingExperience >= EnchantmentCost(slot);
        public bool Enchant(int slot, Func<double> random)
        {
            if (!CanEnchant(slot)) return false;
            int seed = DrawSeed(random), set = EnchantmentOffer(slot);
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
