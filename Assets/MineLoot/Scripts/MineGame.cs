using System;

namespace MineLoot
{
    [Serializable]
    public sealed class MineProgress
    {
        public int version = 1;
        public long coins = 1240;
        public int tickets = 3;
        public int hits;
        public int upgradeLevel;
        public int stratum = 1;
        public int[] depths = new int[5];
    }

    // Pure rules: no scene dependencies, animation or platform SDK calls.
    public sealed class MineGame
    {
        public const int Columns = 5;
        public const int Rows = 5;
        public const int HitsPerTicket = 5;
        public const int BlockReward = 10;
        public const int ChestReward = 250;
        public const int MaxUpgrade = 100;
        public const long CoinLimit = 1000000000000L;
        public MineProgress Progress { get; }
        public int ClickPower => 25 + Progress.upgradeLevel * 10;
        public long UpgradeCost => (long)Math.Min(CoinLimit, Math.Round(350 * Math.Pow(1.6, Progress.upgradeLevel)));
        public bool CanUpgrade => Progress.upgradeLevel < MaxUpgrade && Progress.coins >= UpgradeCost;
        public bool IsComplete => Array.TrueForAll(Progress.depths, d => d == Rows);

        public MineGame(MineProgress progress = null)
        {
            Progress = progress ?? new MineProgress();
            Sanitize();
        }

        private void Sanitize()
        {
            Progress.version = 1;
            Progress.coins = Math.Max(0, Math.Min(CoinLimit, Progress.coins));
            Progress.tickets = Math.Max(0, Math.Min(1000000, Progress.tickets));
            Progress.hits = Math.Max(0, Math.Min(HitsPerTicket - 1, Progress.hits));
            Progress.upgradeLevel = Math.Max(0, Math.Min(MaxUpgrade, Progress.upgradeLevel));
            Progress.stratum = Math.Max(1, Math.Min(1000000, Progress.stratum));
            if (Progress.depths == null || Progress.depths.Length != Columns) Progress.depths = new int[Columns];
            for (int i = 0; i < Columns; i++) Progress.depths[i] = Math.Max(0, Math.Min(Rows, Progress.depths[i]));
        }

        public bool Mine()
        {
            AddCoins(ClickPower);
            Progress.hits++;
            if (Progress.hits < HitsPerTicket) return false;
            Progress.hits = 0;
            Progress.tickets = Math.Min(1000000, Progress.tickets + 1);
            return true;
        }

        public bool Upgrade()
        {
            if (!CanUpgrade) return false;
            Progress.coins -= UpgradeCost;
            Progress.upgradeLevel++;
            return true;
        }

        public static int PickPower(double sample)
        {
            return sample < .55 ? 1 : sample < .90 ? 2 : 4;
        }

        // Resolve atomically before presentation, so closing during animation cannot lose a reward.
        public RollResult Roll(Func<double> random)
        {
            if (Progress.tickets <= 0) return null;
            if (random == null) throw new ArgumentNullException(nameof(random));
            var result = new RollResult();
            // Obtain all draws before mutating progress, including failures of an injected random source.
            for (int i = 0; i < Columns; i++) result.powers[i] = PickPower(random());
            if (IsComplete)
            {
                Array.Clear(Progress.depths, 0, Columns);
                Progress.stratum = Math.Min(1000000, Progress.stratum + 1);
                result.newStratum = true;
            }
            Progress.tickets--;
            Array.Copy(Progress.depths, result.before, Columns);
            for (int i = 0; i < Columns; i++)
            {
                int previous = Progress.depths[i];
                int after = Math.Min(Rows, previous + result.powers[i]);
                result.reward += (after - previous) * BlockReward;
                if (previous < Rows && after == Rows)
                {
                    result.chests++;
                    result.reward += ChestReward;
                }
                Progress.depths[i] = after;
                result.after[i] = after;
            }
            AddCoins(result.reward);
            return result;
        }

        private void AddCoins(long value) => Progress.coins = Math.Min(CoinLimit, Progress.coins + value);
    }

    public sealed class RollResult
    {
        public readonly int[] powers = new int[MineGame.Columns];
        public readonly int[] before = new int[MineGame.Columns];
        public readonly int[] after = new int[MineGame.Columns];
        public int reward;
        public int chests;
        public bool newStratum;
    }
}
