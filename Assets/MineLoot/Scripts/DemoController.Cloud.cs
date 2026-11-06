using System;
using UnityEngine;
using UnityEngine.UI;

namespace MineLoot
{
    public sealed partial class DemoController
    {
        [Serializable] private sealed class CloudPayload { public string status,token,data,local,waitingFor; public bool authoritative; public long nextSyncUtc; }
        private string cloudStatus="unavailable";
        private string cloudWaitingFor;
        private bool cloudRatingReady;
        private long cloudNextSyncUtc;
        private CloudPayload pendingCloud;
        private Text cloudStatusLabel;
        private Button cloudSyncButton;
        private CanvasGroup cloudInput;

        public void OnBrowserCloud(string json)
        {
            CloudPayload payload;try {payload=JsonUtility.FromJson<CloudPayload>(json);} catch {return;}
            if(payload==null || string.IsNullOrEmpty(payload.status)) return;
            cloudStatus=payload.status;
            cloudNextSyncUtc=payload.nextSyncUtc;
            cloudWaitingFor=payload.waitingFor;
            cloudRatingReady=cloudStatus=="local" || cloudStatus=="saved" || cloudStatus=="guest";
            if(cloudStatus=="merged") pendingCloud=payload;
            else if(cloudStatus=="loading" || cloudStatus=="guest" || cloudStatus=="error") pendingCloud=null;
            RefreshCloudStatus();PumpCloudDecision();
        }
        private void RefreshCloudStatus()
        {
            if(modal==null || modal.name!="Yandex account modal" || cloudStatusLabel==null) return;
            cloudStatusLabel.text=cloudStatus=="saved"?"Облако: сохранено.\nСледующая запись — автоматически.":
                cloudStatus=="local"?"Прогресс записан на устройстве.\nОблако — при запуске и примерно раз в 5 минут.":
                cloudStatus=="loading" || cloudStatus=="syncing"?"Синхронизация с облаком…":
                cloudStatus=="merged"?"Объединяем прогресс устройств…":
                cloudStatus=="waiting"?CloudWaitingMessage():
                cloudStatus=="error"?"Облако недоступно. Локальная копия сохранена. Повторим позже.":
                "Для облачного сохранения войди в Яндекс.";
            cloudSyncButton.gameObject.SetActive(accountStatus=="authorized");
            bool intervalPending=SessionUtc<cloudNextSyncUtc && (cloudStatus=="local" || cloudStatus=="saved");
            cloudSyncButton.interactable=!accountPending && !intervalPending && cloudStatus!="loading" && cloudStatus!="syncing" && cloudStatus!="merged";
            cloudSyncButton.GetComponentInChildren<Text>().text=intervalPending?"ОБЛАКО ОБНОВЛЯЕТСЯ АВТОМАТИЧЕСКИ":"СОХРАНИТЬ В ОБЛАКО";
        }
        private string CloudWaitingMessage()
        {
            switch(cloudWaitingFor) {
                case "local-roulette":return "Заверши оплаченную рулетку на этом устройстве.\nЗатем объединим прогресс автоматически.";
                case "remote-roulette":return "На другом устройстве не завершена рулетка.\nОбъединим прогресс после розыгрыша.";
                default:return "Ждём завершения оплаченной рулетки.\nЗатем объединим прогресс автоматически.";
            }
        }
        private void RequestCloudSync()
        {
            if(applicationPaused || accountStatus!="authorized") return;
            Flush(true);BrowserPlatform.SyncCloud();
        }
        private static bool SameCloudProgress(string first,string second)
        {
            try {
                var a=ProgressStore.Decode(first);var b=ProgressStore.Decode(second);
                a.lastSeenUtc=b.lastSeenUtc=0;
                return JsonUtility.ToJson(a)==JsonUtility.ToJson(b);
            } catch {return false;}
        }
        private void PumpCloudDecision()
        {
            // The account panel is a status view: it must not prevent the sync it is displaying.
            // Other dialogs and unacknowledged paid transactions still defer replacement.
            bool keepAccount=modal!=null && modal.name=="Yandex account modal";
            bool keepWarehouse=modal!=null && modal.name=="Offline reward modal";
            if(pendingCloud==null || RewardedVideoPending || accountPending || browserPaused || enginePaused || holding || !pendingCloud.authoritative && (busy || game.ActionsLocked) || modal!=null && !keepAccount && !keepWarehouse) return;
            var decision=pendingCloud;
            Flush(true);
            if(!SameCloudProgress(JsonUtility.ToJson(game.Progress),decision.local)) {
                // The SDK read can finish after another click or production tick. Rebase those
                // changes before installing the merged ledger instead of overwriting them.
                pendingCloud=null;skipEconomyFrame=true;BrowserPlatform.ResolveCloud(decision.token,false);return;
            }
            MineGame merged;
            try {merged=new MineGame(ProgressStore.Decode(decision.data));}
            catch {pendingCloud=null;cloudStatus="error";RefreshCloudStatus();return;}
            if(!SmokeMode) ProgressStore.Save(game,ProgressStore.Key+".before-cloud");
            int page=selectedPage,section=workshopSection;bool production=productionTab;
            pendingCloud=null;
            // Commit the ledger before journaling Unity's normalized snapshot, so its new wallet
            // is acknowledged as imported progress rather than counted as another local reward.
            BrowserPlatform.ResolveCloud(decision.token,true);
            StopAllCoroutines();if(miningGain!=null) {Destroy(miningGain.gameObject);miningGain=null;}
            game=merged;game.PrepareOffline(SessionUtc);RestorePresentation(keepAccount||keepWarehouse);
            if(!game.ActionsLocked) {
                selectedPage=page;workshopSection=section;productionTab=production;ApplyPageVisibility();
            }
            game.RecordSession(SessionUtc);Persist();Flush(true);
            ApplyPlatformPause();Refresh();if(keepAccount) RefreshAccount();if(keepWarehouse)RefreshOfflineWindow();
        }
    }
}
