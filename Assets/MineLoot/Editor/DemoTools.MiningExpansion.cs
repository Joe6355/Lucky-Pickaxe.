using System;
using MineLoot;
using UnityEngine;

public static partial class DemoTools
{
    private static void VerifyMiningExpansion(Action<bool, string> check)
    {
        var old = new MineGame(ProgressStore.Decode("{\"version\":11,\"coins\":745,\"tickets\":4,\"oreKind\":0,\"oreDamage\":3,\"upgradeLevel\":20}"));
        check(old.Progress.version == 14 && old.Progress.coins == 745 && old.Progress.tickets == 4 && old.Progress.oreDamage == 24 && old.PickTier == 4, "v11 keeps wallet, diamond tool and half-damaged coal without replaying rewards");
        var reload = new MineGame(ProgressStore.Decode(JsonUtility.ToJson(old.Progress)));
        check(reload.Progress.oreDamage == 24, "v12 reload never rescales damage a second time");
        var game = new MineGame(new MineProgress { experience = MineGame.ExperienceForLevel(50), coins = 1000000, upgradeLevel = 31 });
        int damage = game.MiningDamage;
        check(game.PickTier == 4 && game.NextPickUpgrade == 32 && game.Upgrade() && game.PickTier == 5 && game.MiningDamage > damage, "Netherite is an earned sixth material with a real damage increase");
        game = new MineGame(new MineProgress { experience = MineGame.ExperienceForLevel(50), coins = 1000000 });
        float swing = game.SwingSeconds; int reward = game.OreReward;
        check(game.BuyUpgrade(UpgradeKind.Haste) && game.SwingSeconds < swing && game.MiningDamage == 1, "Haste accelerates holding without inventing damage");
        check(game.BuyUpgrade(UpgradeKind.Fortune) && game.OreReward == reward * 125 / 100, "Fortune improves the block reward by its displayed percentage");
        reload = new MineGame(ProgressStore.Decode(JsonUtility.ToJson(game.Progress)));
        check(reload.Progress.hasteLevel == 1 && reload.Progress.fortuneLevel == 1 && reload.SwingSeconds == game.SwingSeconds && reload.OreReward == game.OreReward, "Both enchantments survive JSON reload");
        const long now = 1800000000;
        game = new MineGame();
        check(!game.ClaimVideoTickets(0) && !game.ClaimVideoTickets(long.MaxValue), "Invalid reward timestamps never pay");
        check(game.CanClaimVideoTickets(now) && game.ClaimVideoTickets(now) && game.Progress.tickets == 12 && game.Progress.coins == 80 && game.Progress.videoTicketClaims == 1, "A verified video pays exactly ten tickets without coins or experience");
        check(!game.ClaimVideoTickets(now) && !game.ClaimVideoTickets(now + 179) && game.VideoWaitSeconds(now + 179) == 1, "Duplicate reward and cooldown boundary cannot pay again");
        reload = new MineGame(ProgressStore.Decode(JsonUtility.ToJson(game.Progress)));
        check(!reload.ClaimVideoTickets(now + 179) && reload.ClaimVideoTickets(now + 180) && reload.Progress.tickets == 22, "Cooldown survives reload and opens at the exact boundary");
        reload.Progress.experience = MineGame.ExperienceForLevel(50);
        long ready = reload.Progress.videoTicketsReadyUtc;
        check(reload.Prestige(() => .5) && reload.Progress.videoTicketsReadyUtc == ready && !reload.CanClaimVideoTickets(now + 180), "Prestige cannot bypass the video cooldown");
        game = new MineGame(new MineProgress { tickets = 1000000 });
        check(!game.ClaimVideoTickets(now) && game.Progress.videoTicketClaims == 0, "Ticket cap rejects the whole gift without consuming its cooldown");
        game = new MineGame(); game.BeginRoll(() => .5);
        check(!game.ClaimVideoTickets(now), "An unresolved paid roulette draw blocks gift mutations");
    }
    private static void VerifyStoneRules(Action<bool,string> check)
    {
        var game=new MineGame(new MineProgress { coins=345,tickets=8,hits=59,experience=39,oreKind=MineGame.StoneKind,veinCharge=23 });
        check(game.IsMiningStone && game.OreKey=="stone" && game.OreReward==0 && game.OreHealth==32,"Ordinary stone has its own stable resource ID and no coin reward");
        game.Mine();
        check(game.Progress.experience==41 && game.Progress.enchantingExperience==2 && game.MinerLevel==2 && game.LastHit.levels==1 && game.LastHit.coins==0 && game.LastHit.tickets==0,"Level reached through stone awards experience only");
        check(game.Progress.coins==345 && game.Progress.tickets==8 && game.Progress.hits==59 && game.Progress.veinCharge==23,"Stone hit cannot pay coins, advance a ticket or charge a rich vein");
        game.Progress.oreDamage=31; game.Mine();
        check(game.LastHit.broken && game.LastHit.experience==18 && game.Progress.experience==59 && game.Progress.enchantingExperience==20 && game.Progress.miningBlocks==1 && game.Progress.oresMined==0,"Stone break pays its listed XP once and advances the separate all-block counter");
        check(game.Progress.coins==345 && game.Progress.tickets==8 && game.Progress.hits==59 && game.Progress.veinCharge==23 && !game.VeinOffered && !game.IsMiningStone,"Stone break cannot pay currency, discover a vein or trap the next block on stone");
        game=new MineGame(new MineProgress {coins=345,tickets=8,hits=59,experience=MineGame.ExperienceForLevel(5)-1,oreKind=MineGame.StoneKind,oreDamage=31,veinBoostLeft=12,fortuneLevel=10,enchantments=3<<8,prestigeUpgrades=1});
        game.Mine();
        check(game.MinerLevel==5 && game.LastHit.coins==0 && game.LastHit.tickets==0 && game.LastHit.experience==18 && !game.LastHit.rich && game.Progress.coins==345 && game.Progress.tickets==8 && game.Progress.hits==59,"Rich vein, Fortune and permanent income bonuses cannot turn stone or its fifth-level threshold into currency");
        game=new MineGame(new MineProgress { experience=MineGame.ExperienceForLevel(50) });
        for(int block=0;block<16;block++)
        {
            check(game.IsMiningStone==(block%4==3),"One ordinary stone follows every three ore blocks: "+block);
            game.Progress.oreDamage=game.OreHealth-1; game.Mine();
            game=new MineGame(ProgressStore.Decode(JsonUtility.ToJson(game.Progress)));
        }
        check(game.Progress.miningBlocks==16 && game.Progress.oresMined==12,"Reload preserves all-block cadence without counting stone as valuable ore");
        var old=new MineGame(new MineProgress {version=13,coins=987,tickets=12,hits=37,oresMined=17,oreKind=3,oreDamage=20,enchantments=4,enchantingExperience=123,experience=MineGame.ExperienceForLevel(20)});
        check(old.Progress.version==14 && old.Progress.miningBlocks==17 && old.Progress.oreKind==3 && old.Progress.oreDamage==20 && old.Progress.coins==987 && old.Progress.tickets==12 && old.Progress.hits==37 && old.Progress.enchantments==4 && old.Progress.enchantingExperience==123,"v13 migration preserves resource IDs, partial damage, currency and owned enchantments");
        old.BeginRoll(()=>.6); var restored=new MineGame(ProgressStore.Decode(JsonUtility.ToJson(old.Progress)));
        check(restored.HasPendingRoll && restored.Progress.tickets==11 && restored.Progress.miningBlocks==17 && restored.Progress.pendingTypes[0]==old.Progress.pendingTypes[0],"v14 round-trip preserves the already paid roulette choice alongside stone progression");
    }

}
