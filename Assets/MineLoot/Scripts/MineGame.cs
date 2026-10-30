using System;

namespace MineLoot
{
    public enum BlockKind { Ore, Obsidian, Bedrock }
    public enum UpgradeKind { Pickaxe, Forge, Swap, Efficiency, Press, Treasure, Worker, Drill, Cart, Rails, Warehouse }

    [Serializable]
    public sealed class MineProgress
    {
        public int version = 11;
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
        public bool rollPending;
        public int[] pendingTypes = new int[5];
        public bool pendingSwapUsed; // Read only when importing v5; v6 uses the counter below.
        public bool pendingNewStratum;
        public int swapLevel, efficiencyLevel, pressLevel, treasureLevel;
        public int pendingSwapLimit, pendingSwapCount;
        public int milestoneClaims;
        public int workerCount, drillCount, cartCount, railLevel;
        public double productionCarry;
        public long totalProduced;
        public int veinCharge;
        public double veinOfferLeft, veinBoostLeft;
        public int warehouseLevel, offlineCapacitySeconds, offlineCarry;
        public long lastSeenUtc, offlineRateMilli, totalOfflineCoins;
        public long offlineCoins, offlineRewardSeconds, offlineElapsedSeconds, offlineRewardRateMilli;
        public int offlineRewardCapacity;
        public int prestigeCount, crystals, totalCrystals, prestigeUpgrades;
        public int experienceCarry;
        public long[] milestoneBest = new long[16];
        public int guideStep;
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
        public const int VeinOreTarget = 24, VeinOfferSeconds = 20, VeinBoostSeconds = 12, VeinMultiplier = 3;
        public const int MaxWarehouse = 4, MaxOfflineSeconds = 12 * 3600;
        public const int PrestigeUpgradeCount = 9, MaxPrestiges = 1000000, MaxCrystals = 10000000;
        public const int GuideStepCount = 5;
        private const long MaxUtcSeconds = 253402300799L, MaxProductionMilli = 14875000;
        private static readonly PrestigeUpgrade[] PrestigeUpgrades = {
            new PrestigeUpgrade("Верный удар", "+25% монет\nза удар и руду", "pick4", 2),
            new PrestigeUpgrade("Артель", "+25% дохода\nпроизводства", "worker", 3),
            new PrestigeUpgrade("Секрет клада", "+25% награды\nсундуков", "chest", 5),
            new PrestigeUpgrade("Запас монет", "+1000 монет\nв новой шахте", "coin", 2),
            new PrestigeUpgrade("Каменный старт", "Стартовая\nкирка ур. 2", "pick-stone", 3),
            new PrestigeUpgrade("Своя бригада", "Один шахтёр\nна старте", "worker", 5),
            new PrestigeUpgrade("Чутьё", "Жила через\n20 блоков", "tile-gold", 2),
            new PrestigeUpgrade("Долгая жила", "Усиление\nна 16 секунд", "ticket", 3),
            new PrestigeUpgrade("Сила жилы", "Жила даёт\n×4 монеты", "tile-diamond", 5)
        };
        public static PrestigeUpgrade PrestigeUpgradeAt(int index) => PrestigeUpgrades[index];
        private static readonly int[] OreLevels = { 1, 2, 4, 7, 10 };
        private static readonly int[] OreHealths = { 6, 9, 12, 16, 20 };
        private static readonly int[] OreRewards = { 12, 28, 55, 95, 150 };
        private static readonly string[] OreKeys = { "coal", "iron", "gold", "diamond", "emerald" };
        private static readonly int[] OreCycle = { 0, 1, 0, 2, 1, 0, 3, 1, 2, 0, 4, 2 };
        // Stable order: each index is a saved claim bit. Append new goals instead of reordering.
        private static readonly Milestone[] Milestones = {
            new Milestone("Первые искры", "Нанеси 30 ударов по руде", "pick1", 30, 120, 1),
            new Milestone("Каменолом", "Добудь 10 блоков руды", "tile-coal", 10, 180, 1),
            new Milestone("Искатель удачи", "Сделай 5 бросков рулетки", "ticket", 5, 240, 1),
            new Milestone("Железный век", "Улучши кирку до уровня 5", "pick2", 5, 300, 1),
            new Milestone("Проходчик", "Разбей 50 блоков рулетки", "tile-iron", 50, 400, 2),
            new Milestone("Алмазная жила", "Достигни уровня шахтёра 7", "tile-diamond", 7, 500, 2),
            new Milestone("Первый манёвр", "Купи перестановку кирок", "swap", 1, 200, 1),
            new Milestone("Верный удар", "Подними остроту до 3", "pick-head2", 3, 600, 2),
            new Milestone("Новый горизонт", "Доберись до пласта 10", "tile-gold", 10, 800, 2),
            new Milestone("Мастер кузницы", "Улучши кузницу до 6", "forge", 6, 1000, 2),
            new Milestone("Поток жетонов", "Улучши пресс до 3", "ticket", 3, 900, 2),
            new Milestone("Кристальная кирка", "Улучши кирку до уровня 20", "pick4", 20, 1500, 3),
            new Milestone("Охотник за кладом", "Подними сокровища до 5", "chest", 5, 1400, 3),
            new Milestone("Хранитель реликвий", "Собери 3 реликвии", "tile-emerald", 3, 1200, 3),
            new Milestone("Опытный шахтёр", "Достигни уровня шахтёра 25", "pick-gold", 25, 1800, 3),
            new Milestone("Легенда шахты", "Достигни уровня шахтёра 50", "medal", 50, 3000, 5)
        };
        public static int MilestoneCount => Milestones.Length;
        public static Milestone MilestoneAt(int index) => Milestones[index];
        public MineProgress Progress { get; private set; }
        public MineHit LastHit { get; private set; }
        public int MinerLevel => LevelForExperience(Progress.experience);
        public long LevelExperience => Progress.experience - ExperienceForLevel(MinerLevel);
        public long LevelGoal => MinerLevel == MaxMinerLevel ? 0 : ExperienceForLevel(MinerLevel + 1) - ExperienceForLevel(MinerLevel);
        public int UnlockedOres { get { int count = 1; while (count < OreLevels.Length && MinerLevel >= OreLevels[count]) count++; return count; } }
        public string OreKey => OreKeys[Progress.oreKind];
        public int OreHealth => OreHealths[Progress.oreKind];
        public int OreReward => OreRewards[Progress.oreKind] * (HasPrestigeUpgrade(0) ? 125 : 100) / 100;
        public int MiningDamage => 1 + Progress.upgradeLevel / 5 + Progress.efficiencyLevel;
        public int HitsRequired => HitsPerTicket - Progress.pressLevel;
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
        public int ClickPower => (8 + Progress.upgradeLevel * 4 + Relics) * (HasPrestigeUpgrade(0) ? 125 : 100) / 100;
        public int ClickUpgradeGain => HasPrestigeUpgrade(0) ? 5 : 4;
        public int PrestigeProductionPercent => HasPrestigeUpgrade(1) ? 125 : 100;
        public long WorkerProductionMilli => 2000L * PrestigeProductionPercent / 100;
        public long DrillProductionMilli => 12000L * (10 + Progress.forgeLevel) / 10 * PrestigeProductionPercent / 100;
        public int PrestigeStartCoins => HasPrestigeUpgrade(3) ? 1080 : 80;
        public int PrestigeStartPick => HasPrestigeUpgrade(4) ? 2 : 0;
        public int PrestigeStartWorkers => HasPrestigeUpgrade(5) ? 1 : 0;
        public int PrestigeExperiencePercent => 100 + Math.Min(5, Progress.prestigeCount) * 15;
        public int RichVeinTarget => HasPrestigeUpgrade(6) ? 20 : VeinOreTarget;
        public int RichVeinDuration => HasPrestigeUpgrade(7) ? 16 : VeinBoostSeconds;
        public int RichVeinMultiplier => HasPrestigeUpgrade(8) ? 4 : VeinMultiplier;
        public bool VeinOffered => Progress.veinOfferLeft > 0;
        public bool VeinActive => Progress.veinBoostLeft > 0;
        public int ManualMultiplier => VeinActive ? RichVeinMultiplier : 1;
        public int ManualClickPower => ClickPower * ManualMultiplier;
        public int CartBonus => Progress.cartCount * (10 + 2 * Progress.railLevel);
        public int DrillBonus => Progress.forgeLevel * 10;
        public long ProductionMilli => (Progress.workerCount * 2000L + Progress.drillCount * 12000L * (10 + Progress.forgeLevel) / 10) * (100 + CartBonus) / 100 * (HasPrestigeUpgrade(1) ? 125 : 100) / 100;
        public int OfflineHours => 4 + Progress.warehouseLevel * 2;
        public bool HasOfflineReward => Progress.offlineCoins > 0;
        public bool CanClaimOffline => HasOfflineReward && !HasPendingRoll && Progress.coins < CoinLimit;
        public long DoubledOfflineReward => Progress.offlineCoins * 2;
        public bool CanDoubleOffline => CanClaimOffline && DoubledOfflineReward <= CoinLimit - Progress.coins;
        public int Hardness => Math.Min(4, 1 + (Progress.stratum - 1) / 3);
        public int Biome => ((Progress.stratum - 1) / 3) % 3;
        public int BlockReward => 6 + 2 * Math.Min(99, Progress.stratum - 1);
        public int ChestReward => (60 + 20 * Math.Min(99, Progress.stratum - 1)) * (100 + Progress.treasureLevel * 15) * (HasPrestigeUpgrade(2) ? 125 : 100) / 10000;
        public int FloorReward => 100 + 30 * Math.Min(99, Progress.stratum - 1);
        public int PowerBonus => Progress.forgeLevel / 2;
        public int DiamondChance => 10 + Progress.forgeLevel;
        public int IronChance => 35 + Progress.forgeLevel;
        public long UpgradeCost => UpgradePrice(UpgradeKind.Pickaxe);
        public long ForgeCost => UpgradePrice(UpgradeKind.Forge);
        public bool CanForge => CanBuyUpgrade(UpgradeKind.Forge);
        public long ContractTarget => (Progress.claimedContracts + 1L) * 25;
        public int ContractReward => 100 + 40 * Math.Min(1000, Progress.claimedContracts);
        public bool CanClaim => !HasPendingRoll && Progress.totalBlocks >= ContractTarget;
        public bool CanUpgrade => CanBuyUpgrade(UpgradeKind.Pickaxe);
        public bool IsPerfect => Array.TrueForAll(Progress.depths, d => d == Rows);
        public bool IsComplete
        {
            get { for (int c = 0; c < Columns; c++) if (Progress.depths[c] < Rows && !Progress.lostColumns[c]) return false; return true; }
        }
        public bool HasPendingRoll => Progress.rollPending;
        public bool GuideActive => Progress.guideStep < GuideStepCount;
        public bool CanAbandon => !HasPendingRoll && Progress.tickets > 0 && !IsComplete;
        public bool IsActiveColumn(int column) => column >= 0 && column < Columns && Progress.depths[column] < Rows && !Progress.lostColumns[column];
        public int PendingPower(int column) => HasPendingRoll && column >= 0 && column < Columns && Progress.pendingTypes[column] > 0 ? Progress.pendingTypes[column] + PowerBonus : 0;
        public int SwapsRemaining => HasPendingRoll ? Math.Max(0, Progress.pendingSwapLimit - Progress.pendingSwapCount) : 0;
        public bool CanSwapPicks(int first, int second) => HasPendingRoll && SwapsRemaining > 0 && first != second && IsActiveColumn(first) && IsActiveColumn(second) && Progress.pendingTypes[first] != Progress.pendingTypes[second];
        public bool CanSwap
        {
            get { for (int a = 0; a < Columns; a++) for (int b = a + 1; b < Columns; b++) if (CanSwapPicks(a, b)) return true; return false; }
        }

