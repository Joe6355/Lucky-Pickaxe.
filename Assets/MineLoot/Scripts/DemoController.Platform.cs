using System.Collections;
using UnityEngine;

namespace MineLoot
{
    public sealed partial class DemoController
    {
        private bool enginePaused, browserPaused, audioFocused = true;
        private CanvasGroup platformInput;
        public bool IsPaused => applicationPaused;
        private float PlayDelta => applicationPaused ? 0 : Time.unscaledDeltaTime;

        private IEnumerator Start()
        {
            ApplyPlatformPause();
            BrowserPlatform.Attach(gameObject.name);
            // The first UI frame, font and procedural worlds must exist before Game Ready.
            yield return null;
            BrowserPlatform.Ready();
        }

        // Invoked by the template bridge. SDK pause and browser visibility form one host reason.
        public void OnBrowserPause(string paused)
        {
            browserPaused = paused == "1"; ApplyPlatformPause();
        }
        public void OnBrowserSave(string unused) => Flush(true);

        private void ApplyPlatformPause()
        {
            bool paused = enginePaused || browserPaused;
            if (paused == applicationPaused) return;
            if (paused) { holding = false; Flush(true); }
            applicationPaused = paused; skipEconomyFrame = true;
            platformInput.interactable = !paused;
            platformInput.blocksRaycasts = !paused;
            stage.Paused = shaft.Paused = paused;
            sounds.SetSuspended(paused || !audioFocused);
            if (!paused && (!SmokeMode || smokeSessionKey != null)) PrepareReturn(SessionUtc);
        }
    }
}
