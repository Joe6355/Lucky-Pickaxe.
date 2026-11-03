using System;
using MineLoot;
using UnityEngine;

public static partial class DemoTools
{
    private static void VerifyRatingRules(Action<bool,string> check)
    {
        check(MineGame.RatingScore(100,100000,20,1000,2)==12,"MMR uses all four lifetime metrics and one plus completed resets");
        check(MineGame.RatingScore(100,100000,20,1000,0)==4,"A new player can earn MMR before the first reset");
        check(MineGame.RatingScore(1,0,0,0,99)==1,"Fractional MMR survives until multiplication before the final floor");
        check(MineGame.RatingScore(1000,0,0,0,0)==10 && MineGame.RatingScore(0,1000000,0,0,0)==10 && MineGame.RatingScore(0,0,200,0,0)==10 && MineGame.RatingScore(0,0,0,10000,0)==10,"Every metric has a documented nonzero contribution");
        check(MineGame.RatingScore(-1,-1,-1,-1,-1)==0 && MineGame.RatingScore(long.MaxValue,long.MaxValue,long.MaxValue,long.MaxValue,int.MaxValue)==int.MaxValue,"MMR clamps invalid input and cannot overflow at lifetime limits");
        for(int rank=0;rank<MineGame.RatingRankCount;rank++)
        {
            int threshold=MineGame.RatingThreshold(rank);
            check(MineGame.RatingRank(threshold)==rank,"Each medal unlocks at its exact threshold: "+rank);
            if(rank>0) check(MineGame.RatingRank(threshold-1)==rank-1,"A medal cannot unlock one MMR early: "+rank);
            check(MineGame.RatingName(rank).Contains("Зачарованная")==((rank&1)==1),"Enchanted and ordinary medals remain distinct: "+rank);
        }
        var game=new MineGame(new MineProgress{coins=2000,earnedCoins=1000000});
        int before=game.Mmr;game.BuyUpgrade(UpgradeKind.Pickaxe);
        check(game.Mmr==before && game.Progress.earnedCoins==1000000,"Buying a pick cannot reduce gross earnings or MMR");
        long wallet=game.Progress.coins;game.Mine();
        check(game.Progress.earnedCoins==1000000+game.Progress.coins-wallet,"A genuine mining payment records its actual credited coins once");
        game=new MineGame(new MineProgress{coins=MineGame.CoinLimit-1,workerCount=1});game.AdvanceTime(10,false);
        check(game.Progress.coins==MineGame.CoinLimit && game.Progress.earnedCoins==1,"Gross income excludes coins rejected by a full wallet");
        game=new MineGame(new MineProgress{workerCount=1});game.AdvanceTime(10,false);game.RecordSession(1700000000);game.PrepareOffline(1700000010);game.ClaimOffline(true);
        check(game.Progress.earnedCoins==50 && !game.ClaimOffline(true) && game.Progress.earnedCoins==50,"Production and a tripled warehouse count actual credits without replay");
        game=new MineGame(new MineProgress{coins=1000,enchantingExperience=1000});game.Enchant(0,()=>.2);game.Enchant(0,()=>.3);
        check(game.Progress.spentEnchantingExperience==100,"Enchanting records both paid XP prices, including a more expensive reroll");
        game.BuyUpgrade(UpgradeKind.Pickaxe,2);
        check(!game.IsEnchanted && game.Progress.spentEnchantingExperience==100,"A new pick material clears effects but preserves lifetime enchanting expenditure");
        long xpSpent=game.Progress.spentEnchantingExperience;game.Progress.enchantingExperience=0;
        check(!game.Enchant(0,()=>.4) && game.Progress.spentEnchantingExperience==xpSpent,"An unaffordable enchantment adds no expenditure");
        game=new MineGame(new MineProgress{tickets=5,depths=new[]{1,0,0,0,0}});
        check(game.Abandon(()=>.1) && game.Progress.spentTickets==1,"A paid layout change counts its ticket once");
        game.BeginRoll(()=>.1);var reload=new MineGame(ProgressStore.Decode(JsonUtility.ToJson(game.Progress)));reload.ResolveRoll();
        check(reload.Progress.spentTickets==2 && !reload.HasPendingRoll,"Reloading and resolving a paid draw cannot spend its ticket twice");
        game=new MineGame(new MineProgress{tickets=10});game.BeginBatch(()=>.1);
        while(game.Progress.batchActive) {if(game.HasPendingRoll)game.ResolveRoll();else game.ContinueBatch(()=>.1);}
        check(game.Progress.spentTickets==10,"A complete ten-roll series records exactly ten paid tickets");
        var old=new MineGame(new MineProgress{version=15,coins=100000080,enchantingExperience=888,miningBlocks=111,totalBlocks=222,totalRolls=27});
        check(old.Progress.version==16 && old.Progress.coins==100000080 && old.Progress.enchantingExperience==888 && old.RatingBlocks==333 && old.Progress.spentTickets==27,"v15 migration keeps the wallet, XP, real blocks and known paid draws");
        check(old.Progress.earnedCoins==0 && old.Progress.spentEnchantingExperience==0 && old.Progress.ratingHistoryPartial,"A legacy wallet or available XP cannot invent lifetime ranked earnings or expenditure");
        game=new MineGame(new MineProgress{coins=10000,experience=MineGame.ExperienceForLevel(50),earnedCoins=123456,spentTickets=9,spentEnchantingExperience=40,miningBlocks=10,totalBlocks=20,ratingTestProfile=true});
        int scoreBefore=game.Mmr;check(game.Prestige(()=>.2),"A rating fixture can complete the normal confirmed reset");
        reload=new MineGame(ProgressStore.Decode(JsonUtility.ToJson(game.Progress)));
        check(reload.Progress.earnedCoins==123456 && reload.Progress.spentTickets==9 && reload.Progress.spentEnchantingExperience==40 && reload.RatingBlocks==30 && reload.Progress.ratingTestProfile && reload.Mmr>=scoreBefore,"Reset and JSON reload preserve every lifetime metric, test exclusion and accumulated MMR");
        for(int resets=0;resets<10;resets++) check(MineGame.RatingScore(100,123456,9,40,resets+1)>=MineGame.RatingScore(100,123456,9,40,resets),"More completed resets cannot lower MMR: "+resets);
    }
}