        public MineGame(MineProgress progress = null)
        {
            Progress = progress ?? new MineProgress { boardSeed = 17 };
            Sanitize();
        }

        private void Sanitize()
        {
            bool legacy = Progress.version < 3;
            bool legacyBoard = Progress.version < 4;
            bool legacyRoll = Progress.version < 5;
            bool legacyUpgrades = Progress.version < 6;
            bool legacyGuide = Progress.version < 11;
            Progress.version = 11;
            // Existing players opt in through Rules; an update never covers their contract card.
            Progress.guideStep = legacyGuide ? GuideStepCount : Math.Max(0, Math.Min(GuideStepCount, Progress.guideStep));
            Progress.prestigeCount = Math.Max(0, Math.Min(MaxPrestiges, Progress.prestigeCount));
            Progress.experienceCarry = Math.Max(0, Math.Min(99, Progress.experienceCarry));
            Progress.totalCrystals = Math.Max(0, Math.Min(MaxCrystals, Progress.totalCrystals));
            Progress.crystals = Math.Max(0, Math.Min(Progress.totalCrystals, Progress.crystals));
            Progress.prestigeUpgrades = Math.Max(0, Progress.prestigeUpgrades) & ((1 << PrestigeUpgradeCount) - 1);
            for (int i = 0; i < PrestigeUpgradeCount; i++)
                if (i % 3 > 0 && HasPrestigeUpgrade(i) && !HasPrestigeUpgrade(i - 1)) throw new FormatException("Invalid prestige upgrade branch");
            if (Progress.milestoneBest == null || Progress.milestoneBest.Length != MilestoneCount)
            {
                var best = new long[MilestoneCount];
                if (Progress.milestoneBest != null) Array.Copy(Progress.milestoneBest, best, Math.Min(Progress.milestoneBest.Length, best.Length));
                Progress.milestoneBest = best;
            }
            for (int i = 0; i < MilestoneCount; i++) Progress.milestoneBest[i] = Math.Max(0, Math.Min(Milestones[i].target, Progress.milestoneBest[i]));
            Progress.warehouseLevel = Math.Max(0, Math.Min(MaxWarehouse, Progress.warehouseLevel));
            Progress.lastSeenUtc = Math.Max(0, Math.Min(MaxUtcSeconds, Progress.lastSeenUtc));
            Progress.offlineRateMilli = Math.Max(0, Math.Min(MaxProductionMilli, Progress.offlineRateMilli));
            Progress.offlineCapacitySeconds = Math.Max(0, Math.Min(MaxOfflineSeconds, Progress.offlineCapacitySeconds));
            Progress.offlineCarry = Math.Max(0, Math.Min(1999, Progress.offlineCarry));
            Progress.totalOfflineCoins = Math.Max(0, Math.Min(CoinLimit, Progress.totalOfflineCoins));
            Progress.offlineCoins = Math.Max(0, Math.Min(CoinLimit, Progress.offlineCoins));
            Progress.offlineRewardSeconds = Math.Max(0, Math.Min(MaxOfflineSeconds, Progress.offlineRewardSeconds));
            Progress.offlineElapsedSeconds = Math.Max(0, Math.Min(MaxUtcSeconds, Progress.offlineElapsedSeconds));
            Progress.offlineRewardRateMilli = Math.Max(0, Math.Min(MaxProductionMilli, Progress.offlineRewardRateMilli));
            Progress.offlineRewardCapacity = Math.Max(0, Math.Min(MaxOfflineSeconds, Progress.offlineRewardCapacity));
            if (HasOfflineReward)
            {
                long maximum = Progress.offlineRewardRateMilli * Progress.offlineRewardSeconds / 2000 + 1;
                if (Progress.offlineRewardSeconds == 0 || Progress.offlineRewardSeconds > Progress.offlineRewardCapacity || Progress.offlineElapsedSeconds < Progress.offlineRewardSeconds || Progress.offlineRewardRateMilli == 0 || Progress.offlineCoins > maximum) throw new FormatException("Invalid offline reward");
            }
            else ClearOfflineReward();
            Progress.workerCount = Math.Max(0, Math.Min(100, Progress.workerCount));
            Progress.drillCount = Math.Max(0, Math.Min(50, Progress.drillCount));
            Progress.cartCount = Math.Max(0, Math.Min(25, Progress.cartCount));
            Progress.railLevel = Math.Max(0, Math.Min(10, Progress.railLevel));
            Progress.productionCarry = FiniteClamp(Progress.productionCarry, 0, .999999999);
            Progress.totalProduced = Math.Max(0, Math.Min(CoinLimit, Progress.totalProduced));
            Progress.veinCharge = Math.Max(0, Math.Min(RichVeinTarget - 1, Progress.veinCharge));
            Progress.veinOfferLeft = FiniteClamp(Progress.veinOfferLeft, 0, VeinOfferSeconds);
            Progress.veinBoostLeft = FiniteClamp(Progress.veinBoostLeft, 0, RichVeinDuration);
            if (VeinActive) Progress.veinOfferLeft = 0;
            Progress.milestoneClaims = Math.Max(0, Progress.milestoneClaims) & ((1 << MilestoneCount) - 1);
            Progress.coins = Math.Max(0, Math.Min(CoinLimit, Progress.coins));
            Progress.tickets = Math.Max(0, Math.Min(1000000, Progress.tickets));
            Progress.swapLevel = Math.Max(0, Math.Min(3, Progress.swapLevel));
            Progress.efficiencyLevel = Math.Max(0, Math.Min(10, Progress.efficiencyLevel));
            Progress.pressLevel = Math.Max(0, Math.Min(6, Progress.pressLevel));
            Progress.treasureLevel = Math.Max(0, Math.Min(10, Progress.treasureLevel));
            Progress.hits = Math.Max(0, Math.Min(HitsRequired - 1, Progress.hits));
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
            if (legacyRoll || !Progress.rollPending) ClearPendingRoll();
            else
            {
                // Invalid paid rolls recover from the backup, rather than silently drawing new picks.
                if (Progress.pendingTypes == null || Progress.pendingTypes.Length != Columns || IsComplete) throw new FormatException("Invalid pending roll");
                if (legacyUpgrades)
                {
                    // Honour only the already-paid v5 draw; permanent exchanges must now be purchased.
                    Progress.pendingSwapLimit = 1;
                    Progress.pendingSwapCount = Progress.pendingSwapUsed ? 1 : 0;
                }
                if (Progress.pendingSwapLimit < 0 || Progress.pendingSwapLimit > 3 || Progress.pendingSwapCount < 0 || Progress.pendingSwapCount > Progress.pendingSwapLimit) throw new FormatException("Invalid pending exchange budget");
                for (int c = 0; c < Columns; c++)
                {
                    int type = Progress.pendingTypes[c];
                    if (IsActiveColumn(c) ? type != 1 && type != 2 && type != 4 : type != 0) throw new FormatException("Invalid pending pick");
                }
            }
            Progress.pendingSwapUsed = false; // v5 migration field; v6 uses the saved counter.
            ObserveMilestones();
            AdvanceGuide();
        }

