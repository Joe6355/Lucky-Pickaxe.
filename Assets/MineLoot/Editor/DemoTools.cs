using System;
using System.IO;
using MineLoot;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static partial class DemoTools
{
    private const string ScenePath = "Assets/MineLoot/Scenes/Demo.unity";

    [MenuItem("Mine Loot/Prepare demo scene")]
    public static void Prepare()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode first.");
        Directory.CreateDirectory("Assets/MineLoot/Scenes");
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var camera = new GameObject("Main Camera", typeof(Camera));
        camera.tag = "MainCamera";
        var cam = camera.GetComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(.067f, .094f, .14f);
        cam.orthographic = true; camera.transform.position = new Vector3(0, 0, -10);
        new GameObject("Mine Loot Demo", typeof(DemoController));
        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        PlayerSettings.companyName = "MineLootDemo";
        // Keep the existing company/application identifiers so installed v0.1 saves remain reachable.
        PlayerSettings.productName = "Mine Loot";
        PlayerSettings.bundleVersion = "0.17.0";
        PlayerSettings.defaultScreenWidth = 480; PlayerSettings.defaultScreenHeight = 960;
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        PlayerSettings.resizableWindow = true;
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
        PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, "com.mineloot.demo");
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel23;
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
        PlayerSettings.WebGL.decompressionFallback = false;
        PlayerSettings.runInBackground = false;
        AssetDatabase.SaveAssets();
    }

    [MenuItem("Mine Loot/Verify game rules")]
    public static void VerifyRules()
    {
        int assertions = 0;
        void Check(bool condition, string description)
        {
            assertions++;
            if (!condition) throw new Exception("Rule check failed: " + description);
        }
        var game = new MineGame();
        for (int i = 0; i < 11; i++) Check(!game.Mine(), "No premature ticket");
        Check(game.Mine() && game.Progress.tickets == 3 && game.Progress.hits == 0 && game.Progress.coins == 200, "Twelve hits grant one ticket and two coal rewards");
        Check(game.Upgrade() && game.ClickPower == 12 && game.UpgradeCost == 154 && game.Progress.coins == 80, "Upgrade transaction");
        Check(!new MineGame(new MineProgress { coins = 0 }).Upgrade(), "Insufficient coins");
        Check(MineGame.PickPower(0) == 1 && MineGame.PickPower(.54999) == 1 && MineGame.PickPower(.55) == 2 && MineGame.PickPower(.89999) == 2 && MineGame.PickPower(.9) == 4, "Rarity boundaries");
        game = new MineGame(new MineProgress { tickets = 7, coins = 0 });
        for (int i = 0; i < 5; i++)
        {
            var result = game.Roll(() => 0);
            Check(result.reward == (i == 4 ? 430 : 30), "Block, chest and floor rewards");
            foreach (int depth in game.Progress.depths) Check(depth == i + 1, "Depth persists between rolls");
        }
        Check(game.Progress.coins == 550 && game.IsComplete && game.Progress.tickets == 3, "Floor paid once and ticket returned");
        Check(game.CanClaim && game.ClaimContract() && !game.ClaimContract(), "Contract cannot pay twice");
        Check(game.Progress.coins == 650 && game.Progress.tickets == 4 && game.ContractTarget == 50, "Contract advances");
        var next = game.Roll(() => .99);
        Check(next.newStratum && game.Progress.stratum == 2 && game.Progress.boardSeed > 0 && next.reward > 0 && !game.IsComplete, "Next stratum generates a saved risk layout");
        game = new MineGame(new MineProgress { depths = new[] { 5, 0, 0, 0, 0 }, coins = 0 });
        var scrap = game.Roll(() => 0);
        Check(scrap.reward == 24 && scrap.powers[0] == 0 && scrap.chests == 0, "Completed columns never pay consolation coins");
        game = new MineGame(new MineProgress { tickets = 0 });
        Check(game.Roll(() => 0) == null && game.Progress.coins == 80, "Zero tickets cannot spin");
        game = new MineGame(new MineProgress { coins = -2, tickets = -2, hits = 99, upgradeLevel = 500, stratum = -1, depths = null });
        Check(game.Progress.coins == 0 && game.Progress.tickets == 0 && game.Progress.hits == 11 && game.Progress.upgradeLevel == 100 && game.Progress.stratum == 1 && game.Progress.depths.Length == 5, "Save sanitization");
        Check(!game.Upgrade(), "Upgrade cap");
        game = new MineGame(); game.Roll(() => .6); game.Mine();
        var restored = new MineGame(JsonUtility.FromJson<MineProgress>(JsonUtility.ToJson(game.Progress)));
        Check(restored.Progress.coins == game.Progress.coins && restored.Progress.tickets == game.Progress.tickets && restored.Progress.depths[0] == 2 && restored.Progress.hits == 1, "Save round trip");
        try { game.Roll(() => throw new Exception("Injected RNG failure")); } catch { }
        Check(game.Progress.tickets == restored.Progress.tickets, "Failed draw cannot spend ticket");
        try { game.Roll(() => double.NaN); } catch (ArgumentOutOfRangeException) { }
        Check(game.Progress.tickets == restored.Progress.tickets, "Invalid random sample is atomic");
        game = new MineGame(new MineProgress { stratum = 4, coins = 1000 });
        var hit = game.Roll(() => 0);
        Check(hit.broken == 0 && game.Progress.damage[0] == 1 && game.Hardness == 2, "Hard rock keeps partial damage");
        restored = new MineGame(ProgressStore.Decode(JsonUtility.ToJson(game.Progress)));
        Check(restored.Progress.damage[0] == 1 && restored.Roll(() => 0).broken == 5, "Partial damage survives save reload");
        Check(game.Forge() && game.Progress.forgeLevel == 1 && game.Progress.coins == 820 && game.DiamondChance == 11, "Forge cost and rarity");
        Check(game.Forge() && game.PowerBonus == 1 && MineGame.PickPower(.52, 2) == 2, "Forge changes power and boundaries");
        game = new MineGame(new MineProgress { stratum = 3, depths = new[] { 4, 4, 4, 4, 4 } });
        game.Roll(() => 0);
        Check(game.Relics == 1 && game.ClickPower == 9, "Three floors grant permanent relic");
        var legacy = new MineGame(ProgressStore.Decode("{\"version\":1,\"coins\":1240,\"tickets\":3,\"hits\":4,\"upgradeLevel\":2,\"stratum\":4,\"depths\":[1,2,3,4,5]}"));
        Check(legacy.Progress.version == 11 && legacy.Progress.boardSeed == 0 && legacy.Progress.coins == 1240 && legacy.Progress.tickets == 3 && legacy.Progress.upgradeLevel == 2 && legacy.Progress.depths[4] == 5, "v1 migration preserves progress without inserting hazards");
        game = new MineGame();
        for (int i = 0; i < 5; i++) game.Mine();
        Check(game.Progress.oreDamage == 5 && game.Progress.oresMined == 0 && game.Progress.experience == 5, "Ore accumulates damage and click experience");
        game.Mine();
        Check(game.LastHit.broken && game.LastHit.coins == 20 && game.LastHit.experience == 7 && game.Progress.coins == 140 && game.Progress.experience == 12, "Coal break pays exactly once");
        Check(game.Progress.oreDamage == 0 && game.Progress.oresMined == 1, "New block starts intact");
        game = new MineGame(new MineProgress { coins = 0, experience = 39 }); game.Mine();
        Check(game.MinerLevel == 2 && game.Progress.coins == 88 && game.Progress.tickets == 3 && game.UnlockedOres == 2, "Level unlocks iron, grants coins and ticket");
        game.Mine();
        Check(game.Progress.coins == 96 && game.Progress.tickets == 3 && game.LastHit.levels == 0, "Level reward cannot repeat");
        for (int kind = 0; kind < 5; kind++)
        {
            game = new MineGame(new MineProgress { experience = MineGame.ExperienceForLevel(MineGame.OreUnlockLevel(kind)), oreKind = kind });
            Check(game.UnlockedOres == kind + 1 && game.OreKey == MineGame.OreKeyAt(kind), "Ore unlock boundary " + kind);
            if (kind > 0)
            {
                game = new MineGame(new MineProgress { experience = MineGame.ExperienceForLevel(MineGame.OreUnlockLevel(kind)) - 1, oreKind = kind });
                Check(game.UnlockedOres == kind && game.Progress.oreKind < kind, "Locked ore rejected " + kind);
            }
        }
        int[] tiers = { 0, 2, 5, 12, 20 };
        for (int i = 0; i < tiers.Length; i++) Check(new MineGame(new MineProgress { upgradeLevel = tiers[i] }).PickTier == i, "Pick material milestone " + i);
        game = new MineGame(new MineProgress { coins = 10000, upgradeLevel = 4 });
        Check(game.MiningDamage == 1 && game.Upgrade() && game.MiningDamage == 2, "Fifth upgrade increases ore damage");
        for (int i = 0; i < 3; i++) game.Mine();
        Check(game.Progress.oresMined == 1 && game.Progress.oreDamage == 0, "Stronger pick breaks coal in three hits");
        game = new MineGame(new MineProgress { upgradeLevel = 100, experience = long.MaxValue, oreKind = 4, oreDamage = int.MaxValue });
        Check(game.MinerLevel == 50 && game.LevelGoal == 0 && game.Progress.oreDamage == 19, "Level and ore damage caps");
        game.Mine();
        Check(game.Progress.oresMined == 1 && game.LastHit.levels == 0 && game.LastHit.coins == game.ClickPower + 150, "Capped level has no repeated rewards");
        game = new MineGame(new MineProgress { experience = 200, oreKind = 2, oreDamage = 8, oresMined = 17 });
        restored = new MineGame(ProgressStore.Decode(JsonUtility.ToJson(game.Progress)));
        Check(restored.Progress.oreKind == 2 && restored.Progress.oreDamage == 8 && restored.Progress.experience == 200 && restored.Progress.oresMined == 17, "v3 restores ore damage and experience");
        var old = new MineGame(ProgressStore.Decode("{\"version\":2,\"coins\":543,\"tickets\":8,\"totalClicks\":180,\"upgradeLevel\":6}"));
        Check(old.MinerLevel == 4 && old.Progress.coins == 543 && old.Progress.tickets == 8 && old.Progress.upgradeLevel == 6, "v2 clicks migrate without paying historical rewards");
        var sequenceA = new MineGame(new MineProgress { experience = 1080 });
        var sequenceB = new MineGame(new MineProgress { experience = 1080 });
        bool[] seen = new bool[5];
        for (int i = 0; i < 180; i++)
        {
            sequenceA.Mine(); sequenceB.Mine(); seen[sequenceA.Progress.oreKind] = true;
            if (sequenceA.Progress.oreKind != sequenceB.Progress.oreKind) throw new Exception("Non-deterministic ore cycle");
        }
        Check(Array.TrueForAll(seen, value => value), "Unlocked cycle includes every ore");
        string testKey = "LuckyPickaxe.Tests." + Guid.NewGuid().ToString("N");
        try
        {
            ProgressStore.Save(legacy, testKey);
            PlayerPrefs.SetString(testKey, "not-json");
            var backup = ProgressStore.Load(testKey);
            Check(backup.Progress.coins == 1240 && backup.Progress.depths[1] == 2, "Corrupt primary recovers backup");
        }
        finally { PlayerPrefs.DeleteKey(testKey); PlayerPrefs.DeleteKey(testKey + ".backup"); PlayerPrefs.Save(); }
        VerifyRiskRules(Check);
        VerifyExchangeRules(Check);
        VerifyUpgradeRules(Check);
        VerifyMilestoneRules(Check);
        VerifyProductionRules(Check);
        VerifyOfflineRules(Check);
        VerifyRewardedOfflineRules(Check);
        VerifyPrestigeRules(Check);
        VerifyGuideRules(Check);
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/rules-check.json", "{\"result\":\"PASS\",\"assertions\":" + assertions + ",\"unity\":\"" + Application.unityVersion + "\"}");
        Debug.Log("MINE_LOOT_RULES: PASS (" + assertions + " assertions)");
    }

    private static void VerifyGuideRules(Action<bool, string> check)
    {
        var game = new MineGame();
        check(game.GuideActive && game.Progress.guideStep == 0 && !game.AdvanceGuide() && game.Progress.coins == 80 && game.Progress.tickets == 2, "Fresh guide neither grants rewards nor completes without input");
        for (int i = 0; i < 5; i++) game.Mine();
        check(!game.AdvanceGuide() && game.Progress.guideStep == 0, "First block hint remains through five manual hits");
        game.Mine();
        check(game.AdvanceGuide() && game.Progress.guideStep == 1 && game.Progress.coins == 140, "Actual ore break advances the guide without a second reward");
        check(game.Upgrade() && game.AdvanceGuide() && game.Progress.guideStep == 2 && game.Progress.coins == 20, "Guide purchase uses the regular price and upgrade");
        game = new MineGame(ProgressStore.Decode(JsonUtility.ToJson(game.Progress)));
        check(game.Progress.guideStep == 2 && game.Progress.coins == 20 && game.Progress.upgradeLevel == 1, "Guide checkpoint and paid upgrade survive JSON reload");
        game.BeginRoll(() => 0);
        string pending = JsonUtility.ToJson(game.Progress);
        check(!game.AdvanceGuide() && !game.SkipGuide() && !game.StartGuide() && JsonUtility.ToJson(game.Progress) == pending, "A paid unresolved roll blocks all guide mutations");
        game.ResolveRoll(); game.AdvanceGuide();
        check(game.Progress.guideStep == 3 && game.Progress.totalRolls == 1 && game.Progress.tickets >= 1, "Only resolving the first roll opens the worker hint");
        game.Progress.coins = 300; game.Progress.experience = MineGame.ExperienceForLevel(2);
        check(game.BuyUpgrade(UpgradeKind.Worker) && game.AdvanceGuide() && game.Progress.guideStep == 4 && game.Progress.coins == 0 && game.ProductionMilli == 2000, "Worker hint uses the existing production purchase");
        game.Progress.totalClicks = 30;
        check(game.ClaimMilestone(0) && game.AdvanceGuide() && !game.GuideActive && game.Progress.coins == 120 && game.Progress.milestoneClaims == 1, "Claiming a real book reward completes the guide without extra money");
        string complete = JsonUtility.ToJson(game.Progress);
        check(!game.StartGuide() && JsonUtility.ToJson(game.Progress) == complete, "Already completed actions cannot be replayed for additional rewards");
        game = new MineGame();
        check(game.SkipGuide() && !game.SkipGuide() && game.Progress.coins == 80 && game.Progress.tickets == 2 && game.Progress.totalClicks == 0, "Skipping is idempotent and leaves gameplay progress untouched");
        game = new MineGame(ProgressStore.Decode(JsonUtility.ToJson(game.Progress)));
        check(!game.GuideActive && game.StartGuide() && game.Progress.guideStep == 0 && game.Progress.coins == 80, "Skipped hints only restart after an explicit help action");
        game = new MineGame(new MineProgress { guideStep = -100 });
        check(game.Progress.guideStep == 0, "Negative guide checkpoint clamps to the first hint");
        game = new MineGame(new MineProgress { guideStep = int.MaxValue });
        check(!game.GuideActive && game.Progress.guideStep == MineGame.GuideStepCount, "Unknown guide checkpoint cannot index beyond the final step");
        game = new MineGame(new MineProgress { oresMined = 1, workerCount = 1, totalRolls = 1, milestoneClaims = 1 });
        check(game.Progress.guideStep == 1, "Actions performed early wait at the next unfinished guide step");
        game.Progress.coins = 120; game.Upgrade(); game.AdvanceGuide();
        check(!game.GuideActive, "Previously completed later actions skip without repeating a roll or worker purchase");
        for (int version = 1; version <= 10; version++)
        {
            var old = new MineGame(ProgressStore.Decode("{\"version\":" + version + ",\"coins\":745,\"tickets\":4}"));
            check(old.Progress.version == 11 && !old.GuideActive && old.Progress.coins == 745 && old.Progress.tickets == 4, "Existing v" + version + " save opts out without changing its wallet");
        }
        game = new MineGame(new MineProgress { experience = MineGame.ExperienceForLevel(50) });
        check(game.Prestige(() => .5) && !game.GuideActive, "Prestige never restarts first-session hints");
        string key = "LuckyPickaxe.Guide.Tests." + Guid.NewGuid().ToString("N");
        try
        {
            game = new MineGame(); game.SkipGuide(); ProgressStore.Save(game, key);
            PlayerPrefs.SetString(key, "not-json"); var backup = ProgressStore.Load(key);
            check(!backup.GuideActive && backup.Progress.coins == 80 && backup.Progress.tickets == 2, "Backup recovery retains a skipped guide without touching game progress");
        }
        finally { PlayerPrefs.DeleteKey(key); PlayerPrefs.DeleteKey(key + ".backup"); PlayerPrefs.Save(); }
    }

    private static void VerifyRiskRules(Action<bool, string> check)
    {
        var game = new MineGame(new MineProgress { boardSeed = 236, coins = 0, tickets = 10 });
        for (int c = 0; c < 5; c++)
            for (int row = 1; row < 5; row++) if (game.BlockAt(c, row) == BlockKind.Obsidian) game.Progress.depths[c] = row;
        var miss = game.Roll(() => 0);
        check(miss.reward == 0 && miss.broken == 0 && miss.brokenPicks == 5 && game.Progress.tickets == 9, "All wooden picks can fail and lose the ticket");
        for (int c = 0; c < 5; c++) check(miss.after[c] == miss.before[c] && miss.damageAfter[c] == 0 && !miss.lostAfter[c], "Obsidian rejects wood without damaging or losing the lane");
        game.Progress.forgeLevel = 10;
        var iron = game.Roll(() => .6);
        check(iron.reward == 0 && iron.brokenPicks == 5, "Forged iron cannot bypass obsidian material requirement");
        var diamond = game.Roll(() => .99);
        check(diamond.broken >= 5 && diamond.brokenPicks == 0 && diamond.reward > 0, "Diamond picks break obsidian");
        game = new MineGame(new MineProgress { boardSeed = 1758, coins = 0, tickets = 3 });
        for (int c = 0; c < 5; c++) for (int r = 1; r < 5; r++) if (game.BlockAt(c, r) == BlockKind.Bedrock) game.Progress.depths[c] = r;
        var rock = game.Roll(() => .99);
        check(rock.reward == 0 && rock.lostChests == 5 && rock.completed && !rock.perfect && game.IsComplete, "Even diamonds fail on bedrock and lose all chests");
        check(game.Progress.tickets == 2 && game.Progress.failedFloors == 1 && game.Relics == 0, "Lost floor has no refund, floor bonus or relic");
        var restored = new MineGame(ProgressStore.Decode(JsonUtility.ToJson(game.Progress)));
        check(restored.IsComplete && restored.Progress.boardSeed == 1758 && Array.TrueForAll(restored.Progress.lostColumns, value => value), "Bedrock losses persist across reload");
        string before = JsonUtility.ToJson(restored.Progress); int draws = 0;
        try { restored.Roll(() => ++draws == 6 ? double.NaN : .6); } catch (ArgumentOutOfRangeException) { }
        check(JsonUtility.ToJson(restored.Progress) == before, "Invalid next-floor seed cannot mutate completed progress");
        restored.Roll(() => .6);
        check(restored.Progress.stratum == 2 && restored.Progress.failedFloors == 1, "Failed floor advances once without farming relics");
        game = new MineGame(new MineProgress { boardSeed = 236, coins = 42, tickets = 4, stratum = 4 });
        before = JsonUtility.ToJson(game.Progress);
        try { game.Abandon(() => throw new Exception("Injected seed failure")); } catch { }
        check(JsonUtility.ToJson(game.Progress) == before, "Abandon failure is atomic");
        game.Progress.depths[0] = 1; game.Progress.damage[0] = 1;
        check(game.Abandon(() => .5) && game.Progress.tickets == 3 && game.Progress.coins == 42 && game.Progress.stratum == 4 && game.Progress.failedFloors == 0, "Changing shaft costs one ticket and cannot raise reward tier or relics");
        check(Array.TrueForAll(game.Progress.depths, value => value == 0) && Array.TrueForAll(game.Progress.damage, value => value == 0), "Changing shaft resets partial progress");
        check(!new MineGame(new MineProgress { tickets = 0 }).Abandon(() => .5), "No free rerolls without tickets");
        var old = new MineGame(ProgressStore.Decode("{\"version\":3,\"coins\":123,\"tickets\":7,\"stratum\":4,\"depths\":[1,2,3,4,5],\"damage\":[1,0,1,0,0],\"experience\":200,\"oreKind\":2,\"oreDamage\":5}"));
        check(old.Progress.boardSeed == 0 && old.Progress.depths[3] == 4 && old.Progress.damage[2] == 1 && old.Progress.coins == 123 && old.Progress.experience == 200 && old.Progress.oreDamage == 5, "v3 migration preserves damaged shaft and clicker ore");
        for (int c = 0; c < 5; c++) for (int r = 0; r < 5; r++) check(old.BlockAt(c, r) == BlockKind.Ore, "Legacy shaft remains free of inserted hazards");
        game = new MineGame(new MineProgress { boardSeed = 17, stratum = 4, forgeLevel = 0, tickets = 10 });
        int lane = -1, layer = -1;
        for (int c = 0; c < 5 && lane < 0; c++) for (int r = 1; r < 5; r++) if (game.BlockAt(c, r) == BlockKind.Obsidian) { lane = c; layer = r; break; }
        check(lane >= 0, "Partial obsidian fixture exists");
        for (int c = 0; c < 5; c++) game.Progress.depths[c] = c == lane ? layer : 5;
        game.Progress.damage[lane] = 1;
        restored = new MineGame(ProgressStore.Decode(JsonUtility.ToJson(game.Progress)));
        var partial = restored.Roll(() => .99);
        check(partial.damageBefore[lane] == 1 && partial.after[lane] > partial.before[lane], "Obsidian partial damage survives and diamond finishes it");
        int bedrock = 0, obsidian = 0, clear = 0;
        for (int seed = 1; seed <= 1000; seed++)
        {
            var board = new MineGame(new MineProgress { boardSeed = seed });
            for (int c = 0; c < 5; c++)
            {
                int special = 0;
                for (int r = 0; r < 5; r++) { var kind = board.BlockAt(c, r); if (kind == BlockKind.Bedrock) { bedrock++; special++; } if (kind == BlockKind.Obsidian) { obsidian++; special++; } }
                if (special > 1) throw new Exception("More than one obstacle per lane");
                if (special == 0) clear++;
            }
        }
        check(bedrock > 750 && bedrock < 1050 && obsidian > 1300 && obsidian < 1700 && clear > 2300 && clear < 2900, "Risk layout distribution stays close to advertised 18/30/52");
    }

    private static void VerifyExchangeRules(Action<bool, string> check)
    {
        var game = new MineGame(new MineProgress { swapLevel = 1, tickets = 1, coins = 1000, totalBlocks = 25, forgeLevel = 2 });
        int draws = 0;
        check(game.BeginRoll(() => ++draws == 2 ? .99 : 0), "Paid draw enters the choice phase");
        check(draws == 5 && game.HasPendingRoll && game.Progress.tickets == 0 && game.Progress.totalRolls == 1, "Draw spends one ticket even when it was the last one");
        check(game.Progress.coins == 1000 && game.Progress.totalBlocks == 25 && game.Progress.experience == 0 && Array.TrueForAll(game.Progress.depths, d => d == 0), "No damage or reward before confirming picks");
        check(game.Progress.pendingTypes[0] == 1 && game.Progress.pendingTypes[1] == 4 && game.PendingPower(0) == 2 && game.PendingPower(1) == 5, "Pending draw stores material and includes the forge bonus");
        string pending = JsonUtility.ToJson(game.Progress);
        check(!game.BeginRoll(() => throw new Exception("Must not draw again")) && game.Roll(null) == null, "A pending paid draw cannot be replaced");
        check(!game.Upgrade() && !game.Forge() && !game.ClaimContract() && !game.Abandon(() => throw new Exception("Must not reroute")), "Other economy actions cannot alter the pending board");
        game.Mine();
        check(JsonUtility.ToJson(game.Progress) == pending, "Mining cannot change a pending transaction");
        check(!game.SwapPicks(-1, 1) && !game.SwapPicks(0, 5) && !game.SwapPicks(0, 0) && !game.SwapPicks(0, 2) && game.Progress.pendingSwapCount == 0, "Invalid and identical picks never consume the exchange");
        var restored = new MineGame(ProgressStore.Decode(pending));
        check(restored.HasPendingRoll && restored.Progress.tickets == 0 && restored.CanSwap && restored.PendingPower(1) == 5, "Reload resumes the same picks without needing another ticket");
        check(restored.SwapPicks(0, 1) && restored.Progress.pendingTypes[0] == 4 && restored.Progress.pendingTypes[1] == 1 && restored.PendingPower(0) == 5, "One exchange moves the material and full power together");
        check(restored.Progress.coins == 1000 && restored.Progress.tickets == 0 && restored.Progress.totalRolls == 1 && !restored.CanSwap, "Exchange is free and is limited to one per draw");
        restored = new MineGame(ProgressStore.Decode(JsonUtility.ToJson(restored.Progress)));
        check(restored.Progress.pendingSwapCount == 1 && !restored.SwapPicks(0, 2) && restored.Progress.pendingTypes[0] == 4, "Restart cannot undo or renew the used exchange");
        var resolved = restored.ResolveRoll();
        check(resolved != null && resolved.types[0] == 4 && resolved.powers[0] == 5 && resolved.powers[1] == 2 && !restored.HasPendingRoll && restored.Progress.tickets == 0, "Confirm resolves the arranged picks without charging another ticket");
        long coins = restored.Progress.coins;
        check(restored.ResolveRoll() == null && !restored.SwapPicks(0, 1) && restored.Progress.coins == coins, "Confirm and exchange cannot repeat a completed reward");
        restored = new MineGame(ProgressStore.Decode(JsonUtility.ToJson(restored.Progress)));
        check(!restored.HasPendingRoll && restored.ResolveRoll() == null && restored.Progress.coins == coins, "Reload after reward does not pay twice");

        game = new MineGame(new MineProgress { swapLevel = 1, boardSeed = 236, tickets = 2, coins = 0 });
        for (int c = 0; c < 5; c++) for (int row = 1; row < 5; row++) if (game.BlockAt(c, row) == BlockKind.Obsidian) game.Progress.depths[c] = row;
        draws = 0; game.BeginRoll(() => ++draws == 2 ? .99 : 0);
        check(game.SwapPicks(0, 1), "Diamond can be assigned to a revealed obsidian lane");
        resolved = game.ResolveRoll();
        check(resolved.blockedBy[0] == BlockKind.Ore && resolved.after[0] > resolved.before[0] && resolved.blockedBy[1] == BlockKind.Obsidian && resolved.brokenPicks == 4, "The arranged material determines obsidian damage and pick breakage");

        game = new MineGame(new MineProgress { swapLevel = 1, boardSeed = 1758, tickets = 2 });
        game.Progress.depths[0] = 5;
        for (int r = 1; r < 5; r++) if (game.BlockAt(1, r) == BlockKind.Bedrock) game.Progress.depths[1] = r;
        game.Progress.lostColumns[1] = true;
        draws = 0; game.BeginRoll(() => ++draws == 4 ? .99 : 0);
        check(game.Progress.pendingTypes[0] == 0 && game.Progress.pendingTypes[1] == 0 && game.PendingPower(0) == 0 && game.PendingPower(1) == 0, "Closed and lost lanes have no exchangeable pick");
        check(!game.SwapPicks(0, 3) && !game.SwapPicks(1, 3) && game.SwapPicks(2, 3), "Exchange is restricted to two active lanes");
        resolved = game.ResolveRoll();
        check(resolved.powers[0] == 0 && resolved.powers[1] == 0 && resolved.reward >= 0 && game.Progress.lostColumns[1], "Exchanging cannot restore a bedrock chest");
        game = new MineGame(new MineProgress { swapLevel = 1, boardSeed = 1758 });
        for (int c = 0; c < 5; c++) for (int r = 1; r < 5; r++) if (game.BlockAt(c, r) == BlockKind.Bedrock) game.Progress.depths[c] = r;
        draws = 0; game.BeginRoll(() => ++draws == 2 ? .99 : 0); game.SwapPicks(0, 1); resolved = game.ResolveRoll();
        check(resolved.lostChests == 5 && resolved.reward == 0 && !resolved.perfect, "Exchanged diamonds still cannot break bedrock");

        game = new MineGame(new MineProgress { swapLevel = 1, tickets = 1 }); game.BeginRoll(() => 0);
        check(!game.CanSwap && game.ResolveRoll().broken == 5, "Identical picks can be confirmed without an exchange");
        game = new MineGame(new MineProgress { swapLevel = 1, tickets = 1, depths = new[] { 5, 5, 5, 5, 0 } }); game.BeginRoll(() => .99);
        check(!game.CanSwap && game.PendingPower(4) == 4 && game.ResolveRoll().after[4] == 4, "Single remaining lane remains playable");
        game = new MineGame(new MineProgress { swapLevel = 1, tickets = 1, depths = new[] { 5, 5, 5, 5, 5 } });
        pending = JsonUtility.ToJson(game.Progress); draws = 0;
        try { game.BeginRoll(() => ++draws == 6 ? double.NaN : .6); } catch (ArgumentOutOfRangeException) { }
        check(JsonUtility.ToJson(game.Progress) == pending, "Failed next-floor draw is atomic before the choice phase");
        game.BeginRoll(() => .6); int seed = game.Progress.boardSeed;
        restored = new MineGame(ProgressStore.Decode(JsonUtility.ToJson(game.Progress)));
        check(restored.Progress.stratum == 2 && restored.Progress.pendingNewStratum && restored.Progress.boardSeed == seed && restored.Progress.tickets == 0, "New-floor seed and spent last ticket survive pending reload");
        check(restored.ResolveRoll().newStratum && restored.Progress.stratum == 2, "Restoring a pending new floor cannot advance twice");
        var v4 = new MineGame(ProgressStore.Decode("{\"version\":4,\"boardSeed\":17,\"tickets\":7,\"coins\":123,\"depths\":[1,2,2,3,4],\"damage\":[0,0,0,1,0],\"lostColumns\":[false,false,true,false,false]}"));
        check(v4.Progress.version == 11 && !v4.HasPendingRoll && v4.Progress.boardSeed == 17 && v4.Progress.lostColumns[2] && v4.Progress.damage[3] == 1 && v4.Progress.coins == 123 && v4.Progress.tickets == 7, "v4 migration preserves hazards, losses and partial damage");
        string key = "LuckyPickaxe.Exchange.Tests." + Guid.NewGuid().ToString("N");
        try
        {
            game = new MineGame(new MineProgress { swapLevel = 1, tickets = 1 }); draws = 0;
            game.BeginRoll(() => ++draws == 2 ? .99 : 0); ProgressStore.Save(game, key);
            restored = ProgressStore.Load(key);
            check(restored.HasPendingRoll && restored.Progress.tickets == 0 && restored.Progress.pendingTypes[1] == 4, "PlayerPrefs restores the paid choice phase");
            restored.SwapPicks(0, 1); ProgressStore.Save(restored, key);
            restored = ProgressStore.Load(key);
            check(restored.Progress.pendingSwapCount == 1 && restored.Progress.pendingTypes[0] == 4 && !restored.SwapPicks(0, 2), "PlayerPrefs persists the used exchange");
            game.Progress.pendingTypes[0] = 3; PlayerPrefs.SetString(key, JsonUtility.ToJson(game.Progress));
            restored = ProgressStore.Load(key);
            check(restored.Progress.pendingTypes[0] == 4 && restored.Progress.pendingSwapCount == 1, "Malformed pending primary recovers the fixed draw from backup");
            restored.ResolveRoll(); coins = restored.Progress.coins; ProgressStore.Save(restored, key);
            restored = ProgressStore.Load(key);
            check(!restored.HasPendingRoll && restored.ResolveRoll() == null && restored.Progress.coins == coins, "PlayerPrefs persists the final reward once");
        }
        finally { PlayerPrefs.DeleteKey(key); PlayerPrefs.DeleteKey(key + ".backup"); PlayerPrefs.Save(); }
    }

    private static void VerifyUpgradeRules(Action<bool, string> check)
    {
        var game = new MineGame(); int draws = 0;
        game.BeginRoll(() => ++draws == 2 ? .99 : 0);
        check(game.Progress.swapLevel == 0 && game.Progress.pendingSwapLimit == 0 && !game.CanSwap && !game.SwapPicks(0, 1), "New players cannot exchange picks before buying the upgrade");
        check(game.ResolveRoll().broken > 0 && !game.HasPendingRoll, "Locked exchange does not prevent normal mining");
        game = new MineGame(new MineProgress { coins = 10000 });
        check(!game.BuyUpgrade(UpgradeKind.Swap) && game.Progress.coins == 10000, "Exchange purchase requires miner level three");
        game.Progress.experience = MineGame.ExperienceForLevel(3);
        check(game.UpgradePrice(UpgradeKind.Swap) == 600 && game.BuyUpgrade(UpgradeKind.Swap) && game.Progress.coins == 9400 && game.Progress.swapLevel == 1, "First exchange is a permanent purchase for 600 coins");
        check(!game.BuyUpgrade(UpgradeKind.Swap) && game.UpgradeUnlock(UpgradeKind.Swap) == 8, "Second exchange has its own level gate");
        game.Progress.experience = MineGame.ExperienceForLevel(8);
        check(game.BuyUpgrade(UpgradeKind.Swap) && game.Progress.coins == 7000 && game.Progress.swapLevel == 2, "Second rank costs 2400 and grants two exchanges");
        game.Progress.experience = MineGame.ExperienceForLevel(15); game.Progress.coins = 10000;
        check(game.BuyUpgrade(UpgradeKind.Swap) && game.Progress.coins == 400 && !game.BuyUpgrade(UpgradeKind.Swap), "Third exchange costs 9600 and is the cap");
        draws = 0; game.BeginRoll(() => ++draws == 2 ? .99 : 0);
        check(game.Progress.pendingSwapLimit == 3 && game.SwapPicks(0, 1) && game.SwapsRemaining == 2, "Draw fixes the purchased three-exchange budget");
        check(game.SwapPicks(0, 2) && game.SwapsRemaining == 1, "Second exchange consumes one budget unit");
        var restored = new MineGame(ProgressStore.Decode(JsonUtility.ToJson(game.Progress)));
        check(restored.Progress.pendingSwapCount == 2 && restored.SwapsRemaining == 1 && restored.SwapPicks(2, 3) && !restored.SwapPicks(3, 4), "Reload preserves spent exchanges and blocks a fourth one");
        restored.Progress.coins = 1000000; restored.Progress.experience = MineGame.ExperienceForLevel(50);
        foreach (UpgradeKind kind in Enum.GetValues(typeof(UpgradeKind))) check(!restored.BuyUpgrade(kind), "No upgrade can be bought during a pending draw: " + kind);
        restored.ResolveRoll();
        check(!restored.HasPendingRoll && restored.Progress.pendingSwapCount == 0 && restored.Progress.swapLevel == 3, "Resolution clears only the per-draw budget");

        foreach (bool used in new[] { false, true })
        {
            var legacy = new MineGame(ProgressStore.Decode("{\"version\":5,\"coins\":125,\"tickets\":1,\"rollPending\":true,\"pendingTypes\":[1,4,1,1,1],\"pendingSwapUsed\":" + (used ? "true" : "false") + "}"));
            check(legacy.Progress.version == 11 && legacy.Progress.swapLevel == 0 && legacy.Progress.coins == 125 && legacy.Progress.tickets == 1 && legacy.SwapsRemaining == (used ? 0 : 1), "v5 paid draw keeps only its previously available exchange");
            legacy = new MineGame(ProgressStore.Decode(JsonUtility.ToJson(legacy.Progress)));
            check(legacy.SwapsRemaining == (used ? 0 : 1), "Repeated migration load cannot renew spent exchanges");
            legacy.ResolveRoll(); legacy.BeginRoll(() => .6);
            check(legacy.Progress.pendingSwapLimit == 0 && !legacy.CanSwap, "Next draw requires the purchased upgrade after v5 migration");
        }
        game = new MineGame(new MineProgress { coins = 1000, experience = MineGame.ExperienceForLevel(2) });
        check(game.BuyUpgrade(UpgradeKind.Efficiency) && game.Progress.coins == 700 && game.MiningDamage == 2, "Sharpness buys extra ore damage");
        for (int i = 0; i < 3; i++) game.Mine();
        check(game.Progress.oresMined == 1, "Sharpness reduces the hits needed to break coal");
        game = new MineGame(new MineProgress { coins = 1000, experience = MineGame.ExperienceForLevel(4), hits = 11 });
        int tickets = game.Progress.tickets;
        check(game.BuyUpgrade(UpgradeKind.Press) && game.HitsRequired == 11 && game.Progress.hits == 0 && game.Progress.tickets == tickets + 1 && game.Progress.coins == 450, "Press purchase preserves and converts earned ticket progress");
        for (int i = 0; i < 10; i++) game.Mine();
        check(game.Progress.tickets == tickets + 1 && game.Progress.hits == 10, "Press cannot pay a ticket prematurely");
        game.Mine();
        check(game.Progress.tickets == tickets + 2 && game.Progress.hits == 0, "Press ticket repeats at the purchased interval");
        game = new MineGame(new MineProgress { coins = 1000, experience = MineGame.ExperienceForLevel(6), depths = new[] { 4, 4, 4, 4, 4 } });
        check(game.BuyUpgrade(UpgradeKind.Treasure) && game.Progress.coins == 200 && game.ChestReward == 69, "Treasure rank adds fifteen percent to chest rewards");
        var reward = game.Roll(() => 0);
        check(reward.reward == 475 && reward.chests == 5 && reward.perfect, "Chest upgrade is applied once to each opened chest");
        game = new MineGame(new MineProgress { coins = 1000000000, experience = MineGame.ExperienceForLevel(50) });
        foreach (var kind in new[] { UpgradeKind.Swap, UpgradeKind.Efficiency, UpgradeKind.Press, UpgradeKind.Treasure })
        {
            for (int i = 0; i < MineGame.UpgradeMax(kind); i++) check(game.BuyUpgrade(kind), "Affordable rank purchase: " + kind + " " + i);
            long before = game.Progress.coins;
            check(!game.BuyUpgrade(kind) && game.Progress.coins == before && game.UpgradeLevel(kind) == MineGame.UpgradeMax(kind), "Rank cap prevents duplicate spending: " + kind);
        }
        restored = new MineGame(ProgressStore.Decode(JsonUtility.ToJson(game.Progress)));
        check(restored.Progress.swapLevel == 3 && restored.MiningDamage == 11 && restored.HitsRequired == 6 && restored.ChestReward == 150 && restored.Progress.coins == game.Progress.coins, "All purchased branches survive save reload");
        game = new MineGame(new MineProgress { swapLevel = 99, efficiencyLevel = 99, pressLevel = 99, treasureLevel = 99, hits = 99 });
        check(game.Progress.swapLevel == 3 && game.MiningDamage == 11 && game.HitsRequired == 6 && game.Progress.hits == 5 && game.ChestReward == 150, "Save clamps every new upgrade and the ticket counter");
        game = new MineGame(new MineProgress { swapLevel = 1 }); draws = 0; game.BeginRoll(() => ++draws == 2 ? .99 : 0);
        game.Progress.pendingSwapCount = 2; bool invalid = false;
        try { new MineGame(ProgressStore.Decode(JsonUtility.ToJson(game.Progress))); } catch (FormatException) { invalid = true; }
        check(invalid, "Malformed exchange counters are rejected for backup recovery");
    }

    private static void VerifyMilestoneRules(Action<bool, string> check)
    {
        var game = new MineGame();
        check(game.ClaimedMilestones == 0 && game.ReadyMilestones == 0 && !game.ClaimMilestone(0), "Fresh goals cannot pay before completion");
        check(!game.ClaimMilestone(-1) && !game.ClaimMilestone(MineGame.MilestoneCount), "Invalid goal indices never change progress");
        for (int i = 0; i < 29; i++) game.Mine();
        check(game.MilestoneValue(0) == 29 && !game.CanClaimMilestone(0), "Thirty-hit goal has an exact completion threshold");
        game.Mine(); long before = game.Progress.coins; int tickets = game.Progress.tickets; long experience = game.Progress.experience;
        check(game.CanClaimMilestone(0) && game.ClaimMilestone(0) && game.Progress.coins == before + 120 && game.Progress.tickets == tickets + 1, "Goal pays its listed reward only when explicitly claimed");
        var restored = new MineGame(ProgressStore.Decode(JsonUtility.ToJson(game.Progress)));
        check(restored.MilestoneClaimed(0) && !restored.ClaimMilestone(0) && restored.Progress.coins == game.Progress.coins && restored.Progress.experience == experience, "Claim survives reload without duplicate coins, tickets or experience");
        game = new MineGame(new MineProgress { coins = 0, tickets = 1, totalClicks = 30, totalBlocks = 50 });
        game.BeginRoll(() => .6); int claims = game.Progress.milestoneClaims;
        check(!game.ClaimMilestone(0) && !game.ClaimMilestone(4) && game.ReadyMilestones == 0 && game.Progress.coins == 0 && game.Progress.milestoneClaims == claims, "Goals cannot pay during a pending draw");
        game.ResolveRoll();
        check(game.CanClaimMilestone(0) && game.CanClaimMilestone(4) && game.Progress.milestoneClaims == 0, "Resolving a draw restores claim availability without automatic rewards");
        var legacy = new MineGame(ProgressStore.Decode("{\"version\":6,\"coins\":745,\"tickets\":4,\"totalClicks\":36,\"swapLevel\":2,\"efficiencyLevel\":3,\"pressLevel\":2,\"treasureLevel\":4,\"hits\":8,\"experience\":1000}"));
        check(legacy.Progress.version == 11 && legacy.Progress.coins == 745 && legacy.Progress.tickets == 4 && legacy.Progress.hits == 8 && legacy.Progress.swapLevel == 2 && legacy.Progress.efficiencyLevel == 3 && legacy.Progress.pressLevel == 2 && legacy.Progress.treasureLevel == 4, "v6 migration preserves the wallet and every purchased branch");
        check(legacy.ClaimedMilestones == 0 && legacy.CanClaimMilestone(0) && legacy.MilestoneValue(0) == 30, "Existing lifetime progress counts toward unclaimed goals without automatic payouts");
        legacy = new MineGame(ProgressStore.Decode("{\"version\":6,\"coins\":125,\"tickets\":0,\"totalClicks\":30,\"swapLevel\":2,\"rollPending\":true,\"pendingTypes\":[4,1,2,1,1],\"pendingSwapLimit\":2,\"pendingSwapCount\":1}"));
        check(legacy.HasPendingRoll && legacy.Progress.version == 11 && legacy.Progress.coins == 125 && legacy.Progress.tickets == 0 && legacy.Progress.swapLevel == 2 && legacy.SwapsRemaining == 1 && legacy.Progress.pendingTypes[0] == 4 && !legacy.ClaimMilestone(0), "v6 paid draw keeps its fixed picks, spent last ticket and remaining purchased exchange");
        legacy = new MineGame(ProgressStore.Decode(JsonUtility.ToJson(legacy.Progress)));
        check(legacy.SwapsRemaining == 1 && legacy.ClaimedMilestones == 0 && legacy.ResolveRoll() != null && legacy.ResolveRoll() == null && legacy.CanClaimMilestone(0), "Repeated v7 reload cannot renew the migrated exchange budget or resolve its draw twice");
        game = new MineGame(new MineProgress { coins = 0, tickets = 0, totalClicks = 30, oresMined = 10, totalRolls = 5, upgradeLevel = 20, totalBlocks = 50, experience = MineGame.ExperienceForLevel(50), swapLevel = 1, efficiencyLevel = 3, stratum = 10, forgeLevel = 6, pressLevel = 3, treasureLevel = 5 });
        check(game.ReadyMilestones == 16 && game.Relics == 3, "Lifetime and workshop milestones all use existing progression counters");
        for (int i = 0; i < MineGame.MilestoneCount; i++)
        {
            var goal = MineGame.MilestoneAt(i);
            check(game.MilestoneValue(i) == goal.target && game.ClaimMilestone(i), "Each completed goal can be claimed: " + i);
            game = new MineGame(ProgressStore.Decode(JsonUtility.ToJson(game.Progress)));
            check(game.MilestoneClaimed(i) && !game.CanClaimMilestone(i) && !game.ClaimMilestone(i), "Each claim remains spent through reload: " + i);
        }
        check(game.ClaimedMilestones == 16 && game.ReadyMilestones == 0 && game.Progress.coins == 14140 && game.Progress.tickets == 34, "Complete book has a bounded lifetime reward of 14140 coins and 34 tickets");
        string key = "LuckyPickaxe.Milestones.Tests." + Guid.NewGuid().ToString("N");
        try
        {
            ProgressStore.Save(game, key); PlayerPrefs.SetString(key, "not-json"); restored = ProgressStore.Load(key);
            check(restored.Progress.milestoneClaims == 65535 && restored.Progress.coins == 14140 && restored.Progress.tickets == 34 && !restored.ClaimMilestone(15), "Backup recovery preserves paid goal claims and prevents double rewards");
        }
        finally { PlayerPrefs.DeleteKey(key); PlayerPrefs.DeleteKey(key + ".backup"); PlayerPrefs.Save(); }
        check(new MineGame(new MineProgress { milestoneClaims = -1 }).ClaimedMilestones == 0 && new MineGame(new MineProgress { milestoneClaims = (1 << 23) | 1 }).Progress.milestoneClaims == 1, "Save validation removes negative claims and unknown future bits");
        game = new MineGame(new MineProgress { coins = MineGame.CoinLimit, tickets = 1000000, totalClicks = long.MaxValue });
        check(game.MilestoneValue(0) == 30 && game.ClaimMilestone(0) && game.Progress.coins == MineGame.CoinLimit && game.Progress.tickets == 1000000, "Goal progress display and rewards respect existing currency caps");
    }

    private static void VerifyProductionRules(Action<bool, string> check)
    {
        var game = new MineGame(new MineProgress { coins = 10000 });
        check(game.ProductionMilli == 0 && game.AdvanceTime(60, false) == 0 && game.Progress.coins == 10000 && !game.BuyUpgrade(UpgradeKind.Worker), "Production starts empty and the first worker requires miner level two");
        var gated = new MineGame(new MineProgress { coins = 10000, workerCount = 1, experience = MineGame.ExperienceForLevel(4) });
        check(!gated.BuyUpgrade(UpgradeKind.Drill) && gated.Progress.coins == 10000, "Drill level gate applies even with a hired worker");
        gated.Progress.experience = MineGame.ExperienceForLevel(5);
        check(gated.BuyUpgrade(UpgradeKind.Drill) && gated.Progress.coins == 8200, "Drill opens exactly at miner level five");
        gated.Progress.experience = MineGame.ExperienceForLevel(7);
        check(!gated.BuyUpgrade(UpgradeKind.Cart) && gated.Progress.coins == 8200, "Cart level gate applies even with a purchased drill");
        gated.Progress.experience = MineGame.ExperienceForLevel(8);
        check(gated.BuyUpgrade(UpgradeKind.Cart) && gated.Progress.coins == 6800, "Cart opens exactly at miner level eight");
        game.Progress.experience = MineGame.ExperienceForLevel(8);
        check(!game.BuyUpgrade(UpgradeKind.Drill) && !game.BuyUpgrade(UpgradeKind.Cart) && !game.BuyUpgrade(UpgradeKind.Rails), "Production dependencies cannot be skipped with a full wallet");
        check(game.UpgradePrice(UpgradeKind.Worker) == 300 && game.BuyUpgrade(UpgradeKind.Worker) && game.Progress.coins == 9700 && game.ProductionMilli == 2000, "First worker costs 300 and produces two coins each second");
        check(game.UpgradePrice(UpgradeKind.Worker) == 354 && game.UpgradePrice(UpgradeKind.Drill) == 1800 && game.BuyUpgrade(UpgradeKind.Drill) && game.Progress.coins == 7900 && game.ProductionMilli == 14000, "Owned worker unlocks a drill with twelve base coins per second");
        check(game.BuyUpgrade(UpgradeKind.Cart) && game.Progress.coins == 6500 && game.ProductionMilli == 15400 && game.CartBonus == 10, "Cart multiplies both workers and drills by ten percent");
        check(game.BuyUpgrade(UpgradeKind.Rails) && game.Progress.coins == 5600 && game.CartBonus == 12 && game.ProductionMilli == 15680, "Rails raise the contribution of every cart by two percentage points");
        check(!game.BuyUpgrade(UpgradeKind.Rails) && game.UpgradeUnlock(UpgradeKind.Rails) == 10, "Next rails rank has its own miner level requirement");
        game.Progress.forgeLevel = 2;
        check(game.ProductionMilli == 18368 && game.DrillBonus == 20, "Forge boosts drill output before the shared logistics multiplier");
        int tickets = game.Progress.tickets; long clicks = game.Progress.totalClicks, experience = game.Progress.experience, ores = game.Progress.oresMined;
        int hits = game.Progress.hits;
        check(game.AdvanceTime(1.25, false) == 22 && Math.Abs(game.Progress.productionCarry - .96) < .000001 && game.Progress.totalProduced == 22, "Fractional production keeps its remainder instead of rounding up");
        var restored = new MineGame(ProgressStore.Decode(JsonUtility.ToJson(game.Progress)));
        long before = restored.Progress.coins;
        check(restored.Progress.coins == game.Progress.coins && restored.ProductionMilli == 18368 && restored.Progress.totalProduced == 22 && Math.Abs(restored.Progress.productionCarry - .96) < .000001, "Production quantities, totals and fractions survive reload without offline awards");
        check(restored.AdvanceTime(.25, true) == 5 && restored.Progress.coins == before + 5 && Math.Abs(restored.Progress.productionCarry - .552) < .000001, "Saved production fraction is consumed once on the next tick");
        check(restored.Progress.tickets == tickets && restored.Progress.totalClicks == clicks && restored.Progress.experience == experience && restored.Progress.oresMined == ores && restored.Progress.hits == hits, "Automatic production never grants clicks, experience, ore progress or tickets");
        var split = new MineGame(new MineProgress { coins = 0, workerCount = 1, drillCount = 1, cartCount = 1, railLevel = 1, forgeLevel = 2 });
        var whole = new MineGame(ProgressStore.Decode(JsonUtility.ToJson(split.Progress)));
        for (int i = 0; i < 600; i++) split.AdvanceTime(1d / 60, false);
        whole.AdvanceTime(10, false);
        check(split.Progress.coins == whole.Progress.coins && Math.Abs(split.Progress.productionCarry - whole.Progress.productionCarry) < .000001, "Production is independent of frame subdivision");
        game = new MineGame(new MineProgress { coins = 0, workerCount = 1, swapLevel = 1, totalClicks = 30, veinOfferLeft = 10 });
        int draw = 0; game.BeginRoll(() => ++draw == 2 ? .99 : 0); string types = string.Join(",", game.Progress.pendingTypes);
        check(game.AdvanceTime(3, true) == 6 && game.HasPendingRoll && game.Progress.veinOfferLeft == 10 && game.Progress.tickets == 1 && !game.BuyUpgrade(UpgradeKind.Worker) && !game.ActivateVein(), "Paid draw keeps passive income but freezes vein timers, purchases and activation");
        restored = new MineGame(ProgressStore.Decode(JsonUtility.ToJson(game.Progress)));
        check(restored.Progress.coins == 6 && restored.SwapsRemaining == 1 && string.Join(",", restored.Progress.pendingTypes) == types && restored.ResolveRoll() != null && restored.ResolveRoll() == null, "Reload preserves income and the same pending draw with a single payout");
        foreach (double invalid in new[] { -1d, double.NaN, double.PositiveInfinity, 86401d })
        {
            string unchanged = JsonUtility.ToJson(restored.Progress); bool rejected = false;
            try { restored.AdvanceTime(invalid, true); } catch (ArgumentOutOfRangeException) { rejected = true; }
            check(rejected && JsonUtility.ToJson(restored.Progress) == unchanged, "Invalid clock input cannot mutate the economy: " + invalid);
        }
        game = new MineGame(new MineProgress { coins = MineGame.CoinLimit - 1, workerCount = 1 });
        check(game.AdvanceTime(10, false) == 1 && game.Progress.coins == MineGame.CoinLimit && game.Progress.totalProduced == 1, "Production respects the wallet cap and counts only credited coins");
        check(game.AdvanceTime(10, false) == 0 && game.Progress.totalProduced == 1, "A full wallet never records phantom production");
        game = new MineGame(new MineProgress { coins = MineGame.CoinLimit, workerCount = 100, drillCount = 50, cartCount = 25, railLevel = 10, forgeLevel = 10, experience = MineGame.ExperienceForLevel(50) });
        check(game.ProductionMilli == 11900000 && game.ProductionMilli / 1000 == 11900, "Maximum production is bounded at 11900 coins per second");
        foreach (var kind in new[] { UpgradeKind.Worker, UpgradeKind.Drill, UpgradeKind.Cart, UpgradeKind.Rails })
            check(!game.BuyUpgrade(kind) && game.Progress.coins == MineGame.CoinLimit, "Capped production purchase never spends: " + kind);
        game = new MineGame(new MineProgress { workerCount = 999, drillCount = 999, cartCount = 999, railLevel = 999, productionCarry = double.NaN, totalProduced = -1, veinOfferLeft = double.PositiveInfinity, veinBoostLeft = -5, veinCharge = 99 });
        check(game.Progress.workerCount == 100 && game.Progress.drillCount == 50 && game.Progress.cartCount == 25 && game.Progress.railLevel == 10 && game.Progress.productionCarry == 0 && game.Progress.totalProduced == 0 && !game.VeinOffered && !game.VeinActive && game.Progress.veinCharge == 23, "Production and vein save fields clamp malformed values");
        var legacy = new MineGame(ProgressStore.Decode("{\"version\":7,\"coins\":745,\"tickets\":4,\"totalClicks\":36,\"swapLevel\":2,\"milestoneClaims\":5,\"hits\":8,\"experience\":1000}"));
        check(legacy.Progress.version == 11 && legacy.Progress.coins == 745 && legacy.Progress.tickets == 4 && legacy.Progress.hits == 8 && legacy.Progress.swapLevel == 2 && legacy.Progress.milestoneClaims == 5 && legacy.ProductionMilli == 0 && !legacy.VeinOffered, "v7 migration preserves paid progress and claims without free machinery or retrospective events");
        game = new MineGame(new MineProgress { veinCharge = 23, oreDamage = 5 }); game.Mine();
        check(game.LastHit.broken && game.LastHit.veinFound && game.VeinOffered && game.Progress.veinCharge == 0 && game.Progress.veinOfferLeft == 20 && !game.VeinActive, "Every twenty-fourth eligible broken ore reveals an unactivated rich vein");
        game.AdvanceTime(5, false);
        check(game.Progress.veinOfferLeft == 20 && game.Progress.coins == 100, "Offer time pauses away from the mining screen without a free payout");
        game.AdvanceTime(3, true);
        check(game.Progress.veinOfferLeft == 17 && game.ActivateVein() && !game.ActivateVein() && game.ManualMultiplier == 3 && game.Progress.veinBoostLeft == 12 && !game.VeinOffered, "Claiming the live offer starts a single twelve-second manual boost");
        game.Mine();
        check(game.LastHit.rich && game.LastHit.coins == 24 && game.LastHit.experience == 1 && game.LastHit.tickets == 0 && game.Progress.veinCharge == 0, "Rich vein triples click coins without boosting experience or tickets");
        game = new MineGame(new MineProgress { veinBoostLeft = 12, oreDamage = 5, workerCount = 1 }); game.Mine();
        check(game.LastHit.coins == 60 && game.LastHit.broken && game.LastHit.experience == 7 && !game.LastHit.veinFound && game.Progress.veinCharge == 0, "Boost triples ore-break money but cannot charge another vein while active");
        check(game.AdvanceTime(1, true) == 2 && game.Progress.veinBoostLeft == 11 && game.ProductionMilli == 2000, "Manual boost leaves automatic production unchanged");
        game = new MineGame(new MineProgress { experience = 39, veinBoostLeft = 12 }); game.Mine();
        check(game.LastHit.coins == 104 && game.LastHit.levels == 1 && game.LastHit.tickets == 1, "Miner-level reward is added once without the rich-vein multiplier");
        restored = new MineGame(ProgressStore.Decode(JsonUtility.ToJson(game.Progress)));
        restored.AdvanceTime(4.5, true);
        check(restored.Progress.veinBoostLeft == 7.5 && restored.ManualMultiplier == 3, "Reload preserves remaining boost time instead of renewing it");
        restored.AdvanceTime(7.5, true);
        check(!restored.VeinActive && restored.ManualMultiplier == 1 && !restored.ActivateVein(), "Expired boost returns to ordinary mining and cannot reactivate itself");
        game = new MineGame(new MineProgress { veinOfferLeft = 1 }); game.AdvanceTime(1, true);
        check(!game.VeinOffered && !game.ActivateVein() && game.Progress.veinCharge == 0, "Missed offer expires without paying coins or reappearing on reload");
        game = new MineGame(new MineProgress { coins = 100000, workerCount = 3, drillCount = 2, cartCount = 2, railLevel = 2, forgeLevel = 3, productionCarry = .75, totalProduced = 33, veinOfferLeft = 9, veinCharge = 10 });
        string key = "LuckyPickaxe.Production.Tests." + Guid.NewGuid().ToString("N");
        try
        {
            ProgressStore.Save(game, key); PlayerPrefs.SetString(key, "not-json"); restored = ProgressStore.Load(key);
            check(restored.Progress.workerCount == 3 && restored.Progress.drillCount == 2 && restored.Progress.cartCount == 2 && restored.Progress.railLevel == 2 && restored.Progress.productionCarry == .75 && restored.Progress.totalProduced == 33 && restored.Progress.veinOfferLeft == 9 && restored.Progress.veinCharge == 10 && restored.Progress.coins == 100000, "Backup restores production, fractional coins and the exact saved offer time");
        }
        finally { PlayerPrefs.DeleteKey(key); PlayerPrefs.DeleteKey(key + ".backup"); PlayerPrefs.Save(); }
    }

    private static void VerifyRewardedOfflineRules(Action<bool, string> check)
    {
        const long epoch = 1700000000;
        var game = new MineGame(new MineProgress { workerCount = 1 });
        game.RecordSession(epoch); game.PrepareOffline(epoch + 600);
        check(game.CanDoubleOffline && game.DoubledOfflineReward == 1200, "Video offers twice the held offline amount");
        check(game.ClaimOffline(true) && game.Progress.coins == 1280 && game.Progress.totalOfflineCoins == 1200 && game.Progress.totalProduced == 1200 && !game.HasOfflineReward, "A verified video credits twice the crate and consumes it atomically");
        check(!game.ClaimOffline(true) && !game.ClaimOffline() && game.Progress.coins == 1280, "A doubled crate cannot pay again through either claim path");
        check(game.Progress.tickets == 2 && game.Progress.experience == 0 && game.Progress.totalClicks == 0 && game.Progress.offlineRewardRateMilli == 0, "Video awards coins only and clears the consumed crate metadata");
        game = new MineGame(ProgressStore.Decode(JsonUtility.ToJson(game.Progress)));
        check(game.Progress.version == 11 && game.Progress.coins == 1280 && !game.CanDoubleOffline && !game.ClaimOffline(true), "The existing v11 save stores the rewarded wallet without a replayable claim");

        game = new MineGame(new MineProgress { workerCount = 1, tickets = 1, swapLevel = 1 });
        game.RecordSession(epoch); game.PrepareOffline(epoch + 600); game.BeginRoll(() => 0);
        string before = JsonUtility.ToJson(game.Progress);
        check(!game.CanDoubleOffline && !game.ClaimOffline(true) && JsonUtility.ToJson(game.Progress) == before, "Video cannot alter an offline crate during a paid draw");
        game.ResolveRoll(); game.Progress.coins = MineGame.CoinLimit - 1199;
        before = JsonUtility.ToJson(game.Progress);
        check(!game.CanDoubleOffline && !game.ClaimOffline(true) && JsonUtility.ToJson(game.Progress) == before, "Video is refused before credit when its full reward cannot fit");
        game.Progress.coins--;
        check(game.CanDoubleOffline && game.ClaimOffline(true) && game.Progress.coins == MineGame.CoinLimit && !game.HasOfflineReward, "The full doubled amount fits exactly at the wallet limit");

        game = new MineGame(new MineProgress { workerCount = 1, coins = MineGame.CoinLimit - 5 });
        game.RecordSession(epoch); game.PrepareOffline(epoch + 600);
        check(!game.CanDoubleOffline && game.ClaimOffline() && game.Progress.offlineCoins == 595, "Video does not prevent an ordinary partial claim near the wallet cap");
        game.Progress.coins -= 1190;
        check(game.ClaimOffline(true) && game.Progress.coins == MineGame.CoinLimit && game.Progress.totalOfflineCoins == 1195 && !game.HasOfflineReward, "Only the still-unclaimed remainder is doubled after an ordinary partial claim");

        game = new MineGame(new MineProgress { workerCount = 100, drillCount = 50, cartCount = 25, railLevel = 10, forgeLevel = 10, warehouseLevel = 4, prestigeCount = 1, prestigeUpgrades = 3 });
        game.RecordSession(epoch); game.PrepareOffline(epoch + 86400);
        check(game.DoubledOfflineReward == 642600000 && game.ClaimOffline(true) && game.Progress.totalOfflineCoins == 642600000, "The maximum prestige offline crate doubles without overflow");
        string key = "LuckyPickaxe.Rewarded.Tests." + Guid.NewGuid().ToString("N");
        try
        {
            ProgressStore.Save(game, key);
            var paid = ProgressStore.Load(key);
            check(paid.Progress.coins == 642600080 && !paid.HasOfflineReward && !paid.ClaimOffline(true), "PlayerPrefs reload keeps the full doubled payout and no reusable video claim");
            PlayerPrefs.SetString(key, "{\"version\":99}");
            paid = ProgressStore.Load(key);
            check(paid.Progress.coins == 642600080 && !paid.HasOfflineReward && !paid.ClaimOffline(), "Backup recovery preserves the already-paid video result");
        }
        finally { PlayerPrefs.DeleteKey(key); PlayerPrefs.DeleteKey(key + ".backup"); PlayerPrefs.Save(); }
    }

    private static void VerifyOfflineRules(Action<bool, string> check)
    {
        const long epoch = 1700000000;
        var game = new MineGame();
        check(!game.PrepareOffline(epoch) && game.Progress.lastSeenUtc == epoch && game.Progress.coins == 80 && game.OfflineHours == 4, "First launch initializes the offline clock without historical rewards");
        check(!game.PrepareOffline(epoch + 86400) && !game.HasOfflineReward && game.Progress.coins == 80, "No production means no offline money");
        game = new MineGame(new MineProgress { workerCount = 1, hits = 7, veinBoostLeft = 8, productionCarry = .5 });
        game.RecordSession(epoch);
        check(game.PrepareOffline(epoch + 3600) && game.Progress.offlineCoins == 3600 && game.Progress.offlineRewardSeconds == 3600 && game.Progress.offlineElapsedSeconds == 3600 && game.Progress.offlineRewardRateMilli == 2000 && game.Progress.coins == 80, "One worker earns half normal production for an hour, held until explicit claim");
        check(game.Progress.tickets == 2 && game.Progress.hits == 7 && game.Progress.totalClicks == 0 && game.Progress.experience == 0 && game.Progress.veinBoostLeft == 8 && game.Progress.productionCarry == .5 && game.Progress.totalProduced == 0, "Preparing offline rewards never grants tickets, experience or live-production progress and does not expire the rich vein");
        game = new MineGame(ProgressStore.Decode(JsonUtility.ToJson(game.Progress)));
        check(!game.PrepareOffline(epoch + 7200) && game.Progress.offlineCoins == 3600 && game.Progress.offlineRewardSeconds == 3600 && game.Progress.lastSeenUtc == epoch + 7200, "Unclaimed crate survives reload without stacking another absence or renewing its interval");
        check(game.ClaimOffline() && game.Progress.coins == 3680 && game.Progress.totalProduced == 3600 && game.Progress.totalOfflineCoins == 3600 && !game.HasOfflineReward, "Claim credits the held money and production totals exactly once");
        check(!game.ClaimOffline() && !game.PrepareOffline(epoch + 7200) && game.Progress.coins == 3680 && game.Progress.offlineRewardRateMilli == 0, "Claim and return at the same timestamp cannot pay again");
        for (int rank = 0; rank <= MineGame.MaxWarehouse; rank++)
        {
            game = new MineGame(new MineProgress { workerCount = 1, warehouseLevel = rank }); game.RecordSession(epoch);
            int cap = (4 + rank * 2) * 3600;
            check(game.PrepareOffline(epoch + 86400) && game.Progress.offlineCoins == cap && game.Progress.offlineRewardSeconds == cap && game.Progress.offlineElapsedSeconds == 86400 && game.Progress.offlineRewardCapacity == cap, "Warehouse rank limits a full day of absence: " + rank);
        }
        game = new MineGame(new MineProgress { workerCount = 100, drillCount = 50, cartCount = 25, railLevel = 10, forgeLevel = 10, warehouseLevel = 4 }); game.RecordSession(epoch);
        check(game.PrepareOffline(epoch + 86400 * 30) && game.Progress.offlineCoins == 257040000 && game.Progress.offlineRewardSeconds == 43200, "Maximum production and storage have a bounded 257040000-coin offline crate even after a month");
        game = new MineGame(new MineProgress { coins = 0, workerCount = 1, drillCount = 1, cartCount = 1, railLevel = 1 }); game.RecordSession(epoch);
        var whole = new MineGame(ProgressStore.Decode(JsonUtility.ToJson(game.Progress)));
        for (int second = 1; second <= 25; second++)
        {
            game.PrepareOffline(epoch + second); game.ClaimOffline();
            game = new MineGame(ProgressStore.Decode(JsonUtility.ToJson(game.Progress)));
        }
        whole.PrepareOffline(epoch + 25); whole.ClaimOffline();
        check(game.Progress.coins == 196 && whole.Progress.coins == 196 && game.Progress.offlineCarry == 0 && whole.Progress.offlineCarry == 0, "Fractional offline money survives frequent reloads and matches one uninterrupted absence");
        game = new MineGame(new MineProgress { workerCount = 1 }); game.RecordSession(epoch);
        game.Progress.workerCount = 20; game.Progress.warehouseLevel = 4;
        check(game.PrepareOffline(epoch + 86400) && game.Progress.offlineCoins == 14400 && game.Progress.offlineRewardRateMilli == 2000 && game.Progress.offlineRewardCapacity == 14400, "Upgrading production or storage cannot retroactively multiply the previous absence");
        game.ClaimOffline();
        check(game.PrepareOffline(epoch + 86400 + 3600) && game.Progress.offlineCoins == 72000 && game.Progress.offlineRewardCapacity == 43200, "New equipment and capacity apply to the next absence");
        game = new MineGame(new MineProgress { workerCount = 1 }); game.RecordSession(epoch);
        check(!game.PrepareOffline(epoch - 3600) && game.Progress.lastSeenUtc == epoch && game.Progress.offlineCarry == 0, "Rolling the clock backwards produces no reward and cannot lower the saved timestamp");
        check(game.PrepareOffline(epoch + 7200) && game.Progress.offlineCoins == 7200, "Clock advancement credits only time above the saved high-water mark");
        game.ClaimOffline(); game.RecordSession(epoch - 86400);
        check(!game.PrepareOffline(epoch + 3600) && !game.PrepareOffline(epoch + 7200) && game.Progress.lastSeenUtc == epoch + 7200 && game.Progress.coins == 7280, "Returning from a forward clock change cannot replay the already-paid interval");
        foreach (long invalid in new[] { -1L, 0L, long.MaxValue })
        {
            string original = JsonUtility.ToJson(game.Progress); bool rejectedRecord = false, rejectedReturn = false;
            try { game.RecordSession(invalid); } catch (ArgumentOutOfRangeException) { rejectedRecord = true; }
            try { game.PrepareOffline(invalid); } catch (ArgumentOutOfRangeException) { rejectedReturn = true; }
            check(rejectedRecord && rejectedReturn && JsonUtility.ToJson(game.Progress) == original, "Invalid UTC cannot mutate production or the wallet: " + invalid);
        }
        game = new MineGame(new MineProgress { workerCount = 1, tickets = 1, swapLevel = 1 }); game.RecordSession(epoch);
        int draw = 0; game.BeginRoll(() => ++draw == 2 ? .99 : 0);
        check(game.PrepareOffline(epoch + 600) && game.Progress.offlineCoins == 600 && !game.CanClaimOffline && !game.ClaimOffline() && game.Progress.tickets == 0 && game.Progress.pendingTypes[1] == 4 && game.SwapsRemaining == 1, "Offline crate is prepared during a paid draw but cannot be claimed or alter fixed picks");
        game = new MineGame(ProgressStore.Decode(JsonUtility.ToJson(game.Progress))); game.AdvanceTime(5, false);
        check(game.Progress.coins == 90 && game.Progress.offlineCoins == 600 && game.ResolveRoll() != null && game.ClaimOffline() && !game.ClaimOffline(), "Pending draw and live income reload independently from the one-time offline claim");
        game = new MineGame(ProgressStore.Decode("{\"version\":8,\"coins\":745,\"tickets\":4,\"workerCount\":3,\"drillCount\":2,\"cartCount\":1,\"railLevel\":2,\"milestoneClaims\":5,\"hits\":8,\"veinBoostLeft\":7.5,\"productionCarry\":0.75}"));
        check(game.Progress.version == 11 && game.Progress.coins == 745 && game.Progress.tickets == 4 && game.Progress.workerCount == 3 && game.Progress.drillCount == 2 && game.Progress.cartCount == 1 && game.Progress.railLevel == 2 && game.Progress.milestoneClaims == 5 && game.Progress.hits == 8 && game.Progress.veinBoostLeft == 7.5 && game.Progress.productionCarry == .75 && !game.PrepareOffline(epoch) && game.Progress.coins == 745, "v8 migration preserves all live progress without paying an unrecorded absence");
        game = new MineGame(new MineProgress { workerCount = 1, coins = MineGame.CoinLimit }); game.RecordSession(epoch); game.PrepareOffline(epoch + 1000);
        check(!game.CanClaimOffline && !game.ClaimOffline() && game.Progress.offlineCoins == 1000, "Full wallet keeps the unclaimed crate instead of discarding it");
        game.Progress.coins -= 5;
        check(game.ClaimOffline() && game.Progress.coins == MineGame.CoinLimit && game.Progress.offlineCoins == 995 && game.Progress.totalOfflineCoins == 5 && !game.ClaimOffline(), "Partial claim fills the wallet and retains the exact remainder");
        game.Progress.coins -= 1000;
        check(game.ClaimOffline() && !game.HasOfflineReward && game.Progress.totalOfflineCoins == 1000 && game.Progress.coins == MineGame.CoinLimit - 5, "Stored remainder can be claimed later without duplicating earlier coins");
        game = new MineGame(new MineProgress { coins = 100000, experience = MineGame.ExperienceForLevel(5), workerCount = 1 });
        check(!game.BuyUpgrade(UpgradeKind.Warehouse) && game.UpgradePrice(UpgradeKind.Warehouse) == 1500 && game.OfflineHours == 4, "Storage expansion requires miner level six and starts at four hours");
        game.Progress.experience = MineGame.ExperienceForLevel(6); game.Progress.workerCount = 0;
        check(!game.BuyUpgrade(UpgradeKind.Warehouse), "Storage expansion requires at least one hired worker");
        game.Progress.workerCount = 1;
        foreach (int price in new[] { 1500, 3300, 7260, 15972 })
        {
            int rank = game.Progress.warehouseLevel;
            game.Progress.experience = MineGame.ExperienceForLevel(6 + rank * 4);
            check(game.UpgradePrice(UpgradeKind.Warehouse) == price && game.BuyUpgrade(UpgradeKind.Warehouse) && game.OfflineHours == 6 + rank * 2, "Storage rank costs and capacity: " + rank);
        }
        check(game.Progress.coins == 71968 && !game.BuyUpgrade(UpgradeKind.Warehouse) && game.OfflineHours == 12, "Storage stops at twelve hours and cannot spend above its fourth rank");
        var malformed = new MineGame(new MineProgress { warehouseLevel = 100, lastSeenUtc = -1, offlineRateMilli = -5, offlineCapacitySeconds = -1, offlineCarry = 9999, totalOfflineCoins = -1 });
        check(malformed.Progress.warehouseLevel == 4 && malformed.Progress.lastSeenUtc == 0 && malformed.Progress.offlineRateMilli == 0 && malformed.Progress.offlineCapacitySeconds == 0 && malformed.Progress.offlineCarry == 1999 && malformed.Progress.totalOfflineCoins == 0, "Offline save validation clamps invalid ranks, clock values and fractional money");
        game.RecordSession(epoch); game.PrepareOffline(epoch + 600);
        string key = "LuckyPickaxe.Offline.Tests." + Guid.NewGuid().ToString("N");
        try
        {
            ProgressStore.Save(game, key);
            var pending = ProgressStore.Load(key);
            check(pending.Progress.offlineCoins == 600 && pending.Progress.warehouseLevel == 4 && !pending.PrepareOffline(epoch + 600) && pending.Progress.coins == 71968, "PlayerPrefs restores the prepared crate before claiming and cannot prepare it again");
            check(pending.ClaimOffline() && pending.Progress.coins == 72568, "Saved pending crate pays its listed amount"); ProgressStore.Save(pending, key);
            game.Progress.offlineCoins = 1000000; PlayerPrefs.SetString(key, JsonUtility.ToJson(game.Progress));
            var backup = ProgressStore.Load(key);
            check(!backup.HasOfflineReward && !backup.ClaimOffline() && backup.Progress.coins == 72568 && backup.Progress.totalOfflineCoins == 600, "Malformed primary recovers the already-claimed backup without paying again");
        }
        finally { PlayerPrefs.DeleteKey(key); PlayerPrefs.DeleteKey(key + ".backup"); PlayerPrefs.Save(); }
    }

    private static void VerifyPrestigeRules(Action<bool, string> check)
    {
        var game = new MineGame(); string original = JsonUtility.ToJson(game.Progress);
        check(!game.CanPrestige && !game.Prestige(() => .5) && JsonUtility.ToJson(game.Progress) == original, "Fresh players cannot reset or mint crystals");
        game = new MineGame(new MineProgress { experience = MineGame.ExperienceForLevel(50) - 1, stratum = 71 });
        check(game.MinerLevel == 49 && !game.CanPrestige && !game.Prestige(() => .5), "Prestige opens only on reaching miner level fifty");
        game = new MineGame(new MineProgress { experience = MineGame.ExperienceForLevel(50), stratum = 1 });
        check(game.CanPrestige && game.PrestigeReward == 3, "First-floor level fifty gives the minimum three crystals");
        foreach (int floor in new[] { 1, 10, 11, 61, 71, 1000000 })
        {
            game.Progress.stratum = floor;
            check(game.PrestigeReward == 3 + Math.Min(7, (floor - 1) / 10), "Crystal reward floor boundary: " + floor);
        }
        game = new MineGame(new MineProgress { coins = 1000, tickets = 1, experience = MineGame.ExperienceForLevel(50), swapLevel = 1 });
        game.BeginRoll(() => .6); original = JsonUtility.ToJson(game.Progress);
        check(!game.CanPrestige && !game.Prestige(() => .5) && JsonUtility.ToJson(game.Progress) == original, "Paid draw blocks prestige without changing its picks, wallet or exchange budget");
        game.ResolveRoll(); game.RecordSession(1700000000); game.Progress.workerCount = 1; game.RecordSession(1700000000); game.PrepareOffline(1700000600);
        check(game.HasOfflineReward && !game.CanPrestige && !game.Prestige(() => .5), "Unclaimed offline reward must be collected before a reset");
        game.ClaimOffline(); original = JsonUtility.ToJson(game.Progress); bool invalid = false;
        try { game.Prestige(() => double.NaN); } catch (ArgumentOutOfRangeException) { invalid = true; }
        check(invalid && JsonUtility.ToJson(game.Progress) == original, "Invalid reset randomness leaves the entire run and crystal wallet untouched");
        game = new MineGame(new MineProgress {
            coins = 98765, tickets = 12, experience = MineGame.ExperienceForLevel(50), upgradeLevel = 20, forgeLevel = 6,
            swapLevel = 2, efficiencyLevel = 3, pressLevel = 3, treasureLevel = 5, workerCount = 5, drillCount = 2, cartCount = 1,
            railLevel = 2, warehouseLevel = 2, stratum = 31, boardSeed = 17, totalClicks = 1000, oresMined = 200,
            totalBlocks = 70, claimedContracts = 1, totalRolls = 20, totalProduced = 10000, totalOfflineCoins = 9000,
            hits = 3, oreKind = 4, oreDamage = 7, productionCarry = .75, veinCharge = 9, veinBoostLeft = 5,
            depths = new[] { 1, 2, 3, 4, 4 }, damage = new[] { 1, 0, 0, 0, 0 }, milestoneClaims = 1, lastSeenUtc = 1700000000
        });
        long[] goals = new long[MineGame.MilestoneCount]; for (int i = 0; i < goals.Length; i++) goals[i] = game.MilestoneValue(i);
        check(game.PrestigeReward == 6 && game.Prestige(() => .5) && game.Progress.prestigeCount == 1 && game.Progress.crystals == 6 && game.Progress.totalCrystals == 6, "Reset awards the previewed crystals exactly once");
        check(game.MinerLevel == 1 && game.Progress.experience == 0 && game.Progress.coins == 80 && game.Progress.tickets == 2 && game.Progress.stratum == 1 && game.Relics == 0, "New run restores its starter wallet, level, tickets and first stratum");
        check(game.Progress.upgradeLevel == 0 && game.Progress.forgeLevel == 0 && game.Progress.swapLevel == 0 && game.Progress.efficiencyLevel == 0 && game.Progress.pressLevel == 0 && game.Progress.treasureLevel == 0 && game.Progress.workerCount == 0 && game.Progress.drillCount == 0 && game.Progress.cartCount == 0 && game.Progress.railLevel == 0 && game.Progress.warehouseLevel == 0, "Reset clears all eleven run upgrade branches");
        check(Array.TrueForAll(game.Progress.depths, n => n == 0) && Array.TrueForAll(game.Progress.damage, n => n == 0) && !game.HasPendingRoll && game.Progress.hits == 0 && game.Progress.oreKind == 0 && game.Progress.oreDamage == 0 && game.Progress.productionCarry == 0 && !game.VeinActive && !game.VeinOffered && game.Progress.veinCharge == 0, "Reset clears shaft damage, ticket progress, fractions and temporary rich-vein state");
        check(game.Progress.totalClicks == 1000 && game.Progress.oresMined == 200 && game.Progress.totalBlocks == 70 && game.Progress.claimedContracts == 1 && game.Progress.totalRolls == 20 && game.Progress.totalProduced == 10000 && game.Progress.totalOfflineCoins == 9000 && game.Progress.milestoneClaims == 1, "Lifetime statistics and claimed reward ledgers survive the reset");
        check(game.Progress.lastSeenUtc == 1700000000 && game.Progress.offlineRateMilli == 0 && game.Progress.offlineCarry == 0 && !game.PrepareOffline(1700010000), "Reset refreshes the saved production snapshot and cannot pay the abandoned machinery again");
        for (int i = 0; i < goals.Length; i++) check(game.MilestoneValue(i) >= goals[i], "Unclaimed lifetime goal progress survives prestige: " + i);
        check(!game.ClaimMilestone(0) && game.CanClaimMilestone(15) && game.ClaimMilestone(15) && !game.ClaimMilestone(15), "Previously claimed goals stay spent and completed unclaimed goals can still be collected at level one");
        game = new MineGame(ProgressStore.Decode(JsonUtility.ToJson(game.Progress))); original = JsonUtility.ToJson(game.Progress);
        check(!game.Prestige(() => .5) && JsonUtility.ToJson(game.Progress) == original, "Reload cannot replay the reset or its crystal reward");
        game = new MineGame(new MineProgress { prestigeCount = 1, totalCrystals = 30, crystals = 30 });
        check(!game.BuyPrestigeUpgrade(-1) && !game.BuyPrestigeUpgrade(9) && !game.BuyPrestigeUpgrade(1) && !game.BuyPrestigeUpgrade(4) && !game.BuyPrestigeUpgrade(7) && game.Progress.crystals == 30, "Invalid or dependent permanent upgrades cannot spend crystals");
        for (int i = 0; i < MineGame.PrestigeUpgradeCount; i++)
        {
            int before = game.Progress.crystals;
            check(game.BuyPrestigeUpgrade(i) && game.HasPrestigeUpgrade(i) && game.Progress.crystals == before - MineGame.PrestigeUpgradeAt(i).cost && !game.BuyPrestigeUpgrade(i), "Permanent purchase spends once and unlocks its next node: " + i);
            game = new MineGame(ProgressStore.Decode(JsonUtility.ToJson(game.Progress)));
        }
        check(game.Progress.crystals == 0 && game.Progress.prestigeUpgrades == 511 && game.Progress.totalCrystals == 30 && game.Progress.coins == 1080 && game.Progress.upgradeLevel == 2 && game.Progress.workerCount == 1, "Nine nodes cost thirty crystals in total and starter purchases help immediately");
        var experienceGame = new MineGame(new MineProgress { prestigeCount = 1 });
        for (int i = 0; i < 5; i++) experienceGame.Mine();
        check(experienceGame.Progress.experience == 5 && experienceGame.Progress.experienceCarry == 75 && experienceGame.PrestigeExperiencePercent == 115, "One prestige grants fifteen percent experience with a saved fractional remainder");
        experienceGame = new MineGame(ProgressStore.Decode(JsonUtility.ToJson(experienceGame.Progress))); experienceGame.Mine();
        check(experienceGame.Progress.experience == 13 && experienceGame.Progress.experienceCarry == 80 && experienceGame.LastHit.experience == 8, "Experience fraction survives reload and is combined with the ore-break reward once");
        check(new MineGame(new MineProgress { prestigeCount = 100 }).PrestigeExperiencePercent == 175, "Intrinsic prestige experience stops at seventy-five percent");
        var maxProduction = new MineGame(new MineProgress { prestigeCount = 1, prestigeUpgrades = 3, totalCrystals = 5, workerCount = 100, drillCount = 50, cartCount = 25, railLevel = 10, forgeLevel = 10, warehouseLevel = 4 });
        maxProduction.RecordSession(1700000000); maxProduction.PrepareOffline(1700086400);
        maxProduction = new MineGame(ProgressStore.Decode(JsonUtility.ToJson(maxProduction.Progress)));
        check(maxProduction.ProductionMilli == 14875000 && maxProduction.Progress.offlineCoins == 321300000, "Permanent production bonus and maximum offline crate survive save validation");
        check(game.ClickPower == 20 && game.ClickUpgradeGain == 5 && game.OreReward == 15 && game.ProductionMilli == 2500 && game.ChestReward == 75, "Permanent income bonuses apply to clicking, ore, production and chests");
        check(game.RichVeinTarget == 20 && game.RichVeinDuration == 16 && game.RichVeinMultiplier == 4, "Three vein nodes lower its target and improve duration and multiplier");
        game.Progress.veinCharge = 19; game.Progress.oreDamage = 5; game.Mine();
        check(game.LastHit.veinFound && game.ActivateVein() && game.Progress.veinBoostLeft == 16 && game.ManualMultiplier == 4, "Improved rich vein activates at twenty ores for sixteen seconds");
        game.Mine(); check(game.LastHit.coins == 80 && game.LastHit.experience == 1, "Permanent manual bonus combines with the vein without multiplying experience");
        game = new MineGame(ProgressStore.Decode(JsonUtility.ToJson(game.Progress)));
        check(game.Progress.veinBoostLeft == 16 && game.ManualMultiplier == 4 && game.Progress.prestigeUpgrades == 511, "Reload preserves permanent upgrades and the upgraded active timer");
        game.Progress.experience = MineGame.ExperienceForLevel(50); game.Progress.stratum = 71;
        check(game.Prestige(() => .25) && game.Progress.prestigeCount == 2 && game.Progress.crystals == 10 && game.Progress.totalCrystals == 40 && game.Progress.coins == 1080 && game.Progress.upgradeLevel == 2 && game.Progress.workerCount == 1 && game.ClickPower == 20 && game.ProductionMilli == 2500, "Later reset preserves purchased nodes and grants the starter kit again");
        game = new MineGame(new MineProgress { prestigeCount = 1, crystals = 1, totalCrystals = 1 });
        check(!game.BuyPrestigeUpgrade(0) && game.Progress.crystals == 1, "Insufficient crystal balance cannot buy a permanent node");
        game = new MineGame(new MineProgress { prestigeCount = 1, crystals = 30, totalCrystals = 30, swapLevel = 1 }); game.BeginRoll(() => .6);
        for (int i = 0; i < 9; i++) check(!game.BuyPrestigeUpgrade(i), "Pending paid draw blocks permanent upgrades: " + i);
        bool malformed = false;
        try { new MineGame(new MineProgress { prestigeUpgrades = 2 }); } catch (FormatException) { malformed = true; }
        check(malformed, "Malformed permanent branches are rejected for backup recovery");
        game = new MineGame(new MineProgress { prestigeCount = int.MaxValue, totalCrystals = int.MaxValue, crystals = int.MaxValue, prestigeUpgrades = 1 << 20, milestoneBest = new[] { long.MaxValue, -1L } });
        check(game.Progress.prestigeCount == MineGame.MaxPrestiges && game.Progress.totalCrystals == MineGame.MaxCrystals && game.Progress.crystals == MineGame.MaxCrystals && game.Progress.prestigeUpgrades == 0 && game.MilestoneValue(0) == 30 && game.MilestoneValue(1) == 0 && game.Progress.milestoneBest.Length == MineGame.MilestoneCount, "Save validation clamps permanent currencies and restores goal records without unknown upgrade bits");
        game.Progress.experience = MineGame.ExperienceForLevel(50);
        check(!game.CanPrestige && !game.Prestige(() => .5), "Prestige and crystal caps cannot overflow");
        var legacy = new MineGame(ProgressStore.Decode("{\"version\":9,\"coins\":745,\"tickets\":4,\"experience\":25480,\"upgradeLevel\":20,\"workerCount\":3,\"milestoneClaims\":5,\"stratum\":15}"));
        check(legacy.Progress.version == 11 && legacy.Progress.coins == 745 && legacy.Progress.tickets == 4 && legacy.Progress.workerCount == 3 && legacy.Progress.milestoneClaims == 5 && legacy.Progress.crystals == 0 && legacy.Progress.prestigeCount == 0 && legacy.MilestoneValue(11) == 20 && legacy.MilestoneValue(8) == 10 && legacy.CanPrestige, "v9 migration records attained goals without free crystals or automatic reset");
        string key = "LuckyPickaxe.Prestige.Tests." + Guid.NewGuid().ToString("N");
        try
        {
            legacy.Prestige(() => .5); ProgressStore.Save(legacy, key); var loaded = ProgressStore.Load(key);
            check(loaded.Progress.prestigeCount == 1 && loaded.Progress.crystals == 4 && loaded.MinerLevel == 1 && !loaded.Prestige(() => .5), "PlayerPrefs persists the committed reset and its crystals once");
            loaded.Progress.crystals = 30; loaded.Progress.totalCrystals = 30; loaded.BuyPrestigeUpgrade(0); ProgressStore.Save(loaded, key);
            PlayerPrefs.SetString(key, "not-json"); var backup = ProgressStore.Load(key);
            check(backup.HasPrestigeUpgrade(0) && backup.Progress.crystals == 28 && !backup.BuyPrestigeUpgrade(0) && backup.MilestoneValue(11) == 20 && backup.Progress.milestoneClaims == 5, "Backup restores bought permanent nodes, spent crystals and lifetime achievements");
        }
        finally { PlayerPrefs.DeleteKey(key); PlayerPrefs.DeleteKey(key + ".backup"); PlayerPrefs.Save(); }
    }

    [MenuItem("Mine Loot/Simulate balance")]
    public static void SimulateBalance()
    {
        const int runs = 100;
        var rows = new System.Text.StringBuilder("strategy,seed,minute,floor,pick_level,forge_level,miner_level,ores,coins,rolls,blocks,zero_rewards,lost_chests,reroutes,exchanges,swap_level,efficiency_level,press_level,treasure_level,milestones,workers,drills,carts,rails,coins_per_second,produced,warehouse,offline_coins\n");
        int[] checkpoints = { 60, 300, 600, 1200 };
        var sums = new double[4, 4, 18];
        var totalMisses = new int[4]; var totalLosses = new int[4]; var totalReroutes = new int[4]; var totalExchanges = new int[4];
        for (int run = 0; run < runs * 4; run++)
        {
            int strategy = run / runs, seed = run % runs;
            var rng = new System.Random(seed);
            var game = new MineGame(); int wait = 0, emptyStreak = 0, misses = 0, losses = 0, reroutes = 0, exchanges = 0;
            for (int second = 1; second <= 1200; second++)
            {
                if (strategy == 3 && second == 601)
                {
                    game.RecordSession(1700000600);
                    game = new MineGame(ProgressStore.Decode(JsonUtility.ToJson(game.Progress)));
                    if (!game.PrepareOffline(1700000600 + 8 * 3600) || !game.ClaimOffline() || game.ClaimOffline()) throw new Exception("Offline return regression seed " + seed);
                }
                if (wait == 0 && game.VeinOffered) game.ActivateVein();
                game.AdvanceTime(1, wait == 0);
                if (wait > 0) wait--;
                else
                {
                    for (int tap = 0; tap < 6; tap++) game.Mine();
                    if (game.VeinOffered) game.ActivateVein();
                    if (game.CanClaim) game.ClaimContract();
                    for (int goal = 0; goal < MineGame.MilestoneCount; goal++) game.ClaimMilestone(goal);
                    bool productionPurchase = strategy >= 2 && second % 5 == 0 && BuyProductionUpgrade(game, strategy == 3);
                    if (!productionPurchase && (strategy == 0 || !BuyAdditionalUpgrade(game)))
                    {
                        if (game.CanForge && game.Progress.forgeLevel < Math.Min(10, 2 * game.Hardness)) game.Forge();
                        else if (game.CanUpgrade) game.Upgrade();
                    }
                    if (emptyStreak >= 3 && game.CanAbandon) { game.Abandon(() => rng.NextDouble()); reroutes++; emptyStreak = 0; }
                    else if (game.Progress.tickets > 0)
                    {
                        game.BeginRoll(() => rng.NextDouble());
                        while (strategy > 0 && ExchangeForVisibleObsidian(game)) exchanges++;
                        var roll = game.ResolveRoll(); wait = 2;
                        if (roll.reward == 0) { misses++; emptyStreak++; } else emptyStreak = 0;
                        losses += roll.lostChests;
                    }
                }
                for (int i = 0; i < checkpoints.Length; i++) if (second == checkpoints[i])
                {
                    rows.AppendLine(FormattableString.Invariant($"{strategy},{seed},{second / 60},{game.Progress.stratum},{game.Progress.upgradeLevel},{game.Progress.forgeLevel},{game.MinerLevel},{game.Progress.oresMined},{game.Progress.coins},{game.Progress.totalRolls},{game.Progress.totalBlocks},{misses},{losses},{reroutes},{exchanges},{game.Progress.swapLevel},{game.Progress.efficiencyLevel},{game.Progress.pressLevel},{game.Progress.treasureLevel},{game.ClaimedMilestones},{game.Progress.workerCount},{game.Progress.drillCount},{game.Progress.cartCount},{game.Progress.railLevel},{game.ProductionMilli / 1000d},{game.Progress.totalProduced},{game.Progress.warehouseLevel},{game.Progress.totalOfflineCoins}"));
                    sums[strategy, i, 0] += game.Progress.stratum; sums[strategy, i, 1] += game.Progress.upgradeLevel; sums[strategy, i, 2] += game.Progress.forgeLevel;
                    sums[strategy, i, 3] += game.MinerLevel; sums[strategy, i, 4] += game.Progress.oresMined;
                    sums[strategy, i, 5] += game.Progress.swapLevel; sums[strategy, i, 6] += game.Progress.efficiencyLevel;
                    sums[strategy, i, 7] += game.Progress.pressLevel; sums[strategy, i, 8] += game.Progress.treasureLevel;
                    sums[strategy, i, 9] += game.ClaimedMilestones;
                    sums[strategy, i, 10] += game.Progress.workerCount; sums[strategy, i, 11] += game.Progress.drillCount;
                    sums[strategy, i, 12] += game.Progress.cartCount; sums[strategy, i, 13] += game.Progress.railLevel;
                    sums[strategy, i, 14] += game.ProductionMilli / 1000d; sums[strategy, i, 15] += game.Progress.totalProduced;
                    sums[strategy, i, 16] += game.Progress.warehouseLevel; sums[strategy, i, 17] += game.Progress.totalOfflineCoins;
                }
            }
            if (game.Progress.stratum < 10 || game.Progress.coins < 0 || game.Progress.tickets < 0 || game.UnlockedOres != 5 || game.Progress.oreDamage >= game.OreHealth || game.HasPendingRoll) throw new Exception("Balance regression strategy " + strategy + ", seed " + seed);
            if (strategy >= 2 && (game.Progress.totalProduced <= 0 || game.Progress.workerCount == 0 || game.Progress.drillCount == 0 || game.Progress.cartCount == 0 || game.Progress.railLevel == 0)) throw new Exception("Production strategy did not develop all branches: " + seed);
            if (strategy == 3 && (game.Progress.totalOfflineCoins == 0 || game.HasOfflineReward || game.Progress.warehouseLevel == 0)) throw new Exception("Offline strategy failed to develop storage: " + seed);
            totalMisses[strategy] += misses; totalLosses[strategy] += losses; totalReroutes[strategy] += reroutes; totalExchanges[strategy] += exchanges;
        }
        for (int s = 0; s < 4; s++) if (totalMisses[s] == 0 || totalLosses[s] == 0 || totalReroutes[s] == 0 || s > 0 && totalExchanges[s] == 0) throw new Exception("Risk simulation has no actual losses or exchanges");
        Directory.CreateDirectory("Logs"); Directory.CreateDirectory("Docs");
        File.WriteAllText("Logs/balance-runs.csv", rows.ToString());
        var doc = new System.Text.StringBuilder("# Проверка темпа прогрессии 0.16\n\nПо 100 воспроизводимых прогонов, seed 0–99, 20 минут на каждую стратегию. Модель: 6 ударов в секунду вне розыгрыша, розыгрыш занимает 3 секунды. Время на размышление игрока не учитывается. Все стратегии забирают контракты и готовые разовые награды книги целей (не более 14140 монет и 34 жетонов за всё прохождение). Базовый приоритет покупок: кузница до уровня min(10, 2 × прочность), затем кирка при наличии монет. После трёх розыгрышей без награды бот меняет раскладку за жетон, без повышения пласта. Учтены обсидиан, бедрок, потерянные сундуки, отсутствие выплат в пустых проходах и бонус пласта только за пять сундуков. Это проверка экономики, не оценка поведения реальных игроков.\n\nСтратегия 0 покупает только кирку и кузницу, обмен закрыт. Стратегия 1 покупает новые ветки за монеты с соблюдением уровней: перестановку до 3, остроту до min(10, уровень/4), пресс до min(6, уровень/5), сокровища до min(10, уровень/5). Если новая покупка недоступна, действует базовый приоритет кузницы и кирки. Использует до купленного лимита обменов, передавая алмаз на раскрытый обсидиан. Скрытые слои бот не читает.\n");
        doc.Append("\nВо всех стратегиях бот активирует богатую жилу при появлении. Таймер жилы идёт только во время ручной добычи; за время рулетки производство монет продолжается. Стратегия 2 повторяет стратегию 1 и раз в 5 секунд проверяет покупку производства: шахтёры до min(30, уровень), буры до min(15, уровень/3), вагонетки до min(10, уровень/4), рельсы до min(10, уровень/4). Стратегия 3 дополнительно предпочитает склад до min(4, уровень/6) с соблюдением цен и уровней. После 10 минут активной игры она сохраняется, возвращается через 8 часов и забирает офлайн-награду, затем продолжает ещё 10 активных минут. Время отсутствия задано тестом; активные минуты в таблице его не включают. Награда считается по производству и вместимости на момент выхода, новые покупки её не пересчитывают.\n");
        for (int s = 0; s < 4; s++)
        {
            doc.Append(s == 0 ? "\n## Без обмена\n" : s == 1 ? "\n## Снаряжение без производства\n" : s == 2 ? "\n## Снаряжение и производство\n" : "\n## Возвращение после 8 часов\n");
            doc.Append("\n| Минута | Средний пласт | Уровень кирки | Кузница | Уровень шахтёра | Разрушено руд | Наград за цели |\n|---|---:|---:|---:|---:|---:|---:|\n");
            for (int i = 0; i < 4; i++) doc.AppendLine(string.Format(System.Globalization.CultureInfo.InvariantCulture, "| {0} | {1:F1} | {2:F1} | {3:F1} | {4:F1} | {5:F1} | {6:F1} |", checkpoints[i] / 60, sums[s, i, 0] / runs, sums[s, i, 1] / runs, sums[s, i, 2] / runs, sums[s, i, 3] / runs, sums[s, i, 4] / runs, sums[s, i, 9] / runs));
            doc.Append($"\nВсего за 100 прогонов: {totalMisses[s]} розыгрышей без монет, {totalLosses[s]} сундуков потеряно из-за бедрока, {totalReroutes[s]} смен раскладки за жетон, {totalExchanges[s]} обменов кирок.\n");
            if (s > 0)
            {
                doc.Append("\n| Минута | Перестановка | Острота | Пресс | Сокровища |\n|---|---:|---:|---:|---:|\n");
                for (int i = 0; i < 4; i++) doc.AppendLine(string.Format(System.Globalization.CultureInfo.InvariantCulture, "| {0} | {1:F1} | {2:F1} | {3:F1} | {4:F1} |", checkpoints[i] / 60, sums[s, i, 5] / runs, sums[s, i, 6] / runs, sums[s, i, 7] / runs, sums[s, i, 8] / runs));
            }
            if (s >= 2)
            {
                doc.Append("\n| Минута | Шахтёры | Буры | Вагонетки | Рельсы | Монет/с | Монет произведено |\n|---|---:|---:|---:|---:|---:|---:|\n");
                for (int i = 0; i < 4; i++) doc.AppendLine(string.Format(System.Globalization.CultureInfo.InvariantCulture, "| {0} | {1:F1} | {2:F1} | {3:F1} | {4:F1} | {5:F1} | {6:F1} |", checkpoints[i] / 60, sums[s, i, 10] / runs, sums[s, i, 11] / runs, sums[s, i, 12] / runs, sums[s, i, 13] / runs, sums[s, i, 14] / runs, sums[s, i, 15] / runs));
                if (s == 3)
                {
                    doc.Append("\n| Активная минута | Склад | Получено офлайн-монет |\n|---|---:|---:|\n");
                    for (int i = 0; i < 4; i++) doc.AppendLine(string.Format(System.Globalization.CultureInfo.InvariantCulture, "| {0} | {1:F1} | {2:F1} |", checkpoints[i] / 60, sums[s, i, 16] / runs, sums[s, i, 17] / runs));
                }
            }
        }
        doc.Append("\nФормулы и шансы находятся в `Assets/MineLoot/Scripts/MineGame.cs`. Сырые результаты: `Logs/balance-runs.csv` после запуска меню Mine Loot → Simulate balance. Прочность растёт каждые 3 пласта до 4, награды растут до 100-го пласта. Для проверки удержания и удобства нужен плейтест.\n");
        File.WriteAllText("Docs/Balance.md", doc.ToString().Replace("\r\n", "\n"));
        Debug.Log("LUCKY_PICKAXE_BALANCE: PASS (400 seeded runs, four progression strategies)");
        SimulatePrestigeBalance();
    }

    private static void SimulatePrestigeBalance()
    {
        const int runs = 100, cycles = 4;
        var rows = new System.Text.StringBuilder("seed,mine,seconds,crystals_earned,crystals_left,permanent_nodes,claimed_goals\n");
        var secondsTotal = new double[cycles]; var crystalsTotal = new double[cycles]; var nodesTotal = new double[cycles];
        var fastest = new int[cycles]; var slowest = new int[cycles];
        for (int i = 0; i < cycles; i++) fastest[i] = int.MaxValue;
        for (int seed = 0; seed < runs; seed++)
        {
            var rng = new System.Random(seed + 6355); var game = new MineGame();
            for (int cycle = 0; cycle < cycles; cycle++)
            {
                int wait = 0, empty = 0, duration = 0;
                for (int second = 1; second <= 7200; second++)
                {
                    if (wait == 0 && game.VeinOffered) game.ActivateVein();
                    game.AdvanceTime(1, wait == 0);
                    if (wait > 0) { wait--; continue; }
                    for (int tap = 0; tap < 6; tap++) game.Mine();
                    if (game.VeinOffered) game.ActivateVein();
                    if (game.CanClaim) game.ClaimContract();
                    for (int goal = 0; goal < MineGame.MilestoneCount; goal++) game.ClaimMilestone(goal);
                    if (game.MinerLevel == 50) { duration = second; break; }
                    bool productionPurchase = second % 5 == 0 && BuyProductionUpgrade(game);
                    if (!productionPurchase && !BuyAdditionalUpgrade(game))
                    {
                        if (game.CanForge && game.Progress.forgeLevel < Math.Min(10, 2 * game.Hardness)) game.Forge();
                        else if (game.CanUpgrade) game.Upgrade();
                    }
                    if (empty >= 3 && game.CanAbandon) { game.Abandon(() => rng.NextDouble()); empty = 0; }
                    else if (game.Progress.tickets > 0)
                    {
                        game.BeginRoll(() => rng.NextDouble()); while (ExchangeForVisibleObsidian(game)) { }
                        var roll = game.ResolveRoll(); wait = 2; empty = roll.reward == 0 ? empty + 1 : 0;
                    }
                }
                if (duration == 0 || !game.CanPrestige) throw new Exception("Prestige cycle failed to complete: " + seed + "/" + cycle);
                int earned = game.PrestigeReward, claimed = game.ClaimedMilestones;
                if (!game.Prestige(() => rng.NextDouble()) || game.CanPrestige || game.Progress.prestigeCount != cycle + 1 || game.ClaimedMilestones != claimed) throw new Exception("Prestige reset regression: " + seed + "/" + cycle);
                foreach (int node in new[] { 3, 4, 0, 1, 6, 7, 5, 2, 8 }) game.BuyPrestigeUpgrade(node);
                game = new MineGame(ProgressStore.Decode(JsonUtility.ToJson(game.Progress)));
                int owned = 0, spent = 0;
                for (int i = 0; i < 9; i++) if (game.HasPrestigeUpgrade(i)) { owned++; spent += MineGame.PrestigeUpgradeAt(i).cost; }
                if (game.Progress.crystals < 0 || game.Progress.crystals + spent != game.Progress.totalCrystals || game.ClaimedMilestones != claimed || game.Progress.coins != game.PrestigeStartCoins) throw new Exception("Permanent purchase ledger regression: " + seed + "/" + cycle);
                rows.AppendLine($"{seed},{cycle + 1},{duration},{earned},{game.Progress.crystals},{owned},{claimed}");
                secondsTotal[cycle] += duration; crystalsTotal[cycle] += earned; nodesTotal[cycle] += owned;
                fastest[cycle] = Math.Min(fastest[cycle], duration); slowest[cycle] = Math.Max(slowest[cycle], duration);
            }
        }
        if (secondsTotal[1] >= secondsTotal[0]) throw new Exception("Permanent upgrades did not improve average second-run progression");
        File.WriteAllText("Logs/prestige-runs.csv", rows.ToString());
        var doc = new System.Text.StringBuilder("# Повторное прохождение и престиж 0.16\n\n100 воспроизводимых последовательностей, seed 6355–6454, по четыре шахты до уровня 50. Модель: 6 ударов в секунду вне рулетки, 3 секунды на розыгрыш, без времени на размышление. Бот покупает снаряжение и производство по правилам основной симуляции, забирает контракты и цели, использует раскрытый обсидиан и богатые жилы. Офлайн-доход в этой проверке не используется.\n\nПосле каждого достижения уровня 50 подтверждается один престиж. Каждый переезд даёт +15% опыта, максимум +75%; дробный опыт переносится между начислениями. Приоритет постоянных покупок: запас монет, каменный старт, верный удар, артель, чутьё, долгая жила, своя бригада, секрет клада, сила жилы. Цели и контракты сохраняются между шахтами; старые разовые награды не повторяются. После сброса и покупок выполняется JSON-загрузка, проверяются кристаллы и постоянные узлы.\n\n| Шахта | Среднее время, мин | Минимум, сек | Максимум, сек | Кристаллов за переезд | Куплено узлов всего |\n|---|---:|---:|---:|---:|---:|\n");
        for (int i = 0; i < cycles; i++) doc.AppendLine(string.Format(System.Globalization.CultureInfo.InvariantCulture, "| {0} | {1:F2} | {2} | {3} | {4:F2} | {5:F2} |", i + 1, secondsTotal[i] / runs / 60, fastest[i], slowest[i], crystalsTotal[i] / runs, nodesTotal[i] / runs));
        doc.AppendLine(string.Format(System.Globalization.CultureInfo.InvariantCulture, "\nСреднее второе прохождение быстрее первого на {0:F1}%. Это результат заданного поведения бота, не обещание темпа для реального игрока. Сырые результаты: `Logs/prestige-runs.csv` после сборки или меню Simulate balance.", (1 - secondsTotal[1] / secondsTotal[0]) * 100));
        File.WriteAllText("Docs/PrestigeBalance.md", doc.ToString().Replace("\r\n", "\n"));
        Debug.Log("LUCKY_PICKAXE_PRESTIGE_BALANCE: PASS (100 sequences, 400 completed mines)");
    }

    private static bool BuyProductionUpgrade(MineGame game, bool storage = false)
    {
        if (storage && game.Progress.warehouseLevel < Math.Min(MineGame.MaxWarehouse, game.MinerLevel / 6) && game.BuyUpgrade(UpgradeKind.Warehouse)) return true;
        foreach (var kind in new[] { UpgradeKind.Worker, UpgradeKind.Drill, UpgradeKind.Cart, UpgradeKind.Rails })
        {
            int desired = kind == UpgradeKind.Worker ? Math.Min(30, game.MinerLevel) : kind == UpgradeKind.Drill ? Math.Min(15, game.MinerLevel / 3) : Math.Min(10, game.MinerLevel / 4);
            if (game.UpgradeLevel(kind) < desired && game.BuyUpgrade(kind)) return true;
        }
        return false;
    }

    private static bool BuyAdditionalUpgrade(MineGame game)
    {
        foreach (var kind in new[] { UpgradeKind.Swap, UpgradeKind.Efficiency, UpgradeKind.Press, UpgradeKind.Treasure })
        {
            int desired = kind == UpgradeKind.Swap ? 3 : kind == UpgradeKind.Efficiency ? Math.Min(10, game.MinerLevel / 4) : kind == UpgradeKind.Press ? Math.Min(6, game.MinerLevel / 5) : Math.Min(10, game.MinerLevel / 5);
            if (game.UpgradeLevel(kind) < desired && game.BuyUpgrade(kind)) return true;
        }
        return false;
    }

    private static bool ExchangeForVisibleObsidian(MineGame game)
    {
        if (!game.CanSwap) return false;
        for (int target = 0; target < MineGame.Columns; target++)
        {
            if (!game.IsActiveColumn(target) || game.Progress.pendingTypes[target] == 4 || game.BlockAt(target, game.Progress.depths[target]) != BlockKind.Obsidian) continue;
            for (int donor = 0; donor < MineGame.Columns; donor++)
                if (game.CanSwapPicks(target, donor) && game.Progress.pendingTypes[donor] == 4 && game.BlockAt(donor, game.Progress.depths[donor]) == BlockKind.Ore) return game.SwapPicks(target, donor);
        }
        return false;
    }

    [MenuItem("Mine Loot/Build Windows demo")]
    public static void VerifyAndBuild()
    {
        if (Application.unityVersion != "2022.3.47f1") throw new Exception("This demo targets Unity 2022.3.47f1; current: " + Application.unityVersion);
        if (!File.Exists(ScenePath)) Prepare();
        PlayerSettings.bundleVersion = "0.17.0";
        VerifyRules();
        SimulateBalance();
        Directory.CreateDirectory("Builds/Windows");
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { ScenePath }, locationPathName = "Builds/Windows/MineLoot.exe",
            target = BuildTarget.StandaloneWindows64, options = BuildOptions.Development
        });
        var summary = report.summary;
        File.WriteAllText("Logs/build-summary.json", "{\"result\":\"" + summary.result + "\",\"errors\":" + summary.totalErrors + ",\"warnings\":" + summary.totalWarnings + ",\"bytes\":" + summary.totalSize + ",\"unity\":\"" + Application.unityVersion + "\"}");
        if (summary.result != BuildResult.Succeeded) throw new Exception("Windows build failed: " + summary.result);
        Debug.Log("MINE_LOOT_BUILD: PASS " + summary.outputPath);
    }

    [MenuItem("Mine Loot/Build Yandex Games WebGL")]
    public static void BuildWebGL()
    {
        if (Application.unityVersion != "2022.3.47f1") throw new Exception("Use Unity 2022.3.47f1 for WebGL.");
        if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.WebGL, BuildTarget.WebGL)) throw new Exception("Install WebGL Build Support for Unity 2022.3.47f1.");
        PlayerSettings.bundleVersion = "0.17.0";
        PlayerSettings.WebGL.template = "PROJECT:LuckyPickaxe";
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
        PlayerSettings.WebGL.decompressionFallback = true;
        PlayerSettings.WebGL.threadsSupport = false;
        Directory.CreateDirectory("Builds/WebGL");
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
            scenes = new[] { ScenePath }, locationPathName = "Builds/WebGL",
            target = BuildTarget.WebGL, options = BuildOptions.None
        });
        var summary = report.summary;
        File.WriteAllText("Logs/webgl-build-summary.json", "{\"result\":\"" + summary.result + "\",\"errors\":" + summary.totalErrors + ",\"warnings\":" + summary.totalWarnings + ",\"bytes\":" + summary.totalSize + ",\"unity\":\"" + Application.unityVersion + "\"}");
        if (summary.result != BuildResult.Succeeded) throw new Exception("WebGL build failed: " + summary.result);
        const string archive = "Builds/LuckyPickaxe-YandexGames.zip";
        if (File.Exists(archive)) File.Delete(archive);
        System.IO.Compression.ZipFile.CreateFromDirectory("Builds/WebGL", archive, System.IO.Compression.CompressionLevel.Optimal, false);
        Debug.Log("MINE_LOOT_WEBGL_BUILD: PASS " + Path.GetFullPath(archive));
    }
}
