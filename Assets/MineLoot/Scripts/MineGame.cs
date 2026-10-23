using System;

namespace MineLoot
{
    [Serializable]
    public sealed class MineProgress
    {
        public int version = 3;
        public long coins = 80;
        public int tickets = 2;
        public int hits;
        public int upgradeLevel;
        public int stratum = 1;
        public int[] depths = new int[5];
        public int[] damage = new int[5];
        public int forgeLevel;
        public long totalBlocks;
        public int claimedContracts;
        public long totalClicks;
        public int totalRolls;
        public long experience;
        public long oresMined;
        public int oreKind;
        public int oreDamage;
    }

    // Pure rules: no scene dependencies, animation or platform SDK calls.
    public sealed class MineGame
    {
        public const int Columns = 5;
        public const int Rows = 5;
        public const int HitsPerTicket = 12;
        public const int MaxForge = 10;
        public const int MaxUpgrade = 100;
        public const int MaxMinerLevel = 50;
        public const long CoinLimit = 1000000000000L;
        private static readonly int[] OreLevels = { 1, 2, 4, 7, 10 };
        private static readonly int[] OreHealths = { 6, 9, 12, 16, 20 };
        private static readonly int[] OreRewards = { 12, 28, 55, 95, 150 };
        private static readonly string[] OreKeys = { "coal", "iron", "gold", "diamond", "emerald" };
        private static readonly int[] OreCycle = { 0, 1, 0, 2, 1, 0, 3, 1, 2, 0, 4, 2 };
        public MineProgress Progress { get; }
        public MineHit LastHit { get; private set; }
        public int MinerLevel => LevelForExperience(Progress.experience);
        public long LevelExperience => Progress.experience - ExperienceForLevel(MinerLevel);
        public long LevelGoal => MinerLevel == MaxMinerLevel ? 0 : ExperienceForLevel(MinerLevel + 1) - ExperienceForLevel(MinerLevel);
        public int UnlockedOres { get { int count = 1; while (count < OreLevels.Length && MinerLevel >= OreLevels[count]) count++; return count; } }
        public string OreKey => OreKeys[Progress.oreKind];
        public int OreHealth => OreHealths[Progress.oreKind];
        public int OreReward => OreRewards[Progress.oreKind];
        public int MiningDamage => 1 + Progress.upgradeLevel / 5;
        public int PickTier => Progress.upgradeLevel >= 20 ? 4 : Progress.upgradeLevel >= 12 ? 3 : Progress.upgradeLevel >= 5 ? 2 : Progress.upgradeLevel >= 2 ? 1 : 0;
        public int NextPickUpgrade => PickTier == 4 ? MaxUpgrade : new[] { 2, 5, 12, 20 }[PickTier];
        public static string OreKeyAt(int kind) => OreKeys[kind];
        public static int OreUnlockLevel(int kind) => OreLevels[kind];
        public static long ExperienceForLevel(int level) { long l = Math.Max(1, Math.Min(MaxMinerLevel, level)); return (l - 1) * (l + 2) * 10; }
        public static int LevelForExperience(long experience)
        {
            int level = 1;
            while (level < MaxMinerLevel && experience >= ExperienceForLevel(level + 1)) level++;
            return level;
        }
        public int Relics => Math.Min(333333, (Progress.stratum - 1 + (IsComplete ? 1 : 0)) / 3);
        public int ClickPower => 8 + Progress.upgradeLevel * 4 + Relics;
        public int Hardness => Math.Min(4, 1 + (Progress.stratum - 1) / 3);
        public int Biome => ((Progress.stratum - 1) / 3) % 3;
        public int BlockReward => 6 + 2 * Math.Min(99, Progress.stratum - 1);
        public int ChestReward => 60 + 20 * Math.Min(99, Progress.stratum - 1);
        public int FloorReward => 100 + 30 * Math.Min(99, Progress.stratum - 1);
        public int PowerBonus => Progress.forgeLevel / 2;
        public int DiamondChance => 10 + Progress.forgeLevel;
        public int IronChance => 35 + Progress.forgeLevel;
        public long UpgradeCost => (long)Math.Min(CoinLimit, Math.Round(120 * Math.Pow(1.28, Progress.upgradeLevel)));
        public long ForgeCost => (long)Math.Round(180 * Math.Pow(1.42, Progress.forgeLevel));
        public bool CanForge => Progress.forgeLevel < MaxForge && Progress.coins >= ForgeCost;
        public long ContractTarget => (Progress.claimedContracts + 1L) * 25;
        public int ContractReward => 100 + 40 * Math.Min(1000, Progress.claimedContracts);
        public bool CanClaim => Progress.totalBlocks >= ContractTarget;
        public bool CanUpgrade => Progress.upgradeLevel < MaxUpgrade && Progress.coins >= UpgradeCost;
        public bool IsComplete => Array.TrueForAll(Progress.depths, d => d == Rows);