        // Presentation checkpoints only: no gifts, automatic purchases or changes to the economy.
        public bool AdvanceGuide()
        {
            if (HasPendingRoll || !GuideActive) return false;
            int before = Progress.guideStep;
            if (Progress.prestigeCount > 0) Progress.guideStep = GuideStepCount;
            while (GuideActive)
            {
                bool complete;
                switch (Progress.guideStep)
                {
                    case 0: complete = Progress.oresMined > 0; break;
                    case 1: complete = Progress.upgradeLevel > 0; break;
                    case 2: complete = Progress.totalRolls > 0; break;
                    case 3: complete = Progress.workerCount > 0; break;
                    default: complete = Progress.milestoneClaims != 0; break;
                }
                if (!complete) break;
                Progress.guideStep++;
            }
            return before != Progress.guideStep;
        }

        public bool SkipGuide()
        {
            if (HasPendingRoll || !GuideActive) return false;
            Progress.guideStep = GuideStepCount;
            return true;
        }

        public bool StartGuide()
        {
            if (HasPendingRoll) return false;
            Progress.guideStep = 0; AdvanceGuide();
            return GuideActive;
        }

        public bool Mine()
        {
            if (HasPendingRoll) { LastHit = default; return false; }
            var hit = new MineHit { ore = Progress.oreKind, damage = MiningDamage, coins = ManualClickPower, experience = 1, rich = VeinActive };
            int previousLevel = MinerLevel;
            int previousUnlocks = UnlockedOres;
            Progress.oreDamage += MiningDamage;
            if (Progress.oreDamage >= OreHealth)
            {
                hit.broken = true;
                hit.coins += OreReward * ManualMultiplier;
                hit.experience += 6 + Progress.oreKind * 4;
                Progress.oresMined = Math.Min(CoinLimit, Progress.oresMined + 1);
                Progress.oreDamage = 0;
                if (!VeinOffered && !VeinActive && ++Progress.veinCharge >= RichVeinTarget)
                {
                    Progress.veinCharge = 0; Progress.veinOfferLeft = VeinOfferSeconds; hit.veinFound = true;
                }
            }
            hit.coins += GainExperience(hit.experience, out hit.tickets, out hit.experience);
            hit.levels = MinerLevel - previousLevel;
            if (hit.broken)
                Progress.oreKind = UnlockedOres > previousUnlocks ? UnlockedOres - 1 : Math.Min(UnlockedOres - 1, OreCycle[Progress.oresMined % OreCycle.Length]);
            AddCoins(hit.coins);
            Progress.totalClicks = Math.Min(CoinLimit, Progress.totalClicks + 1);
            Progress.hits++;
            bool ticket = Progress.hits == HitsRequired;
            if (ticket) { Progress.hits = 0; hit.tickets++; Progress.tickets = Math.Min(1000000, Progress.tickets + 1); }
            LastHit = hit;
            return ticket;
        }

