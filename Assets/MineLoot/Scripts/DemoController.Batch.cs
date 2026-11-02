using System;
using UnityEngine.UI;

namespace MineLoot
{
    public sealed partial class DemoController
    {
        private Button batchButton;
        private bool batchVisual;
        private float RollAnimationScale => batchVisual ? .3f : FastRoll ? .5f : 1;
        public void SpinBatch()
        {
            if (applicationPaused || modal != null) return;
            if (game.Progress.batchActive)
            {
                game.StopBatch(); Persist(); Flush(); Refresh(); return;
            }
            if (busy || !game.BeginBatch(() => random.NextDouble())) return;
            batchVisual = true; PresentStartedRoll();
        }
        internal void BeginBatchForSmoke(Func<double> draw)
        {
            if (!SmokeMode || busy || !game.BeginBatch(draw)) return;
            batchVisual = true; PresentStartedRoll();
        }
        private void ContinueRollBatch()
        {
            if (!game.ContinueBatch(() => random.NextDouble())) { Persist(); Flush(); Refresh(); return; }
            batchVisual = true; PresentStartedRoll();
        }
        private void RefreshBatchButton()
        {
            if (batchButton == null) return;
            batchButton.interactable = !applicationPaused && (game.Progress.batchActive || !busy && game.CanStartBatch);
            batchButton.GetComponentInChildren<Text>().text = game.Progress.batchActive ? "СТОП · " + (game.Progress.batchCompleted + (game.HasPendingRoll ? 1 : 0)) + "/10" : "×10 · 10 ЖЕТОНОВ";
        }
        private void LayoutRollButtons(float x, float y, float width, float height)
        {
            float primary = (width - 12) * .6f;
            ButtonFrame(spinButton, x, y, primary, height);
            ButtonFrame(batchButton, x + primary + 12, y, width - primary - 12, height);
        }
    }
}
