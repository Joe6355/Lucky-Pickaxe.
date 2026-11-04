using System;
using UnityEngine;
using UnityEngine.UI;

namespace MineLoot
{
    public sealed partial class DemoController
    {
        [Serializable] private sealed class CloudPayload { public string status,token,data,local; }
        private string cloudStatus="unavailable";
        private bool cloudRatingReady,cloudWindowOpen;
        private CloudPayload pendingCloud;
        private Text cloudStatusLabel;
        private Button cloudSyncButton;
        private CanvasGroup cloudInput;

        public void OnBrowserCloud(string json)
        {
            CloudPayload payload;try {payload=JsonUtility.FromJson<CloudPayload>(json);} catch {return;}
            if(payload==null || string.IsNullOrEmpty(payload.status)) return;
            cloudStatus=payload.status;
            cloudRatingReady=cloudStatus=="local" || cloudStatus=="saved" || cloudStatus=="guest";
            if(cloudStatus=="choice" || cloudStatus=="remote") pendingCloud=payload;
            RefreshCloudStatus();PumpCloudDecision();
        }
        private void RefreshCloudStatus()
        {
            if(modal==null || modal.name!="Yandex account modal" || cloudStatusLabel==null) return;
            cloudStatusLabel.text=cloudStatus=="saved"?"Облако: сохранено.\nСледующая запись — автоматически.":
                cloudStatus=="local"?"Прогресс записан на устройстве.\nОблако обновляется каждые 30 секунд.":
                cloudStatus=="loading" || cloudStatus=="syncing"?"Синхронизация с облаком…":
                cloudStatus=="error"?"Облако недоступно. Локальная копия сохранена. Повторим позже.":
                "Для облачного сохранения войди в Яндекс.";
            cloudSyncButton.gameObject.SetActive(accountStatus=="authorized");
            cloudSyncButton.interactable=!accountPending && cloudStatus!="loading" && cloudStatus!="syncing";
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
            if(pendingCloud==null || cloudWindowOpen || RewardedVideoPending || accountPending || browserPaused || enginePaused) return;
            if(pendingCloud.status=="remote" && SameCloudProgress(JsonUtility.ToJson(game.Progress),pendingCloud.local)) {ChooseCloud(true);return;}
            if(modal!=null) CloseModal();
            holding=false;cloudWindowOpen=true;
            modal=CreateModal("Cloud conflict modal");
            // Include the viewport so a frozen conflict can still be scrolled on a short phone.
            cloudInput.ignoreParentGroups=true;
            Card(modal.transform,"Cloud conflict card",24,134,432,692,Panel);
            Label(modal.transform,"ВЫБЕРИ СОХРАНЕНИЕ",44,156,392,44,25,Ink,FontStyle.Bold);
            Label(modal.transform,"На устройстве и в облаке разные версии. Выбранная заменит другую. Ресурсы не складываются.",44,214,392,90,20,Muted);
            Label(modal.transform,"ЭТО УСТРОЙСТВО\n"+CloudSummary(JsonUtility.ToJson(game.Progress)),44,318,392,112,21,Ink);
            Label(modal.transform,"ОБЛАКО ЯНДЕКСА\n"+CloudSummary(pendingCloud.data),44,446,392,112,21,Ink);
            ButtonAt(modal.transform,"ПРОДОЛЖИТЬ С УСТРОЙСТВА",44,596,392,56,()=>ChooseCloud(false),Hex("dfc285"),Ink).name="Use local save";
            ButtonAt(modal.transform,"ЗАГРУЗИТЬ ИЗ ОБЛАКА",44,670,392,56,()=>ChooseCloud(true),Hex("bad5ca"),Ink).name="Use cloud save";
            Label(modal.transform,"Перед загрузкой сохраняем резервную копию на этом устройстве.",44,742,392,58,18,Muted);
            ApplyPlatformPause();
        }
        private static string CloudSummary(string json)
        {
            try {
                var snapshot=new MineGame(string.IsNullOrEmpty(json)?new MineProgress():ProgressStore.Decode(json));
                return "Шахта ур. "+snapshot.MinerLevel+" · сбросов "+snapshot.Progress.prestigeCount+"\n"+snapshot.Progress.coins.ToString("N0")+" монет · "+snapshot.Progress.tickets+" жетонов"+(snapshot.HasPendingRoll?"\nЕсть оплаченная рулетка":"");
            } catch {return "Повреждённая версия. Выбери это устройство.";}
        }
        private void ChooseCloud(bool remote)
        {
            if(pendingCloud==null || enginePaused || browserPaused || RewardedVideoPending || accountPending) return;
            var decision=pendingCloud;
            MineProgress snapshot=null;
            if(remote) {
                try {snapshot=string.IsNullOrEmpty(decision.data)?new MineProgress():ProgressStore.Decode(decision.data);}
                catch {cloudStatus="error";return;}
            }
            // Keep a separate durable backup; never add wallets, XP, draw or reward counters together.
            if(!SmokeMode) ProgressStore.Save(game,ProgressStore.Key+".before-cloud");
            cloudWindowOpen=false;cloudInput.ignoreParentGroups=false;pendingCloud=null;
            if(modal!=null) CloseModal();
            if(remote) {
                StopAllCoroutines();if(miningGain!=null) {Destroy(miningGain.gameObject);miningGain=null;}
                game=new MineGame(snapshot);game.PrepareOffline(SessionUtc);RestorePresentation();
            }
            game.RecordSession(SessionUtc);Persist();Flush(true);
            BrowserPlatform.ResolveCloud(decision.token,remote);ApplyPlatformPause();Refresh();
        }
    }
}