        private int GainExperience(int amount, out int tickets, out int awarded)
        {
            int scaled = amount * PrestigeExperiencePercent + Progress.experienceCarry;
            awarded = scaled / 100; Progress.experienceCarry = scaled % 100;
            int before = MinerLevel, coins = 0;
            Progress.experience = Math.Min(ExperienceForLevel(MaxMinerLevel), Progress.experience + awarded);
            tickets = MinerLevel - before;
            for (int level = before + 1; level <= MinerLevel; level++) coins += 40 * level;
            Progress.tickets = Math.Min(1000000, Progress.tickets + tickets);
            return coins;
        }

        private static double FiniteClamp(double value, double min, double max) => double.IsNaN(value) || double.IsInfinity(value) ? min : Math.Max(min, Math.Min(max, value));

        // Live session time only. No wall clock, offline awards, automatic hits or ticket generation.
        public long AdvanceTime(double seconds, bool miningActive)
        {
            if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0 || seconds > 86400) throw new ArgumentOutOfRangeException(nameof(seconds));
            double produced = Progress.productionCarry + ProductionMilli / 1000d * seconds;
            long whole = (long)Math.Floor(produced + 1e-9);
            Progress.productionCarry = Math.Max(0, produced - whole);
            long before = Progress.coins; AddCoins(whole);
            long earned = Progress.coins - before;
            Progress.totalProduced = Math.Min(CoinLimit, Progress.totalProduced + earned);
            if (miningActive && !HasPendingRoll)
            {
                Progress.veinOfferLeft = Math.Max(0, Progress.veinOfferLeft - seconds);
                Progress.veinBoostLeft = Math.Max(0, Progress.veinBoostLeft - seconds);
            }
            return earned;
        }

