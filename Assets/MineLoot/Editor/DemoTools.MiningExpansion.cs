using System;
using MineLoot;
using UnityEngine;

public static partial class DemoTools
{
    private static void VerifyMiningExpansion(Action<bool, string> check)
    {
        var old = new MineGame(ProgressStore.Decode("{\"version\":11,\"coins\":745,\"tickets\":4,\"oreKind\":0,\"oreDamage\":3,\"upgradeLevel\":20}"));
        check(old.Progress.version == 13 && old.Progress.coins == 745 && old.Progress.tickets == 4 && old.Progress.oreDamage == 24 && old.PickTier == 4, "v11 keeps wallet, diamond tool and half-damaged coal without replaying rewards");
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
        check(game.CanClaimVideoTickets(now) && game.ClaimVideoTickets(now) && game.Progress.tickets == 3 && game.Progress.coins == 80 && game.Progress.videoTicketClaims == 1, "A verified video pays exactly three tickets without coins or experience");
        check(!game.ClaimVideoTickets(now) && !game.ClaimVideoTickets(now + 179) && game.VideoWaitSeconds(now + 179) == 1, "Duplicate reward and cooldown boundary cannot pay again");
        reload = new MineGame(ProgressStore.Decode(JsonUtility.ToJson(game.Progress)));
        check(!reload.ClaimVideoTickets(now + 179) && reload.ClaimVideoTickets(now + 180) && reload.Progress.tickets == 4, "Cooldown survives reload and opens at the exact boundary");
        reload.Progress.experience = MineGame.ExperienceForLevel(50);
        long ready = reload.Progress.videoTicketsReadyUtc;
        check(reload.Prestige(() => .5) && reload.Progress.videoTicketsReadyUtc == ready && !reload.CanClaimVideoTickets(now + 180), "Prestige cannot bypass the video cooldown");
        game = new MineGame(new MineProgress { tickets = 1000000 });
        check(!game.ClaimVideoTickets(now) && game.Progress.videoTicketClaims == 0, "Ticket cap rejects the whole gift without consuming its cooldown");
        game = new MineGame(); game.BeginRoll(() => .5);
        check(!game.ClaimVideoTickets(now), "An unresolved paid roulette draw blocks gift mutations");
    }
}
