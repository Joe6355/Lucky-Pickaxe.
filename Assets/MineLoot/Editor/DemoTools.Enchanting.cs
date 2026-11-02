using System;
using MineLoot;
using UnityEngine;

public static partial class DemoTools
{
    private static void VerifyEnchantingAndBatches(Action<bool,string> check)
    {
        var game = new MineGame(ProgressStore.Decode("{\"version\":12,\"coins\":745,\"tickets\":400,\"hits\":7,\"pressLevel\":2,\"experience\":700,\"upgradeLevel\":20,\"oreDamage\":12}"));
        check(game.Progress.version == 14 && game.Progress.tickets == 400 && game.Progress.hits == 35 && game.HitsRequired == 50 && game.Progress.enchantingExperience == 700, "v12 preserves the ticket stock, counter fraction and level while importing earned enchanting XP once");
        game = new MineGame(ProgressStore.Decode(JsonUtility.ToJson(game.Progress)));
        check(game.Progress.hits == 35 && game.Progress.enchantingExperience == 700, "v13 reload neither multiplies the counter nor grants old XP again");
        game = new MineGame(new MineProgress { enchantingExperience = 10000, coins = 100000, experience = 700 });
        string before = JsonUtility.ToJson(game.Progress); int offer = game.EnchantmentOffer(2); long cost = game.EnchantmentCost(2);
        check(game.EnchantmentOffer(2) == offer && JsonUtility.ToJson(game.Progress) == before, "Offer preview never changes progress or rerolls its result");
        try { game.Enchant(2, () => double.NaN); } catch (ArgumentOutOfRangeException) { }
        check(JsonUtility.ToJson(game.Progress) == before, "An invalid enchantment RNG is atomic");
        check(game.Enchant(2, () => .6) && game.Progress.enchantments == offer && game.Progress.enchantingExperience == 10000-cost && game.Progress.experience == 700, "Enchanting pays its displayed XP and applies exactly the preview without reducing miner level");
        long nextCost = game.EnchantmentCost(2); before=JsonUtility.ToJson(game.Progress);
        check(nextCost > cost && new MineGame(ProgressStore.Decode(before)).EnchantmentOffer(2) == game.EnchantmentOffer(2), "Rerolls cost more and their next offers survive reload");
        int oldSet=game.Progress.enchantments;
        game.Progress.upgradeLevel=1; check(game.Upgrade() && game.PickTier==1 && game.Progress.enchantments==oldSet, "Material improvements retain the actual pick's enchantments");
        game=new MineGame(new MineProgress { enchantments=3<<4 });
        for(int i=0;i<40;i++) game.Mine();
        check(game.Progress.pickWear==10, "Unbreaking III consumes durability once per four actual mining hits");
        game=new MineGame(new MineProgress { pickWear=50, enchantments=1<<12 }); game.Mine();
        check(game.Progress.pickWear==49 && game.Progress.enchantingExperience==0 && game.Progress.experience==1, "Mending repairs with earned XP, preserving lifetime level experience");
        game=new MineGame(new MineProgress { coins=1000, pickWear=160 }); int dull=game.MiningDamage;
        check(game.PickCondition==0 && game.RepairPick() && game.PickCondition==160 && game.Progress.coins==970 && game.MiningDamage>=dull, "A dull pick remains owned and can be repaired by one bounded coin transaction");
        game=new MineGame(new MineProgress { enchantments=5 });
        check(game.MiningDamage>1, "Efficiency V improves the equipped wooden pick's actual mining damage");
        game=new MineGame(new MineProgress { enchantments=3<<8 });
        check(game.OreReward==153, "Fortune III improves the whole ore reward by sixty percent");
        game=new MineGame(new MineProgress { enchantingExperience=10000, tickets=9 });
        check(!game.BeginBatch(()=>.5) && game.Progress.tickets==9, "Ten-roll series requires the displayed ten-ticket budget");
        game=new MineGame(new MineProgress { enchantingExperience=10000, tickets=100, coins=10000, swapLevel=3 });
        before=JsonUtility.ToJson(game.Progress);
        try { game.BeginBatch(()=>throw new Exception("test")); } catch(Exception) { }
        check(before==JsonUtility.ToJson(game.Progress), "A failing first batch draw never starts a queue or charges tickets");
        check(game.BeginBatch(()=>.6) && game.Progress.tickets==99 && game.Progress.batchRemaining==9 && game.Progress.pendingBatch && game.Progress.pendingSwapLimit==0, "Series fixes its first paid draw and queues nine unpaid rolls without manual exchange");
        check(!game.Enchant(0,()=>.5) && !game.RepairPick() && !game.BuyUpgrade(UpgradeKind.Pickaxe), "A queued paid series blocks unrelated transactions");
        long reward=0;
        for(int i=0;i<10;i++)
        {
            game=new MineGame(ProgressStore.Decode(JsonUtility.ToJson(game.Progress)));
            var roll=game.ResolveRoll(); check(roll!=null, "Saved paid batch roll resolves "+i); reward+=roll.reward;
            game=new MineGame(ProgressStore.Decode(JsonUtility.ToJson(game.Progress)));
            check(game.ResolveRoll()==null && game.Progress.batchCompleted==i+1, "Resolved batch reward cannot be replayed "+i);
            if(i<9) check(game.ContinueBatch(()=>.6), "Queue resumes after save between rolls "+i);
        }
        check(!game.Progress.batchActive && !game.HasPendingRoll && game.Progress.totalRolls==10 && game.Progress.batchReward==reward && game.Progress.tickets>=90 && !game.ContinueBatch(()=>.6), "Series completes exactly ten rolls and retains its total reward without an eleventh charge");
        game=new MineGame(new MineProgress {tickets=10}); game.BeginBatch(()=>.6); game.StopBatch();
        game=new MineGame(ProgressStore.Decode(JsonUtility.ToJson(game.Progress))); game.ResolveRoll();
        check(!game.Progress.batchActive && game.Progress.totalRolls==1 && game.Progress.tickets==9 && !game.ContinueBatch(()=>.6), "Stop preserves the current paid draw and never spends its nine unstarted tickets");
    }
    private static void VerifyRoulettePauseRules(Action<bool,string> check)
    {
        var game=new MineGame(new MineProgress {coins=100000,experience=MineGame.ExperienceForLevel(50),tickets=20,swapLevel=2,enchantingExperience=10000,pickWear=20});
        game.BeginRoll(()=>.6); string picks=string.Join(",",game.Progress.pendingTypes);
        check(game.EconomyLocked && !game.CanUpgrade && !game.CanEnchant(0),"A visible paid roll keeps its existing transaction protection");
        game.RoulettePaused=true; long coins=game.Progress.coins; game.Mine();
        check(game.Progress.totalClicks==1 && game.Progress.coins>coins && game.HasPendingRoll && string.Join(",",game.Progress.pendingTypes)==picks && game.Progress.tickets==19,"Paused roulette permits mining without changing its paid picks or spending another ticket");
        check(game.Upgrade() && game.BuyUpgrade(UpgradeKind.Worker) && game.Enchant(0,()=>.5) && game.RepairPick(),"Paused roulette permits safe tool, worker, enchantment and repair transactions");
        check(!game.BuyUpgrade(UpgradeKind.Forge) && !game.BuyUpgrade(UpgradeKind.Swap) && !game.BuyUpgrade(UpgradeKind.Treasure) && !game.Abandon(()=>.5) && !game.Prestige(()=>.5),"Pause cannot change pending roulette power, reward rules, shaft or reset its paid state");
        check(game.ClaimVideoTickets(1800000000) && game.Progress.tickets==29,"Ten-ticket video is available from Rewards while a draw is paused");
        var expected=new MineGame(ProgressStore.Decode(JsonUtility.ToJson(game.Progress)));
        check(!expected.RoulettePaused && expected.EconomyLocked && expected.Progress.tickets==29,"Reload preserves the paid roll without persisting a bypass of its visible lock");
        var roll=game.ResolveRoll();var restoredRoll=expected.ResolveRoll();
        check(roll.reward==restoredRoll.reward && string.Join(",",roll.powers)==string.Join(",",restoredRoll.powers),"Resuming or reloading a paused roll produces exactly the same outcome");
        game=new MineGame(new MineProgress {tickets=20,coins=100000,experience=MineGame.ExperienceForLevel(50)});
        game.BeginBatch(()=>.6);game.RoulettePaused=true;game.Mine();
        check(game.Progress.batchRemaining==9 && game.Progress.batchCompleted==0 && game.Progress.tickets==19 && game.Progress.totalClicks==1,"Independent mining cannot advance the paused ten-roll queue");
        game.StopBatch();game.ResolveRoll();
        check(game.Progress.tickets==19 && game.Progress.totalRolls==1 && game.Progress.batchCompleted==1,"Stopping a paused series retains its one paid result and all unstarted tickets");
        game=new MineGame(new MineProgress {tickets=999991});
        check(!game.ClaimVideoTickets(1800000000) && game.Progress.videoTicketClaims==0,"Ten-ticket gift refuses an overflowing wallet without consuming the cooldown");
        game=new MineGame(new MineProgress {tickets=999990});
        check(game.ClaimVideoTickets(1800000000) && game.Progress.tickets==1000000,"Ten-ticket gift fits exactly at the ticket limit");
    }

}