        public bool ActivateVein()
        {
            if (HasPendingRoll || !VeinOffered || VeinActive) return false;
            Progress.veinOfferLeft = 0; Progress.veinBoostLeft = RichVeinDuration;
            return true;
        }

        private static void ValidateUtc(long utc)
        {
            if (utc < 1 || utc > MaxUtcSeconds) throw new ArgumentOutOfRangeException(nameof(utc));
        }

        // The monotonic high-water mark cannot move backwards when the device clock is rolled back.
        public void RecordSession(long utc)
        {
            ValidateUtc(utc);
            Progress.lastSeenUtc = Math.Max(Progress.lastSeenUtc, utc);
            Progress.offlineRateMilli = ProductionMilli;
            Progress.offlineCapacitySeconds = OfflineHours * 3600;
        }

        // Called once on launch/resume, before purchases. The pending crate is saved before showing UI.
        public bool PrepareOffline(long utc)
        {
            ValidateUtc(utc);
            long elapsed = Progress.lastSeenUtc > 0 ? Math.Max(0, utc - Progress.lastSeenUtc) : 0;
            long rate = Progress.offlineRateMilli;
            int capacity = Progress.offlineCapacitySeconds;
            RecordSession(utc);
            if (HasOfflineReward || elapsed == 0 || rate <= 0 || capacity <= 0) return false;
            long seconds = Math.Min(elapsed, capacity);
            long numerator = rate * seconds + Progress.offlineCarry;
            Progress.offlineCarry = (int)(numerator % 2000);
            long coins = numerator / 2000; // Half normal production; keep fractional coins across absences.
            if (coins == 0) return false;
            Progress.offlineCoins = coins; Progress.offlineRewardSeconds = seconds;
            Progress.offlineElapsedSeconds = elapsed; Progress.offlineRewardRateMilli = rate;
            Progress.offlineRewardCapacity = capacity;
            return true;
        }

        public bool ClaimOffline(bool doubled = false)
        {
            if (doubled ? !CanDoubleOffline : !CanClaimOffline) return false;
            long credit = doubled ? DoubledOfflineReward : Math.Min(CoinLimit - Progress.coins, Progress.offlineCoins);
            AddCoins(credit); Progress.offlineCoins = doubled ? 0 : Progress.offlineCoins - credit;
            Progress.totalProduced = Math.Min(CoinLimit, Progress.totalProduced + credit);
            Progress.totalOfflineCoins = Math.Min(CoinLimit, Progress.totalOfflineCoins + credit);
            if (!HasOfflineReward) ClearOfflineReward();
            return true;
        }

        private void ClearOfflineReward()
        {
            Progress.offlineCoins = Progress.offlineRewardSeconds = Progress.offlineElapsedSeconds = Progress.offlineRewardRateMilli = 0;
            Progress.offlineRewardCapacity = 0;
        }

        public bool Upgrade()
        {
            return BuyUpgrade(UpgradeKind.Pickaxe);
        }

        public bool Forge()
        {
            return BuyUpgrade(UpgradeKind.Forge);
        }