        public MineGame(MineProgress progress = null)
        {
            Progress = progress ?? new MineProgress();
            Sanitize();
        }

        private void Sanitize()
        {
            bool legacy = Progress.version < 3;
            Progress.version = 3;
            Progress.coins = Math.Max(0, Math.Min(CoinLimit, Progress.coins));
            Progress.tickets = Math.Max(0, Math.Min(1000000, Progress.tickets));
            Progress.hits = Math.Max(0, Math.Min(HitsPerTicket - 1, Progress.hits));
            Progress.upgradeLevel = Math.Max(0, Math.Min(MaxUpgrade, Progress.upgradeLevel));
            Progress.stratum = Math.Max(1, Math.Min(1000000, Progress.stratum));
            if (Progress.depths == null || Progress.depths.Length != Columns) Progress.depths = new int[Columns];
            if (Progress.damage == null || Progress.damage.Length != Columns) Progress.damage = new int[Columns];
            for (int i = 0; i < Columns; i++)
            {
                Progress.depths[i] = Math.Max(0, Math.Min(Rows, Progress.depths[i]));
                Progress.damage[i] = Progress.depths[i] == Rows ? 0 : Math.Max(0, Math.Min(Hardness - 1, Progress.damage[i]));
            }
            Progress.forgeLevel = Math.Max(0, Math.Min(MaxForge, Progress.forgeLevel));
            Progress.totalBlocks = Math.Max(0, Math.Min(25000000L, Progress.totalBlocks));
            Progress.claimedContracts = Math.Max(0, Math.Min((int)(Progress.totalBlocks / 25), Progress.claimedContracts));
            Progress.totalClicks = Math.Max(0, Math.Min(CoinLimit, Progress.totalClicks));
            Progress.totalRolls = Math.Max(0, Math.Min(10000000, Progress.totalRolls));
            // Old clicks count toward the new level; migration never pays historical level rewards.
            Progress.experience = Math.Max(0, Math.Min(ExperienceForLevel(MaxMinerLevel), legacy ? Progress.totalClicks : Progress.experience));
            Progress.oresMined = Math.Max(0, Math.Min(CoinLimit, Progress.oresMined));
            Progress.oreKind = Math.Max(0, Math.Min(UnlockedOres - 1, Progress.oreKind));
            Progress.oreDamage = Math.Max(0, Math.Min(OreHealth - 1, Progress.oreDamage));
        }

        public bool Mine()
        {
            var hit = new MineHit { ore = Progress.oreKind, damage = MiningDamage, coins = ClickPower, experience = 1 };
            int previousLevel = MinerLevel;
            int previousUnlocks = UnlockedOres;
            Progress.oreDamage += MiningDamage;
            if (Progress.oreDamage >= OreHealth)
            {
                hit.broken = true;
                hit.coins += OreReward;
                hit.experience += 6 + Progress.oreKind * 4;
                Progress.oresMined = Math.Min(CoinLimit, Progress.oresMined + 1);
                Progress.oreDamage = 0;
            }
            hit.coins += GainExperience(hit.experience, out hit.tickets);
            hit.levels = MinerLevel - previousLevel;
            if (hit.broken)
                Progress.oreKind = UnlockedOres > previousUnlocks ? UnlockedOres - 1 : Math.Min(UnlockedOres - 1, OreCycle[Progress.oresMined % OreCycle.Length]);
            AddCoins(hit.coins);
            Progress.totalClicks = Math.Min(CoinLimit, Progress.totalClicks + 1);
            Progress.hits++;
            bool ticket = Progress.hits == HitsPerTicket;
            if (ticket) { Progress.hits = 0; hit.tickets++; Progress.tickets = Math.Min(1000000, Progress.tickets + 1); }
            LastHit = hit;
            return ticket;
        }

