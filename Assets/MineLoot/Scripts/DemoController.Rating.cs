using System;
using UnityEngine;
using UnityEngine.UI;

namespace MineLoot
{
    public sealed partial class DemoController
    {
        [Serializable] private sealed class RatingEntry
        {
            public int rank,score,resets;
            public long blocks;
            public string name;
            public bool me,hasStats;
        }
        [Serializable] private sealed class RatingPayload
        {
            public string status;
            public int userRank,score;
            public RatingEntry[] entries;
        }
        [Serializable] private sealed class RatingSubmission
        {
            public int version=1,score,resets;
            public long blocks,coins,tickets,xp;
            public bool test;
        }
        private Image ratingBadge,profileRatingBadge;
        private Text ratingCaption,profileRatingTitle,profileRatingScore,profileRatingNext,ratingStatus,ratingPageLabel,ratingFormula;
        private readonly GameObject[] ratingSections=new GameObject[3],ratingRows=new GameObject[5];
        private readonly Image[] ratingRowBadges=new Image[5];
        private readonly Text[] ratingRowNames=new Text[5],ratingRowScores=new Text[5],ratingRowStats=new Text[5];
        private readonly Button[] ratingTabs=new Button[3];
        private Button ratingRefresh,ratingLogin,ratingPrevious,ratingNext;
        private bool ratingWindowOpen,ratingSending;
        private int ratingSection,ratingPage,submittedRating=-1;
        private float nextRatingSend,nextRatingLoad,nextRatingRefresh;
        private RatingPayload ratingData=new RatingPayload {status="idle",entries=new RatingEntry[0]};
        private string ratingReceipt="";
        private Func<string,bool> ratingSubmitForSmoke;
        private Func<bool> ratingLoadForSmoke;

