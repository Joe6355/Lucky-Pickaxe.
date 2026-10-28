using System.Collections;
using UnityEngine;

namespace MineLoot
{
    public sealed partial class DemoController
    {
        private bool enginePaused, browserPaused, audioFocused = true;
        private bool rewardedAvailable, rewardedReturnPending, offlineVideoPaid;
        private string rewardedRequest, offlineVideoMessage;
        private long offlineVideoCredit;
        private System.Func<string, bool> rewardedVideoForSmoke;
        private CanvasGroup platformInput;
        private System.Globalization.CultureInfo interfaceCulture = System.Globalization.CultureInfo.GetCultureInfo("ru-RU");
        public string InterfaceLanguage { get; private set; } = "ru";
        public bool IsPaused => applicationPaused;
        public bool RewardedVideoPending => !string.IsNullOrEmpty(rewardedRequest);
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

        public void OnBrowserLanguage(string language)
        {
            // The existing UI is the complete Russian translation declared in the draft.
            if (language != "ru") return;
            InterfaceLanguage = language;
            interfaceCulture = System.Globalization.CultureInfo.GetCultureInfo(language + "-RU");
            Refresh();
            BrowserPlatform.LanguageReady(language);
        }

        public void OnRewardedAvailable(string available)
        {
            rewardedAvailable = available == "1"; RefreshOfflineWindow();
        }

        public void ClaimOfflineWithVideo()
        {
            if (applicationPaused || busy || !offlineWindowOpen || !rewardedAvailable || !game.CanDoubleOffline || RewardedVideoPending) return;
            offlineVideoMessage = null; offlineVideoPaid = false; offlineVideoCredit = 0;
            rewardedRequest = System.Guid.NewGuid().ToString("N"); rewardedReturnPending = true;
            RefreshOfflineWindow(); ApplyPlatformPause();
            string request = rewardedRequest;
            bool accepted = SmokeMode && rewardedVideoForSmoke != null ? rewardedVideoForSmoke(request) : BrowserPlatform.ShowRewarded(request);
            if (!accepted && rewardedRequest == request) FinishRewardedVideo("unavailable");
        }

        // onRewarded is the only event allowed to credit money; close/error only release the request.
        public void OnRewardedVideo(string result)
        {
            if (!RewardedVideoPending || string.IsNullOrEmpty(result)) return;
            int separator = result.IndexOf(':');
            if (separator < 1 || result.Substring(0, separator) != rewardedRequest) return;
            string outcome = result.Substring(separator + 1);
            if (outcome == "rewarded")
            {
                if (offlineVideoPaid) return;
                long before = game.Progress.coins;
                if (!game.ClaimOffline(true)) return;
                offlineVideoPaid = true; offlineVideoCredit = game.Progress.coins - before;
                game.RecordSession(SessionUtc); Persist(); Flush(true); Refresh();
            }
            else if (outcome == "closed" || outcome == "error") FinishRewardedVideo(outcome);
        }

        private void FinishRewardedVideo(string outcome)
        {
            rewardedRequest = null;
            offlineVideoMessage = outcome == "closed" ? "Просмотр не засчитан.\nДобыча осталась на складе." : "Видео сейчас недоступно.\nОбычная награда сохранена.";
            // Ad time cannot immediately generate another crate and an endless chain of video offers.
            game.RecordSession(SessionUtc); Persist(); Flush(true); RefreshOfflineWindow(); ApplyPlatformPause();
        }

        private void PresentRewardedResult()
        {
            if (offlineVideoPaid)
            {
                if (offlineWindowOpen) CloseModal();
                sounds.Play("reward"); Notice("СКЛАД · НАГРАДА ×2", "+" + ShortAmount(offlineVideoCredit) + " монет за просмотр видео");
                offlineNoticeQueued = game.HasOfflineReward;
                offlineVideoMessage = null;
            }
            else RefreshOfflineWindow();
            offlineVideoPaid = false; offlineVideoCredit = 0;
        }

        internal void ConfigureRewardedForSmoke(System.Func<string, bool> show)
        {
            if (!SmokeMode) return;
            rewardedVideoForSmoke = show; OnRewardedAvailable(show == null ? "0" : "1");
        }

        private void ApplyPlatformPause()
        {
            bool paused = enginePaused || browserPaused || RewardedVideoPending;
            if (paused == applicationPaused) return;
            if (paused) { holding = false; Flush(true); }
            applicationPaused = paused; skipEconomyFrame = true;
            platformInput.interactable = !paused;
            platformInput.blocksRaycasts = !paused;
            stage.Paused = shaft.Paused = paused;
            sounds.SetSuspended(paused || !audioFocused);
            if (!paused)
            {
                bool afterVideo = rewardedReturnPending; rewardedReturnPending = false;
                if (!SmokeMode || smokeSessionKey != null) PrepareReturn(SessionUtc, afterVideo);
                if (afterVideo) PresentRewardedResult();
            }
        }
    }
}