        private int GainExperience(int amount, out int tickets)
        {
            int before = MinerLevel, coins = 0;
            Progress.experience = Math.Min(ExperienceForLevel(MaxMinerLevel), Progress.experience + amount);
            tickets = MinerLevel - before;
            for (int level = before + 1; level <= MinerLevel; level++) coins += 40 * level;
            Progress.tickets = Math.Min(1000000, Progress.tickets + tickets);
            return coins;
        }

        public bool Upgrade()
        {
            if (!CanUpgrade) return false;
            Progress.coins -= UpgradeCost;
            Progress.upgradeLevel++;
            return true;
        }

        public bool Forge()
        {
            if (!CanForge) return false;
            Progress.coins -= ForgeCost;
            Progress.forgeLevel++;
            return true;
        }

        public bool ClaimContract()
        {
            if (!CanClaim) return false;
            AddCoins(ContractReward);
            Progress.tickets = Math.Min(1000000, Progress.tickets + 1);
            Progress.claimedContracts++;
            return true;
        }

        public static int PickPower(double sample, int forgeLevel = 0)
        {
            if (double.IsNaN(sample) || sample < 0 || sample >= 1) throw new ArgumentOutOfRangeException(nameof(sample));
            int level = Math.Max(0, Math.Min(MaxForge, forgeLevel));
            return sample < (.55 - .02 * level) ? 1 : sample < (.90 - .01 * level) ? 2 : 4;
        }

        // Resolve atomically before presentation, so closing during animation cannot lose a reward.
        public RollResult Roll(Func<double> random)
        {
            if (Progress.tickets <= 0) return null;
            if (random == null) throw new ArgumentNullException(nameof(random));
            var result = new RollResult();
            result.levelBefore = MinerLevel;
            // Obtain all draws before mutating progress, including failures of an injected random source.
            for (int i = 0; i < Columns; i++)
            {
                result.types[i] = PickPower(random(), Progress.forgeLevel);
                result.powers[i] = result.types[i] + PowerBonus;
            }
            if (IsComplete)
            {
                Array.Clear(Progress.depths, 0, Columns);
                Array.Clear(Progress.damage, 0, Columns);
                Progress.stratum = Math.Min(1000000, Progress.stratum + 1);
                result.newStratum = true;
            }
            Progress.tickets--;
            Progress.totalRolls = Math.Min(10000000, Progress.totalRolls + 1);
            Array.Copy(Progress.depths, result.before, Columns);
            Array.Copy(Progress.damage, result.damageBefore, Columns);
            result.hardness = Hardness;
            for (int i = 0; i < Columns; i++)
            {
                int previous = Progress.depths[i];
                int damage = Progress.damage[i] + result.powers[i];
                int after = Math.Min(Rows, previous + damage / Hardness);
                Progress.damage[i] = after == Rows ? 0 : damage % Hardness;
                if (previous == Rows) { result.scrap += result.powers[i] * 2; Progress.damage[i] = 0; }
                int broken = after - previous;
                result.broken += broken;
                result.reward += broken * BlockReward;
                if (previous < Rows && after == Rows)
                {
                    result.chests++;
                    result.reward += ChestReward;
                }
                Progress.depths[i] = after;
                result.after[i] = after;
                result.damageAfter[i] = Progress.damage[i];
            }
            result.reward += result.scrap;
            Progress.totalBlocks = Math.Min(25000000, Progress.totalBlocks + result.broken);
            if (IsComplete)
            {
                result.completed = true;
                result.reward += FloorReward;
                Progress.tickets = Math.Min(1000000, Progress.tickets + 1);
            }
            result.levelCoins = GainExperience(result.broken, out result.levelTickets);
            result.levelAfter = MinerLevel;
            result.reward += result.levelCoins;
            AddCoins(result.reward);
            return result;
        }

        private void AddCoins(long value) => Progress.coins = Math.Min(CoinLimit, Progress.coins + value);
    }

    public struct MineHit
    {
        public int ore, damage, coins, experience, levels, tickets;
        public bool broken;
    }

    public sealed class RollResult
    {
        public readonly int[] powers = new int[MineGame.Columns];
        public readonly int[] types = new int[MineGame.Columns];
        public readonly int[] before = new int[MineGame.Columns];
        public readonly int[] after = new int[MineGame.Columns];
        public readonly int[] damageBefore = new int[MineGame.Columns];
        public readonly int[] damageAfter = new int[MineGame.Columns];
        public int hardness;
        public int broken;
        public int scrap;
        public int reward;
        public int chests;
        public bool newStratum;
        public bool completed;
        public int levelBefore, levelAfter, levelCoins, levelTickets;
    }
}