        private void BuildRatingButton(Transform header)
        {
            var button=ButtonAt(header,"",344,56,112,32,ShowRating,Hex("d8c598"),Ink);button.name="Open rating";
            ratingBadge=Icon(button.transform,"rank-medal-0",3,2,28,28);
            ratingCaption=Label(button.transform,"MMR 0",33,1,76,30,16,Ink,FontStyle.Bold,TextAnchor.MiddleCenter);
            ratingCaption.resizeTextForBestFit=true;ratingCaption.resizeTextMinSize=12;ratingCaption.resizeTextMaxSize=16;
        }
        public void ShowRating()
        {
            if(applicationPaused || modal!=null) return;
            holding=false;ratingWindowOpen=true;ratingSection=ratingPage=0;
            modal=CreateModal("Rating modal");Card(modal.transform,"Rating card",24,48,432,864,Panel);
            Label(modal.transform,"РЕЙТИНГ ШАХТЁРОВ",40,62,400,42,26,Ink,FontStyle.Bold);
            var profile=Card(modal.transform,"Your rating",40,116,400,134,Hex("d4dfcf"));
            profileRatingBadge=Icon(profile.transform,"rank-medal-0",12,18,76,76);
            profileRatingTitle=Label(profile.transform,"",100,10,288,32,19,Ink,FontStyle.Bold);
            profileRatingTitle.resizeTextForBestFit=true;profileRatingTitle.resizeTextMinSize=15;profileRatingTitle.resizeTextMaxSize=19;
            profileRatingScore=Label(profile.transform,"",100,46,288,34,26,Ink,FontStyle.Bold);
            profileRatingNext=Label(profile.transform,"",12,100,376,28,16,Muted);
            string[] titles={"ТАБЛИЦА","МЕДАЛИ","РАСЧЁТ ММР"};
            for(int i=0;i<3;i++)
            {
                int section=i;ratingTabs[i]=ButtonAt(modal.transform,titles[i],40+i*136,266,128,32,()=>SelectRatingSection(section),Panel,Ink);
                ratingTabs[i].name="Rating section "+i;ratingTabs[i].GetComponentInChildren<Text>().fontSize=16;
                ratingSections[i]=MakeRect(modal.transform,"Rating content "+i,0,0,480,840).gameObject;
            }
            var list=ratingSections[0].transform;
            ratingStatus=Label(list,"",40,310,400,40,16,Muted);
            for(int i=0;i<5;i++)
            {
                var row=Card(list,"Rating row "+i,40,354+i*66,400,62,Panel);ratingRows[i]=row.gameObject;
                ratingRowBadges[i]=Icon(row.transform,"rank-medal-0",8,7,46,46);
                ratingRowNames[i]=Label(row.transform,"",62,4,218,26,18,Ink,FontStyle.Bold);ratingRowNames[i].supportRichText=false;
                ratingRowScores[i]=Label(row.transform,"",280,4,110,26,18,Ink,FontStyle.Bold,TextAnchor.MiddleRight);
                ratingRowScores[i].resizeTextForBestFit=true;ratingRowScores[i].resizeTextMinSize=12;ratingRowScores[i].resizeTextMaxSize=18;
                ratingRowStats[i]=Label(row.transform,"",62,34,328,22,15,Muted);
            }
            ratingPrevious=ButtonAt(list,"<",40,704,40,30,()=>ChangeRatingPage(-1),Panel,Ink);ratingPrevious.name="Rating previous";
            ratingPageLabel=Label(list,"",90,704,300,30,17,Ink,FontStyle.Bold,TextAnchor.MiddleCenter);
            ratingNext=ButtonAt(list,">",400,704,40,30,()=>ChangeRatingPage(1),Panel,Ink);ratingNext.name="Rating next";
            ratingRefresh=ButtonAt(list,"ОБНОВИТЬ ТАБЛИЦУ",40,750,400,40,RequestRating,Hex("d8c598"),Ink);ratingRefresh.name="Refresh rating";
            ratingLogin=ButtonAt(list,"ВОЙТИ В ЯНДЕКС ДЛЯ УЧАСТИЯ",40,802,400,36,()=>{CloseModal();ShowYandexAccount();},Hex("bad5ca"),Ink);ratingLogin.name="Rating sign in";ratingLogin.GetComponentInChildren<Text>().fontSize=17;
            for(int i=0;i<MineGame.RatingRankCount;i++)
            {
                var medal=Card(ratingSections[1].transform,"Rating medal "+i,40+i%2*204,318+i/2*86,196,80,Hex(i%2==1?"ded3eb":"d7dfd1"));
                Icon(medal.transform,"rank-medal-"+i,5,12,50,50);
                Label(medal.transform,MineGame.RatingName(i),60,4,130,44,16,Ink,FontStyle.Bold);
                Label(medal.transform,"От "+MineGame.RatingThreshold(i).ToString("N0",interfaceCulture)+" MMR",60,50,130,24,16,Muted);
            }
            ratingFormula=Label(ratingSections[2].transform,"",40,316,400,510,18,Ink,FontStyle.Normal,TextAnchor.UpperLeft);
            ButtonAt(modal.transform,"Вернуться в игру",40,858,400,38,CloseModal,Panel,Ink).name="Close rating";
            RefreshRating();RequestRating();
        }
        private void SelectRatingSection(int section) { ratingSection=section;RefreshRating(); }
        private void ChangeRatingPage(int delta) { ratingPage=Mathf.Clamp(ratingPage+delta,0,Mathf.Max(0,(ratingData.entries.Length-1)/5));RefreshRating(); }
        private void RequestRating()
        {
            if(applicationPaused || Time.unscaledTime<nextRatingLoad) return;
            nextRatingLoad=Time.unscaledTime+30;ratingData.status="loading";RefreshRating();
            bool accepted=SmokeMode&&ratingLoadForSmoke!=null?ratingLoadForSmoke():BrowserPlatform.RequestRating();
            if(!accepted) {ratingData.status="unavailable";RefreshRating();}
        }
        public void OnBrowserRating(string json)
        {
            RatingPayload data;try {data=JsonUtility.FromJson<RatingPayload>(json);} catch {return;}
            if(data==null || string.IsNullOrEmpty(data.status)) return;
            data.entries=data.entries??new RatingEntry[0];
            if(data.entries.Length>20) return;
            foreach(var entry in data.entries) if(entry==null || entry.rank<1 || entry.score<0 || entry.blocks<0 || entry.resets<0) return;
            ratingData=data;ratingPage=Mathf.Clamp(ratingPage,0,Mathf.Max(0,(data.entries.Length-1)/5));RefreshRating();
        }
        public void OnBrowserRatingSaved(string json)
        {
            RatingPayload result;try {result=JsonUtility.FromJson<RatingPayload>(json);} catch {return;}
            if(result==null) return;ratingSending=false;
            if(result.status=="saved") {submittedRating=Math.Max(submittedRating,result.score);ratingReceipt="Лучший результат сохранён в Яндексе.";}
            else ratingReceipt="Результат пока не отправлен. Повторим позже.";
            RefreshRating();
        }
        private void PumpRating()
        {
            if(ratingWindowOpen && Time.unscaledTime>=nextRatingRefresh) {nextRatingRefresh=Time.unscaledTime+.5f;RefreshRating();}
            if(game==null || game.Progress.ratingTestProfile || accountStatus!="authorized" || ratingSending || Time.unscaledTime<nextRatingSend || game.Mmr<=Math.Max(0,submittedRating)) return;
            if(SmokeMode && ratingSubmitForSmoke==null) return;
            nextRatingSend=Time.unscaledTime+30;ratingSending=true;
            var data=new RatingSubmission {score=game.Mmr,blocks=game.RatingBlocks,coins=game.Progress.earnedCoins,tickets=game.Progress.spentTickets,xp=game.Progress.spentEnchantingExperience,resets=game.Progress.prestigeCount,test=game.Progress.ratingTestProfile};
            string json=JsonUtility.ToJson(data);
            bool accepted=SmokeMode?ratingSubmitForSmoke(json):BrowserPlatform.SubmitRating(json);
            if(!accepted) ratingSending=false;
        }
        private void RefreshRating()
        {
            if(ratingCaption==null || game==null) return;
            int score=game.Mmr,rank=MineGame.RatingRank(score);
            ratingCaption.text="MMR "+ShortAmount(score);ratingBadge.sprite=art.Get("rank-medal-"+rank);
            if(!ratingWindowOpen || modal==null || modal.name!="Rating modal") return;
            profileRatingBadge.sprite=art.Get("rank-medal-"+rank);profileRatingTitle.text=MineGame.RatingName(rank);profileRatingScore.text=score.ToString("N0",interfaceCulture)+" MMR";
            profileRatingNext.text=rank+1<MineGame.RatingRankCount?"До следующей медали: "+(MineGame.RatingThreshold(rank+1)-score).ToString("N0",interfaceCulture)+" MMR":"Высшая медаль шахтёра";
            for(int i=0;i<3;i++) {ratingSections[i].SetActive(i==ratingSection);ratingTabs[i].image.color=i==ratingSection?Hex("b6c997"):Panel;}
            ratingStatus.text=ratingData.status=="loading"?"Загружаем рейтинг Яндекса…":ratingData.status=="error"?"Не удалось загрузить таблицу. Попробуй позже.":ratingData.status=="unavailable"?"Общий рейтинг доступен в Яндекс Играх.":ratingData.entries.Length==0?"В рейтинге пока нет игроков. Стань первым!":ratingData.userRank>0?"Твоё место: "+ratingData.userRank+" · в таблице лучший результат":"Лучшие игроки и участники рядом с тобой";
            if(game.Progress.ratingTestProfile && ratingData.status!="loading") ratingStatus.text="Тестовый прогресс: в общий топ не отправляется.";
            else if(ratingData.status=="ready" && ratingReceipt.Length>0 && ratingData.userRank==0) ratingStatus.text=ratingReceipt;
            for(int i=0;i<5;i++)
            {
                int index=ratingPage*5+i;bool show=index<ratingData.entries.Length;ratingRows[i].SetActive(show);if(!show) continue;
                var entry=ratingData.entries[index];ratingRows[i].GetComponent<Image>().color=entry.me?Hex("c8dfc9"):Panel;
                ratingRowBadges[i].sprite=art.Get("rank-medal-"+MineGame.RatingRank(entry.score));
                string name=(entry.name??"").Replace('\n',' ').Replace('\r',' ').Trim();if(name.Length==0) name="Игрок";if(name.Length>23) name=name.Substring(0,22)+"…";
                ratingRowNames[i].text=entry.rank+". "+name+(entry.me?" · ты":"");
                ratingRowNames[i].resizeTextForBestFit=true;ratingRowNames[i].resizeTextMinSize=14;ratingRowNames[i].resizeTextMaxSize=18;
                ratingRowScores[i].text=ShortAmount(entry.score)+" MMR";
                ratingRowStats[i].text=entry.hasStats?"Блоков: "+ShortAmount(entry.blocks)+" · Сбросов: "+entry.resets:"Статистика не предоставлена";
            }
            int pages=Mathf.Max(1,(ratingData.entries.Length+4)/5);ratingPageLabel.text=(ratingPage+1)+" / "+pages;
            ratingPrevious.interactable=ratingPage>0;ratingNext.interactable=ratingPage+1<pages;
            int wait=Mathf.CeilToInt(Mathf.Max(0,nextRatingLoad-Time.unscaledTime));ratingRefresh.interactable=wait==0&&ratingData.status!="loading";
            ratingRefresh.GetComponentInChildren<Text>().text=wait>0?"ОБНОВИТЬ ЧЕРЕЗ "+wait+" С":"ОБНОВИТЬ ТАБЛИЦУ";
            ratingLogin.gameObject.SetActive(accountStatus!="authorized");ratingLogin.interactable=accountAvailable&&!accountPending;
            ratingFormula.text="Прогресс за все шахты:\n\n(монеты + блоки × 1 000 +\nжетоны × 5 000 + опыт чар × 100)\n÷ 100 000 × (1 + сбросы).\nИтог округляется вниз.\n\nБлоков добыто: "+game.RatingBlocks.ToString("N0",interfaceCulture)+"\nМонет получено: "+game.Progress.earnedCoins.ToString("N0",interfaceCulture)+"\nЖетонов потрачено: "+game.Progress.spentTickets.ToString("N0",interfaceCulture)+"\nОпыта на чары: "+game.Progress.spentEnchantingExperience.ToString("N0",interfaceCulture)+"\nСбросов шахты: "+game.Progress.prestigeCount+" · множитель ×"+(game.Progress.prestigeCount+1)+"\n\nПокупки не уменьшают ММР.\nВ таблице — лучший результат.\n"+(game.Progress.ratingHistoryPartial?"\nИстория монет и расходов на чары\nраньше не велась. Их учёт начат\nс этого обновления.":game.Progress.ratingTestProfile?"\nТестовый профиль не участвует в общем топе.":"\nДля участия в таблице войди в Яндекс.");
        }
        internal void ConfigureRatingForSmoke(Func<string,bool> submit,Func<bool> load)
        {
            if(!SmokeMode) return;ratingSubmitForSmoke=submit;ratingLoadForSmoke=load;nextRatingLoad=nextRatingSend=0;submittedRating=-1;ratingSending=false;
        }
    }
}