        public int UpgradeLevel(UpgradeKind kind)
        {
            switch (kind)
            {
                case UpgradeKind.Pickaxe: return Progress.upgradeLevel;
                case UpgradeKind.Forge: return Progress.forgeLevel;
                case UpgradeKind.Swap: return Progress.swapLevel;
                case UpgradeKind.Efficiency: return Progress.efficiencyLevel;
                case UpgradeKind.Press: return Progress.pressLevel;
                case UpgradeKind.Treasure: return Progress.treasureLevel;
                case UpgradeKind.Worker: return Progress.workerCount;
                case UpgradeKind.Drill: return Progress.drillCount;
                case UpgradeKind.Cart: return Progress.cartCount;
                case UpgradeKind.Rails: return Progress.railLevel;
                case UpgradeKind.Warehouse: return Progress.warehouseLevel;
                default: throw new ArgumentOutOfRangeException(nameof(kind));
            }
        }

        public static int UpgradeMax(UpgradeKind kind)
        {
            switch (kind)
            {
                case UpgradeKind.Pickaxe: return MaxUpgrade;
                case UpgradeKind.Forge: return MaxForge;
                case UpgradeKind.Swap: return 3;
                case UpgradeKind.Efficiency: case UpgradeKind.Treasure: return 10;
                case UpgradeKind.Press: return 6;
                case UpgradeKind.Worker: return 100;
                case UpgradeKind.Drill: return 50;
                case UpgradeKind.Cart: return 25;
                case UpgradeKind.Rails: return 10;
                case UpgradeKind.Warehouse: return MaxWarehouse;
                default: throw new ArgumentOutOfRangeException(nameof(kind));
            }
        }

        public int UpgradeUnlock(UpgradeKind kind) => UpgradeUnlock(kind, UpgradeLevel(kind));

        private static int UpgradeUnlock(UpgradeKind kind, int level)
        {
            int rank = Math.Min(level, UpgradeMax(kind) - 1);
            switch (kind)
            {
                case UpgradeKind.Swap: return new[] { 3, 8, 15 }[rank];
                case UpgradeKind.Efficiency: return 2 + rank * 2;
                case UpgradeKind.Press: return 4 + rank * 3;
                case UpgradeKind.Treasure: return 6 + rank * 2;
                case UpgradeKind.Worker: return 2;
                case UpgradeKind.Drill: return 5;
                case UpgradeKind.Cart: return 8;
                case UpgradeKind.Rails: return 8 + rank * 2;
                case UpgradeKind.Warehouse: return 6 + rank * 4;
                default: return 1;
            }
        }

        public long UpgradePrice(UpgradeKind kind) => UpgradePrice(kind, UpgradeLevel(kind));

        private static long UpgradePrice(UpgradeKind kind, int rank)
        {
            double start, growth;
            switch (kind)
            {
                case UpgradeKind.Pickaxe: start = 120; growth = 1.28; break;
                case UpgradeKind.Forge: start = 180; growth = 1.42; break;
                case UpgradeKind.Swap: start = 600; growth = 4; break;
                case UpgradeKind.Efficiency: start = 300; growth = 1.58; break;
                case UpgradeKind.Press: start = 550; growth = 1.9; break;
                case UpgradeKind.Treasure: start = 800; growth = 1.65; break;
                case UpgradeKind.Worker: start = 300; growth = 1.18; break;
                case UpgradeKind.Drill: start = 1800; growth = 1.24; break;
                case UpgradeKind.Cart: start = 1400; growth = 1.30; break;
                case UpgradeKind.Rails: start = 900; growth = 1.65; break;
                case UpgradeKind.Warehouse: start = 1500; growth = 2.2; break;
                default: throw new ArgumentOutOfRangeException(nameof(kind));
            }
            return (long)Math.Min(CoinLimit, Math.Round(start * Math.Pow(growth, rank)));
        }

        public UpgradeKind? UpgradePrerequisite(UpgradeKind kind) => kind == UpgradeKind.Drill || kind == UpgradeKind.Warehouse ? UpgradeKind.Worker : kind == UpgradeKind.Cart ? UpgradeKind.Drill : kind == UpgradeKind.Rails ? UpgradeKind.Cart : (UpgradeKind?)null;
        public bool HasUpgradePrerequisite(UpgradeKind kind) => !UpgradePrerequisite(kind).HasValue || UpgradeLevel(UpgradePrerequisite(kind).Value) > 0;
        public bool CanBuyUpgrade(UpgradeKind kind) => !HasPendingRoll && HasUpgradePrerequisite(kind) && UpgradeLevel(kind) < UpgradeMax(kind) && MinerLevel >= UpgradeUnlock(kind) && Progress.coins >= UpgradePrice(kind);

        // Quote without changing the wallet, ticket progress or any upgrade. Each rank keeps its own gate.
        public int QuoteUpgrade(UpgradeKind kind, int requested, out long price)
        {
            price = 0;
            if (requested <= 0 || HasPendingRoll || !HasUpgradePrerequisite(kind)) return 0;
            int rank = UpgradeLevel(kind), count = 0, limit = Math.Min(requested, UpgradeMax(kind) - rank), miner = MinerLevel;
            while (count < limit && miner >= UpgradeUnlock(kind, rank + count))
            {
                long next = UpgradePrice(kind, rank + count);
                if (next > Progress.coins - price) break;
                price += next; count++;
            }
            return count;
        }

