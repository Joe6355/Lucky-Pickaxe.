using System;

namespace MineLoot
{
    public enum BlockKind { Ore, Obsidian, Bedrock }

    [Serializable]
    public sealed class MineProgress
    {
        public int version = 4;
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
        public int boardSeed;
        public bool[] lostColumns = new bool[5];
        public int failedFloors;
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
        public int Relics => Math.Min(333333, Math.Max(0, Progress.stratum - 1 + (IsComplete ? 1 : 0) - Progress.failedFloors) / 3);
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
        public bool IsPerfect => Array.TrueForAll(Progress.depths, d => d == Rows);
        public bool IsComplete
        {
            get { for (int c = 0; c < Columns; c++) if (Progress.depths[c] < Rows && !Progress.lostColumns[c]) return false; return true; }
        }
        public bool CanAbandon => Progress.tickets > 0 && !IsComplete;

        public MineGame(MineProgress progress = null)
        {
            Progress = progress ?? new MineProgress { boardSeed = 17 };
            Sanitize();
        }

        private void Sanitize()
        {
            bool legacy = Progress.version < 3;
            bool legacyBoard = Progress.version < 4;
            Progress.version = 4;
            Progress.coins = Math.Max(0, Math.Min(CoinLimit, Progress.coins));
            Progress.tickets = Math.Max(0, Math.Min(1000000, Progress.tickets));
            Progress.hits = Math.Max(0, Math.Min(HitsPerTicket - 1, Progress.hits));
            Progress.upgradeLevel = Math.Max(0, Math.Min(MaxUpgrade, Progress.upgradeLevel));
            Progress.stratum = Math.Max(1, Math.Min(1000000, Progress.stratum));
            if (Progress.depths == null || Progress.depths.Length != Columns) Progress.depths = new int[Columns];
            if (Progress.damage == null || Progress.damage.Length != Columns) Progress.damage = new int[Columns];
            if (legacyBoard) { Progress.boardSeed = 0; Progress.lostColumns = new bool[Columns]; Progress.failedFloors = 0; }
            Progress.boardSeed = Math.Max(0, Progress.boardSeed);
            if (Progress.lostColumns == null || Progress.lostColumns.Length != Columns) Progress.lostColumns = new bool[Columns];
            for (int i = 0; i < Columns; i++)
            {
                Progress.depths[i] = Math.Max(0, Math.Min(Rows, Progress.depths[i]));
                if (Progress.depths[i] == Rows || BlockAt(i, Progress.depths[i]) != BlockKind.Bedrock) Progress.lostColumns[i] = false;
                Progress.damage[i] = Progress.depths[i] == Rows || BlockAt(i, Progress.depths[i]) == BlockKind.Bedrock ? 0 : Math.Max(0, Math.Min(BlockHardness(i, Progress.depths[i]) - 1, Progress.damage[i]));
            }
            Progress.failedFloors = Math.Max(0, Math.Min(Progress.stratum - 1 + (IsComplete ? 1 : 0), Progress.failedFloors));
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

        // One hidden obstacle at most per lane: 18% bedrock, 30% obsidian, 52% clear.
        // A saved seed fixes its location; reopening the application cannot reroll the shaft.
        public BlockKind BlockAt(int column, int row)
        {
            if (column < 0 || column >= Columns || row < 0 || row > Rows) throw new ArgumentOutOfRangeException();
            if (row == Rows || row == 0 || Progress.boardSeed == 0) return BlockKind.Ore;
            uint h = unchecked((uint)Progress.boardSeed ^ ((uint)(column + 1) * 0x9e3779b9u) ^ ((uint)Progress.stratum * 0x85ebca6bu));
            h ^= h >> 16; h = unchecked(h * 0x7feb352du); h ^= h >> 15;
            h = unchecked(h * 0x846ca68bu); h ^= h >> 16;
            if (row != 1 + (int)((h >> 8) % 4)) return BlockKind.Ore;
            return h % 100 < 18 ? BlockKind.Bedrock : h % 100 < 48 ? BlockKind.Obsidian : BlockKind.Ore;
        }

        public int BlockHardness(int column, int row) => BlockAt(column, row) == BlockKind.Obsidian ? Hardness + 2 : Hardness;

        private static int DrawSeed(Func<double> random)
        {
            if (random == null) throw new ArgumentNullException(nameof(random));
            double sample = random(); PickPower(sample); // Validate before changing any progress.
            return 1 + (int)(sample * (int.MaxValue - 1));
        }

        private void NextFloor(int seed, bool advance = true)
        {
            Array.Clear(Progress.depths, 0, Columns); Array.Clear(Progress.damage, 0, Columns);
            Array.Clear(Progress.lostColumns, 0, Columns);
            if (advance) Progress.stratum = Math.Min(1000000, Progress.stratum + 1);
            Progress.boardSeed = seed;
        }

        public bool Abandon(Func<double> random)
        {
            if (!CanAbandon) return false;
            int seed = DrawSeed(random);
            Progress.tickets--;
            NextFloor(seed, false);
            return true;
        }

        // Resolve atomically before presentation, so closing during animation cannot lose a reward.
        public RollResult Roll(Func<double> random)
        {
            if (Progress.tickets <= 0) return null;
            if (random == null) throw new ArgumentNullException(nameof(random));
            var result = new RollResult();
            result.levelBefore = MinerLevel;
            bool nextFloor = IsComplete;
            // Obtain all draws before mutating progress, including failures of an injected random source.
            for (int i = 0; i < Columns; i++)
            {
                result.types[i] = PickPower(random(), Progress.forgeLevel);
                result.powers[i] = result.types[i] + PowerBonus;
            }
            int seed = nextFloor ? DrawSeed(random) : Progress.boardSeed;
            if (nextFloor)
            {
                NextFloor(seed);
                result.newStratum = true;
            }
            Progress.tickets--;
            Progress.totalRolls = Math.Min(10000000, Progress.totalRolls + 1);
            Array.Copy(Progress.depths, result.before, Columns);
            Array.Copy(Progress.damage, result.damageBefore, Columns);
            Array.Copy(Progress.lostColumns, result.lostBefore, Columns);
            result.hardness = Hardness;
            for (int i = 0; i < Columns; i++)
            {
                int previous = Progress.depths[i];
                if (previous == Rows || Progress.lostColumns[i]) result.powers[i] = 0;
                for (int hit = 0; hit < result.powers[i] && Progress.depths[i] < Rows; hit++)
                {
                    int row = Progress.depths[i];
                    result.usedPower[i]++;
                    BlockKind kind = BlockAt(i, row);
                    if (kind == BlockKind.Bedrock || kind == BlockKind.Obsidian && result.types[i] != 4)
                    {
                        result.blockedBy[i] = kind; result.stopRows[i] = row; result.brokenPicks++;
                        if (kind == BlockKind.Bedrock) { Progress.lostColumns[i] = true; Progress.damage[i] = 0; result.lostChests++; }
                        break;
                    }
                    if (++Progress.damage[i] >= BlockHardness(i, row)) { Progress.damage[i] = 0; Progress.depths[i]++; }
                }
                int after = Progress.depths[i];
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
                result.lostAfter[i] = Progress.lostColumns[i];
            }
            Progress.totalBlocks = Math.Min(25000000, Progress.totalBlocks + result.broken);
            if (IsComplete)
            {
                result.completed = true;
                result.perfect = IsPerfect;
                if (result.perfect) { result.reward += FloorReward; Progress.tickets = Math.Min(1000000, Progress.tickets + 1); }
                else Progress.failedFloors++;
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
        public readonly bool[] lostBefore = new bool[MineGame.Columns], lostAfter = new bool[MineGame.Columns];
        public readonly BlockKind[] blockedBy = new BlockKind[MineGame.Columns];
        public readonly int[] usedPower = new int[MineGame.Columns], stopRows = new int[MineGame.Columns];
        public int hardness;
        public int broken;
        public int reward;
        public int chests;
        public bool newStratum;
        public bool completed;
        public bool perfect;
        public int brokenPicks, lostChests;
        public int levelBefore, levelAfter, levelCoins, levelTickets;
    }
}
