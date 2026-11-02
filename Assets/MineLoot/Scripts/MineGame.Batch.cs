using System;

namespace MineLoot
{
    public sealed partial class MineGame
    {
        public bool ActionsLocked => HasPendingRoll || Progress.batchActive;
        public bool CanStartBatch => !ActionsLocked && Progress.tickets >= 10;
        private void SanitizeBatch()
        {
            if (Progress.batchRemaining < 0 || Progress.batchRemaining > 9 || Progress.batchCompleted < 0 || Progress.batchCompleted > 10 || Progress.batchReward < 0 || Progress.batchReward > CoinLimit * 10)
                throw new FormatException("Invalid roll batch");
            if (Progress.pendingBatch && !HasPendingRoll) throw new FormatException("Batch pick is missing");
            if (Progress.batchActive && (Progress.batchRemaining + Progress.batchCompleted + (Progress.pendingBatch ? 1 : 0) != 10 || Progress.batchCompleted == 10))
                throw new FormatException("Invalid batch continuation");
        }
        public bool BeginBatch(Func<double> random)
        {
            if (!CanStartBatch || !BeginRollCore(random)) return false;
            Progress.batchActive = Progress.pendingBatch = true;
            Progress.batchRemaining = 9; Progress.batchCompleted = 0; Progress.batchReward = 0;
            Progress.pendingSwapLimit = Progress.pendingSwapCount = 0;
            return true;
        }
        public bool ContinueBatch(Func<double> random)
        {
            if (!Progress.batchActive || HasPendingRoll || Progress.batchRemaining <= 0) return false;
            if (!BeginRollCore(random)) { StopBatch(); return false; }
            Progress.pendingBatch = true; Progress.batchRemaining--;
            Progress.pendingSwapLimit = Progress.pendingSwapCount = 0;
            return true;
        }
        public bool StopBatch()
        {
            if (!Progress.batchActive) return false;
            Progress.batchActive = false; Progress.batchRemaining = 0;
            return true;
        }
        private void CompleteBatchRoll(long reward)
        {
            if (!Progress.pendingBatch) return;
            Progress.pendingBatch = false;
            Progress.batchCompleted++; Progress.batchReward = Math.Min(CoinLimit * 10, Progress.batchReward + reward);
            if (Progress.batchRemaining == 0) Progress.batchActive = false;
        }
    }
}