        public int BuyUpgrade(UpgradeKind kind, int requested)
        {
            int count = QuoteUpgrade(kind, requested, out _);
            // Reuse every single-purchase effect, including conversion of earned press progress.
            for (int i = 0; i < count; i++) BuyUpgrade(kind);
            return count;
        }

        public bool BuyUpgrade(UpgradeKind kind)
        {
            if (!CanBuyUpgrade(kind)) return false;
            Progress.coins -= UpgradePrice(kind);
            switch (kind)
            {
                case UpgradeKind.Pickaxe: Progress.upgradeLevel++; break;
                case UpgradeKind.Forge: Progress.forgeLevel++; break;
                case UpgradeKind.Swap: Progress.swapLevel++; break;
                case UpgradeKind.Efficiency: Progress.efficiencyLevel++; break;
                case UpgradeKind.Press:
                    Progress.pressLevel++;
                    if (Progress.hits >= HitsRequired) { Progress.hits -= HitsRequired; Progress.tickets = Math.Min(1000000, Progress.tickets + 1); }
                    break;
                case UpgradeKind.Treasure: Progress.treasureLevel++; break;
                case UpgradeKind.Worker: Progress.workerCount++; break;
                case UpgradeKind.Drill: Progress.drillCount++; break;
                case UpgradeKind.Cart: Progress.cartCount++; break;
                case UpgradeKind.Rails: Progress.railLevel++; break;
                case UpgradeKind.Warehouse: Progress.warehouseLevel++; break;
            }
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

        public long MilestoneValue(int index)
        {
            Progress.milestoneBest[index] = Math.Max(Progress.milestoneBest[index], RawMilestoneValue(index));
            return Progress.milestoneBest[index];
        }

        private void ObserveMilestones() { for (int i = 0; i < MilestoneCount; i++) MilestoneValue(i); }

        private long RawMilestoneValue(int index)
        {
            long value;
            switch (index)
            {
                case 0: value = Progress.totalClicks; break;
                case 1: value = Progress.oresMined; break;
                case 2: value = Progress.totalRolls; break;
                case 3: case 11: value = Progress.upgradeLevel; break;
                case 4: value = Progress.totalBlocks; break;
                case 5: case 14: case 15: value = MinerLevel; break;
                case 6: value = Progress.swapLevel; break;
                case 7: value = Progress.efficiencyLevel; break;
                case 8: value = Progress.stratum; break;
                case 9: value = Progress.forgeLevel; break;
                case 10: value = Progress.pressLevel; break;
                case 12: value = Progress.treasureLevel; break;
                case 13: value = Relics; break;
                default: throw new ArgumentOutOfRangeException(nameof(index));
            }
            return Math.Min(Milestones[index].target, value);
        }

        public bool MilestoneClaimed(int index) => index >= 0 && index < MilestoneCount && (Progress.milestoneClaims & (1 << index)) != 0;
        public bool CanClaimMilestone(int index) => index >= 0 && index < MilestoneCount && !HasPendingRoll && !MilestoneClaimed(index) && MilestoneValue(index) >= Milestones[index].target;
        public int ClaimedMilestones
        {
            get { int count = 0; for (int i = 0; i < MilestoneCount; i++) if (MilestoneClaimed(i)) count++; return count; }
        }
        public int ReadyMilestones
        {
            get { int count = 0; for (int i = 0; i < MilestoneCount; i++) if (CanClaimMilestone(i)) count++; return count; }
        }

        public bool ClaimMilestone(int index)
        {
            if (!CanClaimMilestone(index)) return false;
            // Persisted by the controller together with the reward, before presentation.
            Progress.milestoneClaims |= 1 << index;
            AddCoins(Milestones[index].coins);
            Progress.tickets = Math.Min(1000000, Progress.tickets + Milestones[index].tickets);
            return true;
        }

        public bool HasPrestigeUpgrade(int index) => index >= 0 && index < PrestigeUpgradeCount && (Progress.prestigeUpgrades & (1 << index)) != 0;
        public bool PrestigePrerequisiteMet(int index) => index >= 0 && index < PrestigeUpgradeCount && (index % 3 == 0 || HasPrestigeUpgrade(index - 1));
        public bool CanBuyPrestigeUpgrade(int index) => index >= 0 && index < PrestigeUpgradeCount && Progress.prestigeCount > 0 && !HasPendingRoll && !HasPrestigeUpgrade(index) && PrestigePrerequisiteMet(index) && Progress.crystals >= PrestigeUpgrades[index].cost;
        public int PrestigeReward => Math.Min(MaxCrystals - Progress.totalCrystals, 3 + Math.Min(7, (Progress.stratum - 1) / 10));
        public bool CanPrestige => MinerLevel == MaxMinerLevel && !HasPendingRoll && !HasOfflineReward && Progress.prestigeCount < MaxPrestiges && PrestigeReward > 0;

        public bool BuyPrestigeUpgrade(int index)
        {
            if (!CanBuyPrestigeUpgrade(index)) return false;
            Progress.crystals -= PrestigeUpgrades[index].cost;
            Progress.prestigeUpgrades |= 1 << index;
            // Starter purchases help immediately once, and are granted again by each later reset.
            if (index == 3) AddCoins(1000);
            if (index == 4) Progress.upgradeLevel = Math.Max(2, Progress.upgradeLevel);
            if (index == 5) Progress.workerCount = Math.Max(1, Progress.workerCount);
            if (index == 6 && !VeinOffered && !VeinActive && Progress.veinCharge >= RichVeinTarget)
            {
                Progress.veinCharge = 0; Progress.veinOfferLeft = VeinOfferSeconds;
            }
            ObserveMilestones();
            return true;
        }

        public bool Prestige(Func<double> random)
        {
            if (!CanPrestige) return false;
            int seed = DrawSeed(random); // Validate RNG before touching the wallet or permanent progress.
            int reward = PrestigeReward; ObserveMilestones();
            var previous = Progress;
            Progress = new MineProgress {
                boardSeed = seed, coins = PrestigeStartCoins,
                upgradeLevel = PrestigeStartPick, workerCount = PrestigeStartWorkers,
                prestigeCount = previous.prestigeCount + 1, crystals = previous.crystals + reward,
                totalCrystals = previous.totalCrystals + reward, prestigeUpgrades = previous.prestigeUpgrades,
                milestoneClaims = previous.milestoneClaims, milestoneBest = (long[])previous.milestoneBest.Clone(),
                guideStep = GuideStepCount,
                totalClicks = previous.totalClicks, oresMined = previous.oresMined, totalRolls = previous.totalRolls,
                totalBlocks = previous.totalBlocks, claimedContracts = previous.claimedContracts,
                totalProduced = previous.totalProduced, totalOfflineCoins = previous.totalOfflineCoins,
                lastSeenUtc = previous.lastSeenUtc
            };
            Sanitize();
            if (previous.lastSeenUtc > 0) RecordSession(previous.lastSeenUtc);
            LastHit = default;
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

        // Spend and fix the draw and its purchased exchange budget before presentation.
        public bool BeginRoll(Func<double> random)
        {
            if (HasPendingRoll || Progress.tickets <= 0) return false;
            if (random == null) throw new ArgumentNullException(nameof(random));
            var types = new int[Columns];
            bool nextFloor = IsComplete;
            // Obtain all draws before mutating progress, including failures of an injected random source.
            for (int i = 0; i < Columns; i++) types[i] = PickPower(random(), Progress.forgeLevel);
            int seed = nextFloor ? DrawSeed(random) : Progress.boardSeed;
            if (nextFloor) NextFloor(seed);
            Progress.tickets--;
            Progress.totalRolls = Math.Min(10000000, Progress.totalRolls + 1);
            for (int c = 0; c < Columns; c++) if (!IsActiveColumn(c)) types[c] = 0;
            Progress.pendingTypes = types;
            Progress.pendingSwapUsed = false;
            Progress.pendingSwapCount = 0;
            Progress.pendingSwapLimit = Progress.swapLevel;
            Progress.pendingNewStratum = nextFloor;
            Progress.rollPending = true;
            return true;
        }

        public bool SwapPicks(int first, int second)
        {
            if (!CanSwapPicks(first, second)) return false;
            int type = Progress.pendingTypes[first];
            Progress.pendingTypes[first] = Progress.pendingTypes[second];
            Progress.pendingTypes[second] = type;
            Progress.pendingSwapCount++;
            return true;
        }

        // Commit rewards once, before the strike animation. This method never draws randomness.
        public RollResult ResolveRoll()
        {
            if (!HasPendingRoll) return null;
            var result = new RollResult { levelBefore = MinerLevel, newStratum = Progress.pendingNewStratum };
            for (int c = 0; c < Columns; c++)
            {
                result.types[c] = Progress.pendingTypes[c];
                result.powers[c] = PendingPower(c);
            }
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
            result.levelCoins = GainExperience(result.broken, out result.levelTickets, out result.experience);
            result.levelAfter = MinerLevel;
            result.reward += result.levelCoins;
            AddCoins(result.reward);
            ClearPendingRoll();
            return result;
        }

        private void ClearPendingRoll()
        {
            Progress.rollPending = Progress.pendingSwapUsed = Progress.pendingNewStratum = false;
            Progress.pendingSwapLimit = Progress.pendingSwapCount = 0;
            if (Progress.pendingTypes == null || Progress.pendingTypes.Length != Columns) Progress.pendingTypes = new int[Columns];
            else Array.Clear(Progress.pendingTypes, 0, Columns);
        }

        // Automatic resolution remains available for rule checks and the no-exchange simulation.
        public RollResult Roll(Func<double> random) => BeginRoll(random) ? ResolveRoll() : null;

        private void AddCoins(long value) => Progress.coins = Math.Min(CoinLimit, Progress.coins + value);
    }

    public readonly struct PrestigeUpgrade
    {
        public readonly string title, detail, icon;
        public readonly int cost;
        public PrestigeUpgrade(string title, string detail, string icon, int cost)
        {
            this.title = title; this.detail = detail; this.icon = icon; this.cost = cost;
        }
    }

    public readonly struct Milestone
    {
        public readonly string title, detail, icon;
        public readonly int target, coins, tickets;
        public Milestone(string title, string detail, string icon, int target, int coins, int tickets)
        {
            this.title = title; this.detail = detail; this.icon = icon;
            this.target = target; this.coins = coins; this.tickets = tickets;
        }
    }

    public struct MineHit
    {
        public int ore, damage, coins, experience, levels, tickets;
        public bool broken, rich, veinFound;
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
        public int levelBefore, levelAfter, levelCoins, levelTickets, experience;
    }
}
