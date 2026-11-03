using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace MineLoot
{
    public sealed partial class DemoController
    {
        private bool enginePaused, browserPaused, audioFocused = true;
        private bool rewardedAvailable, rewardedReturnPending, offlineVideoPaid;
        private string rewardedRequest, offlineVideoMessage;
        private long offlineVideoCredit;
        private bool ticketVideo;
        private System.Func<string, bool> rewardedVideoForSmoke;
        private CanvasGroup platformInput;
        private System.Globalization.CultureInfo interfaceCulture = System.Globalization.CultureInfo.GetCultureInfo("ru-RU");
        public string InterfaceLanguage { get; private set; } = "ru";
        public bool IsPaused => applicationPaused;
        public bool RewardedVideoPending => !string.IsNullOrEmpty(rewardedRequest);
        private Button accountButton,accountLoginButton;
        private Text accountStatusLabel;
        private bool accountAvailable,accountPending;
        private string accountStatus="unavailable",accountName="";
        private System.Func<bool> authorizationForSmoke;
        [System.Serializable] private sealed class AccountPayload { public bool available; public string status,name; }
        public void OnBrowserAccount(string json)
        {
            AccountPayload data;try { data=JsonUtility.FromJson<AccountPayload>(json); } catch { return; }
            if(data==null || string.IsNullOrEmpty(data.status)) return;
            bool returning=accountPending && data.status!="pending";
            accountAvailable=data.available;accountStatus=data.status;accountPending=data.status=="pending";
            accountName=(data.name??"").Replace("<","").Replace(">","").Trim();if(accountName.Length>40) accountName=accountName.Substring(0,40);
            if(returning) { game.RecordSession(SessionUtc);Persist();Flush(true); }
            RefreshAccount();ApplyPlatformPause();
            nextRatingSend=0;submittedRating=-1;
            if(returning) nextRatingLoad=0;
        }
        private void ShowYandexAccount()
        {
            if(applicationPaused || modal!=null) return;holding=false;
            modal=CreateModal("Yandex account modal");Card(modal.transform,"Yandex account card",24,148,432,644,Panel);
            Label(modal.transform,"АККАУНТ ЯНДЕКСА",44,168,392,40,26,Ink,FontStyle.Bold);
            accountStatusLabel=Label(modal.transform,"",44,238,392,96,22,Ink,FontStyle.Bold);
            Label(modal.transform,"Подключи профиль Яндекса для участия в рейтинге шахтёров.\n\nСохранение хранится на этом устройстве.",44,356,392,128,19,Muted);
            accountLoginButton=ButtonAt(modal.transform,"ВОЙТИ ЧЕРЕЗ ЯНДЕКС",44,560,392,48,BeginYandexLogin,Hex("dfc285"),Ink);accountLoginButton.name="Authorize Yandex";
            ButtonAt(modal.transform,"Продолжить игру",44,704,392,44,CloseModal,Panel,Ink).name="Close Yandex account";
            RefreshAccount();
        }
        private void BeginYandexLogin()
        {
            if(applicationPaused || accountPending || !accountAvailable || accountStatus=="authorized") return;
            Flush(true);accountPending=true;accountStatus="pending";RefreshAccount();ApplyPlatformPause();
            bool accepted=SmokeMode && authorizationForSmoke!=null?authorizationForSmoke():BrowserPlatform.Authorize();
            if(!accepted) OnBrowserAccount(JsonUtility.ToJson(new AccountPayload{available=accountAvailable,status="guest",name=""}));
        }
        private void RefreshAccount()
        {
            if(accountButton==null) return;
            accountButton.GetComponentInChildren<Text>().text=accountStatus=="authorized"?"ЯНДЕКС · "+(accountName.Length>0?accountName:"АККАУНТ ПОДКЛЮЧЁН"):accountPending?"ЯНДЕКС · ВХОД…":"ВОЙТИ В ЯНДЕКС";
            if(modal==null || modal.name!="Yandex account modal") return;
            accountStatusLabel.text=accountStatus=="authorized"?"Вход выполнен\n"+(accountName.Length>0?accountName:"Игрок Яндекса"):accountPending?"Открывается вход через Яндекс…":!accountAvailable?"Вход доступен в браузерной версии на Яндекс Играх.":accountStatus=="checking"?"Проверяем аккаунт…":"Вы играете как гость.\nМожно войти кнопкой ниже.";
            accountLoginButton.gameObject.SetActive(accountStatus!="authorized");accountLoginButton.interactable=accountAvailable&&!accountPending&&accountStatus!="checking";
        }
        internal void ConfigureAuthorizationForSmoke(System.Func<bool> authorize)
        {
            if(!SmokeMode) return;authorizationForSmoke=authorize;
            OnBrowserAccount(JsonUtility.ToJson(new AccountPayload{available=authorize!=null,status=authorize!=null?"guest":"unavailable",name=""}));
        }
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
            rewardedAvailable = available == "1"; RefreshOfflineWindow(); RefreshRewards();
        }

        public void ClaimOfflineWithVideo()
        {
            if (applicationPaused || EconomyBusy || !offlineWindowOpen || !rewardedAvailable || !game.CanClaimRewardedOffline || RewardedVideoPending) return;
            ticketVideo = false;
            RequestRewardedVideo();
        }

        public void ClaimTicketsWithVideo()
        {
            if (applicationPaused || EconomyBusy || modal != null || !rewardedAvailable || !game.CanClaimVideoTickets(SessionUtc) || RewardedVideoPending) return;
            ticketVideo = true;
            RequestRewardedVideo();
        }

        private void RequestRewardedVideo()
        {
            offlineVideoMessage = null; offlineVideoPaid = false; offlineVideoCredit = 0;
            rewardedRequest = System.Guid.NewGuid().ToString("N"); rewardedReturnPending = true;
            RefreshOfflineWindow(); RefreshRewards(); ApplyPlatformPause();
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
                if (ticketVideo ? !game.ClaimVideoTickets(SessionUtc) : !game.ClaimOffline(true)) return;
                offlineVideoPaid = true; offlineVideoCredit = ticketVideo ? MineGame.VideoTickets : game.Progress.coins - before;
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
                sounds.Play("reward"); Notice(ticketVideo ? "+10 ЖЕТОНОВ" : "СКЛАД · НАГРАДА ×3", ticketVideo ? "Подарок за просмотр видео" : "+" + ShortAmount(offlineVideoCredit) + " монет за просмотр видео");
                offlineNoticeQueued = game.HasOfflineReward;
                offlineVideoMessage = null;
            }
            else { RefreshOfflineWindow(); if (ticketVideo) Notice("ВИДЕО НЕ ЗАСЧИТАНО", "Награда выдаётся за полный просмотр. Попробуй позже."); }
            offlineVideoPaid = false; offlineVideoCredit = 0;
            RefreshRewards();
        }

        internal void ConfigureRewardedForSmoke(System.Func<string, bool> show)
        {
            if (!SmokeMode) return;
            rewardedVideoForSmoke = show; OnRewardedAvailable(show == null ? "0" : "1");
        }

        private void ApplyPlatformPause()
        {
            bool paused = enginePaused || browserPaused || RewardedVideoPending || accountPending;
            if (paused == applicationPaused) { BrowserPlatform.Gameplay(!paused); return; }
            if (paused) { holding = false; Flush(true); }
            applicationPaused = paused; skipEconomyFrame = true;
            platformInput.interactable = !paused;
            platformInput.blocksRaycasts = !paused;
            stage.Paused = enchantingWorld.Paused = paused;
            shaft.Paused = paused || !roulette || settingsWindowOpen;
            sounds.SetSuspended(paused || !audioFocused);
            if (!paused)
            {
                bool afterVideo = rewardedReturnPending; rewardedReturnPending = false;
                if (!SmokeMode || smokeSessionKey != null) PrepareReturn(SessionUtc, afterVideo);
                if (afterVideo) PresentRewardedResult();
            }
            BrowserPlatform.Gameplay(!applicationPaused);
        }
    }
}
