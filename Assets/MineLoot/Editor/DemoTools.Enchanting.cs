using System;
using MineLoot;
using UnityEngine;

public static partial class DemoTools
{
    private static void VerifyEnchantingAndBatches(Action<bool,string> check)
    {
        var game = new MineGame(ProgressStore.Decode("{\"version\":12,\"coins\":745,\"tickets\":400,\"hits\":7,\"pressLevel\":2,\"experience\":700,\"upgradeLevel\":20,\"oreDamage\":12}"));
        check(game.Progress.version == 15 && game.Progress.tickets == 400 && game.Progress.hits == 35 && game.HitsRequired == 50 && game.Progress.enchantingExperience == 700, "v12 preserves the ticket stock, counter fraction and level while importing earned enchanting XP once");
        game = new MineGame(ProgressStore.Decode(JsonUtility.ToJson(game.Progress)));
        check(game.Progress.hits == 35 && game.Progress.enchantingExperience == 700, "v13 reload neither multiplies the counter nor grants old XP again");
        game = new MineGame(new MineProgress { enchantingExperience = 10000, coins = 100000, experience = 700 });
        string before=JsonUtility.ToJson(game.Progress);long cost=game.EnchantmentCost(2);
        check(JsonUtility.ToJson(game.Progress)==before,"Reading hidden grade and cost never spends or draws enchantments");
        try { game.Enchant(2,()=>double.NaN); } catch(ArgumentOutOfRangeException) { }
        check(JsonUtility.ToJson(game.Progress)==before,"An invalid enchantment RNG is atomic");
        check(game.Enchant(2,()=>.6) && game.Progress.enchantments!=0 && game.Progress.enchantingExperience==10000-cost && game.Progress.experience==700,"A paid hidden roll applies random enchantments without lowering mine level");
        long nextCost=game.EnchantmentCost(2);before=JsonUtility.ToJson(game.Progress);
        var restored=new MineGame(ProgressStore.Decode(before));
        check(nextCost>cost && restored.EnchantmentCost(2)==nextCost && restored.Progress.enchantments==game.Progress.enchantments,"Paid effects and the increased reroll cost survive reload");
        int oldSet=game.Progress.enchantments;game.Progress.upgradeLevel=0;
        check(game.Upgrade() && game.PickTier==0 && game.Progress.enchantments==oldSet,"An upgrade within the same material retains its enchantments");
        check(game.Upgrade() && game.PickTier==1 && game.Progress.enchantments==0 && game.Progress.enchantCount==0 && game.Progress.pickWear==0,"A new pick material removes its old enchantments and resets wear and reroll pricing");
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

    private static void VerifyMetaExpansion(Action<bool,string> check)
    {
        int total=0;for(int i=0;i<MineGame.PrestigeUpgradeCount;i++) total+=MineGame.PrestigeUpgradeAt(i).cost;
        var game=new MineGame(new MineProgress{prestigeCount=1,crystals=total,totalCrystals=total});
        for(int i=0;i<MineGame.PrestigeUpgradeCount;i++)
        {
            int coins=game.Progress.crystals;check(game.BuyPrestigeUpgrade(i) && game.Progress.crystals==coins-MineGame.PrestigeUpgradeAt(i).cost && !game.BuyPrestigeUpgrade(i),"Permanent bonus pays its price once: "+i);
            game=new MineGame(ProgressStore.Decode(JsonUtility.ToJson(game.Progress)));
        }
        check(game.Progress.crystals==0 && game.Progress.prestigeUpgrades==(1<<MineGame.PrestigeUpgradeCount)-1,"All eighteen permanent flags survive reload without free crystals");
        check(game.EnchantmentCost(0)==52 && game.EnchantmentDiscountPercent==25 && game.PickLifetime==480 && game.RareOreChance==30 && game.PrestigeExperiencePercent==140,"Tool and mining bonuses change actual prices, lifetime, rare chance and XP");
        game.Progress.pickWear=480;check(game.RepairCost==40,"Permanent workshop cuts a fully worn stone pick repair from eighty to forty coins");
        game.Progress.experience=MineGame.ExperienceForLevel(50);check(game.Prestige(()=>.6) && game.Progress.tickets==12 && game.Progress.enchantingExperience==300 && game.PickLifetime==480,"A reset grants the owned ticket and XP starter bonuses with the durable stone pick");
        var legacy=new MineGame(new MineProgress{version=14,prestigeCount=1,prestigeUpgrades=511,coins=745,tickets=4,enchantments=3,enchantCount=2,enchantingExperience=700});
        check(legacy.Progress.version==15 && legacy.Progress.prestigeUpgrades==511 && legacy.Progress.coins==745 && legacy.Progress.tickets==4 && legacy.Progress.enchantments==3 && legacy.Progress.enchantingExperience==700,"v14 migration keeps owned perks, money and current enchantments until an actual material replacement");
        var normal=new MineGame(new MineProgress{experience=MineGame.ExperienceForLevel(50)});
        var lucky=new MineGame(new MineProgress{experience=MineGame.ExperienceForLevel(50),prestigeCount=1,prestigeUpgrades=(1<<12)|(1<<13)});
        int normalRare=0,luckyRare=0;
        for(int block=0;block<800;block++)
        {
            normal.Progress.oreDamage=normal.OreHealth-1;lucky.Progress.oreDamage=lucky.OreHealth-1;normal.Mine();lucky.Mine();
            if(normal.Progress.oreKind==3||normal.Progress.oreKind==4||normal.Progress.oreKind==8) normalRare++;
            if(lucky.Progress.oreKind==3||lucky.Progress.oreKind==4||lucky.Progress.oreKind==8) luckyRare++;
            if(lucky.IsMiningStone!=normal.IsMiningStone) throw new Exception("Rare boost changed the XP-only stone cadence");
        }
        check(luckyRare>normalRare+40,"Permanent ore luck increases real rare finds while preserving stone cadence");
        var low=new MineGame(new MineProgress{prestigeCount=1,prestigeUpgrades=1<<12});
        for(int block=0;block<30;block++) {low.Progress.oreDamage=low.OreHealth-1;low.Mine();check(low.IsOreUnlocked(low.Progress.oreKind),"Rare bonus cannot grant locked resource "+block);}
        var signatures=new System.Collections.Generic.HashSet<int>();bool withoutEfficiency=false;
        for(int seed=1;seed<=160;seed++)
        {
            var roll=new MineGame(new MineProgress{enchantingExperience=10000});roll.Enchant(2,()=>seed/200d);
            signatures.Add(roll.Progress.enchantments);withoutEfficiency|=roll.EnchantmentLevel(EnchantKind.Efficiency)==0;
            foreach(EnchantKind kind in Enum.GetValues(typeof(EnchantKind))) if(roll.EnchantmentLevel(kind)>MineGame.EnchantmentMax(kind)) throw new Exception("Invalid rolled enchantment level");
        }
        check(signatures.Count>45 && withoutEfficiency,"Hidden enchanting rolls vary effects and levels instead of guaranteeing Efficiency");
        game=new MineGame(new MineProgress{coins=100000,upgradeLevel=1,enchantments=5,enchantingExperience=300});game.BuyUpgrade(UpgradeKind.Pickaxe,5);
        check(game.PickTier==2 && !game.IsEnchanted && game.Progress.enchantingExperience==300,"Bulk material replacements clear enchantments once without spending the XP wallet");
    }

}
